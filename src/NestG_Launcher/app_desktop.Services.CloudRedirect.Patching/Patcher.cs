using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace app_desktop.Services.CloudRedirect.Patching;

internal class Patcher
{
	private class CloudRedirectResolveResult
	{
		public PatchEntry[] Patches;

		public byte[] DynamicCodeCave;

		public int CodeCaveFileOffset;
	}

	private static readonly string[] HijackCandidates = new string[2] { "xinput1_4.dll", "dwmapi.dll" };

	private static readonly byte[] AesKey = SteamToolsCrypto.AesKey;

	private const int CloudRedirectCaveMinSize = 144;

	private static readonly byte[] CloudRedirectCaveContent = new byte[144]
	{
		81, 82, 65, 80, 83, 72, 131, 236, 40, 72,
		141, 13, 94, 0, 0, 0, 255, 21, 0, 0,
		0, 0, 72, 133, 192, 116, 52, 72, 141, 21,
		95, 0, 0, 0, 72, 139, 200, 255, 21, 0,
		0, 0, 0, 72, 133, 192, 116, 31, 72, 139,
		216, 72, 139, 76, 36, 64, 72, 139, 84, 36,
		56, 76, 139, 68, 36, 48, 76, 141, 13, 0,
		0, 0, 0, 255, 211, 133, 192, 117, 19, 72,
		131, 196, 40, 91, 65, 88, 90, 89, 72, 137,
		92, 36, 32, 233, 0, 0, 0, 0, 72, 131,
		196, 40, 91, 65, 88, 90, 89, 176, 1, 195,
		99, 108, 111, 117, 100, 95, 114, 101, 100, 105,
		114, 101, 99, 116, 46, 100, 108, 108, 0, 67,
		108, 111, 117, 100, 79, 110, 83, 101, 110, 100,
		80, 107, 116, 0
	};

	private string _steamPath;

	private string _cloudRedirectDllSourcePath;

	private bool _verbose;

	private Action<string> _log;

	private byte[] _cachedPayload;

	private long _cachedPayloadSize;

	private DateTime _cachedPayloadTime;

	private const string XinputUrl = "https://files.catbox.moe/heom44.dll";

	private const string DwmapiUrl = "https://files.catbox.moe/32p6f9.dll";

	private const string XinputFallbackUrl = "https://update.aaasn.com/update";

	private const string DwmapiFallbackUrl = "https://update.aaasn.com/dwmapi";

	private const string XinputHash = "ddb1f0909c7092f06890674f90b5d4f1198724b05b4bf1e656b4063897340243";

	private const string DwmapiHash = "1ce49ed63af004ad37a4d2921a5659a17001c4c0026d6245fcc0d543e9c265d0";

	private const int StExePatchOffset = 164592;

	private static readonly byte[] StExeOriginal = new byte[2] { 64, 85 };

	private static readonly byte[] StExePatched = new byte[2] { 195, 144 };

	public Patcher(string steamPath, Action<string> log = null, string cloudRedirectDllSourcePath = null)
	{
		_steamPath = steamPath;
		_cloudRedirectDllSourcePath = cloudRedirectDllSourcePath;
		_log = log ?? ((Action<string>)delegate
		{
		});
	}

	private static byte[] ReadFileShared(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		byte[] array = new byte[fileStream.Length];
		fileStream.ReadExactly(array);
		return array;
	}

