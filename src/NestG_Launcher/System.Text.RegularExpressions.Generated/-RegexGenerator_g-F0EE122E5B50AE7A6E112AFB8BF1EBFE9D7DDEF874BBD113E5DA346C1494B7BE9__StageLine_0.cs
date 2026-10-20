using System.CodeDom.Compiler;
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
internal sealed class _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__StageLine_0 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				if (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan))
				{
					runtextpos = inputSpan.Length;
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 52 && num == 0)
				{
					return true;
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if (num != 0)
				{
					UncaptureUntil(0);
					return false;
				}
				num2 = num;
				if ((uint)span.Length < 24u || !char.IsDigit(span[0]) || !char.IsDigit(span[1]) || !char.IsDigit(span[2]) || !char.IsDigit(span[3]) || span[4] != '-' || !char.IsDigit(span[5]) || !char.IsDigit(span[6]) || span[7] != '-' || !char.IsDigit(span[8]) || !char.IsDigit(span[9]) || span[10] != 'T' || !char.IsDigit(span[11]) || !char.IsDigit(span[12]) || span[13] != ':' || !char.IsDigit(span[14]) || !char.IsDigit(span[15]) || span[16] != ':' || !char.IsDigit(span[17]) || !char.IsDigit(span[18]) || span[19] != '.' || !char.IsDigit(span[20]) || !char.IsDigit(span[21]) || !char.IsDigit(span[22]) || span[23] != 'Z')
				{
					UncaptureUntil(0);
					return false;
				}
				num += 24;
				span = inputSpan.Slice(num);
				Capture(1, num2, num);
				if (!span.StartsWith(" PID="))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 5;
				span = inputSpan.Slice(num);
				num3 = num;
				int i;
				for (i = 0; (uint)i < (uint)span.Length && char.IsDigit(span[i]); i++)
				{
				}
				if (i == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(i);
				num += i;
				Capture(2, num3, num);
				if (!span.StartsWith(" TID="))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 5;
				span = inputSpan.Slice(num);
				int j;
				for (j = 0; (uint)j < (uint)span.Length && char.IsDigit(span[j]); j++)
				{
				}
				if (j == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(j);
				num += j;
				if (!span.StartsWith(" Stage="))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 7;
				span = inputSpan.Slice(num);
				num4 = num;
				int num5 = span.IndexOfAnyExcept(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_ascii_40000000000000FEFFFF07);
				if (num5 < 0)
				{
					num5 = span.Length;
				}
				if (num5 == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(num5);
				num += num5;
				Capture(3, num4, num);
				if (!span.StartsWith(" Error="))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 7;
				span = inputSpan.Slice(num);
				int k;
				for (k = 0; (uint)k < (uint)span.Length && char.IsDigit(span[k]); k++)
				{
				}
				if (k == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(k);
				num += k;
				if (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
				{
					UncaptureUntil(0);
					return false;
				}
				runtextpos = num;
				Capture(0, start, num);
				return true;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int capturePosition)
				{
					while (Crawlpos() > capturePosition)
					{
						Uncapture();
					}
				}
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__StageLine_0 Instance = new _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__StageLine_0();

	private _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__StageLine_0()
	{
		pattern = "^(?<time>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z) PID=(?<pid>\\d+) TID=\\d+ Stage=(?<stage>[a-z.]+) Error=\\d+$";
		roptions = RegexOptions.CultureInvariant;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		base.CapNames = new Hashtable
		{
			{ "0", 0 },
			{ "pid", 2 },
			{ "stage", 3 },
			{ "time", 1 }
		};
		capslist = new string[4] { "0", "time", "pid", "stage" };
		capsize = 4;
	}
}
