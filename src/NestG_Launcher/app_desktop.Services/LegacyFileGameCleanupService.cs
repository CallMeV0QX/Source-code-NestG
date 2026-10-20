using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal static class LegacyFileGameCleanupService
{
	[CompilerGenerated]
	private sealed class _003CEnumerateFilesWithoutReparsePoints_003Ed__13 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string rootDirectory;

		public string _003C_003E3__rootDirectory;

		private Stack<string> _003Cpending_003E5__2;

		private string _003CcurrentDirectory_003E5__3;

		private IEnumerator<string> _003C_003E7__wrap3;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CEnumerateFilesWithoutReparsePoints_003Ed__13(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 1)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003Cpending_003E5__2 = null;
			_003CcurrentDirectory_003E5__3 = null;
			_003C_003E7__wrap3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				int num = _003C_003E1__state;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					_003C_003E1__state = -3;
					goto IL_009a;
				}
				_003C_003E1__state = -1;
				_003Cpending_003E5__2 = new Stack<string>();
				_003Cpending_003E5__2.Push(rootDirectory);
				goto IL_0100;
				IL_009a:
				if (_003C_003E7__wrap3.MoveNext())
				{
					string current = _003C_003E7__wrap3.Current;
					_003C_003E2__current = current;
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap3 = null;
				foreach (string item in SafeEnumerateDirectories(_003CcurrentDirectory_003E5__3))
				{
					if (!IsReparsePoint(item))
					{
						_003Cpending_003E5__2.Push(item);
					}
				}
				_003CcurrentDirectory_003E5__3 = null;
				goto IL_0100;
				IL_0100:
				if (_003Cpending_003E5__2.Count > 0)
				{
					_003CcurrentDirectory_003E5__3 = _003Cpending_003E5__2.Pop();
					_003C_003E7__wrap3 = SafeEnumerateFiles(_003CcurrentDirectory_003E5__3).GetEnumerator();
					_003C_003E1__state = -3;
					goto IL_009a;
				}
				return false;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap3 != null)
			{
				_003C_003E7__wrap3.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CEnumerateFilesWithoutReparsePoints_003Ed__13 _003CEnumerateFilesWithoutReparsePoints_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CEnumerateFilesWithoutReparsePoints_003Ed__ = this;
			}
			else
			{
				_003CEnumerateFilesWithoutReparsePoints_003Ed__ = new _003CEnumerateFilesWithoutReparsePoints_003Ed__13(0);
			}
			_003CEnumerateFilesWithoutReparsePoints_003Ed__.rootDirectory = _003C_003E3__rootDirectory;
			return _003CEnumerateFilesWithoutReparsePoints_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private const string LegacyFolderName = "FileGame";

	private const string MigrationFileName = "filegame-cleanup-v1.done";

	public static async Task CleanupAsync(string storageFolderName, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			await Task.Run(delegate
			{
				Cleanup(storageFolderName, cancellationToken);
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
	}

	private static void Cleanup(string storageFolderName, CancellationToken cancellationToken)
	{
		string text = ResolveLegacyFileGameDirectory();
		string directoryName = Path.GetDirectoryName(text);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			TryDeletePendingQuarantineDirectories(directoryName);
		}
		string text2 = ResolveMarkerPath(storageFolderName);
		if (File.Exists(text2) && !Directory.Exists(text))
		{
			return;
		}
		cancellationToken.ThrowIfCancellationRequested();
		if (!Directory.Exists(text))
		{
			WriteMarker(text2, "Legacy FileGame directory was not present.");
		}
		else if (IsSafeLegacyDirectory(text))
		{
			string quarantineDirectory;
			if (TryDeleteDirectory(text))
			{
				WriteMarker(text2, "Deleted " + text);
			}
			else if (TryQuarantineDirectory(text, out quarantineDirectory))
			{
				TryDeleteDirectory(quarantineDirectory);
				WriteMarker(text2, "Quarantined " + text + " to " + quarantineDirectory);
			}
		}
	}

	private static string ResolveLegacyFileGameDirectory()
	{
		return Path.GetFullPath(Path.Combine(ResolveAppDesktopDirectory(), "FileGame"));
	}

	private static string ResolveAppDesktopDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "app_desktop.csproj")))
			{
				return directoryInfo.FullName;
			}
		}
		return AppContext.BaseDirectory;
	}

	private static string ResolveMarkerPath(string storageFolderName)
	{
		string text = (string.IsNullOrWhiteSpace(storageFolderName) ? "NestGLauncher" : Path.GetFileName(storageFolderName.Trim()));
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "NestGLauncher";
		}
		return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), text, "Migrations"), "filegame-cleanup-v1.done");
	}

	private static bool IsSafeLegacyDirectory(string directory)
	{
		try
		{
			string text = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string b = Path.GetPathRoot(text)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return !string.Equals(text, b, StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(text), "FileGame", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryDeleteDirectory(string directory)
	{
		try
		{
			if (!Directory.Exists(directory))
			{
				return true;
			}
			if (IsReparsePoint(directory))
			{
				Directory.Delete(directory);
				return true;
			}
			NormalizeAttributes(directory);
			Directory.Delete(directory, recursive: true);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryQuarantineDirectory(string legacyDirectory, out string quarantineDirectory)
	{
		quarantineDirectory = string.Empty;
		try
		{
			string directoryName = Path.GetDirectoryName(legacyDirectory);
			if (string.IsNullOrWhiteSpace(directoryName))
			{
				return false;
			}
			string text = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
			for (int i = 0; i < 10; i++)
			{
				string text2 = ((i == 0) ? text : $"{text}-{i}");
				string text3 = Path.Combine(directoryName, "FileGame.legacy-delete-pending-" + text2);
				if (!Directory.Exists(text3))
				{
					Directory.Move(legacyDirectory, text3);
					quarantineDirectory = text3;
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void TryDeletePendingQuarantineDirectories(string parentDirectory)
	{
		try
		{
			if (!Directory.Exists(parentDirectory))
			{
				return;
			}
			foreach (string item in Directory.EnumerateDirectories(parentDirectory, "FileGame.legacy-delete-pending-*"))
			{
				TryDeleteDirectory(item);
			}
		}
		catch
		{
		}
	}

	private static void NormalizeAttributes(string directory)
	{
		foreach (string item in EnumerateFilesWithoutReparsePoints(directory))
		{
			try
			{
				File.SetAttributes(item, FileAttributes.Normal);
			}
			catch
			{
			}
		}
		foreach (string item2 in EnumerateDirectoriesWithoutReparsePoints(directory))
		{
			try
			{
				File.SetAttributes(item2, FileAttributes.Directory);
			}
			catch
			{
			}
		}
		try
		{
			File.SetAttributes(directory, FileAttributes.Directory);
		}
		catch
		{
		}
	}

	private static bool IsReparsePoint(string directory)
	{
		try
		{
			return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
		}
		catch
		{
			return true;
		}
	}

	[IteratorStateMachine(typeof(_003CEnumerateFilesWithoutReparsePoints_003Ed__13))]
	private static IEnumerable<string> EnumerateFilesWithoutReparsePoints(string rootDirectory)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CEnumerateFilesWithoutReparsePoints_003Ed__13(-2)
		{
			_003C_003E3__rootDirectory = rootDirectory
		};
	}

	private static IEnumerable<string> EnumerateDirectoriesWithoutReparsePoints(string rootDirectory)
	{
		List<string> list = new List<string>();
		Stack<string> stack = new Stack<string>();
		stack.Push(rootDirectory);
		while (stack.Count > 0)
		{
			foreach (string item in SafeEnumerateDirectories(stack.Pop()))
			{
				if (!IsReparsePoint(item))
				{
					list.Add(item);
					stack.Push(item);
				}
			}
		}
		list.Reverse();
		return list;
	}

	private static IEnumerable<string> SafeEnumerateFiles(string directory)
	{
		try
		{
			return Directory.EnumerateFiles(directory).ToArray();
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static IEnumerable<string> SafeEnumerateDirectories(string directory)
	{
		try
		{
			return Directory.EnumerateDirectories(directory).ToArray();
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static void WriteMarker(string markerPath, string message)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
			File.WriteAllText(markerPath, $"CompletedUtc: {DateTime.UtcNow:O}{Environment.NewLine}{message}{Environment.NewLine}");
		}
		catch
		{
		}
	}
}
