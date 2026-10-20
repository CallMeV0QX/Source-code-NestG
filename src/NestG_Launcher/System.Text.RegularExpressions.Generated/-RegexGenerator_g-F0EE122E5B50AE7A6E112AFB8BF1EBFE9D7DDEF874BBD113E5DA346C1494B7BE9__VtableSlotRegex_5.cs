using System.CodeDom.Compiler;
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
internal sealed class _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__VtableSlotRegex_5 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				while (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan) && runtextpos != inputSpan.Length)
				{
					runtextpos++;
					if (_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 37)
				{
					int num2 = inputSpan.Slice(num).IndexOf("[vthoo", StringComparison.OrdinalIgnoreCase);
					if (num2 >= 0)
					{
						runtextpos = num + num2;
						return true;
					}
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
				int num5 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				char c;
				if ((uint)span.Length < 8u || !span.StartsWith("[vthoo", StringComparison.OrdinalIgnoreCase) || (((c = span[6]) | 0x20) != 107 && c != 'K') || span[7] != ']')
				{
					UncaptureUntil(0);
					return false;
				}
				num += 8;
				span = inputSpan.Slice(num);
				int i;
				for (i = 0; (uint)i < (uint)span.Length && char.IsWhiteSpace(span[i]); i++)
				{
				}
				if (i == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(i);
				num += i;
				if ((uint)span.Length < 12u || !span.StartsWith("vtable slot ", StringComparison.OrdinalIgnoreCase))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 12;
				span = inputSpan.Slice(num);
				num2 = num;
				uint num6;
				if (span.IsEmpty || (int)((uint)(-671088640 << (int)(short)(num6 = (ushort)(span[0] - 52))) & (num6 - 32)) >= 0)
				{
					UncaptureUntil(0);
					return false;
				}
				num++;
				span = inputSpan.Slice(num);
				Capture(1, num2, num);
				if ((uint)span.Length < 9u || !span.StartsWith(" patched:", StringComparison.OrdinalIgnoreCase))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 9;
				span = inputSpan.Slice(num);
				num4 = num;
				int num7 = span.IndexOf('\n');
				if (num7 < 0)
				{
					num7 = span.Length;
				}
				span = span.Slice(num7);
				num += num7;
				num5 = num;
				while (true)
				{
					num3 = Crawlpos();
					if ((uint)span.Length >= 6u && span.StartsWith("(o", StringComparison.OrdinalIgnoreCase) && !(((c = span[2]) | 0x20) != 107 && c != 'K') && span.Slice(3).StartsWith("=1)"))
					{
						break;
					}
					UncaptureUntil(num3);
					if (_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num4 >= num5 || (num5 = inputSpan.Slice(num4, num5 - num4).LastIndexOf('(')) < 0)
					{
						UncaptureUntil(0);
						return false;
					}
					num5 += num4;
					num = num5;
					span = inputSpan.Slice(num);
				}
				Capture(0, start, runtextpos = num + 6);
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

	internal static readonly _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__VtableSlotRegex_5 Instance = new _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__VtableSlotRegex_5();

	private _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__VtableSlotRegex_5()
	{
		pattern = "\\[VtHook\\]\\s+Vtable slot (?<slot>4|5|7|8) patched:.*\\(ok=1\\)";
		roptions = RegexOptions.IgnoreCase;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		base.CapNames = new Hashtable
		{
			{ "0", 0 },
			{ "slot", 1 }
		};
		capslist = new string[2] { "0", "slot" };
		capsize = 2;
	}
}
