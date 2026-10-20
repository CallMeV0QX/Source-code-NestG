using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace UI_Desktop.Store;

internal readonly record struct StoreSpotlightPalette(Color PanelSurface, Color PanelBorder, Color InfoSurface, Color MediaBackdrop, Color PageAmbient)
{
	public static StoreSpotlightPalette Default => new StoreSpotlightPalette(Color.FromArgb(14, 19, 28), Color.FromArgb(49, 70, 98), Color.FromArgb(14, 20, 29), Color.FromArgb(10, 15, 22), AppTheme.WindowBackground);

	public static StoreSpotlightPalette FromGame(StoreGame game)
	{
		Color panelSurface = Blend(Darken(game.PrimaryColor, 0.72f), Color.FromArgb(14, 19, 28), 0.55f);
		Color infoSurface = Blend(Darken(game.SecondaryColor, 0.74f), Color.FromArgb(14, 20, 29), 0.58f);
		Color mediaBackdrop = Blend(Darken(game.PrimaryColor, 0.56f), game.AccentColor, 0.14f);
		Color panelBorder = Blend(Lighten(game.AccentColor, 0.18f), Color.FromArgb(49, 70, 98), 0.32f);
		Color pageAmbient = Blend(AppTheme.WindowBackground, Darken(game.PrimaryColor, 0.68f), 0.22f);
		return new StoreSpotlightPalette(panelSurface, panelBorder, infoSurface, mediaBackdrop, pageAmbient);
	}

	public static StoreSpotlightPalette Extract(Image image, StoreGame fallbackGame)
	{
		StoreSpotlightPalette result = FromGame(fallbackGame);
		try
		{
			using Bitmap bitmap = new Bitmap(28, 28);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
				graphics.DrawImage(image, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
			}
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			double num6 = 0.0;
			double num7 = 0.0;
			double num8 = 0.0;
			double num9 = double.MinValue;
			Color first = fallbackGame.AccentColor;
			for (int i = 0; i < bitmap.Height; i++)
			{
				for (int j = 0; j < bitmap.Width; j++)
				{
					Color pixel = bitmap.GetPixel(j, i);
					if (pixel.A >= 16)
					{
						double num10 = (double)(int)pixel.A / 255.0;
						num += num10;
						num2 += (double)(int)pixel.R * num10;
						num3 += (double)(int)pixel.G * num10;
						num4 += (double)(int)pixel.B * num10;
						double num11 = (0.2126 * (double)(int)pixel.R + 0.7152 * (double)(int)pixel.G + 0.0722 * (double)(int)pixel.B) / 255.0;
						if (num11 <= 0.42)
						{
							num5 += num10;
							num6 += (double)(int)pixel.R * num10;
							num7 += (double)(int)pixel.G * num10;
							num8 += (double)(int)pixel.B * num10;
						}
						double num12 = (double)(int)Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255.0;
						double num13 = (double)(int)Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) / 255.0;
						double num14 = ((num12 <= 0.001) ? 0.0 : ((num12 - num13) / num12)) * 1.8 + (1.0 - Math.Abs(num11 - 0.52));
						if (num14 > num9)
						{
							num9 = num14;
							first = pixel;
						}
					}
				}
			}
			if (num <= 0.001)
			{
				return result;
			}
			Color color = Color.FromArgb(255, ClampColor(num2 / num), ClampColor(num3 / num), ClampColor(num4 / num));
			Color color2 = ((num5 <= 0.001) ? Darken(color, 0.24f) : Color.FromArgb(255, ClampColor(num6 / num5), ClampColor(num7 / num5), ClampColor(num8 / num5)));
			Color color3 = Blend(first, fallbackGame.AccentColor, 0.24f);
			Color panelSurface = Blend(Darken(color2, 0.34f), Color.FromArgb(14, 19, 28), 0.52f);
			Color infoSurface = Blend(Darken(color, 0.6f), Color.FromArgb(14, 20, 29), 0.56f);
			Color mediaBackdrop = Blend(Darken(color2, 0.18f), color3, 0.18f);
			Color panelBorder = Blend(Lighten(color3, 0.2f), Color.FromArgb(49, 70, 98), 0.38f);
			Color pageAmbient = Blend(AppTheme.WindowBackground, Darken(color, 0.62f), 0.2f);
			return new StoreSpotlightPalette(panelSurface, panelBorder, infoSurface, mediaBackdrop, pageAmbient);
		}
		catch
		{
			return result;
		}
	}

	public static StoreSpotlightPalette Lerp(StoreSpotlightPalette from, StoreSpotlightPalette to, float amount)
	{
		return new StoreSpotlightPalette(LerpColor(from.PanelSurface, to.PanelSurface, amount), LerpColor(from.PanelBorder, to.PanelBorder, amount), LerpColor(from.InfoSurface, to.InfoSurface, amount), LerpColor(from.MediaBackdrop, to.MediaBackdrop, amount), LerpColor(from.PageAmbient, to.PageAmbient, amount));
	}

	private static int ClampColor(double value)
	{
		return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
	}

	private static Color LerpColor(Color from, Color to, float amount)
	{
		return Color.FromArgb(255, (int)Math.Round((float)(int)from.R + (float)(to.R - from.R) * amount), (int)Math.Round((float)(int)from.G + (float)(to.G - from.G) * amount), (int)Math.Round((float)(int)from.B + (float)(to.B - from.B) * amount));
	}

	private static Color Blend(Color first, Color second, float secondWeight)
	{
		float num = Math.Clamp(secondWeight, 0f, 1f);
		float num2 = 1f - num;
		return Color.FromArgb(255, (int)Math.Round((float)(int)first.R * num2 + (float)(int)second.R * num), (int)Math.Round((float)(int)first.G * num2 + (float)(int)second.G * num), (int)Math.Round((float)(int)first.B * num2 + (float)(int)second.B * num));
	}

	private static Color Darken(Color color, float amount)
	{
		float num = Math.Clamp(1f - amount, 0f, 1f);
		return Color.FromArgb(255, (int)Math.Round((float)(int)color.R * num), (int)Math.Round((float)(int)color.G * num), (int)Math.Round((float)(int)color.B * num));
	}

	private static Color Lighten(Color color, float amount)
	{
		float num = Math.Clamp(amount, 0f, 1f);
		return Color.FromArgb(255, (int)Math.Round((float)(int)color.R + (float)(255 - color.R) * num), (int)Math.Round((float)(int)color.G + (float)(255 - color.G) * num), (int)Math.Round((float)(int)color.B + (float)(255 - color.B) * num));
	}
}
