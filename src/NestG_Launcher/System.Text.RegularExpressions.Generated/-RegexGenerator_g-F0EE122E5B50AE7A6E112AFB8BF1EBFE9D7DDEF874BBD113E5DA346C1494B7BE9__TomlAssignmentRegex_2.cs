using System.CodeDom.Compiler;
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
internal sealed class _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlAssignmentRegex_2 : Regex
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
				if (num <= inputSpan.Length - 2 && num == 0)
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
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				int num10 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if (num != 0)
				{
					UncaptureUntil(0);
					return false;
				}
				num2 = num;
				int i;
				for (i = 0; (uint)i < (uint)span.Length && char.IsWhiteSpace(span[i]); i++)
				{
				}
				span = span.Slice(i);
				num += i;
				Capture(1, num2, num);
				num3 = num;
				int num11 = span.IndexOfAnyExcept(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_ascii_20FF03FEFFFF87FEFFFF07);
				if (num11 < 0)
				{
					num11 = span.Length;
				}
				if (num11 == 0)
				{
					UncaptureUntil(0);
					return false;
				}
				span = span.Slice(num11);
				num += num11;
				Capture(2, num3, num);
				int j;
				for (j = 0; (uint)j < (uint)span.Length && char.IsWhiteSpace(span[j]); j++)
				{
				}
				span = span.Slice(j);
				num += j;
				if (span.IsEmpty || span[0] != '=')
				{
					UncaptureUntil(0);
					return false;
				}
				num++;
				span = inputSpan.Slice(num);
				num7 = num;
				int k;
				for (k = 0; (uint)k < (uint)span.Length && char.IsWhiteSpace(span[k]); k++)
				{
				}
				span = span.Slice(k);
				num += k;
				num8 = num;
				while (true)
				{
					num5 = Crawlpos();
					num4 = num;
					num9 = num;
					int num12 = span.IndexOf('\n');
					if (num12 < 0)
					{
						num12 = span.Length;
					}
					span = span.Slice(num12);
					num += num12;
					num10 = num;
					while (true)
					{
						num6 = Crawlpos();
						Capture(3, num4, num);
						if (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
						{
							UncaptureUntil(num6);
							if (_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_hasTimeout)
							{
								CheckTimeout();
							}
							if (num9 >= num10)
							{
								break;
							}
							num = --num10;
							span = inputSpan.Slice(num);
							continue;
						}
						runtextpos = num;
						Capture(0, start, num);
						return true;
					}
					UncaptureUntil(num5);
					if (_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num7 >= num8)
					{
						break;
					}
					num = --num8;
					span = inputSpan.Slice(num);
				}
				UncaptureUntil(0);
				return false;
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

	internal static readonly _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlAssignmentRegex_2 Instance = new _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlAssignmentRegex_2();

	private _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlAssignmentRegex_2()
	{
		pattern = "^(?<indent>\\s*)(?<key>[A-Za-z0-9_-]+)\\s*=\\s*(?<value>.*)$";
		roptions = RegexOptions.None;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		base.CapNames = new Hashtable
		{
			{ "0", 0 },
			{ "indent", 1 },
			{ "key", 2 },
			{ "value", 3 }
		};
		capslist = new string[4] { "0", "indent", "key", "value" };
		capsize = 4;
	}
}
