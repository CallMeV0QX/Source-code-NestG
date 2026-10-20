using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace UI_Desktop;

internal class RoundedPanel : Panel
{
	private int cornerRadius = 24;

	private int borderThickness = 1;

	private Color surfaceColor = AppTheme.CardSurface;

	private Color borderColor = AppTheme.CardBorder;

	private Bitmap? cachedBackgroundBitmap;

	private Size cachedBackgroundSize = Size.Empty;

	private Color cachedBackgroundSurfaceColor = Color.Empty;

	private Color cachedBackgroundBorderColor = Color.Empty;

	private Color cachedBackgroundParentColor = Color.Empty;

	private int cachedBackgroundBorderThickness = -1;

	private int cachedBackgroundCornerRadius = -1;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int CornerRadius
	{
		get
		{
			return cornerRadius;
		}
		set
		{
			cornerRadius = Math.Max(2, value);
			InvalidateBackgroundCache();
			UpdateRegion();
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int BorderThickness
	{
		get
		{
			return borderThickness;
		}
		set
		{
			borderThickness = Math.Max(0, value);
			InvalidateBackgroundCache();
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color SurfaceColor
	{
		get
		{
			return surfaceColor;
		}
		set
		{
			surfaceColor = value;
			InvalidateBackgroundCache();
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color BorderColor
	{
		get
		{
			return borderColor;
		}
		set
		{
			borderColor = value;
			InvalidateBackgroundCache();
			Invalidate();
		}
	}

	public RoundedPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
	}

	protected override void OnResize(EventArgs eventargs)
	{
		base.OnResize(eventargs);
		InvalidateBackgroundCache();
		UpdateRegion();
	}

	protected override void OnParentChanged(EventArgs e)
	{
		base.OnParentChanged(e);
		InvalidateBackgroundCache();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (base.Width > 1 && base.Height > 1)
		{
			EnsureBackgroundCache();
			if (cachedBackgroundBitmap != null)
			{
				e.Graphics.DrawImageUnscaled(cachedBackgroundBitmap, Point.Empty);
			}
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		if (BackColor.A == byte.MaxValue || base.Parent == null)
		{
			base.OnPaintBackground(e);
		}
	}

	private void UpdateRegion()
	{
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		using GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, base.Width - 1, base.Height - 1), cornerRadius);
		base.Region = new Region(path);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			cachedBackgroundBitmap?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void EnsureBackgroundCache()
	{
		Color parentBackgroundColor = GetParentBackgroundColor();
		if (cachedBackgroundBitmap != null && cachedBackgroundSize == base.Size && cachedBackgroundSurfaceColor == surfaceColor && cachedBackgroundBorderColor == borderColor && cachedBackgroundParentColor == parentBackgroundColor && cachedBackgroundBorderThickness == borderThickness && cachedBackgroundCornerRadius == cornerRadius)
		{
			return;
		}
		cachedBackgroundBitmap?.Dispose();
		cachedBackgroundBitmap = new Bitmap(base.Width, base.Height, PixelFormat.Format32bppPArgb);
		using Graphics graphics = Graphics.FromImage(cachedBackgroundBitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.CompositingQuality = CompositingQuality.Default;
		graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
		graphics.Clear(parentBackgroundColor);
		Rectangle bounds = new Rectangle(borderThickness, borderThickness, base.Width - borderThickness * 2 - 1, base.Height - borderThickness * 2 - 1);
		if (bounds.Width > 0 && bounds.Height > 0)
		{
			using GraphicsPath path = CreateRoundedPath(bounds, cornerRadius);
			using SolidBrush brush = new SolidBrush(surfaceColor);
			graphics.FillPath(brush, path);
			if (borderThickness > 0)
			{
				using Pen pen = new Pen(borderColor, borderThickness)
				{
					Alignment = PenAlignment.Inset,
					LineJoin = LineJoin.Round
				};
				graphics.DrawPath(pen, path);
			}
		}
		cachedBackgroundSize = base.Size;
		cachedBackgroundSurfaceColor = surfaceColor;
		cachedBackgroundBorderColor = borderColor;
		cachedBackgroundParentColor = parentBackgroundColor;
		cachedBackgroundBorderThickness = borderThickness;
		cachedBackgroundCornerRadius = cornerRadius;
	}

	private void InvalidateBackgroundCache()
	{
		cachedBackgroundBitmap?.Dispose();
		cachedBackgroundBitmap = null;
		cachedBackgroundSize = Size.Empty;
	}

	private Color GetParentBackgroundColor()
	{
		if (base.Parent == null)
		{
			return surfaceColor;
		}
		if (!(base.Parent is RoundedPanel roundedPanel))
		{
			return base.Parent.BackColor;
		}
		return roundedPanel.SurfaceColor;
	}

	internal static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = radius * 2;
		if (num > bounds.Width)
		{
			num = bounds.Width;
		}
		if (num > bounds.Height)
		{
			num = bounds.Height;
		}
		Rectangle rect = new Rectangle(bounds.Location, new Size(num, num));
		graphicsPath.AddArc(rect, 180f, 90f);
		rect.X = bounds.Right - num;
		graphicsPath.AddArc(rect, 270f, 90f);
		rect.Y = bounds.Bottom - num;
		graphicsPath.AddArc(rect, 0f, 90f);
		rect.X = bounds.Left;
		graphicsPath.AddArc(rect, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
