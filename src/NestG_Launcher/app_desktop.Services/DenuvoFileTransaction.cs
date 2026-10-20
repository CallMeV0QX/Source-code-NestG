using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;

namespace app_desktop.Services;

internal sealed class DenuvoFileTransaction
{
	private sealed class Lease(SemaphoreSlim gate, FileStream stream) : IDisposable
	{
		public void Dispose()
		{
			stream.Dispose();
			gate.Release();
		}
	}

	private sealed record StagedFile(string Relative, string Path, string Hash);

	private sealed class Receipt
	{
		public int Version { get; set; } = 1;

		public string Id { get; set; } = "";

		public Guid AssignmentId { get; set; }

		public string GameRoot { get; set; } = "";

		public string LocalRoot { get; set; } = "";

		public string Path { get; set; } = "";

		public string Status { get; set; } = "preparing";

		public List<Entry> Entries { get; set; } = new List<Entry>();
	}

	private sealed class Entry
	{
		public string Target { get; set; } = "";

		public bool Existed { get; set; }

		public string? Backup { get; set; }

		public string? PreHash { get; set; }

		public string PostHash { get; set; } = "";
	}

	private const int MaximumFiles = 8192;

	private const long MaximumUnpackedBytes = 8589934592L;

	private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(10.0);

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NestG.Denuvo.FileTransaction.v1");

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

	private readonly string directory;

	private readonly string receiptPath;

	private readonly Receipt receipt;

	private readonly IProgress<DenuvoInstallProgress>? progress;

	private long lastProgressAt;

	internal static string DefaultLocalRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher");

	private DenuvoFileTransaction(string gameRoot, Guid assignmentId, string localRoot, IProgress<DenuvoInstallProgress>? progress)
	{
		receipt = new Receipt
		{
			Id = Guid.NewGuid().ToString("N"),
			AssignmentId = assignmentId,
			GameRoot = Path.GetFullPath(gameRoot),
			LocalRoot = Path.GetFullPath(localRoot)
		};
		directory = Path.Combine(receipt.LocalRoot, "DenuvoBackups", assignmentId.ToString("N"), receipt.Id);
		receiptPath = Path.Combine(directory, "transaction.dat");
		receipt.Path = receiptPath;
		this.progress = progress;
		CheckPath(directory);
		Directory.CreateDirectory(directory);
		Save();
	}

	internal static Task<DenuvoFileInstallResult> InstallZipAsync(string zipPath, string gameRoot, string configRelativePath, Guid assignmentId, string? password, Func<DenuvoFileInstallResult, string>? stateFactory, CancellationToken cancellationToken = default(CancellationToken), IProgress<DenuvoInstallProgress>? progress = null, string? localRootOverride = null)
	{
		return Task.Run(() => RunAsync(gameRoot, configRelativePath, assignmentId, stateFactory, null, (DenuvoFileTransaction transaction, CancellationToken token) => transaction.StageZipAsync(zipPath, password, token), cancellationToken, progress, localRootOverride));
	}

