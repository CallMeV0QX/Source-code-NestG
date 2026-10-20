using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace UI_Desktop.Store;

internal static class StoreImageRenderHelper
{
	internal static Bitmap RenderToBitmap(Image source, Size targetSize, StoreImageResizeMode resizeMode, string objectPosition = "center center", Color? backgroundColor = null)
	{
		int width = Math.Max(1, targetSize.Width);
		int height = Math.Max(1, targetSize.Height);
		Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.CompositingMode = CompositingMode.SourceOver;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		graphics.Clear(backgroundColor ?? Color.Transparent);
		Rectangle rectangle = new Rectangle(0, 0, width, height);
		switch (resizeMode)
		{
		case StoreImageResizeMode.Stretch:
			graphics.DrawImage(source, rectangle);
			break;
		case StoreImageResizeMode.Cover:
		case StoreImageResizeMode.Contain:
		{
			RectangleF rect = CalculateDestinationRectangle(source, rectangle, resizeMode == StoreImageResizeMode.Cover, objectPosition);
			graphics.DrawImage(source, rect);
			break;
		}
		}
		return bitmap;
	}

	internal static RectangleF CalculateDestinationRectangle(Image image, Rectangle bounds, bool cover, string objectPosition)
	{
		float val = (float)bounds.Width / (float)image.Width;
		float val2 = (float)bounds.Height / (float)image.Height;
		float num = (cover ? Math.Max(val, val2) : Math.Min(val, val2));
		if (num <= 0f)
		{
			return bounds;
		}
		float num2 = (float)image.Width * num;
		float num3 = (float)image.Height * num;
		(float Horizontal, float Vertical) tuple = ParseObjectPosition(objectPosition);
		float item = tuple.Horizontal;
		float item2 = tuple.Vertical;
		float x = (float)bounds.Left + ((float)bounds.Width - num2) * item;
		float y = (float)bounds.Top + ((float)bounds.Height - num3) * item2;
		return new RectangleF(x, y, num2, num3);
	}

	private static (float Horizontal, float Vertical) ParseObjectPosition(string objectPosition)
	{
		if (string.IsNullOrWhiteSpace(objectPosition))
		{
			return (Horizontal: 0.5f, Vertical: 0.5f);
		}
		string[] array = objectPosition.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (array.Length == 1)
		{
			return (Horizontal: ParseAnchor(array[0], 0.5f, horizontal: true), Vertical: 0.5f);
		}
		return (Horizontal: ParseAnchor(array[0], 0.5f, horizontal: true), Vertical: ParseAnchor(array[1], 0.5f, horizontal: false));
	}

	private static float ParseAnchor(string token, float fallback, bool horizontal)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return fallback;
		}
		string text = token.Trim().ToLowerInvariant();
		switch (text)
		{
		case "left":
			if (horizontal)
			{
				return 0f;
			}
			break;
		case "center":
			return 0.5f;
		case "right":
			if (horizontal)
			{
				return 1f;
			}
			break;
		case "top":
			if (!horizontal)
			{
				return 0f;
			}
			break;
		case "bottom":
			if (!horizontal)
			{
				return 1f;
			}
			break;
		}
		if (text.EndsWith("%", StringComparison.Ordinal))
		{
			string text2 = text;
			if (float.TryParse(text2.Substring(0, text2.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return Math.Clamp(result / 100f, 0f, 1f);
			}
		}
		return fallback;
	}
}
