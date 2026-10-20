using System.Buffers;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
internal static class _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Utilities
{
	internal static readonly TimeSpan s_defaultTimeout = ((AppContext.GetData("REGEX_DEFAULT_MATCH_TIMEOUT") is TimeSpan timeSpan) ? timeSpan : Regex.InfiniteMatchTimeout);

	internal static readonly bool s_hasTimeout = s_defaultTimeout != Regex.InfiniteMatchTimeout;

	internal static readonly SearchValues<char> s_asciiHexDigits = SearchValues.Create("0123456789ABCDEFabcdef");

	internal static readonly SearchValues<char> s_ascii_20FF03FEFFFF87FEFFFF07 = SearchValues.Create("-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

	internal static readonly SearchValues<char> s_ascii_40000000000000FEFFFF07 = SearchValues.Create(".abcdefghijklmnopqrstuvwxyz");

	internal static readonly SearchValues<char> s_ascii_60FF03FEFFFF87FEFFFF07 = SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void StackPop(int[] stack, ref int pos, out int arg0, out int arg1)
	{
		arg0 = stack[--pos];
		arg1 = stack[--pos];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void StackPush(ref int[] stack, ref int pos, int arg0, int arg1)
	{
		int[] array = stack;
		int num = pos;
		if ((uint)(num + 1) < (uint)array.Length)
		{
			array[num] = arg0;
			array[num + 1] = arg1;
			pos += 2;
		}
		else
		{
			WithResize(ref stack, ref pos, arg0, arg1);
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WithResize(ref int[] stack, ref int pos, int arg0, int arg1)
		{
			Array.Resize(ref stack, (pos + 1) * 2);
			StackPush(ref stack, ref pos, arg0, arg1);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void StackPush(ref int[] stack, ref int pos, int arg0, int arg1, int arg2)
	{
		int[] array = stack;
		int num = pos;
		if ((uint)(num + 2) < (uint)array.Length)
		{
			array[num] = arg0;
			array[num + 1] = arg1;
			array[num + 2] = arg2;
			pos += 3;
		}
		else
		{
			WithResize(ref stack, ref pos, arg0, arg1, arg2);
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WithResize(ref int[] stack, ref int pos, int arg0, int arg1, int arg2)
		{
			Array.Resize(ref stack, (pos + 2) * 2);
			StackPush(ref stack, ref pos, arg0, arg1, arg2);
		}
	}
}
