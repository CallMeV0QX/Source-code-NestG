using System;
using System.Text;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class PeImportParser
{
	public static string ReadAsciiZ(byte[] data, int offset)
	{
		int i;
		for (i = offset; i < data.Length && data[i] != 0; i++)
		{
		}
		if (i == offset)
		{
			return string.Empty;
		}
		return Encoding.ASCII.GetString(data, offset, i - offset);
	}

	public static (int loadLibA, int getProcAddr) FindKernel32IatEntries(byte[] pe, PeSection[] sections)
	{
		if (pe.Length < 64)
		{
			return (loadLibA: -1, getProcAddr: -1);
		}
		int num = BitConverter.ToInt32(pe, 60);
		if (num < 0 || num + 24 > pe.Length)
		{
			return (loadLibA: -1, getProcAddr: -1);
		}
		int num2 = BitConverter.ToUInt16(pe, num + 24);
		int num3;
		switch (num2)
		{
		case 523:
			num3 = num + 24 + 120;
			break;
		case 267:
			num3 = num + 24 + 104;
			break;
		default:
			return (loadLibA: -1, getProcAddr: -1);
		}
		if (num3 + 8 > pe.Length)
		{
			return (loadLibA: -1, getProcAddr: -1);
		}
		int num4 = BitConverter.ToInt32(pe, num3);
		int num5 = BitConverter.ToInt32(pe, num3 + 4);
		if (num4 == 0 || num5 == 0)
		{
			return (loadLibA: -1, getProcAddr: -1);
		}
		int num6 = PeSection.RvaToFileOffset(sections, num4);
		if (num6 < 0)
		{
			return (loadLibA: -1, getProcAddr: -1);
		}
		int num7 = -1;
		int num8 = -1;
		for (int i = num6; i + 20 <= pe.Length; i += 20)
		{
			int num9 = BitConverter.ToInt32(pe, i + 12);
			if (num9 == 0)
			{
				break;
			}
			int num10 = PeSection.RvaToFileOffset(sections, num9);
			if (num10 < 0 || num10 >= pe.Length)
			{
				continue;
			}
			string text = ReadAsciiZ(pe, num10);
			if (!text.Equals("KERNEL32.dll", StringComparison.OrdinalIgnoreCase) && !text.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int num11 = BitConverter.ToInt32(pe, i);
			int num12 = BitConverter.ToInt32(pe, i + 16);
			if (num11 == 0)
			{
				num11 = num12;
			}
			int num13 = PeSection.RvaToFileOffset(sections, num11);
			int num14 = PeSection.RvaToFileOffset(sections, num12);
			if (num13 < 0 || num14 < 0)
			{
				continue;
			}
			int num15 = ((num2 == 523) ? 8 : 4);
			int num16 = 0;
			while (true)
			{
				int num17 = num13 + num16 * num15;
				int num18 = num14 + num16 * num15;
				if (num17 + num15 > pe.Length || num18 + num15 > pe.Length)
				{
					break;
				}
				long num19 = ((num15 != 8) ? BitConverter.ToUInt32(pe, num17) : BitConverter.ToInt64(pe, num17));
				if (num19 == 0L)
				{
					break;
				}
				if (!((num15 == 8) ? ((num19 & long.MinValue) != 0) : ((num19 & 0x80000000u) != 0)))
				{
					int rva = (int)(num19 & 0xFFFFFFFFu);
					int num20 = PeSection.RvaToFileOffset(sections, rva);
					if (num20 >= 0 && num20 + 2 < pe.Length)
					{
						string text2 = ReadAsciiZ(pe, num20 + 2);
						int num21 = num12 + num16 * num15;
						if (text2 == "LoadLibraryA")
						{
							num7 = num21;
						}
						else if (text2 == "GetProcAddress")
						{
							num8 = num21;
						}
						if (num7 >= 0 && num8 >= 0)
						{
							return (loadLibA: num7, getProcAddr: num8);
						}
					}
				}
				num16++;
			}
			break;
		}
		return (loadLibA: num7, getProcAddr: num8);
	}
}
