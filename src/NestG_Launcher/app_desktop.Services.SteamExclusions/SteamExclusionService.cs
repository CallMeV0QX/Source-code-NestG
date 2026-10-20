using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionService(ISteamGameFolderDiscovery discovery, IDefenderExclusionClient defender, ISteamExclusionConsentStore consentStore, Func<IReadOnlyList<string>, bool> confirm, TimeProvider? timeProvider = null)
{
	private int started;

	internal async Task<SteamExclusionOutcome> RunOnceAsync(CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref started, 1) != 0)
		{
			return SteamExclusionOutcome.Deferred;
		}
		TimeProvider clock = timeProvider ?? TimeProvider.System;
		try
		{
			string[] paths = (await discovery.FindAsync(cancellationToken)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
			if (paths.Length == 0)
			{
				return SteamExclusionOutcome.NoSteam;
			}
			DefenderSnapshot snapshot = await defender.ReadAsync(cancellationToken);
			if (snapshot.Status != 0)
			{
				return snapshot.Status switch
				{
					DefenderAvailability.Unavailable => SteamExclusionOutcome.Unavailable, 
					DefenderAvailability.Blocked => SteamExclusionOutcome.Blocked, 
					_ => SteamExclusionOutcome.Unknown, 
				};
			}
			string[] array = paths.Where((string path) => !SteamExclusionPaths.IsCovered(path, snapshot.Paths)).ToArray();
			if (array.Length == 0)
			{
				return SteamExclusionOutcome.AlreadyCovered;
			}
			SteamExclusionConsent consent = consentStore.Load();
			if (consent.RetryAfterUtc > clock.GetUtcNow())
			{
				return SteamExclusionOutcome.Deferred;
			}
			cancellationToken.ThrowIfCancellationRequested();
			string[] array2 = array.Except<string>(consent.ApprovedPaths, StringComparer.OrdinalIgnoreCase).ToArray();
			if (array2.Length != 0)
			{
				if (!confirm(array2))
				{
					consentStore.Save(consent with
					{
						RetryAfterUtc = clock.GetUtcNow().AddDays(7.0)
					});
					return SteamExclusionOutcome.Declined;
				}
				consent = consent with
				{
					ApprovedPaths = consent.ApprovedPaths.Union<string>(array2, StringComparer.OrdinalIgnoreCase).ToArray()
				};
				consentStore.Save(consent);
			}
			cancellationToken.ThrowIfCancellationRequested();
			ExclusionApplyResult exclusionApplyResult = await defender.AddAndVerifyAsync(array, cancellationToken);
			consentStore.Save(consent with
			{
				RetryAfterUtc = ((exclusionApplyResult == ExclusionApplyResult.Verified) ? DateTimeOffset.MinValue : clock.GetUtcNow().AddDays(1.0))
			});
			return exclusionApplyResult switch
			{
				ExclusionApplyResult.Verified => SteamExclusionOutcome.Added, 
				ExclusionApplyResult.Declined => SteamExclusionOutcome.Declined, 
				ExclusionApplyResult.TimedOut => SteamExclusionOutcome.TimedOut, 
				_ => SteamExclusionOutcome.Failed, 
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			return SteamExclusionOutcome.TimedOut;
		}
		catch
		{
			return SteamExclusionOutcome.Failed;
		}
	}
}
