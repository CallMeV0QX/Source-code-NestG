using System;
using System.Text;

namespace app_desktop.Services.CloudRedirect.Patching;

internal readonly struct PeSection
{
	public readonly string Name;

	public readonly uint VirtualAddress;

	public readonly uint VirtualSize;

	public readonly uint RawOffset;

	public readonly uint RawSize;

	public readonly uint Characteristics;

	private const uint SCN_MEM_EXECUTE = 536870912u;

	public bool IsExecutable => (Characteristics & 0x20000000) != 0;

	public PeSection(string name, uint va, uint vsize, uint raw, uint rawSize, uint characteristics = 0u)
	{
		Name = name;
		VirtualAddress = va;
		VirtualSize = vsize;
		RawOffset = raw;
		RawSize = rawSize;
		Characteristics = characteristics;
	}

	public static PeSection[] Parse(byte[] pe)
	{
		if (pe.Length < 64)
		{
			return Array.Empty<PeSection>();
		}
		int num = BitConverter.ToInt32(pe, 60);
		if (num < 0 || num + 24 > pe.Length)
		{
			return Array.Empty<PeSection>();
		}
		if (pe[num] != 80 || pe[num + 1] != 69)
		{
			return Array.Empty<PeSection>();
		}
		int num2 = BitConverter.ToUInt16(pe, num + 6);
		if (num2 > 96)
		{
			return Array.Empty<PeSection>();
		}
		int num3 = BitConverter.ToUInt16(pe, num + 20);
		int num4 = num + 24 + num3;
		if (num4 > pe.Length)
		{
			return Array.Empty<PeSection>();
		}
		PeSection[] array = new PeSection[num2];
		int num5 = 0;
		for (int i = 0; i < num2; i++)
		{
			int num6 = num4 + i * 40;
			if (num6 + 40 > pe.Length)
			{
				break;
			}
			int count = 0;
			for (int j = 0; j < 8 && pe[num6 + j] != 0; j++)
			{
				count = j + 1;
			}
			string @string = Encoding.ASCII.GetString(pe, num6, count);
			uint vsize = BitConverter.ToUInt32(pe, num6 + 8);
			uint va = BitConverter.ToUInt32(pe, num6 + 12);
			uint rawSize = BitConverter.ToUInt32(pe, num6 + 16);
			uint raw = BitConverter.ToUInt32(pe, num6 + 20);
			uint characteristics = BitConverter.ToUInt32(pe, num6 + 36);
			array[i] = new PeSection(@string, va, vsize, raw, rawSize, characteristics);
			num5++;
		}
		if (num5 < num2)
		{
			Array.Resize(ref array, num5);
		}
		return array;
	}

	public static PeSection? Find(PeSection[] sections, string name)
	{
		for (int i = 0; i < sections.Length; i++)
		{
			if (sections[i].Name == name)
			{
				return sections[i];
			}
		}
		return null;
	}

	public static int FileOffsetToRva(PeSection[] sections, int fileOffset)
	{
		if (fileOffset < 0)
		{
			return -1;
		}
		for (int i = 0; i < sections.Length; i++)
		{
			PeSection peSection = sections[i];
			if ((uint)fileOffset >= peSection.RawOffset && (uint)(fileOffset - (int)peSection.RawOffset) < peSection.RawSize)
			{
				return (int)peSection.VirtualAddress + (fileOffset - (int)peSection.RawOffset);
			}
		}
		return -1;
	}

	public static int RvaToFileOffset(PeSection[] sections, int rva)
	{
		if (rva < 0)
		{
			return -1;
		}
		for (int i = 0; i < sections.Length; i++)
		{
			PeSection peSection = sections[i];
			long num = Math.Max(peSection.VirtualSize, peSection.RawSize);
			if ((uint)rva >= peSection.VirtualAddress && (uint)(rva - (int)peSection.VirtualAddress) < (uint)num)
			{
				uint num2 = (uint)rva - peSection.VirtualAddress;
				if (num2 >= peSection.RawSize)
				{
					return -1;
				}
				return (int)(peSection.RawOffset + num2);
			}
		}
		return -1;
	}

	public static PeSection? FindByFileOffset(PeSection[] sections, int fileOffset)
	{
		if (fileOffset < 0)
		{
			return null;
		}
		for (int i = 0; i < sections.Length; i++)
		{
			PeSection value = sections[i];
			if ((uint)fileOffset >= value.RawOffset && (uint)fileOffset < value.RawOffset + value.RawSize)
			{
				return value;
			}
		}
		return null;
	}

	public static PeSection? FindByRva(PeSection[] sections, int rva)
	{
		if (rva < 0)
		{
			return null;
		}
		for (int i = 0; i < sections.Length; i++)
		{
			PeSection value = sections[i];
			long num = Math.Max(value.VirtualSize, value.RawSize);
			if ((uint)rva >= value.VirtualAddress && (uint)(rva - (int)value.VirtualAddress) < (uint)num)
			{
				return value;
			}
		}
		return null;
	}
}