	internal static Task<DenuvoFileInstallResult> InstallConfigAsync(string source, string gameRoot, string configRelativePath, Guid assignmentId, Func<DenuvoFileInstallResult, string>? stateFactory, Func<CancellationToken, Task>? beforeWrite = null, CancellationToken cancellationToken = default(CancellationToken), IProgress<DenuvoInstallProgress>? progress = null, string? localRootOverride = null)
	{
		return Task.Run(() => RunAsync(gameRoot, configRelativePath, assignmentId, stateFactory, beforeWrite, async delegate(DenuvoFileTransaction transaction, CancellationToken token)
		{
			transaction.progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.Validating));
			StagedFile item = await transaction.StageFileAsync(source, NormalizeRelative(configRelativePath), token);
			return new List<StagedFile> { item };
		}, cancellationToken, progress, localRootOverride));
	}

	private static async Task<DenuvoFileInstallResult> RunAsync(string gameRoot, string configRelativePath, Guid assignmentId, Func<DenuvoFileInstallResult, string>? stateFactory, Func<CancellationToken, Task>? beforeWrite, Func<DenuvoFileTransaction, CancellationToken, Task<List<StagedFile>>> prepare, CancellationToken caller, IProgress<DenuvoInstallProgress>? progress, string? localRootOverride)
	{
		string localRoot = Path.GetFullPath(localRootOverride ?? DefaultLocalRoot);
		gameRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
		string configPath = ResolveTarget(gameRoot, NormalizeRelative(configRelativePath));
		if (!Directory.Exists(gameRoot))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục game Denuvo.");
		}
		using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(caller))
		{
			deadline.CancelAfter(OperationTimeout);
			IDisposable lease;
			DenuvoFileTransaction transaction;
			try
			{
				lease = await AcquireAsync(gameRoot, localRoot, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				try
				{
					await RecoverForRootAsync(gameRoot, localRoot, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					transaction = new DenuvoFileTransaction(gameRoot, assignmentId, localRoot, progress);
				}
				catch
				{
					lease.Dispose();
					throw;
				}
			}
			catch (OperationCanceledException innerException) when (!caller.IsCancellationRequested)
			{
				throw new TimeoutException("Denuvo quá thời gian chờ thao tác đang xử lý; chưa cài file mới.", innerException);
			}
			using (lease)
			{
				try
				{
					List<StagedFile> files = await prepare(transaction, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					if (files.Count == 0)
					{
						throw new InvalidDataException("Package Denuvo không có file nào để cài.");
					}
					if (beforeWrite != null)
					{
						await beforeWrite(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					await transaction.SnapshotAllAsync(files, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					transaction.receipt.Status = "applying";
					transaction.Save();
					for (int i = 0; i < files.Count; i++)
					{
						transaction.ReportFiles(DenuvoInstallStage.Installing, i, files.Count);
						await transaction.PublishEntryAsync(transaction.receipt.Entries[i], files[i].Path, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					DenuvoFileInstallResult result = new DenuvoFileInstallResult(transaction.receipt.Entries.Select((Entry entry) => entry.Target).ToArray(), (from entry in transaction.receipt.Entries
						where entry.Existed
						select entry.Target).ToArray(), transaction.directory, ExistingFile(configPath) ? configPath : null);
					if (stateFactory != null)
					{
						string contents = stateFactory(result);
						string stateSource = Path.Combine(transaction.directory, "staged", "state.json");
						await File.WriteAllTextAsync(stateSource, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
						string target = StatePath(transaction.receipt);
						await transaction.PublishEntryAsync(await transaction.SnapshotAsync(target, Hash(stateSource), deadline.Token).ConfigureAwait(continueOnCapturedContext: false), stateSource, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					deadline.Token.ThrowIfCancellationRequested();
					transaction.receipt.Status = "completed";
					transaction.Save();
					CleanupStaging(transaction.receipt);
					progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.Installing, files.Count, files.Count));
					return result;
				}
				catch (Exception ex)
				{
					Exception error = ex;
					progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.RollingBack));
					using CancellationTokenSource rollbackDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2.0));
					_ = 9;
					try
					{
						await RollbackAsync(Read(transaction.receiptPath, localRoot), rollbackDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (Exception rollbackError)
					{
						throw new DenuvoRollbackException(transaction.directory, error, rollbackError);
					}
					if (error is OperationCanceledException && !caller.IsCancellationRequested)
					{
						throw new TimeoutException("Cài Denuvo quá thời gian chờ; các thay đổi đã được khôi phục.", error);
					}
					ExceptionDispatchInfo.Capture((ex as Exception) ?? throw ex).Throw();
				}
			}
		}
		throw null;
	}

	private async Task<List<StagedFile>> StageZipAsync(string zipPath, string? password, CancellationToken token)
	{
		progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.Validating));
		CheckPath(zipPath);
		using FileStream stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
		using ZipFile archive = new ZipFile(stream);
		if (!string.IsNullOrEmpty(password))
		{
			archive.Password = password;
		}
		Dictionary<string, StagedFile> staged = new Dictionary<string, StagedFile>(StringComparer.OrdinalIgnoreCase);
		long total = 0L;
		if (archive.Count > 8192)
		{
			throw new InvalidDataException("Package Denuvo có quá nhiều file.");
		}
		for (int index = 0; index < archive.Count; index++)
		{
			token.ThrowIfCancellationRequested();
			ZipEntry entry = archive[index];
			if (entry.IsDirectory)
			{
				continue;
			}
			string relative = NormalizeRelative(entry.Name);
			ResolveTarget(receipt.GameRoot, relative);
			if (entry.IsCrypted && string.IsNullOrEmpty(password))
			{
				throw new InvalidDataException("Package Denuvo yêu cầu mật khẩu ZIP.");
			}
			if (entry.Size < 0 || entry.Size > 8589934592L - total)
			{
				throw new InvalidDataException("Dung lượng giải nén Denuvo vượt giới hạn.");
			}
			string path = Path.Combine(directory, "staged", index.ToString("D4"));
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using Stream input = archive.GetInputStream(entry);
			Crc32 crc = new Crc32();
			(long, string) tuple = await CopyNewAsync(input, path, token, entry.Size, crc).ConfigureAwait(continueOnCapturedContext: false);
			if (tuple.Item1 != entry.Size || (entry.Crc >= 0 && (entry.AESKeySize <= 0 || entry.Crc != 0L) && crc.Value != entry.Crc))
			{
				throw new InvalidDataException("File trong ZIP Denuvo sai kích thước hoặc CRC.");
			}
			total += tuple.Item1;
			staged[relative] = new StagedFile(relative, path, tuple.Item2);
		}
		return staged.Values.ToList();
	}

	private async Task<StagedFile> StageFileAsync(string source, string relative, CancellationToken token)
	{
		CheckPath(source);
		string path = Path.Combine(directory, "staged", "config");
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		using FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
		(long, string) tuple = await CopyNewAsync(input, path, token, 8589934592L).ConfigureAwait(continueOnCapturedContext: false);
		if (tuple.Item1 == 0L)
		{
			throw new InvalidDataException("Config Denuvo tải về trống; giữ nguyên config hiện tại.");
		}
		return new StagedFile(relative, path, tuple.Item2);
	}

	private async Task SnapshotAllAsync(List<StagedFile> files, CancellationToken token)
	{
		for (int index = 0; index < files.Count; index++)
		{
			ReportFiles(DenuvoInstallStage.BackingUp, index, files.Count);
			await SnapshotAsync(ResolveTarget(receipt.GameRoot, files[index].Relative), files[index].Hash, token).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task<Entry> SnapshotAsync(string target, string postHash, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		ValidateTarget(receipt, target);
		Entry entry = new Entry
		{
			Target = target,
			Existed = ExistingFile(target),
			PostHash = postHash
		};
		if (entry.Existed)
		{
			entry.Backup = Path.Combine(directory, "files", receipt.Entries.Count.ToString("D4") + ".bak");
			CheckPath(entry.Backup);
			Directory.CreateDirectory(Path.GetDirectoryName(entry.Backup));
			using FileStream source = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
			Entry entry2 = entry;
			entry2.PreHash = (await CopyNewAsync(source, entry.Backup, token, 8589934592L).ConfigureAwait(continueOnCapturedContext: false)).Item2;
			if (Hash(entry.Backup) != entry.PreHash)
			{
				throw new InvalidDataException("Backup Denuvo sai hash.");
			}
		}
		receipt.Entries.Add(entry);
		Save();
		return entry;
	}

	private async Task PublishEntryAsync(Entry entry, string source, CancellationToken token)
	{
		Receipt receipt = Read(receiptPath, this.receipt.LocalRoot);
		Entry owned = receipt.Entries.Single((Entry item) => Equal(item.Target, entry.Target));
		ValidateCurrent(owned);
		await AtomicCopyAsync(source, entry.Target, entry.PostHash, token, delegate
		{
			ValidateCurrent(owned);
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static async Task RecoverAllAsync(string? localRootOverride = null, CancellationToken token = default(CancellationToken))
	{
		string localRoot = Path.GetFullPath(localRootOverride ?? DefaultLocalRoot);
		foreach (string path in EnumerateReceipts(localRoot))
		{
			token.ThrowIfCancellationRequested();
			try
			{
				Receipt receipt = Read(path, localRoot);
				string status = receipt.Status;
				if ((status == "completed" || status == "rolled_back") ? true : false)
				{
					CleanupStaging(receipt);
					continue;
				}
				using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
				deadline.CancelAfter(TimeSpan.FromMinutes(2.0));
				using (await AcquireAsync(receipt.GameRoot, localRoot, deadline.Token).ConfigureAwait(continueOnCapturedContext: false))
				{
					Receipt receipt2 = Read(path, localRoot);
					status = receipt2.Status;
					if ((status == "completed" || status == "rolled_back") ? true : false)
					{
						CleanupStaging(receipt2);
						continue;
					}
					await RollbackAsync(receipt2, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (Exception ex) when (!token.IsCancellationRequested)
			{
				DesktopDiagnosticLog.Write("DenuvoRecovery", "RecoveryPending", "ErrorType=" + ex.GetType().Name);
			}
		}
	}

	private static async Task RecoverForRootAsync(string gameRoot, string localRoot, CancellationToken token)
	{
		foreach (string path in EnumerateReceipts(localRoot))
		{
			try
			{
				Receipt receipt = Read(path, localRoot);
				bool flag = !Equal(receipt.GameRoot, gameRoot);
				if (!flag)
				{
					string status = receipt.Status;
					bool flag2 = ((status == "completed" || status == "rolled_back") ? true : false);
					flag = flag2;
				}
				if (!flag)
				{
					await RollbackAsync(receipt, token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (Exception ex)
			{
				throw new DenuvoRollbackException(Path.GetDirectoryName(path), ex, ex);
			}
		}
	}

	private static IEnumerable<string> EnumerateReceipts(string localRoot)
	{
		string path2 = Path.Combine(localRoot, "DenuvoBackups");
		CheckPath(path2);
		if (!Directory.Exists(path2))
		{
			return Array.Empty<string>();
		}
		return (from path in Directory.EnumerateFiles(path2, "transaction.*", new EnumerationOptions
			{
				RecurseSubdirectories = true,
				AttributesToSkip = FileAttributes.ReparsePoint,
				IgnoreInaccessible = false
			}).Where(delegate(string path)
			{
				string fileName = Path.GetFileName(path);
				return (fileName == "transaction.dat" || fileName == "transaction.json") ? true : false;
			})
			select Path.Combine(Path.GetDirectoryName(path), "transaction.dat")).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static async Task RollbackAsync(Receipt receipt, CancellationToken token)
	{
		Validate(receipt);
		foreach (Entry entry2 in receipt.Entries)
		{
			token.ThrowIfCancellationRequested();
			ValidateCurrent(entry2);
			if (entry2.Existed && (!ExistingFile(entry2.Backup) || Hash(entry2.Backup) != entry2.PreHash))
			{
				throw new InvalidDataException("Backup Denuvo bị mất hoặc thay đổi; đã giữ nguyên file.");
			}
		}
		try
		{
			foreach (Entry entry in receipt.Entries.AsEnumerable().Reverse())
			{
				token.ThrowIfCancellationRequested();
				ValidateTarget(receipt, entry.Target);
				ValidateCurrent(entry);
				if (entry.Existed)
				{
					if (!ExistingFile(entry.Target) || !(Hash(entry.Target) == entry.PreHash))
					{
						await AtomicCopyAsync(entry.Backup, entry.Target, entry.PreHash, token, delegate
						{
							ValidateCurrent(entry);
						}).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				else if (ExistingFile(entry.Target))
				{
					File.Delete(entry.Target);
				}
			}
			receipt.Status = "rolled_back";
			SaveReceipt(receipt);
			CleanupStaging(receipt);
		}
		catch
		{
			receipt.Status = "rollback_failed";
			try
			{
				SaveReceipt(receipt);
			}
			catch
			{
			}
			throw;
		}
	}

	private void Save()
	{
		SaveReceipt(receipt);
	}

	private static void SaveReceipt(Receipt receipt)
	{
		Validate(receipt);
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(receipt, Json);
		if (array.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký Denuvo vượt giới hạn.");
		}
		PublishBytes(receipt.Path, ProtectedData.Protect(array, Entropy, DataProtectionScope.CurrentUser));
		try
		{
			PublishBytes(Path.ChangeExtension(receipt.Path, ".json"), array);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("DenuvoRecovery", "DiagnosticMirrorUnavailable", "ErrorType=" + ex.GetType().Name);
		}
	}

	private static Receipt Read(string path, string localRoot)
	{
		CheckPath(path);
		if (!ExistingFile(path))
		{
			throw new InvalidDataException("Thiếu nhật ký bảo vệ Denuvo; giữ nguyên backup để kiểm tra.");
		}
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		if (fileStream.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký Denuvo vượt giới hạn.");
		}
		using MemoryStream memoryStream = new MemoryStream();
		fileStream.CopyTo(memoryStream);
		Receipt receipt;
		try
		{
			receipt = JsonSerializer.Deserialize<Receipt>(ProtectedData.Unprotect(memoryStream.ToArray(), Entropy, DataProtectionScope.CurrentUser)) ?? throw new InvalidDataException("Nhật ký Denuvo trống.");
		}
		catch (Exception ex) when (((ex is CryptographicException || ex is JsonException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Không xác minh được nhật ký Denuvo.", ex);
		}
		if (!Equal(receipt.Path, path) || !Equal(receipt.LocalRoot, localRoot))
		{
			throw new InvalidDataException("Nhật ký không thuộc transaction Denuvo này.");
		}
		Validate(receipt);
		return receipt;
	}

	private static void Validate(Receipt receipt)
	{
		Guid result;
		bool flag = receipt.Version != 1 || !Guid.TryParseExact(receipt.Id, "N", out result) || receipt.AssignmentId == Guid.Empty || !Path.IsPathFullyQualified(receipt.GameRoot) || !Path.IsPathFullyQualified(receipt.LocalRoot) || receipt.Entries == null || receipt.Entries.Count > 8193;
		if (!flag)
		{
			bool flag2;
			switch (receipt.Status)
			{
			case "preparing":
			case "applying":
			case "completed":
			case "rolled_back":
			case "rollback_failed":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		if (flag)
		{
			throw new InvalidDataException("Nhật ký Denuvo không hợp lệ.");
		}
		string text = Path.Combine(receipt.LocalRoot, "DenuvoBackups", receipt.AssignmentId.ToString("N"), receipt.Id);
		if (!Equal(receipt.Path, Path.Combine(text, "transaction.dat")) || Within(text, receipt.GameRoot) || Within(receipt.GameRoot, Path.Combine(receipt.LocalRoot, "DenuvoBackups")))
		{
			throw new InvalidDataException("Phạm vi transaction Denuvo không hợp lệ.");
		}
		CheckPath(receipt.Path);
		CheckPath(receipt.GameRoot);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < receipt.Entries.Count; i++)
		{
			Entry entry = receipt.Entries[i];
			if (entry == null || !hashSet.Add(Path.GetFullPath(entry.Target)))
			{
				throw new InvalidDataException("Nhật ký Denuvo có file trùng.");
			}
			ValidateTarget(receipt, entry.Target);
			if (!ValidHash(entry.PostHash) || (entry.Existed && (!ValidHash(entry.PreHash) || !Equal(entry.Backup, Path.Combine(text, "files", i.ToString("D4") + ".bak")))) || (!entry.Existed && (entry.Backup != null || entry.PreHash != null)))
			{
				throw new InvalidDataException("Thiếu bằng chứng backup Denuvo.");
			}
			if (entry.Backup != null)
			{
				CheckPath(entry.Backup);
			}
		}
	}

	private static void ValidateTarget(Receipt receipt, string path)
	{
		CheckPath(path);
		if (!Equal(path, StatePath(receipt)))
		{
			if (!Path.IsPathFullyQualified(path) || !Within(path, receipt.GameRoot) || Equal(path, receipt.GameRoot))
			{
				throw new InvalidDataException("Đích Denuvo nằm ngoài thư mục game.");
			}
			NormalizeRelative(Path.GetRelativePath(receipt.GameRoot, path));
		}
	}

	private static void ValidateCurrent(Entry entry)
	{
		CheckPath(entry.Target);
		if (!ExistingFile(entry.Target))
		{
			if (entry.Existed)
			{
				throw new InvalidDataException("File Denuvo có sẵn bị mất; đã dừng để kiểm tra.");
			}
			return;
		}
		string text = Hash(entry.Target);
		if (!(text != entry.PostHash) || (entry.Existed && !(text != entry.PreHash)))
		{
			return;
		}
		throw new InvalidDataException("File thay đổi ngoài thao tác Denuvo; đã giữ nguyên file.");
	}

	internal static string NormalizeRelative(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || Path.IsPathRooted(raw))
		{
			throw new InvalidDataException("Đường dẫn ZIP/config Denuvo không hợp lệ.");
		}
		string[] array = raw.Replace('\\', '/').Split('/');
		string[] array2 = array;
		foreach (string text in array2)
		{
			bool flag = text.Length == 0;
			if (!flag)
			{
				bool flag2 = ((text == "." || text == "..") ? true : false);
				flag = flag2;
			}
			if (flag || text.EndsWith('.') || text.EndsWith(' ') || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(text, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|$)", RegexOptions.IgnoreCase))
			{
				throw new InvalidDataException("Đường dẫn ZIP/config Denuvo không an toàn.");
			}
		}
		return string.Join('/', array);
	}

	private static string ResolveTarget(string root, string relative)
	{
		string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
		if (!Within(fullPath, root))
		{
			throw new InvalidDataException("Đích Denuvo nằm ngoài thư mục game.");
		}
		CheckPath(fullPath);
		return fullPath;
	}

	internal static void CheckPath(string path)
	{
		for (string text = Path.GetFullPath(path); text != null; text = Directory.GetParent(text)?.FullName)
		{
			try
			{
				if ((File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
				{
					throw new InvalidDataException("Đường dẫn Denuvo chứa liên kết; đã dừng để bảo vệ file.");
				}
			}
			catch (FileNotFoundException)
			{
			}
			catch (DirectoryNotFoundException)
			{
			}
		}
	}

	private static bool ExistingFile(string path)
	{
		try
		{
			if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
			{
				throw new InvalidDataException("Đích Denuvo không phải file thường.");
			}
			return true;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (DirectoryNotFoundException)
		{
			return false;
		}
	}

	private static string StatePath(Receipt receipt)
	{
		return Path.Combine(receipt.LocalRoot, "DenuvoFixState", receipt.AssignmentId.ToString("N") + ".json");
	}

	private static bool Equal(string left, string right)
	{
		return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
	}

	private static bool Within(string path, string root)
	{
		return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	private static bool ValidHash(string? hash)
	{
		if (hash != null && hash.Length == 64)
		{
			return hash.All(Uri.IsHexDigit);
		}
		return false;
	}

	private static string Hash(string path)
	{
		CheckPath(path);
		using FileStream source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		return Convert.ToHexString(SHA256.HashData(source));
	}

	private static async Task<(long Length, string Hash)> CopyNewAsync(Stream input, string target, CancellationToken token, long maximum, Crc32? crc = null, Action? onCreated = null)
	{
		CheckPath(target);
		using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		(long Length, string Hash) result;
		await using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough | FileOptions.Asynchronous))
		{
			onCreated?.Invoke();
			byte[] buffer = new byte[81920];
			long total = 0L;
			while (true)
			{
				token.ThrowIfCancellationRequested();
				int num = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(continueOnCapturedContext: false);
				if (num == 0)
				{
					break;
				}
				total += num;
				if (total > maximum)
				{
					throw new InvalidDataException("Dung lượng file Denuvo vượt giới hạn.");
				}
				hash.AppendData(buffer, 0, num);
				crc?.Update(new ArraySegment<byte>(buffer, 0, num));
				await output.WriteAsync(buffer.AsMemory(0, num), token).ConfigureAwait(continueOnCapturedContext: false);
			}
			await output.FlushAsync(token).ConfigureAwait(continueOnCapturedContext: false);
			output.Flush(flushToDisk: true);
			result = (Length: total, Hash: Convert.ToHexString(hash.GetHashAndReset()));
		}
		return result;
	}

	private static async Task AtomicCopyAsync(string source, string target, string expectedHash, CancellationToken token, Action validateDestination)
	{
		CheckPath(source);
		CheckPath(target);
		Directory.CreateDirectory(Path.GetDirectoryName(target));
		string temporary = target + ".nestg-" + Guid.NewGuid().ToString("N") + ".tmp";
		bool created = false;
		try
		{
			using FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
			if ((await CopyNewAsync(input, temporary, token, 8589934592L, null, delegate
			{
				created = true;
			}).ConfigureAwait(continueOnCapturedContext: false)).Item2 != expectedHash)
			{
				throw new InvalidDataException("File Denuvo nguồn sai hash.");
			}
			token.ThrowIfCancellationRequested();
			CheckPath(target);
			validateDestination();
			File.Move(temporary, target, overwrite: true);
		}
		finally
		{
			if (created && File.Exists(temporary))
			{
				CheckPath(temporary);
				File.Delete(temporary);
			}
		}
	}

	private void ReportFiles(DenuvoInstallStage stage, int completed, int total)
	{
		long tickCount = Environment.TickCount64;
		if (completed == 0 || total <= 8 || tickCount - lastProgressAt >= 200)
		{
			lastProgressAt = tickCount;
			progress?.Report(new DenuvoInstallProgress(stage, completed, total));
		}
	}

	private static void CleanupStaging(Receipt receipt)
	{
		try
		{
			string path = Path.Combine(Path.GetDirectoryName(receipt.Path), "staged");
			CheckPath(path);
			if (!Directory.Exists(path))
			{
				return;
			}
			foreach (string item in Directory.EnumerateFiles(path))
			{
				string fileName = Path.GetFileName(item);
				bool flag = ((fileName == "config" || fileName == "state.json") ? true : false);
				if (flag || (fileName.Length == 4 && fileName.All(char.IsAsciiDigit)))
				{
					CheckPath(item);
					File.Delete(item);
				}
			}
			if (!Directory.EnumerateFileSystemEntries(path).Any())
			{
				Directory.Delete(path);
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("DenuvoRecovery", "StagingCleanupPending", "ErrorType=" + ex.GetType().Name);
		}
	}

	private static void PublishBytes(string path, byte[] bytes)
	{
		CheckPath(path);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		bool flag = false;
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
			{
				flag = true;
				fileStream.Write(bytes);
				fileStream.Flush(flushToDisk: true);
			}
			CheckPath(path);
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			if (flag && File.Exists(text))
			{
				CheckPath(text);
				File.Delete(text);
			}
		}
	}

	private static async Task<IDisposable> AcquireAsync(string gameRoot, string localRoot, CancellationToken token)
	{
		CheckPath(gameRoot);
		SemaphoreSlim gate = Gates.GetOrAdd(gameRoot, (string _) => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			string lockPath = Path.Combine(localRoot, "DenuvoTransactionLocks", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gameRoot.ToUpperInvariant()))) + ".lock");
			CheckPath(lockPath);
			Directory.CreateDirectory(Path.GetDirectoryName(lockPath));
			while (true)
			{
				token.ThrowIfCancellationRequested();
				try
				{
					return new Lease(gate, new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
				}
				catch (IOException ex) when ((uint)((ex.HResult & 0xFFFF) - 32) <= 1u)
				{
					await Task.Delay(100, token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		catch
		{
			gate.Release();
			throw;
		}
	}
}
