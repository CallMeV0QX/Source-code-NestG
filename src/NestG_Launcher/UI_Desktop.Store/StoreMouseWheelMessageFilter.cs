using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace UI_Desktop.Store;

internal sealed class StoreMouseWheelMessageFilter : IMessageFilter
{
	private const int WmMouseWheel = 522;

	private static readonly StoreMouseWheelMessageFilter instance = new StoreMouseWheelMessageFilter();

	public static StoreMouseWheelMessageFilter Instance => instance;

	private StoreMouseWheelMessageFilter()
	{
	}

	public bool PreFilterMessage(ref Message m)
	{
		if (m.Msg != 522)
		{
			return false;
		}
		nint num = WindowFromPoint(Cursor.Position);
		if (num == IntPtr.Zero)
		{
			return false;
		}
		Control control = Control.FromChildHandle(num);
		if (control == null || ShouldLetControlHandleWheel(control))
		{
			return false;
		}
		int wheelDelta = GetWheelDelta(m.WParam);
		if (wheelDelta == 0)
		{
			return false;
		}
		for (Control control2 = control; control2 != null; control2 = control2.Parent)
		{
			if (control2 is StoreScrollPanel storeScrollPanel && storeScrollPanel.TryHandleMouseWheel(wheelDelta))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ShouldLetControlHandleWheel(Control control)
	{
		if (control is WebView2)
		{
			return true;
		}
		if ((control is ComboBox || control is ListBox || control is NumericUpDown || control is DateTimePicker || control is MonthCalendar || control is TrackBar || control is DataGridView || control is HScrollBar || control is VScrollBar) ? true : false)
		{
			return true;
		}
		if (control is TextBoxBase textBoxBase)
		{
			return textBoxBase.Multiline;
		}
		return false;
	}

	private static int GetWheelDelta(nint wParam)
	{
		return (short)((((IntPtr)wParam).ToInt64() >> 16) & 0xFFFF);
	}

	[DllImport("user32.dll")]
	private static extern nint WindowFromPoint(Point point);
}
