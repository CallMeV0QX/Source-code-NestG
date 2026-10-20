using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace app_desktop.Services.Warp;

internal sealed class CloudflareWarpPackageVerifier : IWarpPackageVerifier
{
	private struct WinTrustFileInfo
	{
		public uint Size;

		public nint FilePath;

		public nint FileHandle;

		public nint KnownSubject;
	}

	private struct WinTrustData
	{
		public uint Size;

		public nint PolicyCallbackData;

		public nint SipClientData;

		public uint UiChoice;

		public uint RevocationChecks;

		public uint UnionChoice;

		public nint File;

		public uint StateAction;

		public nint StateData;

		public nint UrlReference;

		public uint ProviderFlags;

		public uint UiContext;

		public nint SignatureSettings;
	}

	private static readonly byte[] MsiMagic = new byte[8] { 208, 207, 17, 224, 161, 177, 26, 225 };

	private static readonly Guid VerifyAction = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

	public bool IsOfficialInstaller(string path)
	{
		try
		{
			using (FileStream fileStream = File.OpenRead(path))
			{
				Span<byte> span = stackalloc byte[8];
				if (fileStream.Read(span) != span.Length || !span.SequenceEqual(MsiMagic))
				{
					return false;
				}
			}
			using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
			using X509Certificate2 x509Certificate = new X509Certificate2(certificate);
			if (!string.Equals(x509Certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false), "Cloudflare, Inc.", StringComparison.Ordinal))
			{
				return false;
			}
			return VerifySignature(path);
		}
		catch (Exception ex) when (((ex is CryptographicException || ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	private static bool VerifySignature(string path)
	{
		nint num = Marshal.StringToCoTaskMemUni(path);
		WinTrustFileInfo winTrustFileInfo = default(WinTrustFileInfo);
		winTrustFileInfo.Size = (uint)Marshal.SizeOf<WinTrustFileInfo>();
		winTrustFileInfo.FilePath = num;
		WinTrustFileInfo structure = winTrustFileInfo;
		nint num2 = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
		nint num3 = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustData>());
		try
		{
			Marshal.StructureToPtr(structure, num2, fDeleteOld: false);
			WinTrustData structure2 = default(WinTrustData);
			structure2.Size = (uint)Marshal.SizeOf<WinTrustData>();
			structure2.UiChoice = 2u;
			structure2.RevocationChecks = 1u;
			structure2.UnionChoice = 1u;
			structure2.File = num2;
			structure2.StateAction = 0u;
			structure2.ProviderFlags = 128u;
			Marshal.StructureToPtr(structure2, num3, fDeleteOld: false);
			Guid action = VerifyAction;
			return WinVerifyTrust(IntPtr.Zero, ref action, num3) == 0;
		}
		finally
		{
			Marshal.FreeCoTaskMem(num3);
			Marshal.FreeCoTaskMem(num2);
			Marshal.FreeCoTaskMem(num);
		}
	}

	[DllImport("wintrust.dll", ExactSpelling = true)]
	private static extern int WinVerifyTrust(nint window, ref Guid action, nint data);
}
