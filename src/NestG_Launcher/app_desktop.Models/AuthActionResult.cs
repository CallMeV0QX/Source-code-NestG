namespace app_desktop.Models;

internal sealed class AuthActionResult
{
	public AuthSession? Session { get; private init; }

	public OtpChallengeInfo? Challenge { get; private init; }

	public string? ErrorCode { get; private init; }

	public string Message { get; private init; } = string.Empty;

	public bool IsSuccess
	{
		get
		{
			if (Session == null)
			{
				return Challenge != null;
			}
			return true;
		}
	}

	public bool HasSession => Session != null;

	public bool RequiresChallenge => Challenge != null;

	public static AuthActionResult FromSession(AuthSession session, string message = "")
	{
		return new AuthActionResult
		{
			Session = session,
			Message = message
		};
	}

	public static AuthActionResult FromChallenge(OtpChallengeInfo challenge)
	{
		return new AuthActionResult
		{
			Challenge = challenge,
			Message = challenge.Message
		};
	}

	public static AuthActionResult FromError(string? errorCode, string message)
	{
		return new AuthActionResult
		{
			ErrorCode = errorCode,
			Message = message
		};
	}
}
