using System;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class Signatures
{
	public static readonly PatternPatch[] CorePatchDefs = new PatternPatch[2]
	{
		new PatternPatch
		{
			Name = "Core1 (download call)",
			Pattern = new byte[26]
			{
				72, 139, 76, 36, 0, 72, 141, 85, 0, 0,
				0, 0, 0, 0, 133, 192, 15, 132, 0, 0,
				0, 0, 65, 131, 252, 1
			},
			Mask = new byte[26]
			{
				255, 255, 255, 255, 0, 255, 255, 255, 0, 0,
				0, 0, 0, 0, 255, 255, 255, 255, 0, 0,
				0, 0, 255, 255, 255, 255
			},
			PatchOffset = 9,
			Original = new byte[5] { 232, 124, 245, 255, 255 },
			Replacement = new byte[5] { 184, 1, 0, 0, 0 },
			Region = ScanRegion.Text,
			WildcardStart = 1,
			WildcardLen = 4,
			Validator = delegate(byte[] data, int hit)
			{
				byte b = data[hit + 9];
				return (b == 232) ? (BitConverter.ToInt32(data, hit + 10) < 0) : (b == 184);
			}
		},
		new PatternPatch
		{
			Name = "Core2 (hash check jz)",
			Pattern = new byte[19]
			{
				73, 139, 213, 72, 141, 77, 0, 232, 0, 0,
				0, 0, 133, 192, 0, 0, 51, 255, 233
			},
			Mask = new byte[19]
			{
				255, 255, 255, 255, 255, 255, 0, 255, 0, 0,
				0, 0, 255, 255, 0, 0, 255, 255, 255
			},
			PatchOffset = 14,
			Original = new byte[1] { 116 },
			Replacement = new byte[1] { 235 },
			Region = ScanRegion.Text,
			RelativeToPatchIndex = 0,
			RelativeStart = -768,
			RelativeEnd = 768,
			Validator = delegate(byte[] data, int hit)
			{
				byte b2 = data[hit + 14];
				return b2 == 116 || b2 == 235;
			}
		}
	};

	public static readonly PatternPatch[] PayloadP123Defs = new PatternPatch[3]
	{
		new PatternPatch
		{
			Name = "P1 (cloud rewrite skip)",
			Pattern = new byte[24]
			{
				68, 139, 61, 0, 0, 0, 0, 133, 192, 15,
				133, 0, 0, 0, 0, 69, 133, 255, 0, 0,
				0, 0, 0, 0
			},
			Mask = new byte[24]
			{
				255, 255, 255, 0, 0, 0, 0, 255, 255, 255,
				255, 0, 0, 255, 255, 255, 255, 255, 0, 0,
				0, 0, 255, 255
			},
			PatchOffset = 18,
			Original = new byte[6] { 15, 132, 59, 1, 0, 0 },
			Replacement = new byte[6] { 144, 233, 59, 1, 0, 0 },
			Region = ScanRegion.Text,
			WildcardStart = 2,
			WildcardLen = 4,
			Validator = (byte[] data, int hit) => (data[hit + 18] == 15 && data[hit + 19] == 132) || (data[hit + 18] == 144 && data[hit + 19] == 233)
		},
		new PatternPatch
		{
			Name = "P2 (proxy appid zero)",
			Pattern = new byte[39]
			{
				72, 139, 240, 76, 139, 199, 76, 139, 124, 36,
				0, 73, 139, 215, 72, 139, 200, 232, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 72, 141,
				20, 62, 72, 129, 249, 128, 0, 0, 0
			},
			Mask = new byte[39]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				0, 255, 255, 255, 255, 255, 255, 255, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 255, 255,
				255, 255, 255, 255, 255, 255, 255, 255, 255
			},
			PatchOffset = 22,
			Original = new byte[6] { 139, 13, 125, 202, 27, 0 },
			Replacement = new byte[6] { 49, 201, 144, 144, 144, 144 },
			Region = ScanRegion.Text,
			RelativeToPatchIndex = 0,
			RelativeStart = 0,
			RelativeEnd = 1280,
			Validator = (byte[] data, int hit) => (data[hit + 22] == 139 && data[hit + 23] == 13) || (data[hit + 22] == 49 && data[hit + 23] == 201)
		},
		new PatternPatch
		{
			Name = "P3 (IPC appid preserve)",
			Pattern = new byte[7] { 199, 64, 9, 224, 1, 0, 0 },
			Mask = new byte[7] { 255, 255, 255, 255, 255, 255, 255 },
			PatchOffset = 0,
			Original = new byte[6] { 137, 61, 0, 0, 0, 0 },
			Replacement = new byte[6] { 144, 144, 144, 144, 144, 144 },
			Region = ScanRegion.Obfuscated,
			WildcardStart = 2,
			WildcardLen = 4,
			PatchSiteResolver = delegate(byte[] data, int hit)
			{
				int num = hit + 7;
				int num2 = Math.Min(num + 30, data.Length);
				for (int i = num; i < num2 - 5; i++)
				{
					if (data[i] == 137 && data[i + 1] == 61)
					{
						return i;
					}
					if (data[i] == 144 && data[i + 1] == 144 && data[i + 2] == 144 && data[i + 3] == 144 && data[i + 4] == 144 && data[i + 5] == 144)
					{
						return i;
					}
				}
				return -1;
			}
		}
	};

	public static readonly PatternPatch[] PayloadSetupDefs = new PatternPatch[3]
	{
		new PatternPatch
		{
			Name = "P4 (activation flag)",
			Pattern = new byte[3] { 77, 133, 192 },
			Mask = new byte[3] { 255, 255, 255 },
			PatchOffset = 0,
			Original = new byte[7] { 198, 5, 0, 0, 0, 0, 0 },
			Replacement = new byte[7] { 198, 5, 0, 0, 0, 0, 1 },
			Region = ScanRegion.Obfuscated,
			WildcardStart = 2,
			WildcardLen = 4,
			PatchSiteResolver = delegate(byte[] data, int hit)
			{
				int pos2 = hit + 3;
				pos2 = SkipOptionalBridge(data, pos2);
				if (!HasBytes(data, pos2, new byte[2] { 15, 132 }))
				{
					return -1;
				}
				pos2 += 6;
				if (!HasBytes(data, pos2, new byte[1] { 232 }))
				{
					return -1;
				}
				pos2 += 5;
				if (!HasBytes(data, pos2, new byte[2] { 133, 192 }))
				{
					return -1;
				}
				pos2 += 2;
				pos2 = SkipOptionalBridge(data, pos2);
				if (!HasBytes(data, pos2, new byte[2] { 15, 133 }))
				{
					return -1;
				}
				pos2 += 6;
				if (!HasBytes(data, pos2, new byte[2] { 198, 5 }))
				{
					return -1;
				}
				if (pos2 + 6 >= data.Length || data[pos2 + 6] != 1)
				{
					return -1;
				}
				pos2 += 7;
				pos2 = SkipOptionalBridge(data, pos2);
				if (!HasBytes(data, pos2, new byte[1] { 233 }))
				{
					return -1;
				}
				pos2 += 5;
				if (!HasBytes(data, pos2, new byte[2] { 198, 5 }))
				{
					return -1;
				}
				if (pos2 + 6 >= data.Length)
				{
					return -1;
				}
				byte b3 = data[pos2 + 6];
				return (b3 != 0 && b3 != 1) ? (-1) : pos2;
				static int SkipOptionalBridge(byte[] bytes, int pos)
				{
					if (!HasBytes(bytes, pos, new byte[1] { 233 }))
					{
						return pos;
					}
					return pos + 5;
					static bool HasBytes(byte[] bytes, int pos, params byte[] expected)
					{
						if (pos < 0 || pos + expected.Length > bytes.Length)
						{
							return false;
						}
						for (int j = 0; j < expected.Length; j++)
						{
							if (bytes[pos + j] != expected[j])
							{
								return false;
							}
						}
						return true;
					}
				}
			}
		},
		new PatternPatch
		{
			Name = "P5 (GetCookie retry skip)",
			Pattern = new byte[23]
			{
				102, 72, 15, 126, 199, 102, 72, 15, 126, 206,
				72, 141, 77, 0, 232, 0, 0, 0, 0, 72,
				133, 246, 0
			},
			Mask = new byte[23]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 0, 255, 0, 0, 0, 0, 255,
				255, 255, 0
			},
			PatchOffset = 22,
			Original = new byte[1] { 117 },
			Replacement = new byte[1] { 235 },
			Region = ScanRegion.Text,
			Validator = delegate(byte[] data, int hit)
			{
				if (hit + 24 > data.Length)
				{
					return false;
				}
				byte b4 = data[hit + 22];
				if (b4 != 117 && b4 != 235)
				{
					return false;
				}
				int num3 = (sbyte)data[hit + 23];
				if (num3 <= 0)
				{
					return false;
				}
				int num4 = hit + 24 + num3;
				if (num4 > data.Length)
				{
					return false;
				}
				for (int k = hit + 24; k < num4 && k < data.Length - 4; k++)
				{
					if (data[k] == 233 && BitConverter.ToInt32(data, k + 1) < 0)
					{
						return true;
					}
				}
				return false;
			}
		},
		new PatternPatch
		{
			Name = "P6 (GMRC pattern fix)",
			Pattern = new byte[18]
			{
				52, 56, 32, 56, 57, 32, 53, 67, 32, 50,
				52, 32, 49, 56, 32, 53, 53, 32
			},
			Mask = new byte[18]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 255, 255, 255, 255, 255
			},
			PatchOffset = 0,
			Validator = delegate(byte[] data, int hit)
			{
				if (hit + 48 > data.Length)
				{
					return false;
				}
				bool num5 = data[hit + 18] == 53 && data[hit + 19] == 54 && data[hit + 44] == 94 && data[hit + 45] == 0;
				bool flag = data[hit + 18] == 53 && data[hit + 19] == 55 && data[hit + 46] == 67 && data[hit + 47] == 0;
				return num5 || flag;
			},
			Original = new byte[48]
			{
				52, 56, 32, 56, 57, 32, 53, 67, 32, 50,
				52, 32, 49, 56, 32, 53, 53, 32, 53, 54,
				32, 53, 55, 32, 52, 49, 32, 53, 53, 32,
				52, 49, 32, 53, 55, 32, 52, 56, 32, 56,
				68, 32, 54, 67, 94, 0, 0, 0
			},
			Replacement = new byte[48]
			{
				52, 56, 32, 56, 57, 32, 53, 67, 32, 50,
				52, 32, 49, 56, 32, 53, 53, 32, 53, 55,
				32, 52, 49, 32, 53, 52, 32, 52, 49, 32,
				53, 54, 32, 52, 49, 32, 53, 55, 32, 52,
				56, 32, 56, 68, 32, 54, 67, 0
			},
			Region = ScanRegion.All
		}
	};

	public static int ScanForPattern(byte[] data, int start, int end, byte[] pattern, byte[] mask)
	{
		int num = Math.Min(end, data.Length) - pattern.Length;
		for (int i = start; i <= num; i++)
		{
			bool flag = true;
			for (int j = 0; j < pattern.Length; j++)
			{
				if (mask[j] != 0 && data[i + j] != pattern[j])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}

	public static int ScanForBytes(byte[] data, int start, int end, byte[] needle)
	{
		int num = Math.Min(end, data.Length) - needle.Length;
		for (int i = start; i <= num; i++)
		{
			bool flag = true;
			for (int j = 0; j < needle.Length; j++)
			{
				if (data[i + j] != needle[j])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}

	public static int ResolvePatternPatch(byte[] data, PatternPatch patch, int sectionStart, int sectionEnd, int[]? resolvedOffsets = null)
	{
		int num = sectionStart;
		int num2 = sectionEnd;
		if (patch.RelativeToPatchIndex.HasValue && resolvedOffsets != null)
		{
			int value = patch.RelativeToPatchIndex.Value;
			if (value >= 0 && value < resolvedOffsets.Length && resolvedOffsets[value] >= 0)
			{
				num = Math.Max(sectionStart, resolvedOffsets[value] + patch.RelativeStart);
				num2 = Math.Min(resolvedOffsets[value] + patch.RelativeEnd, sectionEnd);
			}
		}
		int num3 = num;
		while (num3 < num2)
		{
			int num4 = ScanForPattern(data, num3, num2, patch.Pattern, patch.Mask);
			if (num4 < 0)
			{
				break;
			}
			if (patch.Validator != null && !patch.Validator(data, num4))
			{
				num3 = num4 + 1;
				continue;
			}
			if (patch.PatchSiteResolver != null)
			{
				int num5 = patch.PatchSiteResolver(data, num4);
				if (num5 >= 0)
				{
					return num5;
				}
				num3 = num4 + 1;
				continue;
			}
			return num4 + patch.PatchOffset;
		}
		return -1;
	}

	public static PatchEntry[]? ResolvePatternGroup(byte[] data, PatternPatch[] patches, int textStart, int textEnd, int obfStart, int obfEnd, Action<string>? log = null)
	{
		PatchEntry[] array = new PatchEntry[patches.Length];
		int[] array2 = new int[patches.Length];
		for (int i = 0; i < patches.Length; i++)
		{
			PatternPatch patternPatch = patches[i];
			int sectionStart;
			int sectionEnd;
			switch (patternPatch.Region)
			{
			case ScanRegion.Text:
				sectionStart = textStart;
				sectionEnd = textEnd;
				break;
			case ScanRegion.Obfuscated:
				sectionStart = obfStart;
				sectionEnd = obfEnd;
				break;
			default:
				sectionStart = 0;
				sectionEnd = data.Length;
				break;
			}
			int num = ResolvePatternPatch(data, patternPatch, sectionStart, sectionEnd, array2);
			if (num < 0)
			{
				log?.Invoke("  Could not locate " + patternPatch.Name);
				return null;
			}
			array2[i] = num;
			array[i] = SnapshotPatch(data, num, patternPatch);
			log?.Invoke($"  {patternPatch.Name} at 0x{num:X}");
		}
		return array;
	}

	private static PatchEntry SnapshotPatch(byte[] data, int offset, PatternPatch template)
	{
		byte[] array = (byte[])template.Original.Clone();
		byte[] array2 = (byte[])template.Replacement.Clone();
		int num = array.Length;
		if (template.WildcardLen > 0 && template.WildcardStart + template.WildcardLen <= num && offset + template.WildcardStart + template.WildcardLen <= data.Length)
		{
			Buffer.BlockCopy(data, offset + template.WildcardStart, array, template.WildcardStart, template.WildcardLen);
			Buffer.BlockCopy(data, offset + template.WildcardStart, array2, template.WildcardStart, template.WildcardLen);
		}
		return new PatchEntry(offset, array, array2);
	}

	public static int FindSendPktFunction(byte[] data, int textStart, int textEnd)
	{
		byte[] needle = new byte[6] { 184, 0, 17, 0, 0, 232 };
		int num = textStart;
		while (num < textEnd)
		{
			int num2 = ScanForBytes(data, num, textEnd, needle);
			if (num2 < 0)
			{
				break;
			}
			int num3 = num2 - 24;
			if (num3 < textStart)
			{
				num = num2 + 1;
				continue;
			}
			if ((data[num3] == 72 && data[num3 + 1] == 137 && data[num3 + 2] == 92 && data[num3 + 3] == 36 && data[num3 + 4] == 32) || data[num3] == 233)
			{
				return num3;
			}
			num = num2 + 1;
		}
		return -1;
	}

	public static int FindCodeCave(byte[] data, PeSection[] sections, int requiredSize)
	{
		for (int i = 0; i < sections.Length; i++)
		{
			PeSection peSection = sections[i];
			if (peSection.IsExecutable)
			{
				int num = (int)(peSection.RawOffset + peSection.RawSize);
				if (num > data.Length)
				{
					num = data.Length;
				}
				int rawOffset = (int)peSection.RawOffset;
				int num2 = 0;
				int num3 = num - 1;
				while (num3 >= rawOffset && data[num3] == 0)
				{
					num2++;
					num3--;
				}
				if (num2 >= requiredSize)
				{
					return num - num2;
				}
			}
		}
		return -1;
	}

	public static int FindRecvPktGlobalRva(byte[] data, PeSection[] sections, int sendPktRva, int searchStart, int searchEnd)
	{
		for (int i = searchStart; i < searchEnd - 7; i++)
		{
			if (data[i] != 72 || data[i + 1] != 141 || data[i + 2] != 13)
			{
				continue;
			}
			int num = BitConverter.ToInt32(data, i + 3);
			int num2 = PeSection.FileOffsetToRva(sections, i);
			if (num2 < 0 || num2 + 7 + num != sendPktRva)
			{
				continue;
			}
			for (int j = i + 7; j < Math.Min(i + 256, searchEnd) - 7; j++)
			{
				if (data[j] == 72 && data[j + 1] == 137 && data[j + 2] == 13)
				{
					int num3 = BitConverter.ToInt32(data, j + 3);
					int num4 = PeSection.FileOffsetToRva(sections, j);
					if (num4 >= 0)
					{
						return num4 + 7 + num3;
					}
				}
			}
		}
		return -1;
	}

	private static bool HasBytes(byte[] bytes, int pos, params byte[] expected)
	{
		if (pos < 0 || pos + expected.Length > bytes.Length)
		{
			return false;
		}
		for (int j = 0; j < expected.Length; j++)
		{
			if (bytes[pos + j] != expected[j])
			{
				return false;
			}
		}
		return true;
	}
}
