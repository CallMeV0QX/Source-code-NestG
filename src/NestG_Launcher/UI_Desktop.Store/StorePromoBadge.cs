using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StorePromoBadge : Control
{
	private string badgeText = string.Empty;

	private Color startColor = Color.FromArgb(54, 120, 255);

	private Color endColor = Color.FromArgb(30, 70, 196);

	private Color borderColor = Color.FromArgb(150, 194, 255);

	private Color glowColor = Color.FromArgb(120, 83, 155, 255);

	private Color textColor = Color.White;

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
			badgeText = (string.IsNullOrWhiteSpace(value) ? "TAG" : value.Trim().ToUpperInvariant());
			ApplyPalette(badgeText);
			UpdateBadgeSize();
			Invalidate();
		}
	}

	public StorePromoBadge()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold, GraphicsUnit.Point);
		base.Size = new Size(84, 28);
		BadgeText = "SALE";
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		UpdateShapeRegion();
	}

	protected override void OnParentChanged(EventArgs e)
	{
		base.OnParentChanged(e);
		UpdateShapeRegion();
	}

	private void ApplyPalette(string text)
	{
		switch (text)
		{
		case "FREE":
			startColor = Color.FromArgb(88, 182, 255);
			endColor = Color.FromArgb(38, 99, 214);
			borderColor = Color.FromArgb(194, 233, 255);
			glowColor = Color.FromArgb(128, 80, 179, 255);
			textColor = Color.FromArgb(245, 251, 255);
			break;
		case "SALE":
			startColor = Color.FromArgb(255, 146, 84);
			endColor = Color.FromArgb(211, 54, 66);
			borderColor = Color.FromArgb(255, 214, 185);
			glowColor = Color.FromArgb(118, 255, 128, 74);
			textColor = Color.FromArgb(255, 249, 243);
			break;
		case "HOT":
			startColor = Color.FromArgb(255, 121, 94);
			endColor = Color.FromArgb(196, 29, 52);
			borderColor = Color.FromArgb(255, 207, 194);
			glowColor = Color.FromArgb(126, 255, 120, 105);
			textColor = Color.FromArgb(255, 245, 244);
			break;
		default:
			startColor = Color.FromArgb(120, 141, 170);
			endColor = Color.FromArgb(57, 71, 95);
			borderColor = Color.FromArgb(197, 209, 225);
			glowColor = Color.FromArgb(112, 133, 152, 190);
			textColor = Color.FromArgb(246, 248, 251);
			break;
		}
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
		e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		using (GraphicsPath path = RoundedPanel.CreateRoundedPath(new Rectangle(1, 2, base.Width - 3, base.Height - 3), 12))
		{
			PathGradientBrush pathGradientBrush = new PathGradientBrush(path);
			pathGradientBrush.CenterColor = glowColor;
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, glowColor) };
			using PathGradientBrush brush = pathGradientBrush;
			e.Graphics.FillPath(brush, path);
		}
		Rectangle rectangle = new Rectangle(1, 1, base.Width - 3, base.Height - 4);
		using GraphicsPath path2 = RoundedPanel.CreateRoundedPath(rectangle, 11);
		using (LinearGradientBrush brush2 = new LinearGradientBrush(rectangle, startColor, endColor, LinearGradientMode.Vertical))
		{
			e.Graphics.FillPath(brush2, path2);
		}
		Rectangle rectangle2 = new Rectangle(rectangle.X + 1, rectangle.Y + 1, rectangle.Width - 2, Math.Max(10, rectangle.Height / 2));
		using (GraphicsPath path3 = RoundedPanel.CreateRoundedPath(rectangle2, 10))
		{
			using LinearGradientBrush brush3 = new LinearGradientBrush(rectangle2, Color.FromArgb(92, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical);
			e.Graphics.FillPath(brush3, path3);
		}
		using (Pen pen = new Pen(Color.FromArgb(86, 255, 255, 255), 1f))
		{
			e.Graphics.DrawPath(pen, path2);
		}
		using (Pen pen2 = new Pen(borderColor, 1.15f))
		{
			e.Graphics.DrawPath(pen2, path2);
		}
		TextRenderer.DrawText(e.Graphics, badgeText, Font, rectangle, textColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private void UpdateBadgeSize()
	{
		base.Size = new Size(Math.Max(70, TextRenderer.MeasureText(badgeText, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 28), 28);
	}

	private void UpdateShapeRegion()
	{
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(new Rectangle(1, 1, base.Width - 3, base.Height - 4), 11);
		base.Region = new Region(path);
	}
}
