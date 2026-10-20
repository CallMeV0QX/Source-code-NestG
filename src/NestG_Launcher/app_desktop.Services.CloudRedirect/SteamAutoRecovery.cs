using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamAutoRecovery(SteamRecoveryStore store, ISteamRecoveryActions actions)
{
	internal async Task<SteamRecoveryOperation> RunAsync(SteamRecoveryOperation operation, Action<string>? progress = null)
	{
		if (operation.Phase == SteamRecoveryPhase.Complete)
		{
			return operation;
		}
		try
		{
			SteamRecoveryPhase phase2 = operation.Phase;
			if ((phase2 == SteamRecoveryPhase.Installing || phase2 == SteamRecoveryPhase.FilesCommitted) ? true : false)
			{
				switch (await actions.ReconcileInstallationAsync())
				{
				case SteamInstallationDisposition.RolledBack:
					operation.RecoveryAttempted = true;
					operation.RollbackSucceeded = true;
					Advance(SteamRecoveryPhase.VerifyRollback);
					break;
				case SteamInstallationDisposition.Incomplete:
					operation.RecoveryAttempted = true;
					Advance(SteamRecoveryPhase.Rollback);
					break;
				default:
				{
					Advance(SteamRecoveryPhase.FilesCommitted);
					progress?.Invoke("Đang chờ Steam ổn định để xác minh kích hoạt...");
					SteamStartupResult steamStartupResult = await actions.PrepareVerificationAsync();
					if ((object)steamStartupResult != null && steamStartupResult.ProcessStateUnknown)
					{
						return Pending(steamStartupResult.Message);
					}
					if (!operation.BetterOnly && (object)steamStartupResult != null && steamStartupResult.IsRunning && (steamStartupResult.Health?.IsReady ?? false) && steamStartupResult.BetterRuntimeReady && !steamStartupResult.ProcessStateUnknown && !steamStartupResult.CoreInitializationFailed)
					{
						return Complete(SteamRecoveryOutcome.ActiveReady, "ACTIVE hoàn tất; Steam và Cloud Save đã được xác nhận.");
					}
					Advance((!operation.BetterOnly) ? SteamRecoveryPhase.VerifyActivation : SteamRecoveryPhase.VerifyBetter);
					break;
				}
				}
			}
			if (operation.Phase == SteamRecoveryPhase.VerifyActivation)
			{
				if (!(await EnsureStartRequestedAsync()))
				{
					return Pending(actions.DeferredMessage);
				}
				SteamStartupResult steamStartupResult2 = await actions.ObserveAsync(operation.SteamDirectory, operation.HealthMarker);
				operation.StartupFailed = steamStartupResult2.StartupFailed;
				operation.CloudInitializationFailed = steamStartupResult2.Health?.IsDefinitiveFailure ?? false;
				if (steamStartupResult2.ProcessStateUnknown)
				{
					return Pending(steamStartupResult2.Message);
				}
				if (steamStartupResult2.IsRunning && (steamStartupResult2.Health?.IsReady ?? false) && !steamStartupResult2.CoreInitializationFailed && steamStartupResult2.BetterRuntimeReady)
				{
					return Complete(SteamRecoveryOutcome.ActiveReady, "ACTIVE hoàn tất; Steam và Cloud Save đã được xác nhận.");
				}
				if (operation.AllowBetterFallback && !operation.FallbackAttempted && !steamStartupResult2.CoreInitializationFailed && ((steamStartupResult2.Health?.IsDefinitiveFailure ?? false) || (steamStartupResult2.IsRunning && steamStartupResult2.BetterRuntimeReady)))
				{
					operation.FallbackAttempted = true;
					operation.FallbackTransactionPath = store.FallbackTransactionPath(operation.Id);
					Advance(SteamRecoveryPhase.DisableCloud);
				}
				else
				{
					if (!steamStartupResult2.StartupFailed)
					{
						CloudRedirectHealthResult? health = steamStartupResult2.Health;
						if (((object)health == null || !health.IsDefinitiveFailure) && !steamStartupResult2.CoreInitializationFailed)
						{
							return Pending(steamStartupResult2.Message);
						}
					}
					operation.RecoveryAttempted = true;
					Advance(string.IsNullOrEmpty(operation.TransactionPath) ? SteamRecoveryPhase.DetachLoaders : SteamRecoveryPhase.Rollback);
				}
			}
			if (operation.Phase == SteamRecoveryPhase.DisableCloud)
			{
				progress?.Invoke("Đang hoàn tất cấu hình kích hoạt...");
				bool flag = !(await actions.CloseSteamAsync(operation.SteamDirectory));
				if (!flag)
				{
					flag = !(await actions.DisableCloudAsync(operation.SteamDirectory, operation.Id));
				}
				if (flag)
				{
					return Pending(actions.DeferredMessage);
				}
				operation.BetterOnly = true;
				operation.BetterLogMarker = BetterSteamToolsHealthReader.Capture();
				Advance(SteamRecoveryPhase.VerifyBetter);
			}
			if (operation.Phase == SteamRecoveryPhase.VerifyBetter)
			{
				progress?.Invoke("Đang kiểm tra Steam và thành phần kích hoạt...");
				SteamStartupResult steamStartupResult3;
				try
				{
					steamStartupResult3 = await StartAndObserveAsync();
				}
				catch (InvalidDataException ex)
				{
					DesktopDiagnosticLog.Write("SteamAutoRecovery", "BetterVerificationFailed", "Error=" + ex.GetType().Name);
					steamStartupResult3 = new SteamStartupResult(IsRunning: false, StartupFailed: false, null, null, "Better verification failed.")
					{
						CoreInitializationFailed = true
					};
				}
				if (steamStartupResult3.ProcessStateUnknown)
				{
					return Pending(steamStartupResult3.Message);
				}
				if (steamStartupResult3.IsRunning && steamStartupResult3.BetterRuntimeReady && !steamStartupResult3.CoreInitializationFailed)
				{
					return Complete(SteamRecoveryOutcome.BetterReady, "Active thành công.");
				}
				if (!steamStartupResult3.StartupFailed && !steamStartupResult3.CoreInitializationFailed)
				{
					return Pending("Chưa xác nhận được Steam và thành phần kích hoạt. Hãy thử lại sau khi kiểm tra Steam.");
				}
				operation.RecoveryAttempted = true;
				Advance((string.IsNullOrEmpty(operation.TransactionPath) && string.IsNullOrEmpty(operation.FallbackTransactionPath)) ? SteamRecoveryPhase.DetachLoaders : SteamRecoveryPhase.Rollback);
			}
			if (operation.Phase == SteamRecoveryPhase.Rollback)
			{
				progress?.Invoke("Đang tự phục hồi lần ACTIVE vừa lỗi...");
				if (!(await actions.CloseSteamAsync(operation.SteamDirectory)))
				{
					return Pending(actions.DeferredMessage);
				}
				try
				{
					bool flag = !string.IsNullOrEmpty(operation.FallbackTransactionPath);
					if (flag)
					{
						flag = !(await actions.RollbackAsync(operation.SteamDirectory, operation.FallbackTransactionPath));
					}
					if (flag)
					{
						return Pending(actions.DeferredMessage);
					}
					flag = !string.IsNullOrEmpty(operation.TransactionPath);
					if (flag)
					{
						flag = !(await actions.RollbackAsync(operation.SteamDirectory, operation.TransactionPath));
					}
					if (flag)
					{
						return Pending(actions.DeferredMessage);
					}
					operation.RollbackSucceeded = true;
				}
				catch (Exception ex2) when (((ex2 is InvalidDataException || ex2 is InvalidOperationException) ? 1 : 0) != 0)
				{
					operation.RollbackFailed = true;
					DesktopDiagnosticLog.Write("SteamAutoRecovery", "RollbackFailed", null, ex2);
				}
				Advance(SteamRecoveryPhase.VerifyRollback);
			}
			if (operation.Phase == SteamRecoveryPhase.VerifyRollback)
			{
				progress?.Invoke("Đang kiểm tra Steam sau khi hoàn tác...");
				SteamStartupResult steamStartupResult4 = await StartAndObserveAsync();
				if (steamStartupResult4.ProcessStateUnknown)
				{
					return Pending(steamStartupResult4.Message);
				}
				if (steamStartupResult4.IsRunning)
				{
					return Complete(SteamRecoveryOutcome.SteamRestored, "ACTIVE chưa hoàn tất; Steam đã chạy lại sau phục hồi tự động.");
				}
				if (!steamStartupResult4.StartupFailed)
				{
					return Pending("Chưa xác nhận được Steam sau rollback; launcher đã dừng để giữ nguyên dữ liệu.");
				}
				Advance(SteamRecoveryPhase.DetachLoaders);
			}
			if (operation.Phase == SteamRecoveryPhase.DetachLoaders)
			{
				progress?.Invoke("Đang tự phục hồi bộ nạp Steam...");
				bool flag2 = !(await actions.CloseSteamAsync(operation.SteamDirectory));
				if (!flag2)
				{
					flag2 = !(await actions.DetachLoadersAsync(operation.SteamDirectory, operation.Id));
				}
				if (flag2)
				{
					return Pending(actions.DeferredMessage);
				}
				operation.LoadersDetached = true;
				Advance(SteamRecoveryPhase.VerifyDetached);
			}
			if (operation.Phase == SteamRecoveryPhase.VerifyDetached)
			{
				progress?.Invoke("Đang mở lại Steam và kiểm tra kết quả phục hồi...");
				SteamStartupResult steamStartupResult5 = await StartAndObserveAsync();
				if (steamStartupResult5.ProcessStateUnknown)
				{
					return Pending(steamStartupResult5.Message);
				}
				if (!steamStartupResult5.IsRunning && !steamStartupResult5.StartupFailed)
				{
					return Pending("Chưa xác nhận được Steam sau phục hồi; hãy kiểm tra Steam rồi thử lại.");
				}
				return Complete(steamStartupResult5.IsRunning ? SteamRecoveryOutcome.SteamRestored : SteamRecoveryOutcome.Failed, steamStartupResult5.IsRunning ? "Steam đã chạy lại; launcher đã tạm ngắt bộ hỗ trợ NestG. ACTIVE chưa thành công; game và save được giữ nguyên." : "Đã thử phục hồi tự động nhưng chưa xác nhận Steam chạy lại. Cần kiểm tra log và nguyên nhân lỗi trước khi ACTIVE lại.");
			}
			return operation;
		}
		catch (Exception ex3) when (((ex3 is SteamRecoveryPendingException || ex3 is IOException || ex3 is UnauthorizedAccessException || ex3 is Win32Exception) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("SteamAutoRecovery", "Deferred", $"Operation={operation.Id}; Phase={operation.Phase}; Error={ex3.GetType().Name}");
			return Pending("Chưa thể tiếp tục kiểm tra Steam hoặc file cài đặt. Launcher giữ nguyên dữ liệu; hãy kiểm tra rồi thử lại.");
		}
		catch (Exception ex4) when (((ex4 is InvalidDataException || ex4 is InvalidOperationException) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("SteamAutoRecovery", "Stopped", null, ex4);
			return Complete(SteamRecoveryOutcome.Failed, "Phục hồi tự động chưa hoàn tất. Launcher đã dừng để bảo vệ dữ liệu; cần kiểm tra log trước khi ACTIVE lại.");
		}
		void Advance(SteamRecoveryPhase phase)
		{
			DesktopDiagnosticLog.Write("SteamAutoRecovery", "PhaseChanged", $"Operation={operation.Id}; From={operation.Phase}; To={phase}; BetterOnly={operation.BetterOnly}");
			operation.Phase = phase;
			operation.StartRequested = false;
			operation.LaunchAcknowledged = false;
			operation.LaunchAcknowledgementRequired = false;
			operation.Outcome = SteamRecoveryOutcome.None;
			store.Save(operation);
		}
		SteamRecoveryOperation Complete(SteamRecoveryOutcome outcome, string message)
		{
			operation.Outcome = outcome;
			operation.Message = message;
			operation.Phase = SteamRecoveryPhase.Complete;
			store.Save(operation);
			DesktopDiagnosticLog.Write("SteamAutoRecovery", outcome.ToString(), "Operation=" + operation.Id);
			return operation;
		}
		async Task<bool> EnsureStartRequestedAsync()
		{
			if (!(await actions.PrepareLaunchAsync()))
			{
				return false;
			}
			store.Save(operation);
			if (!operation.StartRequested || (operation.LaunchAcknowledgementRequired && !operation.LaunchAcknowledged))
			{
				operation.StartRequested = true;
				operation.LaunchAcknowledgementRequired = true;
				store.Save(operation);
				if (!(await actions.StartSteamAsync(operation.SteamDirectory)))
				{
					return false;
				}
				operation.LaunchAcknowledged = true;
				store.Save(operation);
			}
			return true;
		}
		SteamRecoveryOperation Pending(string message)
		{
			operation.Outcome = SteamRecoveryOutcome.Unconfirmed;
			operation.Message = message;
			store.Save(operation);
			return operation;
		}
		async Task<SteamStartupResult> StartAndObserveAsync()
		{
			if (!(await EnsureStartRequestedAsync()))
			{
				return new SteamStartupResult(IsRunning: false, StartupFailed: false, null, null, actions.DeferredMessage)
				{
					ProcessStateUnknown = true
				};
			}
			return await actions.ObserveAsync(operation.SteamDirectory, null);
		}
	}
}
