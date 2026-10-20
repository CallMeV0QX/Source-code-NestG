using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UI_Desktop;

internal sealed class GradientButton : Button
{
	private bool isHovered;

	private bool isPressed;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color StartColor { get; set; } = Color.FromArgb(86, 140, 255);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color EndColor { get; set; } = Color.FromArgb(50, 103, 242);

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int CornerRadius { get; set; } = 26;

	public GradientButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.FlatStyle = FlatStyle.Flat;
		base.FlatAppearance.BorderSize = 0;
		Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point);
		ForeColor = Color.White;
		base.Height = 56;
		MinimumSize = new Size(0, 56);
		base.TabStop = true;
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
		isPressed = false;
		Invalidate();
	}

	protected override void OnMouseDown(MouseEventArgs mevent)
	{
		base.OnMouseDown(mevent);
		isPressed = true;
		Invalidate();
	}

	protected override void OnMouseUp(MouseEventArgs mevent)
	{
		base.OnMouseUp(mevent);
		isPressed = false;
		Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, base.Width - 1, base.Height - 1), CornerRadius);
		base.Region = new Region(path);
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		if (BackColor.A == byte.MaxValue || base.Parent == null)
		{
			base.OnPaintBackground(pevent);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (base.Width <= 1 || base.Height <= 1)
		{
			return;
		}
		Color color = ((base.Parent is RoundedPanel roundedPanel) ? roundedPanel.SurfaceColor : (base.Parent?.BackColor ?? BackColor));
		e.Graphics.Clear(color);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
		e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		Rectangle rectangle = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(rectangle, CornerRadius);
		using Brush brush = CreateBackgroundBrush(rectangle);
		e.Graphics.FillPath(brush, path);
		using Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255));
		e.Graphics.DrawPath(pen, path);
		TextRenderer.DrawText(e.Graphics, Text, Font, rectangle, base.Enabled ? ForeColor : AppTheme.SecondaryText, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private Brush CreateBackgroundBrush(Rectangle rect)
	{
		if (!base.Enabled)
		{
			return new SolidBrush(Color.FromArgb(63, 76, 95));
		}
		Color color = StartColor;
		Color color2 = EndColor;
		if (isHovered)
		{
			color = Lighten(color, 0.08f);
			color2 = Lighten(color2, 0.08f);
		}
		if (isPressed)
		{
			color = Darken(color, 0.08f);
			color2 = Darken(color2, 0.08f);
		}
		return new LinearGradientBrush(rect, color, color2, LinearGradientMode.Vertical);
	}

	private static Color Lighten(Color color, float factor)
	{
		float num = (float)(int)color.R + (float)(255 - color.R) * factor;
		float num2 = (float)(int)color.G + (float)(255 - color.G) * factor;
		float num3 = (float)(int)color.B + (float)(255 - color.B) * factor;
		return Color.FromArgb(color.A, (int)num, (int)num2, (int)num3);
	}

	private static Color Darken(Color color, float factor)
	{
		float num = (float)(int)color.R * (1f - factor);
		float num2 = (float)(int)color.G * (1f - factor);
		float num3 = (float)(int)color.B * (1f - factor);
		return Color.FromArgb(color.A, (int)num, (int)num2, (int)num3);
	}
}
