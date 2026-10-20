using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreIconButton : Button
{
	private bool isHovered;

	private string badgeText = string.Empty;

	private Image? glyphImage;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public StoreIconKind IconKind { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string BadgeText
	{
		get
		{
			return badgeText;
		}
		set
		{
			badgeText = value ?? string.Empty;
			UpdateInteractiveRegion();
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color SurfaceColor { get; set; } = Color.FromArgb(18, 24, 33);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Image? GlyphImage
	{
		get
		{
			return glyphImage;
		}
		set
		{
			glyphImage = value;
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool UseOriginalGlyphColors { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color HoverSurfaceColor { get; set; } = Color.FromArgb(26, 34, 46);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color OutlineColor { get; set; } = Color.FromArgb(35, 47, 61);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color HoverOutlineColor { get; set; } = Color.FromArgb(50, 66, 84);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool ShowChrome { get; set; } = true;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color GlyphColor { get; set; } = Color.FromArgb(149, 161, 175);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color HoverGlyphColor { get; set; } = Color.FromArgb(220, 228, 236);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float GlyphSize { get; set; } = 12.5f;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int GlyphYOffset { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float GlyphStrokeWidth { get; set; } = 1.3f;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int CornerRadius { get; set; } = 22;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int ChromeWidth { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int BadgeOffsetX { get; set; } = 10;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int BadgeOffsetY { get; set; } = -6;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Size BadgeSize { get; set; } = new Size(20, 20);

	public StoreIconButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.FlatStyle = FlatStyle.Flat;
		base.FlatAppearance.BorderSize = 0;
		base.Margin = new Padding(0, 0, 10, 0);
		base.Size = new Size(42, 42);
		base.TabStop = false;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		isHovered = true;
		Invalidate();
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		isHovered = false;
		Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		UpdateInteractiveRegion();
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(base.Parent?.BackColor ?? AppTheme.WindowBackground);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
		e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
		Rectangle chromeBounds = GetChromeBounds();
		if (ShowChrome)
		{
			using GraphicsPath path = RoundedPanel.CreateRoundedPath(chromeBounds, Math.Min(CornerRadius, Math.Min(chromeBounds.Width, chromeBounds.Height) / 2));
			using SolidBrush brush = new SolidBrush(isHovered ? HoverSurfaceColor : SurfaceColor);
			using Pen pen = new Pen(isHovered ? HoverOutlineColor : OutlineColor, 1f)
			{
				Alignment = PenAlignment.Inset,
				LineJoin = LineJoin.Round
			};
			e.Graphics.FillPath(brush, path);
			e.Graphics.DrawPath(pen, path);
		}
		Color color = (isHovered ? HoverGlyphColor : GlyphColor);
		if (!DrawAssetGlyph(e.Graphics, color, chromeBounds))
		{
			using Pen pen2 = new Pen(color, GlyphStrokeWidth)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			};
			using SolidBrush brush2 = new SolidBrush(color);
			DrawGlyph(e.Graphics, pen2, brush2, chromeBounds);
		}
		DrawBadge(e.Graphics);
	}

	private bool DrawAssetGlyph(Graphics graphics, Color glyphColor, Rectangle chromeRect)
	{
		if (GlyphImage == null)
		{
			return false;
		}
		int num = Math.Max(10, (int)Math.Round(GlyphSize));
		Rectangle rectangle = new Rectangle(chromeRect.X + (chromeRect.Width - num) / 2, chromeRect.Y + (chromeRect.Height - num) / 2 + GlyphYOffset, num, num);
		Rectangle opaqueBounds = GetOpaqueBounds(GlyphImage);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		if (UseOriginalGlyphColors)
		{
			graphics.DrawImage(GlyphImage, rectangle, opaqueBounds.X, opaqueBounds.Y, opaqueBounds.Width, opaqueBounds.Height, GraphicsUnit.Pixel);
		}
		else
		{
			using Bitmap image = CreateRecoloredGlyph(GlyphImage, opaqueBounds, glyphColor);
			graphics.DrawImage(image, rectangle);
		}
		return true;
	}

	private void DrawGlyph(Graphics graphics, Pen pen, Brush brush, Rectangle chromeRect)
	{
		GraphicsState gstate = graphics.Save();
		try
		{
			float num = GlyphSize / 14f;
			graphics.TranslateTransform((float)chromeRect.Left + (float)chromeRect.Width / 2f, (float)chromeRect.Top + (float)chromeRect.Height / 2f + (float)GlyphYOffset);
			graphics.ScaleTransform(num, num);
			switch (IconKind)
			{
			case StoreIconKind.Notification:
				DrawBell(graphics, pen, brush);
				break;
			case StoreIconKind.Cart:
				DrawCart(graphics, pen, brush);
				break;
			default:
				DrawInfo(graphics, pen, brush);
				break;
			}
		}
		finally
		{
			graphics.Restore(gstate);
		}
	}

	private void DrawBadge(Graphics graphics)
	{
		if (string.IsNullOrWhiteSpace(BadgeText))
		{
			return;
		}
		Rectangle badgeBounds = GetBadgeBounds(GetChromeBounds());
		using SolidBrush brush = new SolidBrush(AppTheme.Accent);
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(badgeBounds, Math.Min(11, Math.Min(badgeBounds.Width, badgeBounds.Height) / 2));
		graphics.FillPath(brush, path);
		using Font font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
		TextRenderer.DrawText(graphics, BadgeText, font, badgeBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	private Rectangle GetBadgeBounds(Rectangle chromeRect)
	{
		int num = BadgeSize.Width;
		if (BadgeText.Length > 2)
		{
			using Font font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
			Size size = TextRenderer.MeasureText(BadgeText, font, Size.Empty, TextFormatFlags.NoPadding);
			num = Math.Max(BadgeSize.Width, size.Width + 8);
		}
		int x = Math.Min(chromeRect.Right - BadgeOffsetX, Math.Max(0, base.Width - num));
		int value = chromeRect.Top + BadgeOffsetY;
		value = Math.Clamp(value, 0, Math.Max(0, base.Height - BadgeSize.Height));
		return new Rectangle(x, value, num, BadgeSize.Height);
	}

	private Rectangle GetChromeBounds()
	{
		if (!ShowChrome)
		{
			return new Rectangle(0, 0, base.Width, base.Height);
		}
		int val = ((ChromeWidth > 0) ? Math.Min(ChromeWidth, base.Width - 2) : (base.Width - 3));
		val = Math.Max(base.Height - 3, val);
		return new Rectangle(1, 1, val, base.Height - 3);
	}

	private void UpdateInteractiveRegion()
	{
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		if (!ShowChrome && string.IsNullOrWhiteSpace(BadgeText))
		{
			base.Region = null;
			return;
		}
		Rectangle chromeBounds = GetChromeBounds();
		using GraphicsPath graphicsPath = RoundedPanel.CreateRoundedPath(chromeBounds, Math.Min(CornerRadius, Math.Min(chromeBounds.Width, chromeBounds.Height) / 2));
		if (!string.IsNullOrWhiteSpace(BadgeText))
		{
			graphicsPath.AddEllipse(GetBadgeBounds(chromeBounds));
		}
		base.Region = new Region(graphicsPath);
	}

	private static void DrawBell(Graphics graphics, Pen pen, Brush brush)
	{
		graphics.DrawLine(pen, 0f, -6.1f, 0f, -4.8f);
		graphics.DrawArc(pen, -5.1f, -4.9f, 10.2f, 8.9f, 202f, 136f);
		graphics.DrawLine(pen, -4.35f, -0.25f, -4.35f, 2.8f);
		graphics.DrawLine(pen, 4.35f, -0.25f, 4.35f, 2.8f);
		graphics.DrawArc(pen, -4.9f, 1.2f, 9.8f, 4.8f, 6f, 168f);
		graphics.DrawLine(pen, -1.85f, 5.9f, 1.85f, 5.9f);
		graphics.FillEllipse(brush, -0.95f, 6.05f, 1.9f, 1.9f);
	}

	private static void DrawCart(Graphics graphics, Pen pen, Brush brush)
	{
		graphics.DrawLine(pen, -6f, -4.7f, -3.8f, -4.7f);
		graphics.DrawLine(pen, -3.8f, -4.7f, -1.6f, 1.1f);
		graphics.DrawLine(pen, -1.6f, 1.1f, 5.8f, 1.1f);
		graphics.DrawLine(pen, 5.8f, 1.1f, 7f, -2.3f);
		graphics.DrawLine(pen, -2f, -1.6f, 6.5f, -1.6f);
		graphics.DrawEllipse(pen, -0.55f, 4.1f, 2.1f, 2.1f);
		graphics.DrawEllipse(pen, 3.95f, 4.1f, 2.1f, 2.1f);
	}

	private static void DrawInfo(Graphics graphics, Pen pen, Brush brush)
	{
		graphics.FillEllipse(brush, -1.1f, -5.6f, 2.2f, 2.2f);
		graphics.DrawLine(pen, 0f, -1.9f, 0f, 4.5f);
		graphics.DrawLine(pen, -1.3f, 4.6f, 1.3f, 4.6f);
	}

	private static ColorMatrix CreateTintMatrix(Color color)
	{
		float num = (float)(int)color.R / 255f;
		float num2 = (float)(int)color.G / 255f;
		float num3 = (float)(int)color.B / 255f;
		float num4 = (float)(int)color.A / 255f;
		return new ColorMatrix(new float[5][]
		{
			new float[5] { 0f, 0f, 0f, num, 0f },
			new float[5] { 0f, 0f, 0f, num2, 0f },
			new float[5] { 0f, 0f, 0f, num3, 0f },
			new float[5] { 0f, 0f, 0f, num4, 0f },
			new float[5] { 0f, 0f, 0f, 0f, 1f }
		});
	}

	private static Bitmap CreateRecoloredGlyph(Image image, Rectangle sourceBounds, Color glyphColor)
	{
		using Bitmap bitmap = ((!(image is Bitmap)) ? new Bitmap(image) : null);
		Bitmap bitmap2 = bitmap ?? ((Bitmap)image);
		Bitmap bitmap3 = new Bitmap(sourceBounds.Width, sourceBounds.Height);
		for (int i = 0; i < sourceBounds.Height; i++)
		{
			for (int j = 0; j < sourceBounds.Width; j++)
			{
				Color color = Color.FromArgb(bitmap2.GetPixel(sourceBounds.X + j, sourceBounds.Y + i).A, glyphColor.R, glyphColor.G, glyphColor.B);
				bitmap3.SetPixel(j, i, color);
			}
		}
		return bitmap3;
	}

	private static Rectangle GetOpaqueBounds(Image image)
	{
		using Bitmap bitmap = ((!(image is Bitmap)) ? new Bitmap(image) : null);
		Bitmap bitmap2 = bitmap ?? ((Bitmap)image);
		int num = bitmap2.Width;
		int num2 = bitmap2.Height;
		int num3 = -1;
		int num4 = -1;
		for (int i = 0; i < bitmap2.Height; i++)
		{
			for (int j = 0; j < bitmap2.Width; j++)
			{
				if (bitmap2.GetPixel(j, i).A > 10)
				{
					num = Math.Min(num, j);
					num2 = Math.Min(num2, i);
					num3 = Math.Max(num3, j);
					num4 = Math.Max(num4, i);
				}
			}
		}
		if (num3 < num || num4 < num2)
		{
			return new Rectangle(0, 0, bitmap2.Width, bitmap2.Height);
		}
		return Rectangle.FromLTRB(num, num2, num3 + 1, num4 + 1);
	}
}
