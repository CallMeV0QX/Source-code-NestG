using System;
using System.Drawing;
using System.Linq;

namespace UI_Desktop.Store;

internal readonly record struct StoreGamePalette(Color PrimaryColor, Color SecondaryColor, Color AccentColor)
{
	public static StoreGamePalette FromSeed(string seed)
	{
		int num = Math.Abs(seed.Aggregate(17, (int current, char character) => current * 31 + character) % 360);
		int num2 = (num + 42) % 360;
		return new StoreGamePalette(AccentColor: FromHsl((num + 300) % 360, 0.82, 0.62), PrimaryColor: FromHsl(num, 0.54, 0.36), SecondaryColor: FromHsl(num2, 0.68, 0.52));
	}

	private static Color FromHsl(double hue, double saturation, double lightness)
	{
		hue %= 360.0;
		hue /= 360.0;
		double num = ((lightness < 0.5) ? (lightness * (1.0 + saturation)) : (lightness + saturation - lightness * saturation));
		double p = 2.0 * lightness - num;
		double num2 = HueToRgb(p, num, hue + 1.0 / 3.0);
		double num3 = HueToRgb(p, num, hue);
		double num4 = HueToRgb(p, num, hue - 1.0 / 3.0);
		return Color.FromArgb((int)Math.Round(num2 * 255.0), (int)Math.Round(num3 * 255.0), (int)Math.Round(num4 * 255.0));
	}

	private static double HueToRgb(double p, double q, double t)
	{
		if (t < 0.0)
		{
			t += 1.0;
		}
		if (t > 1.0)
		{
			t -= 1.0;
		}
		if (t < 1.0 / 6.0)
		{
			return p + (q - p) * 6.0 * t;
		}
		if (t < 0.5)
		{
			return q;
		}
		if (t < 2.0 / 3.0)
		{
			return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
		}
		return p;
	}
}
