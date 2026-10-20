using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace UI_Desktop;

internal static class AppIconProvider
{
	private static Icon? appIcon;

	internal static Icon? GetAppIcon()
	{
		if (appIcon != null)
		{
			return appIcon;
		}
		string text = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
		if (!File.Exists(text))
		{
			text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
		}
		if (!File.Exists(text))
		{
			return null;
		}
		using Icon icon = new Icon(text);
		appIcon = (Icon)icon.Clone();
		return appIcon;
	}

	internal static void ApplyIcon(Form form)
	{
		Icon icon = GetAppIcon();
		if (icon != null)
		{
			form.Icon = icon;
		}
	}
}
