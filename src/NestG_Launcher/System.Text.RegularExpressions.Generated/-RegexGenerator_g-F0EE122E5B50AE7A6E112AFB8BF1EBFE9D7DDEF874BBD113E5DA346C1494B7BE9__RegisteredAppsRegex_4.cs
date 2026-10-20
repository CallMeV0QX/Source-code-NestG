using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
internal sealed class _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__RegisteredAppsRegex_4 : Regex
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
				if (num <= inputSpan.Length - 26)
				{
					int num2 = inputSpan.Slice(num).IndexOf("[cr_api]", StringComparison.OrdinalIgnoreCase);
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
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if ((uint)span.Length < 8u || !span.StartsWith("[cr_api]", StringComparison.OrdinalIgnoreCase))
				{
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
					return false;
				}
				span = span.Slice(i);
				num += i;
				if ((uint)span.Length < 8u || !span.StartsWith("setapps:", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				num += 8;
				span = inputSpan.Slice(num);
				int j;
				for (j = 0; (uint)j < (uint)span.Length && char.IsWhiteSpace(span[j]); j++)
				{
				}
				if (j == 0)
				{
					return false;
				}
				span = span.Slice(j);
				num += j;
				if (span.IsEmpty || !char.IsBetween(span[0], '1', '9'))
				{
					return false;
				}
				int num2 = span.Slice(1).IndexOfAnyExceptInRange('0', '9');
				if (num2 < 0)
				{
					num2 = span.Length - 1;
				}
				span = span.Slice(num2);
				num += num2;
				num++;
				span = inputSpan.Slice(num);
				int k;
				for (k = 0; (uint)k < (uint)span.Length && char.IsWhiteSpace(span[k]); k++)
				{
				}
				if (k == 0)
				{
					return false;
				}
				span = span.Slice(k);
				num += k;
				if ((uint)span.Length < 6u || !span.StartsWith("app(s)", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				Capture(0, start, runtextpos = num + 6);
				return true;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__RegisteredAppsRegex_4 Instance = new _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__RegisteredAppsRegex_4();

	private _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__RegisteredAppsRegex_4()
	{
		pattern = "\\[CR_API\\]\\s+SetApps:\\s+[1-9][0-9]*\\s+app\\(s\\)";
		roptions = RegexOptions.IgnoreCase;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
