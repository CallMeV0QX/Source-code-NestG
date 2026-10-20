using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace UI_Desktop.Store;

internal static class StoreArtworkFactory
{
	internal static Bitmap CreateBanner(StoreGame game, Size size)
	{
		return CreateBannerArtwork(game, size);
	}

	internal static Bitmap CreateCover(StoreGame game, Size size)
	{
		return CreateArtwork(game, size, 18f, 8.5f, 11f, isBanner: false);
	}

	private static Bitmap CreateArtwork(StoreGame game, Size size, float titleSize, float subtitleSize, float chipSize, bool isBanner)
	{
		int num = Math.Max(120, size.Width);
		int num2 = Math.Max(120, size.Height);
		Bitmap bitmap = new Bitmap(num, num2);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		graphics.Clear(Color.FromArgb(18, 23, 30));
		using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, num, num2), game.PrimaryColor, game.SecondaryColor, LinearGradientMode.ForwardDiagonal);
		graphics.FillRectangle(brush, 0, 0, num, num2);
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(70, 8, 12, 18));
		graphics.FillRectangle(brush2, 0, 0, num, num2);
		DrawOrbs(graphics, num, num2, game);
		DrawDiagonalSlices(graphics, num, num2, game);
		DrawFraming(graphics, num, num2);
		using Font font = new Font("Segoe UI Semibold", chipSize, FontStyle.Bold, GraphicsUnit.Point);
		using Font font2 = new Font("Segoe UI Semibold", titleSize, FontStyle.Bold, GraphicsUnit.Point);
		using Font font3 = new Font("Segoe UI", subtitleSize, FontStyle.Regular, GraphicsUnit.Point);
		using SolidBrush brush4 = new SolidBrush(Color.FromArgb(220, 240, 248, 255));
		using SolidBrush brush6 = new SolidBrush(Color.White);
		using SolidBrush brush7 = new SolidBrush(Color.FromArgb(222, 230, 238));
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(165, game.AccentColor));
		using Pen pen = new Pen(Color.FromArgb(42, 255, 255, 255));
		RectangleF rectangleF = new RectangleF(18f, 16f, Math.Min(num - 36, (float)num * 0.28f), isBanner ? 34 : 26);
		using (GraphicsPath path = RoundedPanel.CreateRoundedPath(Rectangle.Round(rectangleF), 14))
		{
			graphics.FillPath(brush3, path);
			graphics.DrawPath(pen, path);
		}
		graphics.DrawString(game.PromoLabel, font, brush4, rectangleF, new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		});
		RectangleF rectangleF2 = (isBanner ? new RectangleF(24f, (float)num2 - (float)num2 * 0.32f, (float)num * 0.62f, (float)num2 * 0.22f) : new RectangleF(18f, (float)num2 - (float)num2 * 0.33f, (float)num * 0.72f, (float)num2 * 0.22f));
		RectangleF layoutRectangle = (isBanner ? new RectangleF(24f, rectangleF2.Bottom + 8f, (float)num * 0.45f, 24f) : new RectangleF(18f, rectangleF2.Bottom + 6f, (float)num * 0.58f, 18f));
		using SolidBrush brush5 = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
		int num3 = (isBanner ? 3 : 2);
		RectangleF rectangleF3 = rectangleF2;
		rectangleF3.X = rectangleF2.X + (float)num3;
		rectangleF3.Y = rectangleF2.Y + (float)num3;
		RectangleF layoutRectangle2 = rectangleF3;
		graphics.DrawString(game.Title, font2, brush5, layoutRectangle2);
		graphics.DrawString(game.Title, font2, brush6, rectangleF2);
		graphics.DrawString(game.Subtitle, font3, brush7, layoutRectangle);
		if (isBanner)
		{
			using Pen pen2 = new Pen(Color.FromArgb(70, 255, 255, 255), 2f);
			graphics.DrawLine(pen2, (float)num * 0.62f, 30f, num - 24, num2 - 30);
		}
		return bitmap;
	}

	private static Bitmap CreateBannerArtwork(StoreGame game, Size size)
	{
		int num = Math.Max(260, size.Width);
		int num2 = Math.Max(220, size.Height);
		Bitmap bitmap = new Bitmap(num, num2);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		Rectangle rect = new Rectangle(0, 0, num, num2);
		graphics.Clear(Color.FromArgb(10, 14, 20));
		using LinearGradientBrush linearGradientBrush = new LinearGradientBrush(rect, Color.Black, Color.Black, 0f);
		linearGradientBrush.InterpolationColors = new ColorBlend
		{
			Positions = new float[4] { 0f, 0.38f, 0.74f, 1f },
			Colors = new Color[4]
			{
				Darken(Blend(game.PrimaryColor, game.AccentColor, 0.14f), 0.42f),
				Blend(game.PrimaryColor, game.SecondaryColor, 0.24f),
				Blend(game.SecondaryColor, game.AccentColor, 0.2f),
				Darken(game.SecondaryColor, 0.4f)
			}
		};
		graphics.FillRectangle(linearGradientBrush, rect);
		using LinearGradientBrush brush = new LinearGradientBrush(new PointF(0f, 0f), new PointF(0f, num2), Color.FromArgb(26, 255, 255, 255), Color.FromArgb(132, 5, 8, 14));
		graphics.FillRectangle(brush, rect);
		DrawGlow(graphics, new RectangleF((float)(-num) * 0.08f, (float)num2 * 0.54f, (float)num * 0.34f, (float)num2 * 0.44f), Blend(game.PrimaryColor, game.AccentColor, 0.35f), 88);
		DrawGlow(graphics, new RectangleF((float)num * 0.58f, (float)(-num2) * 0.16f, (float)num * 0.28f, (float)num2 * 0.34f), Blend(game.SecondaryColor, Color.White, 0.1f), 74);
		int num3 = Math.Max(220, (int)((float)num * 0.44f));
		Rectangle bounds = new Rectangle(0, 0, num3, num2);
		Rectangle bounds2 = new Rectangle(num3, 0, num - num3, num2);
		DrawFeaturedPoster(graphics, bounds, game);
		DrawFeaturedMosaic(graphics, bounds2, game);
		DrawFeaturedEyebrow(graphics, num, game);
		DrawFraming(graphics, num, num2);
		using Pen pen = new Pen(Color.FromArgb(82, 255, 255, 255), 1.1f);
		graphics.DrawLine(pen, bounds.Right - 1, 0, bounds.Right - 1, num2);
		return bitmap;
	}

	private static void DrawFeaturedPoster(Graphics graphics, Rectangle bounds, StoreGame game)
	{
		using LinearGradientBrush brush = new LinearGradientBrush(bounds, Blend(game.PrimaryColor, Color.White, 0.08f), Darken(game.PrimaryColor, 0.16f), LinearGradientMode.Vertical);
		graphics.FillRectangle(brush, bounds);
		using LinearGradientBrush brush2 = new LinearGradientBrush(new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Bottom), Color.FromArgb(30, 255, 255, 255), Color.FromArgb(110, 9, 12, 18));
		graphics.FillRectangle(brush2, bounds);
		DrawGlow(graphics, new RectangleF((float)bounds.X - (float)bounds.Width * 0.08f, (float)bounds.Y + (float)bounds.Height * 0.56f, (float)bounds.Width * 0.54f, (float)bounds.Height * 0.34f), Blend(game.PrimaryColor, game.AccentColor, 0.42f), 96);
		DrawGlow(graphics, new RectangleF((float)bounds.X + (float)bounds.Width * 0.28f, (float)bounds.Y + (float)bounds.Height * 0.26f, (float)bounds.Width * 0.42f, (float)bounds.Height * 0.34f), Blend(game.AccentColor, Color.White, 0.12f), 82);
		DrawPosterSweep(graphics, bounds, Color.FromArgb(78, 255, 255, 255));
		DrawPosterRings(graphics, bounds, game);
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
		using SolidBrush brush4 = new SolidBrush(Color.White);
		using SolidBrush brush5 = new SolidBrush(Color.FromArgb(228, 236, 242));
		using Font font = new Font("Segoe UI Semibold", Math.Max(22f, Math.Min(34f, (float)bounds.Width * 0.078f)), FontStyle.Bold, GraphicsUnit.Point);
		using Font font2 = new Font("Segoe UI", Math.Max(10.5f, Math.Min(13f, (float)bounds.Width * 0.026f)), FontStyle.Regular, GraphicsUnit.Point);
		using StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisWord
		};
		RectangleF rectangleF = new RectangleF((float)bounds.X + (float)bounds.Width * 0.11f, (float)bounds.Y + (float)bounds.Height * 0.28f, (float)bounds.Width * 0.62f, (float)bounds.Height * 0.42f);
		RectangleF rectangleF2 = rectangleF;
		rectangleF2.X = rectangleF.X + 4f;
		rectangleF2.Y = rectangleF.Y + 4f;
		RectangleF layoutRectangle = rectangleF2;
		graphics.DrawString(game.Title, font, brush3, layoutRectangle, format);
		graphics.DrawString(game.Title, font, brush4, rectangleF, format);
		RectangleF layoutRectangle2 = new RectangleF(bounds.X + 28, bounds.Bottom - 44, (float)bounds.Width * 0.56f, 24f);
		graphics.DrawString(game.Subtitle, font2, brush5, layoutRectangle2);
	}

	private static void DrawFeaturedMosaic(Graphics graphics, Rectangle bounds, StoreGame game)
	{
		Color[] array = new Color[6]
		{
			Blend(game.PrimaryColor, Color.White, 0.18f),
			Blend(game.SecondaryColor, Color.White, 0.14f),
			Blend(game.AccentColor, Color.White, 0.12f),
			Darken(game.PrimaryColor, 0.06f),
			Darken(game.SecondaryColor, 0.02f),
			Blend(game.AccentColor, game.SecondaryColor, 0.45f)
		};
		LinearGradientMode[] array2 = new LinearGradientMode[6]
		{
			LinearGradientMode.ForwardDiagonal,
			LinearGradientMode.BackwardDiagonal,
			LinearGradientMode.Vertical,
			LinearGradientMode.Horizontal,
			LinearGradientMode.ForwardDiagonal,
			LinearGradientMode.BackwardDiagonal
		};
		int num = (bounds.Width - 2) / 2;
		int num2 = (bounds.Height - 4) / 3;
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 2; j++)
			{
				int num3 = i * 2 + j;
				Rectangle rectangle = new Rectangle(bounds.X + j * (num + 2), bounds.Y + i * (num2 + 2), num, num2);
				using LinearGradientBrush brush = new LinearGradientBrush(rectangle, array[num3], Blend(array[(num3 + 1) % array.Length], Color.Black, 0.18f), array2[num3]);
				graphics.FillRectangle(brush, rectangle);
				using LinearGradientBrush brush2 = new LinearGradientBrush(new Point(rectangle.Left, rectangle.Top), new Point(rectangle.Left, rectangle.Bottom), Color.FromArgb(12, 255, 255, 255), Color.FromArgb(118, 7, 10, 16));
				graphics.FillRectangle(brush2, rectangle);
				DrawFeaturedTileAccent(graphics, rectangle, game, num3);
			}
		}
	}

	private static void DrawFeaturedTileAccent(Graphics graphics, Rectangle bounds, StoreGame game, int index)
	{
		Color color = Blend(game.AccentColor, Color.White, 0.14f);
		Color baseColor = Color.FromArgb(118, 255, 255, 255);
		switch (index)
		{
		case 0:
		{
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(52, baseColor));
			graphics.FillPolygon(brush2, new PointF[4]
			{
				new PointF(bounds.Left, bounds.Bottom),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.56f, bounds.Top),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.82f, bounds.Top),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.18f, bounds.Bottom)
			});
			DrawGlow(graphics, new RectangleF((float)bounds.Right - (float)bounds.Width * 0.42f, bounds.Top - 10, (float)bounds.Width * 0.36f, (float)bounds.Height * 0.42f), color, 70);
			return;
		}
		case 1:
		{
			using Pen pen3 = new Pen(Color.FromArgb(220, Blend(color, Color.White, 0.2f)), 2.4f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			PointF pointF = new PointF((float)bounds.Left + (float)bounds.Width * 0.66f, (float)bounds.Top + (float)bounds.Height * 0.24f);
			graphics.DrawLine(pen3, pointF.X - 26f, pointF.Y, pointF.X + 26f, pointF.Y);
			graphics.DrawLine(pen3, pointF.X, pointF.Y - 26f, pointF.X, pointF.Y + 26f);
			graphics.DrawLine(pen3, pointF.X - 18f, pointF.Y - 18f, pointF.X + 18f, pointF.Y + 18f);
			graphics.DrawLine(pen3, pointF.X - 18f, pointF.Y + 18f, pointF.X + 18f, pointF.Y - 18f);
			return;
		}
		case 2:
		{
			using Pen pen2 = new Pen(Color.FromArgb(170, color), 3.2f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddBezier(bounds.Left - 10, bounds.Bottom - 12, (float)bounds.Left + (float)bounds.Width * 0.24f, (float)bounds.Top + (float)bounds.Height * 0.28f, (float)bounds.Left + (float)bounds.Width * 0.56f, bounds.Bottom - 6, bounds.Right + 8, bounds.Top + 10);
			graphics.DrawPath(pen2, graphicsPath);
			return;
		}
		case 3:
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb(80, baseColor));
			graphics.FillPolygon(brush, new PointF[4]
			{
				new PointF(bounds.Left, (float)bounds.Top + (float)bounds.Height * 0.68f),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.34f, bounds.Top),
				new PointF(bounds.Right, bounds.Top),
				new PointF((float)bounds.Right - (float)bounds.Width * 0.28f, bounds.Bottom)
			});
			return;
		}
		case 4:
		{
			using Pen pen = new Pen(Color.FromArgb(210, Blend(color, Color.White, 0.16f)), 3.2f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			graphics.DrawLines(pen, new PointF[4]
			{
				new PointF((float)bounds.Left + (float)bounds.Width * 0.48f, bounds.Top + 10),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.4f, (float)bounds.Top + (float)bounds.Height * 0.42f),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.54f, (float)bounds.Top + (float)bounds.Height * 0.42f),
				new PointF((float)bounds.Left + (float)bounds.Width * 0.34f, bounds.Bottom - 10)
			});
			return;
		}
		}
		using Pen pen4 = new Pen(Color.FromArgb(156, color), 2f);
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(128, baseColor));
		RectangleF rect = new RectangleF((float)bounds.Left + (float)bounds.Width * 0.32f, (float)bounds.Top + (float)bounds.Height * 0.3f, (float)bounds.Width * 0.34f, (float)bounds.Height * 0.34f);
		graphics.DrawEllipse(pen4, rect);
		graphics.FillEllipse(brush3, rect.X + rect.Width * 0.33f, rect.Y + rect.Height * 0.33f, rect.Width * 0.34f, rect.Height * 0.34f);
	}

	private static void DrawFeaturedEyebrow(Graphics graphics, int width, StoreGame game)
	{
		using Font font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point);
		using Font font2 = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(116, 68, 115, 196));
		using Pen pen = new Pen(Color.FromArgb(142, 129, 185, 255), 1f);
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(236, 246, 252));
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(236, 240, 244));
		Rectangle rectangle = new Rectangle(22, 18, Math.Min(96, Math.Max(82, (int)((float)width * 0.105f))), 32);
		using (GraphicsPath path = RoundedPanel.CreateRoundedPath(rectangle, 16))
		{
			graphics.FillPath(brush, path);
			graphics.DrawPath(pen, path);
		}
		graphics.DrawString("SALE NOW", font, brush2, rectangle, new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		});
		RectangleF layoutRectangle = new RectangleF(rectangle.Right + 14, rectangle.Top + 3, (float)width * 0.22f, rectangle.Height);
		graphics.DrawString("Ưu đãi nổi bật", font2, brush3, layoutRectangle);
	}

	private static void DrawPosterSweep(Graphics graphics, Rectangle bounds, Color color)
	{
		GraphicsState gstate = graphics.Save();
		graphics.TranslateTransform((float)bounds.Left + (float)bounds.Width * 0.1f, (float)bounds.Top + (float)bounds.Height * 0.08f);
		graphics.RotateTransform(-18f);
		Rectangle bounds2 = new Rectangle(0, 0, (int)((float)bounds.Width * 0.78f), (int)((float)bounds.Height * 0.12f));
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(bounds2, Math.Max(18, bounds2.Height / 2));
		using LinearGradientBrush brush = new LinearGradientBrush(new Point(0, 0), new Point(0, bounds2.Height), color, Color.FromArgb(0, color));
		graphics.FillPath(brush, path);
		graphics.Restore(gstate);
	}

	private static void DrawPosterRings(Graphics graphics, Rectangle bounds, StoreGame game)
	{
		using Pen pen = new Pen(Color.FromArgb(224, Blend(game.AccentColor, Color.Orange, 0.34f)), Math.Max(4.2f, (float)bounds.Width * 0.014f))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		using Pen pen2 = new Pen(Color.FromArgb(108, 255, 255, 255), Math.Max(1.4f, (float)bounds.Width * 0.004f));
		RectangleF rect = new RectangleF((float)bounds.Left + (float)bounds.Width * 0.14f, (float)bounds.Top + (float)bounds.Height * 0.23f, (float)bounds.Width * 0.42f, (float)bounds.Width * 0.42f);
		RectangleF rect2 = new RectangleF(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.12f, rect.Width * 0.76f, rect.Height * 0.76f);
		graphics.DrawArc(pen, rect, 196f, 284f);
		graphics.DrawArc(pen2, rect2, 204f, 268f);
	}

	private static void DrawOrbs(Graphics graphics, int width, int height, StoreGame game)
	{
		using SolidBrush brush = new SolidBrush(Color.FromArgb(70, game.AccentColor));
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(50, game.SecondaryColor));
		graphics.FillEllipse(brush, (float)(-width) * 0.12f, (float)height * 0.56f, (float)width * 0.42f, (float)height * 0.58f);
		graphics.FillEllipse(brush2, (float)width * 0.62f, (float)(-height) * 0.1f, (float)width * 0.34f, (float)height * 0.44f);
	}

	private static void DrawDiagonalSlices(Graphics graphics, int width, int height, StoreGame game)
	{
		using SolidBrush brush = new SolidBrush(Color.FromArgb(38, 255, 255, 255));
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(65, game.AccentColor));
		PointF[] points = new PointF[4]
		{
			new PointF(0f, (float)height * 0.18f),
			new PointF((float)width * 0.22f, 0f),
			new PointF((float)width * 0.42f, 0f),
			new PointF((float)width * 0.06f, (float)height * 0.46f)
		};
		PointF[] points2 = new PointF[4]
		{
			new PointF((float)width * 0.64f, height),
			new PointF(width, (float)height * 0.58f),
			new PointF(width, (float)height * 0.84f),
			new PointF((float)width * 0.82f, height)
		};
		graphics.FillPolygon(brush, points);
		graphics.FillPolygon(brush2, points2);
	}

	private static void DrawFraming(Graphics graphics, int width, int height)
	{
		using Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.2f);
		graphics.DrawRectangle(pen, 0, 0, width - 1, height - 1);
	}

	private static void DrawGlow(Graphics graphics, RectangleF bounds, Color color, int alpha)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(bounds);
		PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = Color.FromArgb(alpha, color);
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, color) };
		using PathGradientBrush brush = pathGradientBrush;
		graphics.FillPath(brush, graphicsPath);
	}

	private static Color Darken(Color color, float amount)
	{
		amount = Math.Clamp(amount, 0f, 1f);
		return Color.FromArgb(color.A, (int)((float)(int)color.R * (1f - amount)), (int)((float)(int)color.G * (1f - amount)), (int)((float)(int)color.B * (1f - amount)));
	}

	private static Color Blend(Color from, Color to, float amount)
	{
		amount = Math.Clamp(amount, 0f, 1f);
		float num = 1f - amount;
		return Color.FromArgb((int)((float)(int)from.R * num + (float)(int)to.R * amount), (int)((float)(int)from.G * num + (float)(int)to.G * amount), (int)((float)(int)from.B * num + (float)(int)to.B * amount));
	}
}
