using System;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Infrastructure;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class AuthSessionManager
{
	private readonly DesktopAuthApiClient apiClient;

	private readonly SecureTokenStore tokenStore;

	private readonly DesktopAppSettings settings;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private bool persistCurrentSession;

	public AuthSession? CurrentSession { get; private set; }

	public AuthSessionManager(DesktopAuthApiClient apiClient, SecureTokenStore tokenStore, DesktopAppSettings settings)
	{
		this.apiClient = apiClient;
		this.tokenStore = tokenStore;
		this.settings = settings;
	}

	public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			AuthSession authSession = await tokenStore.LoadAsync(cancellationToken);
			if (authSession == null)
			{
				return false;
			}
			CurrentSession = authSession;
			persistCurrentSession = true;
			if (authSession.RefreshTokenExpiresAt <= DateTime.UtcNow)
			{
				await ClearSessionLockedAsync();
				return false;
			}
			return true;
		}
		finally
		{
			gate.Release();
		}
	}

	public Task<AuthActionResult> LoginAsync(string email, string password, bool persistSession, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteAuthFlowAsync(() => apiClient.LoginAsync(email, password, cancellationToken), persistSession, cancellationToken);
	}

	public Task<AuthActionResult> RegisterAsync(string displayName, string? phone, string email, string password, bool persistSession, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteAuthFlowAsync(() => apiClient.RegisterAsync(displayName, phone, email, password, cancellationToken), persistSession, cancellationToken);
	}

	public Task<AuthActionResult> VerifyEmailOtpAsync(string email, string otp, bool persistSession, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteAuthFlowAsync(() => apiClient.VerifyEmailOtpAsync(email, otp, cancellationToken), persistSession, cancellationToken);
	}

	public Task<AuthActionResult> ResendEmailOtpAsync(string email, CancellationToken cancellationToken = default(CancellationToken))
	{
		return apiClient.ResendEmailOtpAsync(email, cancellationToken);
	}

	public Task<AuthActionResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default(CancellationToken))
	{
		return apiClient.ForgotPasswordAsync(email, cancellationToken);
	}

	public Task<AuthActionResult> ResetPasswordAsync(string email, string otp, string password, bool persistSession, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteAuthFlowAsync(() => apiClient.ResetPasswordAsync(email, otp, password, cancellationToken), persistSession, cancellationToken);
	}

	public async Task<bool> EnsureFreshSessionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			return await EnsureFreshSessionLockedAsync(cancellationToken);
		}
		finally
		{
			gate.Release();
		}
	}

	public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return (!(await EnsureFreshSessionAsync(cancellationToken))) ? null : CurrentSession?.AccessToken;
	}

	public async Task<AuthActionResult> RefreshCurrentSessionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			if (CurrentSession == null)
			{
				return AuthActionResult.FromError(null, "Chưa có phiên đăng nhập để làm mới.");
			}
			AuthActionResult result = await apiClient.RefreshAsync(CurrentSession.RefreshToken, cancellationToken);
			if (result.Session != null)
			{
				await ApplySessionLockedAsync(result.Session, persistCurrentSession, cancellationToken);
			}
			else if (!result.IsSuccess)
			{
				await ClearSessionLockedAsync();
			}
			return result;
		}
		finally
		{
			gate.Release();
		}
	}

	public async Task SignOutAsync()
	{
		await gate.WaitAsync();
		try
		{
			await ClearSessionLockedAsync();
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task<AuthActionResult> ExecuteAuthFlowAsync(Func<Task<AuthActionResult>> operation, bool persistSession, CancellationToken cancellationToken)
	{
		AuthActionResult result = await operation();
		if (result.Session == null)
		{
			return result;
		}
		await gate.WaitAsync(cancellationToken);
		try
		{
			await ApplySessionLockedAsync(result.Session, persistSession, cancellationToken);
		}
		finally
		{
			gate.Release();
		}
		return result;
	}

	private async Task<bool> EnsureFreshSessionLockedAsync(CancellationToken cancellationToken)
	{
		if (CurrentSession == null)
		{
			return false;
		}
		DateTime dateTime = DateTime.UtcNow.Add(settings.RefreshThreshold);
		if (CurrentSession.AccessTokenExpiresAt > dateTime)
		{
			return true;
		}
		if (CurrentSession.RefreshTokenExpiresAt <= DateTime.UtcNow)
		{
			await ClearSessionLockedAsync();
			return false;
		}
		AuthActionResult authActionResult = await apiClient.RefreshAsync(CurrentSession.RefreshToken, cancellationToken);
		if (authActionResult.Session == null)
		{
			await ClearSessionLockedAsync();
			return false;
		}
		await ApplySessionLockedAsync(authActionResult.Session, persistCurrentSession, cancellationToken);
		return true;
	}

	private async Task ApplySessionLockedAsync(AuthSession session, bool persistSession, CancellationToken cancellationToken)
	{
		CurrentSession = session;
		persistCurrentSession = persistSession;
		if (persistSession)
		{
			await tokenStore.SaveAsync(session, cancellationToken);
		}
		else
		{
			await tokenStore.ClearAsync();
		}
	}

	private async Task ClearSessionLockedAsync()
	{
		CurrentSession = null;
		persistCurrentSession = false;
		await tokenStore.ClearAsync();
	}
}
