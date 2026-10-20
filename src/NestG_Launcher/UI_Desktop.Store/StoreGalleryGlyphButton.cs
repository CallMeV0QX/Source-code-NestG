using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreGalleryGlyphButton : Button
{
	private bool isHovered;

	private bool isPressed;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int CornerRadius { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color HoverBackColor { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color PressedBackColor { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public StoreGalleryButtonIcon IconKind { get; set; }

	public StoreGalleryGlyphButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.FlatStyle = FlatStyle.Flat;
		base.FlatAppearance.BorderSize = 0;
		Cursor = Cursors.Hand;
		base.Padding = Padding.Empty;
		TextAlign = ContentAlignment.MiddleCenter;
		base.UseMnemonic = false;
		base.TabStop = false;
		ForeColor = Color.FromArgb(244, 247, 250);
		BackColor = Color.FromArgb(112, 9, 9, 9);
		HoverBackColor = Color.FromArgb(148, 18, 18, 18);
		PressedBackColor = Color.FromArgb(176, 30, 30, 30);
		CornerRadius = 12;
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
		using GraphicsPath path = RoundedPanel.CreateRoundedPath(new Rectangle(0, 0, base.Width - 1, base.Height - 1), CornerRadius);
		using SolidBrush brush = new SolidBrush(GetCurrentBackgroundColor());
		e.Graphics.FillPath(brush, path);
		Color color = (base.Enabled ? ForeColor : Color.FromArgb(168, 173, 181));
		StoreGalleryButtonIcon iconKind = IconKind;
		if ((uint)(iconKind - 1) <= 1u)
		{
			DrawCenteredChevron(e.Graphics, base.ClientRectangle, IconKind, color);
		}
		else
		{
			DrawCenteredGlyph(e.Graphics, base.ClientRectangle, Text, Font, color);
		}
	}

	private Color GetCurrentBackgroundColor()
	{
		if (!base.Enabled)
		{
			return Color.FromArgb(92, 20, 20, 20);
		}
		if (isPressed)
		{
			return PressedBackColor;
		}
		if (isHovered)
		{
			return HoverBackColor;
		}
		return BackColor;
	}

	private static void DrawCenteredChevron(Graphics graphics, Rectangle bounds, StoreGalleryButtonIcon iconKind, Color color)
	{
		float num = Math.Clamp((float)bounds.Height * 0.26f, 12f, 22f);
		float num2 = Math.Clamp((float)bounds.Width * 0.18f, 7f, 14f);
		float num3 = (float)bounds.Left + (float)bounds.Width / 2f;
		float num4 = (float)bounds.Top + (float)bounds.Height / 2f;
		float num5 = num2 / 2f;
		float num6 = num / 2f;
		PointF[] points = ((iconKind != StoreGalleryButtonIcon.ChevronLeft) ? new PointF[3]
		{
			new PointF(num3 - num5, num4 - num6),
			new PointF(num3 + num5, num4),
			new PointF(num3 - num5, num4 + num6)
		} : new PointF[3]
		{
			new PointF(num3 + num5, num4 - num6),
			new PointF(num3 - num5, num4),
			new PointF(num3 + num5, num4 + num6)
		});
		using Pen pen = new Pen(color, Math.Clamp((float)Math.Min(bounds.Width, bounds.Height) * 0.08f, 2.2f, 3.4f))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		graphics.DrawLines(pen, points);
	}

	private static void DrawCenteredGlyph(Graphics graphics, Rectangle bounds, string text, Font font, Color color)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		using SolidBrush brush = new SolidBrush(color);
		using StringFormat stringFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
		stringFormat.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
		stringFormat.Alignment = StringAlignment.Near;
		stringFormat.LineAlignment = StringAlignment.Near;
		stringFormat.SetMeasurableCharacterRanges(new CharacterRange[1]
		{
			new CharacterRange(0, text.Length)
		});
		RectangleF value = new RectangleF(0f, 0f, Math.Max((float)bounds.Width * 2f, 200f), Math.Max((float)bounds.Height * 2f, 200f));
		RectangleF bounds2 = graphics.MeasureCharacterRanges(text, font, Rectangle.Round(value), stringFormat)[0].GetBounds(graphics);
		float x = (float)bounds.Left + ((float)bounds.Width - bounds2.Width) / 2f - bounds2.Left;
		float y = (float)bounds.Top + ((float)bounds.Height - bounds2.Height) / 2f - bounds2.Top;
		graphics.DrawString(text, font, brush, new PointF(x, y), stringFormat);
	}
}
