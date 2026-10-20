using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace app_desktop.Services;

internal static class SteamInstallLocator
{
	[CompilerGenerated]
	private sealed class _003CEnumerateCommonCandidates_003Ed__4 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private List<string?>.Enumerator _003C_003E7__wrap1;

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
		public _003CEnumerateCommonCandidates_003Ed__4(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || (uint)(num - 1) <= 1u)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003C_003E7__wrap1 = default(List<string>.Enumerator);
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
				{
					_003C_003E1__state = -1;
					List<string> list = new List<string>
					{
						Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
						Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
					};
					foreach (DriveInfo item in from drive in DriveInfo.GetDrives()
						where drive.IsReady
						select drive)
					{
						string fullName = item.RootDirectory.FullName;
						list.Add(Path.Combine(fullName, "Steam"));
						list.Add(Path.Combine(fullName, "Program Files", "Steam"));
						list.Add(Path.Combine(fullName, "Program Files (x86)", "Steam"));
						list.Add(Path.Combine(fullName, "Games", "Steam"));
						list.Add(Path.Combine(fullName, "Program", "Steam"));
						list.Add(Path.Combine(fullName, "Program", "Stream", "Steam"));
					}
					_003C_003E7__wrap1 = list.GetEnumerator();
					_003C_003E1__state = -3;
					break;
				}
				case 1:
					_003C_003E1__state = -3;
					break;
				case 2:
					_003C_003E1__state = -3;
					break;
				}
				while (_003C_003E7__wrap1.MoveNext())
				{
					string current = _003C_003E7__wrap1.Current;
					if (!string.IsNullOrWhiteSpace(current))
					{
						if (string.Equals(Path.GetFileName(current), "Steam", StringComparison.OrdinalIgnoreCase))
						{
							_003C_003E2__current = current;
							_003C_003E1__state = 1;
							return true;
						}
						_003C_003E2__current = Path.Combine(current, "Steam");
						_003C_003E1__state = 2;
						return true;
					}
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap1 = default(List<string>.Enumerator);
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
			((IDisposable)_003C_003E7__wrap1/*cast due to .constrained prefix*/).Dispose();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CEnumerateCommonCandidates_003Ed__4(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CEnumerateRegistryCandidates_003Ed__2 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IEnumerator<string> _003C_003E7__wrap1;

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
		public _003CEnumerateRegistryCandidates_003Ed__2(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			switch (_003C_003E1__state)
			{
			case -3:
			case 1:
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
				break;
			case -4:
			case 2:
				try
				{
				}
				finally
				{
					_003C_003Em__Finally2();
				}
				break;
			case -5:
			case 3:
				try
				{
				}
				finally
				{
					_003C_003Em__Finally3();
				}
				break;
			}
			_003C_003E7__wrap1 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003C_003E7__wrap1 = ReadRegistryPath(Registry.CurrentUser, "Software\\Valve\\Steam").GetEnumerator();
					_003C_003E1__state = -3;
					goto IL_0078;
				case 1:
					_003C_003E1__state = -3;
					goto IL_0078;
				case 2:
					_003C_003E1__state = -4;
					goto IL_00df;
				case 3:
					{
						_003C_003E1__state = -5;
						break;
					}
					IL_0078:
					if (_003C_003E7__wrap1.MoveNext())
					{
						string current = _003C_003E7__wrap1.Current;
						_003C_003E2__current = current;
						_003C_003E1__state = 1;
						return true;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap1 = null;
					_003C_003E7__wrap1 = ReadRegistryPath(Registry.LocalMachine, "SOFTWARE\\Valve\\Steam").GetEnumerator();
					_003C_003E1__state = -4;
					goto IL_00df;
					IL_00df:
					if (_003C_003E7__wrap1.MoveNext())
					{
						string current2 = _003C_003E7__wrap1.Current;
						_003C_003E2__current = current2;
						_003C_003E1__state = 2;
						return true;
					}
					_003C_003Em__Finally2();
					_003C_003E7__wrap1 = null;
					_003C_003E7__wrap1 = ReadRegistryPath(Registry.LocalMachine, "SOFTWARE\\WOW6432Node\\Valve\\Steam").GetEnumerator();
					_003C_003E1__state = -5;
					break;
				}
				if (_003C_003E7__wrap1.MoveNext())
				{
					string current3 = _003C_003E7__wrap1.Current;
					_003C_003E2__current = current3;
					_003C_003E1__state = 3;
					return true;
				}
				_003C_003Em__Finally3();
				_003C_003E7__wrap1 = null;
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
			if (_003C_003E7__wrap1 != null)
			{
				_003C_003E7__wrap1.Dispose();
			}
		}

		private void _003C_003Em__Finally2()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap1 != null)
			{
				_003C_003E7__wrap1.Dispose();
			}
		}

		private void _003C_003Em__Finally3()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap1 != null)
			{
				_003C_003E7__wrap1.Dispose();
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
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CEnumerateRegistryCandidates_003Ed__2(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CReadRegistryPath_003Ed__3 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private RegistryKey root;

		public RegistryKey _003C_003E3__root;

		private string subKeyName;

		public string _003C_003E3__subKeyName;

		private RegistryKey _003Ckey_003E5__2;

		private string[] _003C_003E7__wrap2;

		private int _003C_003E7__wrap3;

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
		public _003CReadRegistryPath_003Ed__3(int _003C_003E1__state)
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
			_003Ckey_003E5__2 = null;
			_003C_003E7__wrap2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			bool result;
			try
			{
				int num = _003C_003E1__state;
				if (num == 0)
				{
					_003C_003E1__state = -1;
					_003Ckey_003E5__2 = root.OpenSubKey(subKeyName);
					_003C_003E1__state = -3;
					if (_003Ckey_003E5__2 == null)
					{
						result = false;
						goto IL_00cd;
					}
					_003C_003E7__wrap2 = SteamRegistryValueNames;
					_003C_003E7__wrap3 = 0;
					goto IL_00b4;
				}
				if (num == 1)
				{
					_003C_003E1__state = -3;
					goto IL_00a6;
				}
				result = false;
				goto end_IL_0000;
				IL_00b4:
				if (_003C_003E7__wrap3 >= _003C_003E7__wrap2.Length)
				{
					_003C_003E7__wrap2 = null;
					result = false;
					goto IL_00cd;
				}
				string name = _003C_003E7__wrap2[_003C_003E7__wrap3];
				if (!(_003Ckey_003E5__2.GetValue(name) is string value) || string.IsNullOrWhiteSpace(value))
				{
					goto IL_00a6;
				}
				_003C_003E2__current = value;
				_003C_003E1__state = 1;
				result = true;
				goto end_IL_0000;
				IL_00cd:
				_003C_003Em__Finally1();
				goto end_IL_0000;
				IL_00a6:
				_003C_003E7__wrap3++;
				goto IL_00b4;
				end_IL_0000:;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
			return result;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003Ckey_003E5__2 != null)
			{
				((IDisposable)_003Ckey_003E5__2).Dispose();
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
			_003CReadRegistryPath_003Ed__3 _003CReadRegistryPath_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CReadRegistryPath_003Ed__ = this;
			}
			else
			{
				_003CReadRegistryPath_003Ed__ = new _003CReadRegistryPath_003Ed__3(0);
			}
			_003CReadRegistryPath_003Ed__.root = _003C_003E3__root;
			_003CReadRegistryPath_003Ed__.subKeyName = _003C_003E3__subKeyName;
			return _003CReadRegistryPath_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private static readonly string[] SteamRegistryValueNames = new string[3] { "SteamPath", "InstallPath", "SteamExe" };

	public static string? FindSteamDirectory(bool allowDriveScan = true)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in EnumerateRegistryCandidates().Concat(EnumerateCommonCandidates()))
		{
			string text = NormalizeCandidate(item);
			if (text != null && hashSet.Add(text) && IsSteamDirectory(text))
			{
				return text;
			}
		}
		if (!allowDriveScan)
		{
			return null;
		}
		DriveInfo[] drives = DriveInfo.GetDrives();
		foreach (DriveInfo driveInfo in drives)
		{
			if (driveInfo.IsReady && (driveInfo.DriveType == DriveType.Fixed || driveInfo.DriveType == DriveType.Removable))
			{
				string text2 = ScanDriveForSteam(driveInfo.RootDirectory.FullName, hashSet);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return text2;
				}
			}
		}
		return null;
	}

	[IteratorStateMachine(typeof(_003CEnumerateRegistryCandidates_003Ed__2))]
	private static IEnumerable<string> EnumerateRegistryCandidates()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CEnumerateRegistryCandidates_003Ed__2(-2);
	}

	[IteratorStateMachine(typeof(_003CReadRegistryPath_003Ed__3))]
	private static IEnumerable<string> ReadRegistryPath(RegistryKey root, string subKeyName)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CReadRegistryPath_003Ed__3(-2)
		{
			_003C_003E3__root = root,
			_003C_003E3__subKeyName = subKeyName
		};
	}

	[IteratorStateMachine(typeof(_003CEnumerateCommonCandidates_003Ed__4))]
	private static IEnumerable<string> EnumerateCommonCandidates()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CEnumerateCommonCandidates_003Ed__4(-2);
	}

	private static string? ScanDriveForSteam(string rootDirectory, ISet<string> checkedDirectories)
	{
		Stack<string> stack = new Stack<string>();
		stack.Push(rootDirectory);
		while (stack.Count > 0)
		{
			string text = NormalizeCandidate(stack.Pop());
			if (text == null || !checkedDirectories.Add(text))
			{
				continue;
			}
			if (IsSteamDirectory(text))
			{
				return text;
			}
			if (ShouldSkipDirectory(text))
			{
				continue;
			}
			foreach (string item in SafeEnumerateDirectories(text))
			{
				stack.Push(item);
			}
		}
		return null;
	}

	private static bool IsSteamDirectory(string directory)
	{
		if (Directory.Exists(directory))
		{
			return File.Exists(Path.Combine(directory, "steam.exe"));
		}
		return false;
	}

	private static string? NormalizeCandidate(string? candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate))
		{
			return null;
		}
		string text = candidate.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar);
		if (File.Exists(text) && string.Equals(Path.GetFileName(text), "steam.exe", StringComparison.OrdinalIgnoreCase))
		{
			text = Path.GetDirectoryName(text) ?? text;
		}
		try
		{
			string fullPath = Path.GetFullPath(text);
			string pathRoot = Path.GetPathRoot(fullPath);
			if (!string.IsNullOrWhiteSpace(pathRoot) && fullPath.Length <= pathRoot.Length)
			{
				return pathRoot;
			}
			return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}
		catch
		{
			return null;
		}
	}

	private static bool ShouldSkipDirectory(string directory)
	{
		try
		{
			if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
			{
				return true;
			}
			string fileName = Path.GetFileName(directory);
			return fileName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) || fileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Windows", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return true;
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
}
