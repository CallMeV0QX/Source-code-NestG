using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54Service
{
	private sealed class Snapshot(string original, string backup, string stage, string originalHash)
	{
		internal string Original { get; } = original;

		internal string Backup { get; } = backup;

		internal string Stage { get; } = stage;

		internal string OriginalHash { get; } = originalHash;

		internal string? OutputHash { get; set; }
	}

	internal const int MaximumExecutableFiles = 1024;

	internal const long MaximumExecutableBytes = 1073741824L;

	internal const long MaximumTotalBytes = 4294967296L;

	private static readonly string[] SkipPatterns = new string[22]
	{
		"redist", "vcredist", "directx", "dxsetup", "vc_redist", "dotnet", "setup", "install", "uninstall", "crashreport",
		"crashhandler", "bugsplat", "easyanticheat", "battleye", "be_service", "launch", "_launcher", "bootstrap", "cefprocess", "cef_process",
		"steamwebhelper", "uplay"
	};

	private readonly IFix54GameLocator locator;

	private readonly IFix54GameProcessGuard processGuard;

	private readonly IFix54RuntimePreparer runtime;

	private readonly IFix54ProcessRunner runner;

	private readonly IFix54FilePublisher publisher;

	private readonly string operationsRoot;

	public Fix54Service()
		: this(new Fix54GameLocator(), new Fix54GameProcessGuard(), new Fix54RuntimePreparer(), new Fix54PowerShell(), new Fix54FilePublisher(), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "Fix54", "Operations"))
	{
	}

	internal Fix54Service(IFix54GameLocator locator, IFix54GameProcessGuard processGuard, IFix54RuntimePreparer runtime, IFix54ProcessRunner runner, IFix54FilePublisher publisher, string operationsRoot)
	{
		this.locator = locator;
		this.processGuard = processGuard;
		this.runtime = runtime;
		this.runner = runner;
		this.publisher = publisher;
		this.operationsRoot = Fix54Paths.SafePath(operationsRoot);
	}

	public Task<Fix54Result> RunAsync(int steamAppId, IProgress<Fix54Progress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.Run(() => RunCoreAsync(steamAppId, progress, cancellationToken));
	}

	private async Task<Fix54Result> RunCoreAsync(int steamAppId, IProgress<Fix54Progress>? progress, CancellationToken cancellationToken)
	{
		if (steamAppId <= 0)
		{
			return new Fix54Result(Fix54Outcome.Failed, "Game chưa có Steam App ID hợp lệ.", 0, 0, null, RollbackCompleted: true);
		}
		using (CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10.0)))
		{
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
			CancellationToken token = linked.Token;
			string operation = null;
			string originals = null;
			string gameRoot = null;
			IReadOnlyList<string> allExes = Array.Empty<string>();
			List<Snapshot> snapshots = new List<Snapshot>();
			List<Snapshot> published = new List<Snapshot>();
			bool completeRollback = true;
			bool rollbackAttempted = false;
			bool processStopped = true;
			try
			{
				Report("locating", "Đang tìm game cài trong Steam…", 5);
				string text = await locator.ResolveAsync(steamAppId, token);
				token.ThrowIfCancellationRequested();
				if (text == null)
				{
					return new Fix54Result(Fix54Outcome.Failed, "Bạn chưa cài game, vui lòng hãy cài game.", 0, 0, null, RollbackCompleted: true);
				}
				gameRoot = Fix54Paths.SafePath(text);
				Fix54Paths.EnsureNoReparse(gameRoot);
				allExes = EnumerateExecutables(gameRoot, token);
				processGuard.EnsureClosed(gameRoot, allExes);
				string[] array = allExes.Where(IsCandidate).ToArray();
				if (array.Length == 0)
				{
					return new Fix54Result(Fix54Outcome.Unchanged, "Không tìm thấy EXE phù hợp để xử lý FIX54.", 0, 0, null, RollbackCompleted: true);
				}
				operation = Path.Combine(operationsRoot, Guid.NewGuid().ToString("N"));
				Fix54Paths.EnsureNoReparse(operationsRoot);
				Directory.CreateDirectory(operation);
				Fix54Paths.EnsureInside(operation, operationsRoot);
				if (Fix54Paths.IsInside(operation, gameRoot) || Fix54Paths.IsInside(gameRoot, operation))
				{
					throw new Fix54Exception("Thư mục game không thể nằm trong vùng staging FIX54.");
				}
				originals = Path.Combine(operation, "Originals");
				Directory.CreateDirectory(originals);
				string steamRoot = Path.Combine(operation, "Steam");
				string stagedGame = Path.Combine(steamRoot, "steamapps", "common", "Game");
				Directory.CreateDirectory(stagedGame);
				Report("backup", "Đang sao lưu EXE gốc và chuẩn bị bản sao…", 15);
				string[] array2 = array;
				foreach (string path in array2)
				{
					token.ThrowIfCancellationRequested();
					string relativePath = Path.GetRelativePath(gameRoot, path);
					string backup = Path.Combine(originals, relativePath);
					string stage2 = Path.Combine(stagedGame, relativePath);
					Fix54Paths.EnsureInside(backup, originals);
					Fix54Paths.EnsureInside(stage2, stagedGame);
					string hash = await Fix54Paths.HashAsync(path, token);
					await CopyAsync(path, backup, token);
					await CopyAsync(backup, stage2, token);
					bool flag = await Fix54Paths.HashAsync(backup, token) != hash;
					if (!flag)
					{
						flag = await Fix54Paths.HashAsync(path, token) != hash;
					}
					if (flag)
					{
						throw new Fix54Exception("EXE gốc thay đổi trong khi backup. FIX54 dừng và giữ nguyên game.");
					}
					snapshots.Add(new Snapshot(path, backup, stage2, hash));
				}
				await File.WriteAllTextAsync(Path.Combine(operation, "originals.json"), JsonSerializer.Serialize(new
				{
					SteamAppId = steamAppId,
					GameRoot = gameRoot,
					Files = snapshots.Select((Snapshot item) => new { item.Original, item.Backup, item.OriginalHash })
				}), token);
				string text2 = steamRoot.Replace("\\", "\\\\");
				await File.WriteAllTextAsync(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + text2 + "\" } }", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), token);
				await File.WriteAllTextAsync(Path.Combine(steamRoot, "steamapps", "appmanifest_" + steamAppId.ToString(CultureInfo.InvariantCulture) + ".acf"), "\"AppState\" { \"appid\" \"" + steamAppId.ToString(CultureInfo.InvariantCulture) + "\" \"installdir\" \"Game\" }", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), token);
				Report("preparing", "Đang xác minh nguồn script và công cụ FIX54…", 30);
				using Fix54PreparedRuntime prepared = await runtime.PrepareAsync(operation, token);
				string wrapper = Path.Combine(operation, "run-fix54.ps1");
				await File.WriteAllTextAsync(wrapper, "param([ValidateRange(1,2147483647)][int]$AppId)\r\n$ErrorActionPreference = 'Stop'\r\n[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\r\nif ([string]::IsNullOrWhiteSpace($env:NESTG_FIX54_STEAM) -or -not [IO.Path]::IsPathRooted($env:NESTG_FIX54_STEAM)) { exit 1 }\r\nfunction Get-ItemProperty {\r\n    param([string]$Path,[string]$Name,[string]$ErrorAction)\r\n    if ($Name -ne 'SteamPath' -or $Path -notin @('HKCU:\\Software\\Valve\\Steam','HKLM:\\Software\\WOW6432Node\\Valve\\Steam','HKLM:\\Software\\Valve\\Steam')) { throw 'Unexpected registry lookup.' }\r\n    [pscustomobject]@{ SteamPath = [IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM) }\r\n}\r\nfunction Get-Content {\r\n    param([string]$Path,[switch]$Raw)\r\n    $resolved = [IO.Path]::GetFullPath($Path)\r\n    $steam = [IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM)\r\n    if (-not $resolved.StartsWith($steam + '\\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Metadata outside staged Steam.' }\r\n    Microsoft.PowerShell.Management\\Get-Content -LiteralPath $resolved -Raw:$Raw -Encoding UTF8 -ErrorAction Stop\r\n}\r\nfunction Get-ChildItem {\r\n    param([string]$Path,[string]$Filter,[switch]$Recurse,[string]$ErrorAction)\r\n    $game = [IO.Path]::Combine([IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM),'steamapps','common','Game')\r\n    if (-not [IO.Path]::GetFullPath($Path).Equals($game,[StringComparison]::OrdinalIgnoreCase) -or $Filter -ne '*.exe') { throw 'Enumeration outside staged game.' }\r\n    Microsoft.PowerShell.Management\\Get-ChildItem -LiteralPath $game -Filter '*.exe' -Recurse:$Recurse -ErrorAction Stop\r\n}\r\nfunction Assert-Fix54File([string]$Path) {\r\n    $resolved = [IO.Path]::GetFullPath($Path)\r\n    $game = [IO.Path]::Combine([IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM),'steamapps','common','Game')\r\n    if (-not $resolved.StartsWith($game + '\\',[StringComparison]::OrdinalIgnoreCase) -or -not $resolved.EndsWith('.exe',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected staged executable.' }\r\n    if (([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked staged executable.' }\r\n    $parent = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($resolved))\r\n    while ($parent -and ($parent.FullName.StartsWith($game + '\\',[StringComparison]::OrdinalIgnoreCase) -or $parent.FullName.Equals($game,[StringComparison]::OrdinalIgnoreCase))) {\r\n        if (($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked staged directory.' }\r\n        $parent = $parent.Parent\r\n    }\r\n    $resolved\r\n}\r\nfunction Remove-Item {\r\n    param([string]$Path,[switch]$Force)\r\n    $resolved = Assert-Fix54File $Path\r\n    Microsoft.PowerShell.Management\\Remove-Item -LiteralPath $resolved -Force:$Force\r\n}\r\nfunction Rename-Item {\r\n    param([string]$Path,[string]$NewName)\r\n    $resolved = Assert-Fix54File $Path\r\n    $destination = [IO.Path]::GetFullPath($NewName)\r\n    if (-not [IO.Path]::GetDirectoryName($resolved).Equals([IO.Path]::GetDirectoryName($destination),[StringComparison]::OrdinalIgnoreCase) -or\r\n        -not $resolved.Equals($destination + '.unpacked.exe',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected unpacked destination.' }\r\n    Microsoft.PowerShell.Management\\Rename-Item -LiteralPath $resolved -NewName ([IO.Path]::GetFileName($destination))\r\n}\r\ntry { & $env:NESTG_FIX54_SCRIPT -AppId $AppId; exit 0 }\r\ncatch { [Console]::Error.WriteLine('FIX54_SCRIPT_FAILED ' + $_.Exception.GetType().Name + ' line ' + $_.InvocationInfo.ScriptLineNumber); exit 1 }", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), token);
				string text3 = Path.Combine(operation, "Temp");
				Directory.CreateDirectory(text3);
				processGuard.EnsureClosed(gameRoot, allExes);
				Report("processing", "Đang xử lý bản sao EXE…", 45);
				using (new FileStream(wrapper, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					Fix54ProcessResult obj = await runner.RunAsync(new Fix54ProcessRequest(prepared.ScriptPath, wrapper, steamRoot, text3, operation, steamAppId), token);
					token.ThrowIfCancellationRequested();
					if (obj.ExitCode != 0)
					{
						throw new Fix54Exception("Công cụ FIX54 chưa hoàn tất. EXE gốc được giữ nguyên.");
					}
					List<Snapshot> changed = new List<Snapshot>();
					IReadOnlyList<string> readOnlyList = EnumerateExecutables(stagedGame, token);
					if (readOnlyList.Count != snapshots.Count || readOnlyList.Except<string>(snapshots.Select((Snapshot item) => item.Stage), StringComparer.OrdinalIgnoreCase).Any())
					{
						throw new Fix54Exception("Kết quả FIX54 có EXE thiếu hoặc tệp phát sinh chưa hợp lệ. Game được giữ nguyên.");
					}
					foreach (Snapshot snapshot in snapshots)
					{
						Snapshot snapshot2 = snapshot;
						snapshot2.OutputHash = await Fix54Paths.HashAsync(snapshot.Stage, token);
						if (!(snapshot.OutputHash == snapshot.OriginalHash))
						{
							Fix54Paths.ValidatePortableExecutable(snapshot.Stage);
							changed.Add(snapshot);
						}
					}
					if (changed.Count == 0)
					{
						return new Fix54Result(Fix54Outcome.Unchanged, "Không có EXE nào thay đổi. Công cụ có thể không tìm thấy DRM phù hợp hoặc chưa xử lý được; chưa thể xác nhận đã sửa lỗi 54.", snapshots.Count, 0, originals, RollbackCompleted: true);
					}
					Report("publishing", "Đang kiểm tra và cập nhật EXE đã xử lý…", 80);
					foreach (Snapshot snapshot in snapshots)
					{
						if (await Fix54Paths.HashAsync(snapshot.Original, token) != snapshot.OriginalHash)
						{
							throw new Fix54Exception("EXE game đã thay đổi từ lúc backup. FIX54 không ghi đè thay đổi đó.");
						}
					}
					foreach (Snapshot snapshot in changed)
					{
						token.ThrowIfCancellationRequested();
						processGuard.EnsureClosed(gameRoot, allExes);
						if (await Fix54Paths.HashAsync(snapshot.Original, token) != snapshot.OriginalHash)
						{
							throw new Fix54Exception("EXE game đã thay đổi. FIX54 dừng cập nhật.");
						}
						published.Add(snapshot);
						await publisher.ReplaceAsync(snapshot.Stage, snapshot.Original, snapshot.OriginalHash, token);
						if (await Fix54Paths.HashAsync(snapshot.Original, token) != snapshot.OutputHash)
						{
							throw new Fix54Exception("Chưa xác minh được EXE đã cập nhật.");
						}
					}
					Report("completed", "Đã xử lý EXE. Hãy mở game để kiểm tra kết quả.", 100);
					return new Fix54Result(Fix54Outcome.Changed, $"Đã cập nhật {changed.Count} EXE. Hãy mở game để kiểm tra; kết quả này chưa bảo đảm game hết lỗi 54.", snapshots.Count, changed.Count, originals, RollbackCompleted: false);
				}
			}
			catch (Exception ex)
			{
				processStopped = !(ex is Fix54ProcessStillRunningException);
				if (published.Count > 0)
				{
					rollbackAttempted = true;
					Report("rollback", "Đang khôi phục EXE gốc…", 90);
					using CancellationTokenSource rollbackDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2.0));
					foreach (Snapshot snapshot in published.AsEnumerable().Reverse())
					{
						try
						{
							processGuard.EnsureClosed(gameRoot, allExes);
							string text4 = await Fix54Paths.HashAsync(snapshot.Original, rollbackDeadline.Token);
							if (!(text4 == snapshot.OriginalHash))
							{
								bool flag = text4 != snapshot.OutputHash;
								if (!flag)
								{
									flag = await Fix54Paths.HashAsync(snapshot.Backup, rollbackDeadline.Token) != snapshot.OriginalHash;
								}
								if (flag)
								{
									completeRollback = false;
									continue;
								}
								await publisher.ReplaceAsync(snapshot.Backup, snapshot.Original, snapshot.OutputHash, rollbackDeadline.Token);
								bool flag2 = completeRollback;
								completeRollback = flag2 & (await Fix54Paths.HashAsync(snapshot.Original, rollbackDeadline.Token) == snapshot.OriginalHash);
							}
						}
						catch
						{
							completeRollback = false;
						}
					}
				}
				bool flag3 = ex is OperationCanceledException && cancellationToken.IsCancellationRequested;
				string message2 = ((!completeRollback) ? "FIX54 dừng nhưng chưa khôi phục được đầy đủ. Giữ bản backup EXE để kiểm tra và phục hồi." : (flag3 ? "Đã hủy FIX54. EXE gốc được giữ nguyên hoặc đã khôi phục." : ((ex is Fix54Exception ex2) ? ex2.Message : "Không thể hoàn tất FIX54. EXE gốc được giữ nguyên hoặc đã khôi phục.")));
				return new Fix54Result(flag3 ? Fix54Outcome.Cancelled : Fix54Outcome.Failed, message2, snapshots.Count, (!completeRollback) ? published.Count : 0, (snapshots.Count == 0) ? null : originals, completeRollback)
				{
					RollbackAttempted = rollbackAttempted
				};
			}
			finally
			{
				if (operation != null && completeRollback && processStopped)
				{
					string[] array3 = new string[2] { "Steam", "Temp" };
					foreach (string path2 in array3)
					{
						string text5 = Path.Combine(operation, path2);
						try
						{
							Fix54Paths.EnsureInside(text5, operation);
							Fix54Paths.EnsureNoReparse(text5);
							if (Directory.Exists(text5))
							{
								Directory.Delete(text5, recursive: true);
							}
						}
						catch
						{
						}
					}
				}
			}
		}
		void Report(string stage, string message, int percent)
		{
			try
			{
				progress?.Report(new Fix54Progress(stage, message, percent));
			}
			catch
			{
			}
		}
	}

	internal static IReadOnlyList<string> EnumerateExecutables(string root, CancellationToken cancellationToken)
	{
		Fix54Paths.EnsureNoReparse(root);
		Stack<string> stack = new Stack<string>();
		stack.Push(root);
		List<string> list = new List<string>();
		int num = 0;
		long num2 = 0L;
		string result;
		while (stack.TryPop(out result))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (++num > 8192)
			{
				throw new Fix54Exception("Cấu trúc thư mục game vượt giới hạn FIX54.");
			}
			foreach (string item in Directory.EnumerateDirectories(result))
			{
				Fix54Paths.EnsureInside(item, root);
				Fix54Paths.EnsureNoReparse(item);
				stack.Push(item);
			}
			foreach (string item2 in Directory.EnumerateFiles(result, "*.exe"))
			{
				Fix54Paths.EnsureInside(item2, root);
				Fix54Paths.EnsureNoReparse(item2);
				long length = new FileInfo(item2).Length;
				if (length <= 0 || length > 1073741824 || (num2 += length) > 4294967296L || list.Count >= 1024)
				{
					throw new Fix54Exception("Số lượng hoặc kích thước EXE vượt giới hạn FIX54.");
				}
				list.Add(item2);
			}
		}
		return list.OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	internal static bool IsCandidate(string path)
	{
		if (!Path.GetFileName(path).EndsWith(".unpacked.exe", StringComparison.OrdinalIgnoreCase))
		{
			return !SkipPatterns.Any((string pattern) => Path.GetFileName(path).Contains(pattern, StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}

	private static async Task CopyAsync(string source, string destination, CancellationToken token)
	{
		Fix54Paths.EnsureNoReparse(source);
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		Fix54Paths.EnsureNoReparse(destination);
		await using FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous);
		await using FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough | FileOptions.Asynchronous);
		await input.CopyToAsync(output, token);
		await output.FlushAsync(token);
		output.Flush(flushToDisk: true);
	}
}
