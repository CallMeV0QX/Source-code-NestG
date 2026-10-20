using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamConnection;

internal sealed class SteamDpiRuntimeClient : ISteamDpiRuntime, IAsyncDisposable
{
	private sealed record HelperManifest(long Size, string Sha256);

	internal sealed record HelperReply(int Version, string? Id, string Code, [property: JsonRequired] bool Running, int Pid, bool DnsReady, bool DpiRunning, bool RecoveryPending = false, string? Stage = null, string? Cause = null);

	private sealed class HelperRejectedException(string code) : IOException("Steam helper declined the session.")
	{
		internal string Code { get; } = code;
	}

	private const string ResourcePrefix = "NestG.SteamDpi.";

	private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);

	private NamedPipeServerStream? pipe;

	private Process? helper;

	private FileStream? executableLease;

	private string? sessionDirectory;

	private bool mayBeRunning;

	private bool disposed;

	public bool IsAvailable
	{
		get
		{
			if (typeof(SteamDpiRuntimeClient).Assembly.GetManifestResourceNames().Contains("NestG.SteamDpi.Helper.exe"))
			{
				return typeof(SteamDpiRuntimeClient).Assembly.GetManifestResourceNames().Contains("NestG.SteamDpi.Helper.json");
			}
			return false;
		}
	}

	public Task<SteamDpiRuntimeResult> StartAsync(string profile, CancellationToken cancellationToken)
	{
		bool flag;
		switch (profile)
		{
		case "dns-only":
		case "z-split":
		case "z-disorder":
		case "z-fake":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new ArgumentException("Unsupported Steam DPI profile.", "profile");
		}
		return SendAsync("start", profile, cancellationToken);
	}

	public Task<SteamDpiRuntimeResult> ReadAsync(CancellationToken cancellationToken)
	{
		return SendAsync("status", null, cancellationToken);
	}

	public Task<SteamDpiRuntimeResult> StopAsync(CancellationToken cancellationToken)
	{
		return SendAsync("stop", null, cancellationToken);
	}

	private async Task<SteamDpiRuntimeResult> SendAsync(string command, string? profile, CancellationToken cancellationToken)
	{
		await commandGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (pipe == null || helper == null || helper.HasExited)
			{
				if (!(await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false)))
				{
					return new SteamDpiRuntimeResult(mayBeRunning, "helper-unavailable", DnsReady: false, DpiRunning: false, mayBeRunning, "helper-exit");
				}
				if (command != "start" && (!(command == "stop") || !mayBeRunning))
				{
					return new SteamDpiRuntimeResult(mayBeRunning, mayBeRunning ? "dns-recovery-pending" : "ok", DnsReady: false, DpiRunning: false, mayBeRunning, mayBeRunning ? "journal-recovery" : null);
				}
				await ConnectAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			string id = Guid.NewGuid().ToString("N");
			if (command == "start")
			{
				mayBeRunning = true;
			}
			using CancellationTokenSource replyDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			replyDeadline.CancelAfter((command == "start") ? TimeSpan.FromSeconds(90.0) : TimeSpan.FromSeconds(20.0));
			HelperReply reply = await ExchangeAsync(pipe, (command == "stop") ? "exit" : command, profile, id, replyDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			mayBeRunning = reply.Running || reply.RecoveryPending || (command == "stop" && reply.Code != "ok");
			bool flag = command == "stop" && !mayBeRunning;
			if (flag)
			{
				flag = !(await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false));
			}
			if (flag)
			{
				mayBeRunning = true;
				return new SteamDpiRuntimeResult(Running: true, "helper-unavailable", DnsReady: false, DpiRunning: false, RecoveryPending: true, "helper-exit");
			}
			return new SteamDpiRuntimeResult(mayBeRunning, reply.Code, reply.DnsReady, reply.DpiRunning, reply.RecoveryPending, reply.Stage, reply.Cause);
		}
		catch (HelperRejectedException ex)
		{
			await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			return new SteamDpiRuntimeResult(mayBeRunning, ex.Code, DnsReady: false, DpiRunning: false, mayBeRunning);
		}
		catch (Win32Exception ex2) when (ex2.NativeErrorCode == 1223)
		{
			await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			return new SteamDpiRuntimeResult(mayBeRunning, "uac-cancelled", DnsReady: false, DpiRunning: false, mayBeRunning, "helper-uac");
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			return new SteamDpiRuntimeResult(mayBeRunning, "helper-timeout", DnsReady: false, DpiRunning: false, mayBeRunning, "helper-reply");
		}
		catch (OperationCanceledException)
		{
			await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			throw;
		}
		catch (Exception ex5) when (((ex5 is IOException || ex5 is Win32Exception || ex5 is UnauthorizedAccessException || ex5 is JsonException || ex5 is InvalidOperationException) ? 1 : 0) != 0)
		{
			await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			return new SteamDpiRuntimeResult(mayBeRunning, "helper-unavailable", DnsReady: false, DpiRunning: false, mayBeRunning, "helper-transport");
		}
		finally
		{
			commandGate.Release();
		}
	}

	private async Task ConnectAsync(CancellationToken cancellationToken)
	{
		if (!IsAvailable)
		{
			throw new FileNotFoundException("Bundled Steam helper missing.");
		}
		HelperManifest manifest = ReadManifest();
		string session = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "SteamConnection", "HelperSessions", Guid.NewGuid().ToString("N"));
		EnsureNoReparseAncestors(session);
		Directory.CreateDirectory(session);
		sessionDirectory = session;
		EnsureNoReparseAncestors(session);
		string executable = Path.Combine(session, "NestG.SteamDpi.Helper.exe");
		using (Stream resource = typeof(SteamDpiRuntimeClient).Assembly.GetManifestResourceStream("NestG.SteamDpi.Helper.exe"))
		{
			using FileStream target = new FileStream(executable, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			await resource.CopyToAsync(target, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			target.Flush(flushToDisk: true);
		}
		FileStream lease = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
		try
		{
			bool flag = lease.Length != manifest.Size;
			if (!flag)
			{
				long length = lease.Length;
				bool flag2 = ((length < 1024 || length > 33554432) ? true : false);
				flag = flag2;
			}
			if (flag || !Convert.ToHexString(SHA256.HashData(lease)).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Bundled helper integrity failure.");
			}
			executableLease = lease;
			string text = "NestGSteamDpi-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
			pipe = CreatePipe(text);
			using Process parent = Process.GetCurrentProcess();
			ProcessStartInfo start = new ProcessStartInfo(executable)
			{
				UseShellExecute = true,
				Verb = "runas",
				WindowStyle = ProcessWindowStyle.Hidden,
				WorkingDirectory = session
			};
			start.ArgumentList.Add("--pipe");
			start.ArgumentList.Add(text);
			start.ArgumentList.Add("--parent");
			start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
			start.ArgumentList.Add("--created");
			start.ArgumentList.Add(parent.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture));
			Task<Process> launch = Task.Run(() => Process.Start(start), CancellationToken.None);
			try
			{
				helper = await launch.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
				executableLease = null;
				launch.ContinueWith(delegate(Task<Process> task)
				{
					if (task.Status == TaskStatus.RanToCompletion)
					{
						task.Result?.Dispose();
					}
					_ = task.Exception;
					lease.Dispose();
				}, TaskScheduler.Default);
				throw;
			}
			if (helper == null)
			{
				throw new IOException("Helper could not launch.");
			}
			using CancellationTokenSource connectionDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			connectionDeadline.CancelAfter(TimeSpan.FromSeconds(15.0));
			await pipe.WaitForConnectionAsync(connectionDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var clientProcessId) || clientProcessId != helper.Id)
			{
				throw new IOException("Unexpected helper identity.");
			}
			HelperReply helperReply = await ReadReplyAsync(pipe, connectionDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (helperReply.Version == 2 && helperReply.Code == "busy")
			{
				throw new HelperRejectedException("busy");
			}
			if (helperReply.Version != 2 || helperReply.Code != "ready" || helperReply.Pid != helper.Id)
			{
				throw new IOException("Helper handshake failed.");
			}
		}
		catch
		{
			if (executableLease == lease)
			{
				executableLease.Dispose();
				executableLease = null;
			}
			throw;
		}
	}

	private static NamedPipeServerStream CreatePipe(string name)
	{
		PipeSecurity pipeSecurity = new PipeSecurity();
		pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
		pipeSecurity.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
		pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
		pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
		return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, pipeSecurity);
	}

	private static HelperManifest ReadManifest()
	{
		using Stream utf8Json = typeof(SteamDpiRuntimeClient).Assembly.GetManifestResourceStream("NestG.SteamDpi.Helper.json");
		HelperManifest helperManifest = JsonSerializer.Deserialize<HelperManifest>(utf8Json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
		if ((object)helperManifest == null || helperManifest.Size <= 0 || helperManifest.Sha256.Length != 64)
		{
			throw new InvalidDataException("Helper manifest invalid.");
		}
		return helperManifest;
	}

	internal static async Task<HelperReply> ExchangeAsync(Stream stream, string command, string? profile, string id, CancellationToken cancellationToken)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, command, profile }) + "\n");
		await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		HelperReply helperReply = await ReadReplyAsync(stream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (helperReply.Version != 2 || helperReply.Id != id || string.IsNullOrEmpty(helperReply.Code))
		{
			throw new InvalidDataException("Helper response mismatch.");
		}
		return helperReply;
	}

	private static async Task<HelperReply> ReadReplyAsync(Stream stream, CancellationToken cancellationToken)
	{
		using MemoryStream bytes = new MemoryStream();
		byte[] one = new byte[1];
		while (bytes.Length <= 4096)
		{
			if (await stream.ReadAsync(one, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) == 0)
			{
				throw new EndOfStreamException();
			}
			if (one[0] == 10)
			{
				return JsonSerializer.Deserialize<HelperReply>(bytes.ToArray(), new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				}) ?? throw new InvalidDataException("Empty reply.");
			}
			bytes.WriteByte(one[0]);
		}
		throw new InvalidDataException("Helper reply too large.");
	}

	internal static void EnsureNoReparseAncestors(string path)
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetFullPath(path)); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (directoryInfo.Exists && (directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException("Reparse points are not supported for the Steam helper.");
			}
		}
	}

	private async Task<bool> ResetConnectionAsync()
	{
		pipe?.Dispose();
		pipe = null;
		if (helper != null)
		{
			try
			{
				await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(18.0)).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex) when (((ex is TimeoutException || ex is Win32Exception || ex is InvalidOperationException) ? 1 : 0) != 0)
			{
				return false;
			}
		}
		helper?.Dispose();
		helper = null;
		executableLease?.Dispose();
		executableLease = null;
		string text = sessionDirectory;
		if (text != null)
		{
			sessionDirectory = null;
			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "SteamConnection", "HelperSessions");
				if (Path.GetDirectoryName(Path.GetFullPath(text)) == Path.GetFullPath(path) && Guid.TryParseExact(Path.GetFileName(text), "N", out var _))
				{
					EnsureNoReparseAncestors(text);
					File.Delete(Path.Combine(text, "NestG.SteamDpi.Helper.exe"));
					Directory.Delete(text, recursive: false);
				}
			}
			catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
			}
		}
		return true;
	}

	public async ValueTask DisposeAsync()
	{
		await commandGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (disposed)
			{
				return;
			}
			using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20.0));
			Process process = helper;
			if (process != null && !process.HasExited)
			{
				NamedPipeServerStream namedPipeServerStream = pipe;
				if (namedPipeServerStream != null && namedPipeServerStream.IsConnected)
				{
					try
					{
						string id = Guid.NewGuid().ToString("N");
						await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
						{
							id = id,
							command = "exit"
						}) + "\n"), deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
						await pipe.FlushAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
						HelperReply helperReply = await ReadReplyAsync(pipe, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
						if (helperReply.Version != 2 || helperReply.Id != id)
						{
							throw new InvalidDataException("Helper response mismatch.");
						}
						mayBeRunning = helperReply.Running || helperReply.RecoveryPending || helperReply.Code != "ok";
						await helper.WaitForExitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (Exception ex) when (((ex is IOException || ex is OperationCanceledException || ex is JsonException) ? 1 : 0) != 0)
					{
					}
				}
			}
			if (!(await ResetConnectionAsync().ConfigureAwait(continueOnCapturedContext: false)))
			{
				throw new IOException("Steam helper shutdown is not confirmed.");
			}
			if (mayBeRunning)
			{
				throw new IOException("Steam DNS recovery is not confirmed.");
			}
			disposed = true;
		}
		finally
		{
			commandGate.Release();
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetNamedPipeClientProcessId(nint pipe, out uint clientProcessId);
}
