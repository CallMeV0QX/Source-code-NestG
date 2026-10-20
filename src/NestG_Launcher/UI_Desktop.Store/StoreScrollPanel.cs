using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal class StoreScrollPanel : Panel
{
	private const int DefaultSmallChange = 36;

	private float verticalWheelCarry;

	public StoreScrollPanel()
	{
		SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		DoubleBuffered = true;
		AutoScroll = true;
		base.VerticalScroll.SmallChange = 36;
		base.HorizontalScroll.SmallChange = 36;
	}

	protected override Point ScrollToControl(Control activeControl)
	{
		return DisplayRectangle.Location;
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (!TryHandleMouseWheel(e.Delta))
		{
			base.OnMouseWheel(e);
		}
	}

	internal bool TryHandleMouseWheel(int delta)
	{
		if (delta == 0 || base.IsDisposed || !base.Visible || !AutoScroll || !CanScrollVertically())
		{
			verticalWheelCarry = 0f;
			return false;
		}
		float num = (0f - (float)delta / 120f) * GetPixelsPerDetent();
		if (Math.Abs(num) < 0.01f)
		{
			return false;
		}
		if (!CanMoveVertically(num))
		{
			verticalWheelCarry = 0f;
			return false;
		}
		verticalWheelCarry += num;
		int num2 = (int)Math.Truncate(verticalWheelCarry);
		verticalWheelCarry -= num2;
		if (num2 == 0)
		{
			return true;
		}
		bool num3 = ScrollByPixels(num2);
		if (!num3)
		{
			verticalWheelCarry = 0f;
		}
		return num3;
	}

	internal bool ScrollByPixels(int pixelDelta)
	{
		if (pixelDelta == 0 || base.IsDisposed || !base.Visible || !AutoScroll || !CanScrollVertically())
		{
			return false;
		}
		VScrollProperties verticalScroll = base.VerticalScroll;
		int value = verticalScroll.Value;
		int maxVerticalOffset = GetMaxVerticalOffset(verticalScroll);
		int num = Math.Clamp(value + pixelDelta, verticalScroll.Minimum, maxVerticalOffset);
		if (num == value)
		{
			return false;
		}
		int x = Math.Max(0, -base.AutoScrollPosition.X);
		base.AutoScrollPosition = new Point(x, num);
		return true;
	}

	private bool CanScrollVertically()
	{
		VScrollProperties verticalScroll = base.VerticalScroll;
		return GetMaxVerticalOffset(verticalScroll) > verticalScroll.Minimum;
	}

	private bool CanMoveVertically(float pixelDelta)
	{
		VScrollProperties verticalScroll = base.VerticalScroll;
		int value = verticalScroll.Value;
		int maxVerticalOffset = GetMaxVerticalOffset(verticalScroll);
		if (!(pixelDelta > 0f))
		{
			return value > verticalScroll.Minimum;
		}
		return value < maxVerticalOffset;
	}

	private float GetPixelsPerDetent()
	{
		int mouseWheelScrollLines = SystemInformation.MouseWheelScrollLines;
		if (mouseWheelScrollLines == 0)
		{
			return 0f;
		}
		if (mouseWheelScrollLines < 0)
		{
			return Math.Max(40, base.ClientSize.Height - 24);
		}
		int value = ((base.VerticalScroll.SmallChange > 0) ? base.VerticalScroll.SmallChange : 36) * Math.Max(1, mouseWheelScrollLines);
		int max = Math.Max(40, base.ClientSize.Height - 24);
		return Math.Clamp(value, 40, max);
	}

	private static int GetMaxVerticalOffset(VScrollProperties verticalScroll)
	{
		return Math.Max(verticalScroll.Minimum, verticalScroll.Maximum - verticalScroll.LargeChange + 1);
	}
}
