using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreNavButton : Button
{
	private bool isActive;

	private bool isHovered;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			isActive = value;
			Invalidate();
		}
	}

	public StoreNavButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		AutoSize = true;
		base.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.FlatStyle = FlatStyle.Flat;
		base.FlatAppearance.BorderSize = 0;
		Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
		ForeColor = AppTheme.SecondaryText;
		base.Margin = new Padding(0, 0, 18, 0);
		base.Padding = new Padding(10, 14, 10, 16);
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

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		if (base.Parent != null)
		{
			pevent.Graphics.Clear(base.Parent.BackColor);
		}
		else
		{
			base.OnPaintBackground(pevent);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.Clear(base.Parent?.BackColor ?? AppTheme.WindowBackground);
		Color foreColor = (isActive ? AppTheme.PrimaryText : (isHovered ? Color.FromArgb(209, 221, 238) : AppTheme.SecondaryText));
		TextRenderer.DrawText(e.Graphics, Text, Font, base.ClientRectangle, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		if (!isActive)
		{
			return;
		}
		using SolidBrush brush = new SolidBrush(Color.FromArgb(38, 214, 196));
		e.Graphics.FillRectangle(brush, 12, base.Height - 4, Math.Max(18, base.Width - 24), 3);
	}
}