	private byte[] GetDecryptedPayload(string cachePath)
	{
		FileInfo fileInfo = new FileInfo(cachePath);
		if (!fileInfo.Exists)
		{
			return null;
		}
		long length = fileInfo.Length;
		DateTime lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
		if (_cachedPayload != null && _cachedPayloadSize == length && _cachedPayloadTime == lastWriteTimeUtc)
		{
			return _cachedPayload;
		}
		byte[] array;
		try
		{
			using FileStream fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			array = new byte[fileStream.Length];
			fileStream.ReadExactly(array);
		}
		catch (IOException)
		{
			return null;
		}
		if (array.Length < 32)
		{
			return null;
		}
		try
		{
			byte[] iv = array.AsSpan(0, 16).ToArray();
			byte[] array2 = PayloadCrypto.AesCbcDecrypt(array.AsSpan(16).ToArray(), AesKey, iv);
			if (array2.Length < 4)
			{
				return null;
			}
			using ZLibStream zLibStream = new ZLibStream(new MemoryStream(array2, 4, array2.Length - 4), CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			zLibStream.CopyTo(memoryStream);
			_cachedPayload = memoryStream.ToArray();
			_cachedPayloadSize = length;
			_cachedPayloadTime = lastWriteTimeUtc;
			return _cachedPayload;
		}
		catch
		{
			return null;
		}
	}

	private (byte[] payload, byte[] iv, string error) ReadAndDecryptPayload(string cachePath)
	{
		byte[] array;
		try
		{
			array = ReadFileShared(cachePath);
		}
		catch (IOException)
		{
			return (payload: null, iv: null, error: "Payload cache is in use - close Steam first");
		}
		if (array.Length < 32)
		{
			return (payload: null, iv: null, error: "Cache file too small");
		}
		byte[] array2 = array.AsSpan(0, 16).ToArray();
		byte[] ct = array.AsSpan(16).ToArray();
		Log("  Decrypting..");
		byte[] array3;
		try
		{
			array3 = PayloadCrypto.AesCbcDecrypt(ct, AesKey, array2);
		}
		catch (Exception ex2)
		{
			return (payload: null, iv: null, error: "Decryption failed: " + ex2.Message);
		}
		if (array3.Length < 4)
		{
			return (payload: null, iv: null, error: "Decrypted payload too small");
		}
		byte[] array4;
		try
		{
			using ZLibStream zLibStream = new ZLibStream(new MemoryStream(array3, 4, array3.Length - 4), CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			zLibStream.CopyTo(memoryStream);
			array4 = memoryStream.ToArray();
		}
		catch (Exception ex3)
		{
			return (payload: null, iv: null, error: "Decompression failed: " + ex3.Message);
		}
		Log($"  Payload: {array4.Length} bytes");
		return (payload: array4, iv: array2, error: null);
	}

	private string FindCoreDll()
	{
		string[] hijackCandidates = HijackCandidates;
		foreach (string text in hijackCandidates)
		{
			string path = Path.Combine(_steamPath, text);
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				byte[] array = new byte[fileStream.Length];
				fileStream.ReadExactly(array);
				if (Signatures.ScanForBytes(array, 0, array.Length, AesKey) >= 0)
				{
					return text;
				}
			}
			catch (IOException)
			{
			}
		}
		return null;
	}

	public bool HasCoreDll()
	{
		return FindCoreDll() != null;
	}

	public bool HasPayloadCache()
	{
		return Fingerprint.FindCachePath(_steamPath, verbose: false, _log) != null;
	}

	private static string ComputeSha256(byte[] data)
	{
		return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
	}

	public PatchResult RepairCoreDlls()
	{
		return RepairCoreDllsAsync().GetAwaiter().GetResult();
	}

	public async Task<PatchResult> RepairCoreDllsAsync()
	{
		_verbose = true;
		PatchResult result = new PatchResult();
		try
		{
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(60.0)
			};
			http.DefaultRequestHeaders.UserAgent.ParseAdd("Stella/1.0");
			(string, string, string, string)[] array = new(string, string, string, string)[2]
			{
				("xinput1_4.dll", "https://files.catbox.moe/heom44.dll", "https://update.aaasn.com/update", "ddb1f0909c7092f06890674f90b5d4f1198724b05b4bf1e656b4063897340243"),
				("dwmapi.dll", "https://files.catbox.moe/32p6f9.dll", "https://update.aaasn.com/dwmapi", "1ce49ed63af004ad37a4d2921a5659a17001c4c0026d6245fcc0d543e9c265d0")
			};
			(string name, string url, string fallback, string hash)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, string, string, string) tuple = array2[i];
				string name = tuple.Item1;
				string item = tuple.Item2;
				string fallback = tuple.Item3;
				string hash = tuple.Item4;
				string destPath = Path.Combine(_steamPath, name);
				if (File.Exists(destPath))
				{
					try
					{
						if (ComputeSha256(ReadFileShared(destPath)) == hash)
						{
							Log("  " + name + ": already present, hash OK");
							continue;
						}
						Log("  " + name + ": present but hash mismatch, re-downloading..");
					}
					catch (IOException)
					{
						Log("  " + name + ": could not read existing file, re-downloading..");
					}
				}
				byte[] data = null;
				bool fromFallback = false;
				Log("Downloading " + name + "..");
				try
				{
					byte[] array3 = await http.GetByteArrayAsync(item).ConfigureAwait(continueOnCapturedContext: false);
					if (array3 != null && array3.Length != 0 && ComputeSha256(array3) == hash)
					{
						data = array3;
					}
					else
					{
						Log($"  Primary returned bad data (len={((array3 != null) ? array3.Length : 0)})");
					}
				}
				catch (Exception ex2)
				{
					Log("  Primary failed: " + ex2.Message);
				}
				if (data == null)
				{
					Log("  Trying fallback..");
					try
					{
						byte[] array4 = await http.GetByteArrayAsync(fallback).ConfigureAwait(continueOnCapturedContext: false);
						if (array4 != null && array4.Length != 0 && ComputeSha256(array4) == hash)
						{
							data = array4;
							fromFallback = true;
						}
						else
						{
							Log($"  Fallback returned bad data (len={((array4 != null) ? array4.Length : 0)})");
						}
					}
					catch (Exception ex3)
					{
						Log("  Fallback failed: " + ex3.Message);
					}
				}
				if (data == null)
				{
					return result.Fail("Could not download " + name + ": no source returned a valid file");
				}
				try
				{
					FileUtils.AtomicWriteAllBytes(destPath, data);
					Log($"  {name}: {data.Length} bytes" + (fromFallback ? " (fallback)" : ""));
				}
				catch (IOException ex4)
				{
					return result.Fail("Could not write " + name + ": " + ex4.Message);
				}
			}
			result.DllPatched = true;
			result.Succeeded = true;
			Log("DLL repair complete.");
		}
		catch (Exception ex5)
		{
			result.Fail("Unexpected error: " + ex5.Message);
		}
		return result;
	}

	public PatchState GetPatchState()
	{
		_verbose = false;
		string text = FindCoreDll();
		if (text == null)
		{
			return PatchState.NotInstalled;
		}
		string path = Path.Combine(_steamPath, text);
		byte[] array;
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			array = new byte[fileStream.Length];
			fileStream.ReadExactly(array);
		}
		catch (IOException)
		{
			return PatchState.UnknownVersion;
		}
		PatchEntry[] array2 = ResolveCorePatchOffsets(array);
		if (array2 == null)
		{
			return PatchState.UnknownVersion;
		}
		(byte[] data, int applied, int skipped, List<string> errors) tuple = CheckPatches(array, array2);
		int item = tuple.applied;
		int item2 = tuple.skipped;
		if (tuple.errors.Count > 0)
		{
			return PatchState.OutOfDate;
		}
		if (item == 0 && item2 == array2.Length)
		{
			return PatchState.Patched;
		}
		if (item2 == 0 && item == array2.Length)
		{
			return PatchState.Unpatched;
		}
		return PatchState.PartiallyPatched;
	}

	public PatchState GetOfflinePatchState()
	{
		_verbose = false;
		string text = Fingerprint.FindCachePath(_steamPath, verbose: false, _log);
		if (text == null)
		{
			return PatchState.NotInstalled;
		}
		byte[] decryptedPayload = GetDecryptedPayload(text);
		if (decryptedPayload == null)
		{
			return PatchState.UnknownVersion;
		}
		PatchEntry[] array = ResolveSetupPatchOffsets(decryptedPayload);
		if (array == null)
		{
			return PatchState.UnknownVersion;
		}
		(byte[] data, int applied, int skipped, List<string> errors) tuple = CheckPatches(decryptedPayload, array);
		int item = tuple.applied;
		int item2 = tuple.skipped;
		if (tuple.errors.Count > 0)
		{
			return PatchState.OutOfDate;
		}
		if (item == 0 && item2 == array.Length)
		{
			return PatchState.Patched;
		}
		if (item2 == 0 && item == array.Length)
		{
			return PatchState.Unpatched;
		}
		return PatchState.PartiallyPatched;
	}

	public PatchResult ApplyOfflineSetup()
	{
		_verbose = true;
		PatchResult patchResult = new PatchResult();
		long? steamVersion = SteamDetector.GetSteamVersion(_steamPath);
		if (!steamVersion.HasValue)
		{
			Log("  WARNING: Could not read Steam version from manifest");
			return patchResult.Fail("Steam version could not be determined. Cannot safely patch.");
		}
		if (!SteamDetector.IsSupportedSteamVersion(steamVersion.Value))
		{
			string text = string.Join(", ", SteamDetector.SupportedSteamVersions);
			Log($"  Steam version: {steamVersion.Value} (UNSUPPORTED)");
			Log("  Supported versions: " + text);
			return patchResult.Fail($"Steam version mismatch: installed {steamVersion.Value}, supported {text}. " + "Patching an unsupported version risks corrupting steamclient64.dll. Update CloudRedirect or downgrade Steam.");
		}
		Log($"  Steam version: {steamVersion.Value} (OK)");
		try
		{
			string text2 = FindCoreDll();
			if (text2 == null)
			{
				return patchResult.Fail("SteamTools Core DLL not found. Is SteamTools installed?");
			}
			string text3 = Path.Combine(_steamPath, text2);
			byte[] array;
			try
			{
				array = ReadFileShared(text3);
			}
			catch (IOException)
			{
				return patchResult.Fail(text2 + " is in use - close Steam first");
			}
			Log("Patching " + text2 + "..");
			PatchEntry[] array2 = ResolveCorePatchOffsets(array);
			if (array2 == null)
			{
				return patchResult.Fail("Could not identify patch locations in " + text2 + " - unsupported version?");
			}
			var (data, num, num2, list) = ApplyPatches(array, array2);
			if (list.Count > 0)
			{
				foreach (string item in list)
				{
					Log(item);
				}
				return patchResult.Fail("Byte mismatch in " + text2 + " - wrong version?");
			}
			string text4 = Fingerprint.FindCachePath(_steamPath, verbose: true, _log);
			if (text4 == null)
			{
				Log("Payload cache not found. Deploying embedded payload..");
				text4 = DeployEmbeddedPayload();
				if (text4 == null)
				{
					return patchResult.Fail("Could not deploy payload cache.");
				}
			}
			Log("Patching payload (offline setup)..");
			var (array3, iv, error) = ReadAndDecryptPayload(text4);
			if (array3 == null)
			{
				return patchResult.Fail(error);
			}
			PatchEntry[] array4 = ResolveSetupPatchOffsets(array3);
			if (array4 == null || array4.Length == 0)
			{
				return patchResult.Fail("Could not identify activation patch locations in payload - unsupported version?");
			}
			var (patchedPayload, num3, num4, list2) = ApplyPatches(array3, array4);
			if (list2.Count > 0)
			{
				foreach (string item2 in list2)
				{
					Log(item2);
				}
				return patchResult.Fail("Byte mismatch in payload - wrong version?");
			}
			BackupBoth(text4, text3);
			if (num3 > 0)
			{
				ReEncryptAndWrite(text4, patchedPayload, iv);
				Log($"  {num3} patch(es) applied to payload" + ((num4 > 0) ? $", {num4} already done" : ""));
			}
			else
			{
				Log("  Payload: already patched");
			}
			patchResult.CachePatched = true;
			if (num > 0)
			{
				FileUtils.AtomicWriteAllBytes(text3, data);
				Log($"  {num} patch(es) applied to {text2}" + ((num2 > 0) ? $", {num2} already done" : ""));
			}
			else
			{
				Log("  " + text2 + ": already patched");
			}
			patchResult.DllPatched = true;
			patchResult.Succeeded = true;
			Log("Done.");
		}
		catch (Exception ex2)
		{
			patchResult.Fail("Unexpected error: " + ex2.Message);
			Log("Error: " + ex2.Message);
		}
		return patchResult;
	}

	public PatchResult RevertOfflineSetup()
	{
		_verbose = true;
		PatchResult patchResult = new PatchResult();
		long? steamVersion = SteamDetector.GetSteamVersion(_steamPath);
		if (!steamVersion.HasValue)
		{
			Log("  WARNING: Could not read Steam version from manifest");
			return patchResult.Fail("Steam version could not be determined. Cannot safely revert.");
		}
		if (!SteamDetector.IsSupportedSteamVersion(steamVersion.Value))
		{
			string text = string.Join(", ", SteamDetector.SupportedSteamVersions);
			Log($"  Steam version: {steamVersion.Value} (UNSUPPORTED)");
			Log("  Supported versions: " + text);
			return patchResult.Fail($"Steam version mismatch: installed {steamVersion.Value}, supported {text}. " + "Reverting on an unsupported version risks corrupting steamclient64.dll. Update CloudRedirect or downgrade Steam.");
		}
		Log($"  Steam version: {steamVersion.Value} (OK)");
		try
		{
			string text2 = FindCoreDll();
			if (text2 == null)
			{
				return patchResult.Fail("SteamTools Core DLL not found.");
			}
			string text3 = Path.Combine(_steamPath, text2);
			byte[] array;
			try
			{
				array = ReadFileShared(text3);
			}
			catch (IOException)
			{
				return patchResult.Fail(text2 + " is in use - close Steam first");
			}
			PatchEntry[] array2 = ResolveCorePatchOffsets(array);
			if (array2 == null)
			{
				return patchResult.Fail("Could not identify patch locations in " + text2);
			}
			var (data, num, num2, list) = UnapplyPatches(array, array2);
			if (list.Count > 0)
			{
				foreach (string item in list)
				{
					Log(item);
				}
				return patchResult.Fail("Byte mismatch in " + text2);
			}
			string text4 = Fingerprint.FindCachePath(_steamPath, verbose: false, _log);
			if (text4 == null)
			{
				return patchResult.Fail("Payload cache not found.");
			}
			var (array3, iv, error) = ReadAndDecryptPayload(text4);
			if (array3 == null)
			{
				return patchResult.Fail(error);
			}
			PatchEntry[] array4 = ResolveSetupPatchOffsets(array3);
			if (array4 == null)
			{
				return patchResult.Fail("Could not identify patch locations in payload");
			}
			var (patchedPayload, num3, num4, list2) = UnapplyPatches(array3, array4);
			if (list2.Count > 0)
			{
				foreach (string item2 in list2)
				{
					Log(item2);
				}
				return patchResult.Fail("Byte mismatch in payload");
			}
			if (num3 > 0 || num > 0)
			{
				BackupBoth(text4, text3);
			}
			if (num3 > 0)
			{
				ReEncryptAndWrite(text4, patchedPayload, iv);
				Log($"  {num3} patch(es) reverted in payload" + ((num4 > 0) ? $", {num4} already original" : ""));
			}
			else
			{
				Log("  Payload: already original");
			}
			if (num > 0)
			{
				FileUtils.AtomicWriteAllBytes(text3, data);
				Log($"  {num} patch(es) reverted in {text2}" + ((num2 > 0) ? $", {num2} already original" : ""));
			}
			else
			{
				Log("  " + text2 + ": already original");
			}
			patchResult.Succeeded = true;
			Log("Offline setup reverted.");
		}
		catch (Exception ex2)
		{
			patchResult.Fail("Unexpected error: " + ex2.Message);
			Log("Error: " + ex2.Message);
		}
		return patchResult;
	}

	private static string FindSteamToolsExe()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steamtools");
			if (!(registryKey?.GetValue("SteamPath") is string text))
			{
				return null;
			}
			string text2 = Path.Combine(text.Replace('/', '\\'), "SteamTools.exe");
			return File.Exists(text2) ? text2 : null;
		}
		catch
		{
			return null;
		}
	}

	private void KillSteamTools()
	{
		Process[] processesByName = Process.GetProcessesByName("SteamTools");
		foreach (Process process in processesByName)
		{
			try
			{
				Log($"  Killing SteamTools.exe (PID {process.Id})...");
				process.Kill();
				process.WaitForExit(5000);
			}
			catch
			{
			}
			finally
			{
				process.Dispose();
			}
		}
	}

	public int GetSteamToolsExePatchState()
	{
		string text = FindSteamToolsExe();
		if (text == null)
		{
			return -1;
		}
		try
		{
			byte[] array = ReadFileShared(text);
			if (array.Length < 164594)
			{
				return -1;
			}
			if (array[164592] == StExePatched[0] && array[164593] == StExePatched[1])
			{
				return 0;
			}
			if (array[164592] == StExeOriginal[0] && array[164593] == StExeOriginal[1])
			{
				return 1;
			}
			return -1;
		}
		catch
		{
			return -1;
		}
	}

	public int PatchSteamToolsExe()
	{
		_verbose = true;
		string text = FindSteamToolsExe();
		if (text == null)
		{
			Log("  SteamTools.exe not found -- skipping");
			return 0;
		}
		try
		{
			KillSteamTools();
			byte[] array = ReadFileShared(text);
			if (array.Length < 164594)
			{
				Log("  SteamTools.exe too small - unrecognized version");
				return -1;
			}
			if (array[164592] == StExePatched[0] && array[164593] == StExePatched[1])
			{
				Log("  SteamTools.exe: already patched");
				return 1;
			}
			if (array[164592] != StExeOriginal[0] || array[164593] != StExeOriginal[1])
			{
				Log($"  SteamTools.exe: unexpected bytes at patch site ({array[164592]:X2} {array[164593]:X2}) - unrecognized version");
				return -1;
			}
			Backup(text);
			array[164592] = StExePatched[0];
			array[164593] = StExePatched[1];
			FileUtils.AtomicWriteAllBytes(text, array);
			Log("  SteamTools.exe: patched (DLL deploy disabled)");
			return 1;
		}
		catch (UnauthorizedAccessException)
		{
			Log("  SteamTools.exe: access denied - run as administrator");
			return -1;
		}
		catch (IOException ex2)
		{
			Log("  SteamTools.exe: " + ex2.Message);
			return -1;
		}
	}

	public bool UnpatchSteamToolsExe()
	{
		_verbose = true;
		string text = FindSteamToolsExe();
		if (text == null)
		{
			return false;
		}
		try
		{
			KillSteamTools();
			byte[] array = ReadFileShared(text);
			if (array.Length < 164594)
			{
				return false;
			}
			if (array[164592] == StExeOriginal[0] && array[164593] == StExeOriginal[1])
			{
				return true;
			}
			if (array[164592] != StExePatched[0] || array[164593] != StExePatched[1])
			{
				return false;
			}
			array[164592] = StExeOriginal[0];
			array[164593] = StExeOriginal[1];
			FileUtils.AtomicWriteAllBytes(text, array);
			Log("  SteamTools.exe: restored to original");
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			Log("  SteamTools.exe: access denied - run as administrator");
			return false;
		}
		catch (IOException ex2)
		{
			Log("  SteamTools.exe: " + ex2.Message);
			return false;
		}
	}

	private void Log(string msg)
	{
		if (_verbose)
		{
			_log?.Invoke(msg);
		}
	}

	private string DeployEmbeddedPayload()
	{
		try
		{
			long? steamVersion = SteamDetector.GetSteamVersion(_steamPath);
			if (!steamVersion.HasValue)
			{
				Log("  Embedded payload deploy failed: could not determine Steam version");
				return null;
			}
			if (!EmbeddedBundledPayload.TryInstall(_steamPath, steamVersion.Value, Log))
			{
				return null;
			}
			return Fingerprint.GetExpectedCachePath(_steamPath);
		}
		catch (Exception ex)
		{
			Log("  Deploy failed: " + ex.Message);
			return null;
		}
	}

	private void Backup(string path)
	{
		string text = path + ".orig";
		if (!File.Exists(text))
		{
			FileUtils.AtomicCopy(path, text);
			Log("  Original saved to " + text);
		}
		string text2 = path + ".bak";
		FileUtils.AtomicCopy(path, text2);
		Log("  Backed up to " + text2);
	}

	private void BackupBoth(string firstPath, string secondPath)
	{
		Backup(firstPath);
		Backup(secondPath);
	}

	private void ReEncryptAndWrite(string cachePath, byte[] patchedPayload, byte[] iv)
	{
		Log("  Re-encrypting..");
		using MemoryStream memoryStream = new MemoryStream();
		using (ZLibStream zLibStream = new ZLibStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
		{
			zLibStream.Write(patchedPayload, 0, patchedPayload.Length);
		}
		byte[] array = new byte[4 + memoryStream.Length];
		BitConverter.TryWriteBytes(array.AsSpan(0, 4), patchedPayload.Length);
		memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length).CopyTo(array.AsSpan(4));
		byte[] array2 = PayloadCrypto.AesCbcEncrypt(array, AesKey, iv);
		byte[] array3 = new byte[16 + array2.Length];
		iv.CopyTo(array3, 0);
		array2.CopyTo(array3, 16);
		FileUtils.AtomicWriteAllBytes(cachePath, array3);
		_cachedPayload = null;
	}

	private static byte[] BuildDynamicCloudRedirectCave(int caveRva, int sendPktRva, int loadLibAIatRva, int getProcAddrIatRva, int recvPktGlobalRva)
	{
		byte[] obj = (byte[])CloudRedirectCaveContent.Clone();
		int value = loadLibAIatRva - (caveRva + 22);
		BitConverter.TryWriteBytes(obj.AsSpan(18, 4), value);
		int value2 = getProcAddrIatRva - (caveRva + 43);
		BitConverter.TryWriteBytes(obj.AsSpan(39, 4), value2);
		int value3 = recvPktGlobalRva - (caveRva + 73);
		BitConverter.TryWriteBytes(obj.AsSpan(69, 4), value3);
		int value4 = sendPktRva + 5 - (caveRva + 98);
		BitConverter.TryWriteBytes(obj.AsSpan(94, 4), value4);
		return obj;
	}

	private static bool BytesMatch(byte[] data, int dataOffset, byte[] pattern, int patOffset, int length)
	{
		if (dataOffset + length > data.Length)
		{
			return false;
		}
		for (int i = 0; i < length; i++)
		{
			if (data[dataOffset + i] != pattern[patOffset + i])
			{
				return false;
			}
		}
		return true;
	}

	private static string SafeHexDump(byte[] data, int offset, int length)
	{
		if (offset < 0 || offset >= data.Length)
		{
			return "(out of bounds)";
		}
		int num = Math.Min(length, data.Length - offset);
		return BitConverter.ToString(data, offset, num);
	}

	private static (byte[] data, int applied, int skipped, List<string> errors) CheckPatches(byte[] data, PatchEntry[] patches)
	{
		int num = 0;
		int num2 = 0;
		List<string> list = new List<string>();
		foreach (PatchEntry patchEntry in patches)
		{
			if (BytesMatch(data, patchEntry.Offset, patchEntry.Replacement, 0, patchEntry.Replacement.Length))
			{
				num2++;
				continue;
			}
			if (BytesMatch(data, patchEntry.Offset, patchEntry.Original, 0, patchEntry.Original.Length))
			{
				num++;
				continue;
			}
			list.Add($"  Mismatch at 0x{patchEntry.Offset:X}: expected {BitConverter.ToString(patchEntry.Original)}, got {SafeHexDump(data, patchEntry.Offset, patchEntry.Original.Length)}");
		}
		return (data: data, applied: num, skipped: num2, errors: list);
	}

	private static (byte[] data, int applied, int skipped, List<string> errors) ApplyPatches(byte[] data, PatchEntry[] patches)
	{
		byte[] array = (byte[])data.Clone();
		int num = 0;
		int num2 = 0;
		List<string> list = new List<string>();
		foreach (PatchEntry patchEntry in patches)
		{
			if (BytesMatch(array, patchEntry.Offset, patchEntry.Replacement, 0, patchEntry.Replacement.Length))
			{
				num2++;
				continue;
			}
			if (BytesMatch(array, patchEntry.Offset, patchEntry.Original, 0, patchEntry.Original.Length))
			{
				Buffer.BlockCopy(patchEntry.Replacement, 0, array, patchEntry.Offset, patchEntry.Replacement.Length);
				num++;
				continue;
			}
			list.Add($"  Mismatch at 0x{patchEntry.Offset:X}: expected {BitConverter.ToString(patchEntry.Original)}, got {SafeHexDump(array, patchEntry.Offset, patchEntry.Original.Length)}");
		}
		return (data: array, applied: num, skipped: num2, errors: list);
	}

	private static (byte[] data, int reverted, int skipped, List<string> errors) UnapplyPatches(byte[] data, PatchEntry[] patches)
	{
		byte[] array = (byte[])data.Clone();
		int num = 0;
		int num2 = 0;
		List<string> list = new List<string>();
		foreach (PatchEntry patchEntry in patches)
		{
			if (BytesMatch(array, patchEntry.Offset, patchEntry.Original, 0, patchEntry.Original.Length))
			{
				num2++;
				continue;
			}
			if (BytesMatch(array, patchEntry.Offset, patchEntry.Replacement, 0, patchEntry.Replacement.Length))
			{
				Buffer.BlockCopy(patchEntry.Original, 0, array, patchEntry.Offset, patchEntry.Original.Length);
				num++;
				continue;
			}
			list.Add($"  Mismatch at 0x{patchEntry.Offset:X}: expected {BitConverter.ToString(patchEntry.Replacement)}, got {SafeHexDump(array, patchEntry.Offset, patchEntry.Replacement.Length)}");
		}
		return (data: array, reverted: num, skipped: num2, errors: list);
	}

	private PatchEntry[] ResolveCorePatchOffsets(byte[] dll)
	{
		PeSection[] sections = PeSection.Parse(dll);
		PeSection? peSection = PeSection.Find(sections, ".rdata");
		if (!peSection.HasValue)
		{
			Log("  Core.dll: no .rdata section found");
			return null;
		}
		int num = Signatures.ScanForBytes(dll, (int)peSection.Value.RawOffset, (int)(peSection.Value.RawOffset + peSection.Value.RawSize), AesKey);
		if (num < 0)
		{
			num = Signatures.ScanForBytes(dll, 0, dll.Length, AesKey);
		}
		if (num < 0)
		{
			Log("  Core.dll: AES key not found - not a recognized SteamTools version");
			return null;
		}
		Log($"  AES key found at 0x{num:X}");
		PeSection? peSection2 = PeSection.Find(sections, ".text");
		if (!peSection2.HasValue)
		{
			Log("  Core.dll: no .text section found");
			return null;
		}
		int rawOffset = (int)peSection2.Value.RawOffset;
		int textEnd = Math.Min(rawOffset + (int)peSection2.Value.RawSize, dll.Length);
		return Signatures.ResolvePatternGroup(dll, Signatures.CorePatchDefs, rawOffset, textEnd, 0, 0, _verbose ? _log : null);
	}

	private bool ResolvePayloadSections(byte[] payload, out PeSection[] sections, out int tStart, out int tEnd, out int gStart, out int gEnd)
	{
		tStart = (tEnd = (gStart = (gEnd = 0)));
		sections = PeSection.Parse(payload);
		Log($"  Payload PE: {sections.Length} sections, file size=0x{payload.Length:X}");
		PeSection[] array = sections;
		for (int i = 0; i < array.Length; i++)
		{
			PeSection peSection = array[i];
			Log($"    '{peSection.Name}': VA=0x{peSection.VirtualAddress:X} VSize=0x{peSection.VirtualSize:X} Raw=0x{peSection.RawOffset:X} RawSize=0x{peSection.RawSize:X} Chars=0x{peSection.Characteristics:X8}");
		}
		PeSection? peSection2 = PeSection.Find(sections, ".text");
		HashSet<string> hashSet = new HashSet<string> { ".text", ".rdata", ".data", ".pdata", ".fptable", ".rsrc", ".reloc" };
		PeSection? peSection3 = null;
		array = sections;
		for (int i = 0; i < array.Length; i++)
		{
			PeSection value = array[i];
			if (!hashSet.Contains(value.Name))
			{
				peSection3 = value;
				break;
			}
		}
		if (!peSection2.HasValue || !peSection3.HasValue)
		{
			Log("  Payload: missing expected sections");
			return false;
		}
		tStart = (int)peSection2.Value.RawOffset;
		tEnd = Math.Min(tStart + (int)peSection2.Value.RawSize, payload.Length);
		gStart = (int)peSection3.Value.RawOffset;
		gEnd = Math.Min(gStart + (int)peSection3.Value.RawSize, payload.Length);
		return true;
	}

	private PatchEntry[] ResolvePayloadPatchOffsets(byte[] payload)
	{
		if (!ResolvePayloadSections(payload, out var _, out var tStart, out var tEnd, out var gStart, out var gEnd))
		{
			return null;
		}
		return Signatures.ResolvePatternGroup(payload, Signatures.PayloadP123Defs, tStart, tEnd, gStart, gEnd, _verbose ? _log : null);
	}

	private PatchEntry[] ResolveSetupPatchOffsets(byte[] payload)
	{
		if (!ResolvePayloadSections(payload, out var _, out var tStart, out var tEnd, out var gStart, out var gEnd))
		{
			return null;
		}
		return Signatures.ResolvePatternGroup(payload, Signatures.PayloadSetupDefs, tStart, tEnd, gStart, gEnd, _verbose ? _log : null);
	}

	private CloudRedirectResolveResult ResolveCloudRedirectPatchOffsets(byte[] payload)
	{
		if (!ResolvePayloadSections(payload, out var sections, out var tStart, out var tEnd, out var gStart, out var gEnd))
		{
			return null;
		}
		int num = Signatures.FindSendPktFunction(payload, tStart, tEnd);
		if (num < 0)
		{
			Log("  Payload: could not locate SendPkt function by pattern");
			return null;
		}
		byte[] array = new byte[5] { 72, 137, 92, 36, 32 };
		bool flag = BytesMatch(payload, num, array, 0, 5);
		bool flag2 = payload[num] == 233;
		if (!flag && !flag2)
		{
			Log($"  Payload: unexpected bytes at SendPkt (0x{num:X}): {SafeHexDump(payload, num, 5)}");
			return null;
		}
		int num2 = PeSection.FileOffsetToRva(sections, num);
		if (num2 < 0)
		{
			Log($"  Payload: RVA resolution failed for SendPkt (file=0x{num:X})");
			return null;
		}
		int num5;
		if (flag2)
		{
			int num3 = BitConverter.ToInt32(payload, num + 1);
			int num4 = num2 + 5 + num3;
			num5 = PeSection.RvaToFileOffset(sections, num4);
			if (num5 < 0)
			{
				Log($"  Payload: existing jmp target RVA 0x{num4:X} does not resolve to a file offset");
				return null;
			}
		}
		else
		{
			num5 = Signatures.FindCodeCave(payload, sections, CloudRedirectCaveContent.Length);
			if (num5 < 0)
			{
				Log("  Payload: could not find code cave in any executable section");
				return null;
			}
		}
		int num6 = PeSection.FileOffsetToRva(sections, num5);
		if (num6 < 0)
		{
			Log($"  Payload: RVA resolution failed for cave (file=0x{num5:X})");
			return null;
		}
		var (num7, num8) = PeImportParser.FindKernel32IatEntries(payload, sections);
		if (num7 < 0 || num8 < 0)
		{
			Log($"  Payload: KERNEL32 IAT not found (LoadLib={num7:X}, GetProc={num8:X})");
			return null;
		}
		int num9 = Signatures.FindRecvPktGlobalRva(payload, sections, num2, gStart, gEnd);
		if (num9 < 0)
		{
			Log("  Payload: could not locate recvPktGlobal by pattern");
			return null;
		}
		PeSection? peSection = PeSection.FindByRva(sections, num9);
		if (!peSection.HasValue)
		{
			Log($"  Payload: recvPktGlobalRva 0x{num9:X} does not map to any PE section");
			PeSection[] array2 = sections;
			for (int i = 0; i < array2.Length; i++)
			{
				PeSection peSection2 = array2[i];
				Log($"    Section '{peSection2.Name}': VA=0x{peSection2.VirtualAddress:X} VSize=0x{peSection2.VirtualSize:X} Raw=0x{peSection2.RawOffset:X} RawSize=0x{peSection2.RawSize:X}");
			}
			return null;
		}
		Log($"  recvPktGlobal RVA 0x{num9:X} -> section '{peSection.Value.Name}'");
		byte[] array3 = new byte[5];
		if (flag)
		{
			Buffer.BlockCopy(payload, num, array3, 0, 5);
		}
		else
		{
			Buffer.BlockCopy(array, 0, array3, 0, 5);
		}
		int value = num6 - (num2 + 5);
		byte[] array4 = new byte[5] { 233, 0, 0, 0, 0 };
		BitConverter.TryWriteBytes(array4.AsSpan(1, 4), value);
		byte[] dynamicCodeCave = BuildDynamicCloudRedirectCave(num6, num2, num7, num8, num9);
		Log($"  CloudRedirect: SendPkt at file=0x{num:X} rva=0x{num2:X}");
		Log($"  CloudRedirect: cave at file=0x{num5:X} rva=0x{num6:X}");
		Log($"  IAT: LoadLibraryA=0x{num7:X}, GetProcAddress=0x{num8:X}");
		List<PatchEntry> list = new List<PatchEntry>
		{
			new PatchEntry(num, array3, array4)
		};
		return new CloudRedirectResolveResult
		{
			Patches = list.ToArray(),
			DynamicCodeCave = dynamicCodeCave,
			CodeCaveFileOffset = num5
		};
	}

	private string DeployCloudRedirect()
	{
		if (string.IsNullOrWhiteSpace(_cloudRedirectDllSourcePath) || !File.Exists(_cloudRedirectDllSourcePath))
		{
			return "Không tìm thấy file cloud_redirect.dll để cài vào Steam.";
		}
		string path = Path.Combine(_steamPath, "cloud_redirect.dll");
		try
		{
			byte[] data = File.ReadAllBytes(_cloudRedirectDllSourcePath);
			FileUtils.AtomicWriteAllBytes(path, data);
		}
		catch (UnauthorizedAccessException)
		{
			return "Không có quyền ghi cloud_redirect.dll vào thư mục Steam. Hãy chạy launcher bằng quyền Administrator.";
		}
		catch (IOException ex2)
		{
			return "Không copy được cloud_redirect.dll vào Steam: " + ex2.Message;
		}
		catch (Exception ex3)
		{
			return "Không cài được cloud_redirect.dll: " + ex3.Message;
		}
		Log("  Deployed cloud_redirect.dll to " + _steamPath);
		return null;
	}

	public PatchResult ApplyCloudRedirectNamespace()
	{
		_verbose = true;
		PatchResult patchResult = new PatchResult();
		long? steamVersion = SteamDetector.GetSteamVersion(_steamPath);
		if (!steamVersion.HasValue)
		{
			Log("  WARNING: Could not read Steam version from manifest");
			return patchResult.Fail("Steam version could not be determined. Cannot safely patch.");
		}
		if (!SteamDetector.IsSupportedSteamVersion(steamVersion.Value))
		{
			string text = string.Join(", ", SteamDetector.SupportedSteamVersions);
			Log($"  Steam version: {steamVersion.Value} (UNSUPPORTED)");
			Log("  Supported versions: " + text);
			return patchResult.Fail($"Steam version mismatch: installed {steamVersion.Value}, supported {text}. " + "Patching an unsupported version risks corrupting steamclient64.dll. Update CloudRedirect or downgrade Steam.");
		}
		Log($"  Steam version: {steamVersion.Value} (OK)");
		try
		{
			string text2 = FindCoreDll();
			if (text2 == null)
			{
				return patchResult.Fail("SteamTools Core DLL not found. Is SteamTools installed?");
			}
			string text3 = Path.Combine(_steamPath, text2);
			byte[] array;
			try
			{
				array = ReadFileShared(text3);
			}
			catch (IOException)
			{
				return patchResult.Fail(text2 + " is in use - close Steam first");
			}
			PatchEntry[] array2 = ResolveCorePatchOffsets(array);
			if (array2 == null)
			{
				return patchResult.Fail("Could not identify patch locations in " + text2 + " - unsupported version?");
			}
			var (data, num, _, list) = ApplyPatches(array, array2);
			if (list.Count > 0)
			{
				foreach (string item in list)
				{
					Log(item);
				}
				return patchResult.Fail("Byte mismatch in " + text2 + " - wrong version?");
			}
			string text4 = Fingerprint.FindCachePath(_steamPath, verbose: true, _log);
			if (text4 == null)
			{
				Log("Payload cache not found. Deploying embedded payload..");
				text4 = DeployEmbeddedPayload();
				if (text4 == null)
				{
					return patchResult.Fail("Could not deploy payload cache.");
				}
			}
			Log("Patching payload (CloudRedirect namespace mode)..");
			BackupBoth(text4, text3);
			var (array3, iv, error) = ReadAndDecryptPayload(text4);
			if (array3 == null)
			{
				return patchResult.Fail(error);
			}
			PatchEntry[] array4 = ResolvePayloadPatchOffsets(array3);
			byte[] array5 = array3;
			if (array4 != null)
			{
				var (array6, num2, num3, list2) = ApplyPatches(array3, array4);
				if (list2.Count > 0)
				{
					foreach (string item2 in list2)
					{
						Log(item2);
					}
					Log("  Warning: could not apply some P1/P2/P3 patches (continuing -- non-fatal)");
				}
				if (num2 > 0)
				{
					Log($"  Applied {num2} cloud-disable patch(es) (P1/P2/P3)");
				}
				else if (num3 == array4.Length)
				{
					Log("  P1/P2/P3: already applied (SteamTools cloud redirect disabled)");
				}
				array5 = array6;
			}
			else
			{
				Log("  Could not resolve P1/P2/P3 offsets (continuing without them)");
			}
			CloudRedirectResolveResult cloudRedirectResolveResult = ResolveCloudRedirectPatchOffsets(array5);
			if (cloudRedirectResolveResult == null)
			{
				return patchResult.Fail("Could not locate CloudRedirect patch sites in payload");
			}
			var (array7, num4, num5, list3) = ApplyPatches(array5, cloudRedirectResolveResult.Patches);
			if (list3.Count > 0)
			{
				foreach (string item3 in list3)
				{
					Log(item3);
				}
				return patchResult.Fail("Byte mismatch at CloudRedirect patch sites");
			}
			if (cloudRedirectResolveResult.CodeCaveFileOffset + cloudRedirectResolveResult.DynamicCodeCave.Length > array7.Length)
			{
				return patchResult.Fail("Payload too small for code cave injection");
			}
			bool flag = BytesMatch(array7, cloudRedirectResolveResult.CodeCaveFileOffset, cloudRedirectResolveResult.DynamicCodeCave, 0, cloudRedirectResolveResult.DynamicCodeCave.Length);
			string text5 = DeployCloudRedirect();
			if (text5 != null)
			{
				return patchResult.Fail(text5);
			}
			Buffer.BlockCopy(cloudRedirectResolveResult.DynamicCodeCave, 0, array7, cloudRedirectResolveResult.CodeCaveFileOffset, cloudRedirectResolveResult.DynamicCodeCave.Length);
			ReEncryptAndWrite(text4, array7, iv);
			int value = num4 + ((!flag) ? 1 : 0);
			Log($"  {value} cave change(s) applied" + ((num5 > 0) ? $", {num5} already done" : ""));
			patchResult.CachePatched = true;
			if (num > 0)
			{
				FileUtils.AtomicWriteAllBytes(text3, data);
				Log($"  {num} core patch(es) applied to {text2}");
			}
			patchResult.DllPatched = true;
			patchResult.Succeeded = true;
			Log("Done. P1/P2/P3 applied (SteamTools redirect disabled) + namespace DLL will be loaded.");
		}
		catch (Exception ex2)
		{
			patchResult.Fail("Unexpected error: " + ex2.Message);
			Log("Error: " + ex2.Message);
		}
		return patchResult;
	}

	public PatchResult RevertCloudRedirectNamespace()
	{
		_verbose = true;
		PatchResult patchResult = new PatchResult();
		try
		{
			string text = FindCoreDll();
			if (text == null)
			{
				return patchResult.Fail("SteamTools Core DLL not found.");
			}
			string text2 = Path.Combine(_steamPath, text);
			byte[] array;
			try
			{
				array = ReadFileShared(text2);
			}
			catch (IOException)
			{
				return patchResult.Fail(text + " is in use - close Steam first");
			}
			PatchEntry[] array2 = ResolveCorePatchOffsets(array);
			if (array2 == null)
			{
				return patchResult.Fail("Could not identify patch locations in " + text);
			}
			var (data, num, _, list) = UnapplyPatches(array, array2);
			if (list.Count > 0)
			{
				foreach (string item in list)
				{
					Log(item);
				}
				return patchResult.Fail("Byte mismatch in " + text);
			}
			string text3 = Fingerprint.FindCachePath(_steamPath, verbose: false, _log);
			if (text3 == null)
			{
				return patchResult.Fail("Payload cache not found.");
			}
			var (array3, iv, error) = ReadAndDecryptPayload(text3);
			if (array3 == null)
			{
				return patchResult.Fail(error);
			}
			CloudRedirectResolveResult cloudRedirectResolveResult = ResolveCloudRedirectPatchOffsets(array3);
			byte[] array4 = array3;
			if (cloudRedirectResolveResult != null)
			{
				var (array5, num2, _, list2) = UnapplyPatches(array3, cloudRedirectResolveResult.Patches);
				if (list2.Count > 0)
				{
					foreach (string item2 in list2)
					{
						Log(item2);
					}
					return patchResult.Fail("Byte mismatch at CloudRedirect patch sites");
				}
				if (num2 > 0)
				{
					Log($"  Reverted {num2} CloudRedirect hook patch(es)");
				}
				else
				{
					Log("  CloudRedirect hook: already original");
				}
				array4 = array5;
			}
			PatchEntry[] array6 = ResolvePayloadPatchOffsets(array4);
			byte[] patchedPayload = array4;
			if (array6 != null)
			{
				var (array7, num3, num4, list3) = UnapplyPatches(array4, array6);
				if (list3.Count > 0)
				{
					foreach (string item3 in list3)
					{
						Log(item3);
					}
					Log("  Warning: could not revert some P1/P2/P3 patches");
				}
				if (num3 > 0)
				{
					Log($"  Reverted {num3} P1/P2/P3 patch(es)");
				}
				else if (num4 == array6.Length)
				{
					Log("  P1/P2/P3: already original");
				}
				patchedPayload = array7;
			}
			BackupBoth(text3, text2);
			ReEncryptAndWrite(text3, patchedPayload, iv);
			Log("  Payload written.");
			if (num > 0)
			{
				FileUtils.AtomicWriteAllBytes(text2, data);
				Log($"  {num} core patch(es) reverted in {text}");
			}
			else
			{
				Log("  " + text + ": already original");
			}
			patchResult.Succeeded = true;
			Log("Cloud redirect patch reverted.");
		}
		catch (Exception ex2)
		{
			patchResult.Fail("Unexpected error: " + ex2.Message);
			Log("Error: " + ex2.Message);
		}
		return patchResult;
	}
}
