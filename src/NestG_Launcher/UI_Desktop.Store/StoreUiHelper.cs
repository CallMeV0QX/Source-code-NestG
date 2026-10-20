using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal static class StoreUiHelper
{
	private const string SearchPlaceholderLabelName = "__StoreSearchPlaceholderLabel";

	private static readonly Dictionary<TextBox, string> SearchPlaceholders = new Dictionary<TextBox, string>();

	internal static void ApplyWindowIcon(Form form)
	{
		AppIconProvider.ApplyIcon(form);
	}

	internal static void RestoreWindowToForeground(Form? form)
	{
		if (form == null || form.IsDisposed || form.Disposing)
		{
			return;
		}
		if (form.InvokeRequired)
		{
			form.BeginInvoke(delegate
			{
				RestoreWindowToForeground(form);
			});
			return;
		}
		if (!form.Visible)
		{
			form.Show();
		}
		if (form.WindowState == FormWindowState.Minimized)
		{
			form.WindowState = FormWindowState.Normal;
		}
		form.BringToFront();
		form.Activate();
		if (form.TopMost)
		{
			return;
		}
		form.TopMost = true;
		form.BringToFront();
		form.Activate();
		form.BeginInvoke(delegate
		{
			if (!form.IsDisposed && !form.Disposing)
			{
				form.TopMost = false;
				form.BringToFront();
				form.Activate();
			}
		});
	}

	internal static void EnableDoubleBuffer(Control control)
	{
		typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(control, true);
	}

	internal static void LoadLogo(PictureBox pictureBox)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
			if (!File.Exists(text))
			{
				return;
			}
			using Image original = Image.FromFile(text);
			pictureBox.Image = new Bitmap(original);
		}
		catch
		{
		}
	}

	internal static Image? LoadAssetImage(string fileName)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
			if (!File.Exists(text))
			{
				return null;
			}
			using Image original = Image.FromFile(text);
			return new Bitmap(original);
		}
		catch
		{
			return null;
		}
	}

	internal static void ConfigureSearchInput(RoundedPanel shell, Label iconLabel, TextBox textBox)
	{
		Label placeholderLabel = EnsurePlaceholderLabel(shell, textBox);
		shell.SurfaceColor = AppTheme.CardSurface;
		shell.BorderColor = AppTheme.CardBorder;
		shell.BorderThickness = 1;
		shell.CornerRadius = 20;
		iconLabel.ForeColor = AppTheme.SecondaryText;
		iconLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point);
		iconLabel.TextAlign = ContentAlignment.MiddleCenter;
		textBox.Dock = DockStyle.None;
		textBox.Multiline = false;
		textBox.BackColor = AppTheme.CardSurface;
		textBox.BorderStyle = BorderStyle.None;
		textBox.ForeColor = AppTheme.PrimaryText;
		textBox.Margin = Padding.Empty;
		CapturePlaceholderText(textBox);
		textBox.Enter += delegate
		{
			UpdateState(focused: true);
		};
		textBox.Leave += delegate
		{
			UpdateState(focused: false);
		};
		textBox.TextChanged += delegate
		{
			UpdatePlaceholder(textBox);
		};
		textBox.FontChanged += delegate
		{
			placeholderLabel.Font = textBox.Font;
			LayoutChildren();
		};
		shell.Click += delegate
		{
			textBox.Focus();
		};
		iconLabel.Click += delegate
		{
			textBox.Focus();
		};
		placeholderLabel.Click += delegate
		{
			textBox.Focus();
		};
		shell.Resize += delegate
		{
			LayoutChildren();
		};
		textBox.Disposed += delegate
		{
			SearchPlaceholders.Remove(textBox);
		};
		if (!shell.Controls.Contains(iconLabel))
		{
			shell.Controls.Add(iconLabel);
		}
		if (!shell.Controls.Contains(textBox))
		{
			shell.Controls.Add(textBox);
		}
		if (!shell.Controls.Contains(placeholderLabel))
		{
			shell.Controls.Add(placeholderLabel);
		}
		LayoutChildren();
		UpdateState(textBox.Focused);
		void LayoutChildren()
		{
			int preferredHeight = textBox.PreferredHeight;
			int num = Math.Max(8, (shell.ClientSize.Height - preferredHeight) / 2);
			bool flag = !string.IsNullOrWhiteSpace(iconLabel.Text);
			iconLabel.Visible = flag;
			if (flag)
			{
				iconLabel.SetBounds(18, num - 1, 20, preferredHeight + 2);
			}
			int num2 = (flag ? (iconLabel.Right + 10) : 18);
			int num3 = Math.Max(10, shell.ClientSize.Width - num2 - 18);
			textBox.SetBounds(num2, num, num3, preferredHeight + 1);
			placeholderLabel.SetBounds(num2 + 1, num - 1, Math.Max(10, num3 - 2), preferredHeight + 4);
		}
		void UpdateState(bool focused)
		{
			Color color = (focused ? Color.FromArgb(22, 29, 39) : AppTheme.CardSurface);
			shell.SurfaceColor = color;
			shell.BorderColor = (focused ? AppTheme.InputBorderFocus : AppTheme.CardBorder);
			iconLabel.ForeColor = (focused ? AppTheme.PrimaryText : AppTheme.SecondaryText);
			textBox.BackColor = color;
			placeholderLabel.ForeColor = (focused ? Color.FromArgb(129, 141, 158) : Color.FromArgb(146, 158, 176));
			UpdatePlaceholder(textBox);
		}
	}

	internal static void RefreshCueBanner(TextBox textBox)
	{
		if (!TryScheduleOnUiThread(textBox, delegate
		{
			RefreshCueBanner(textBox);
		}))
		{
			CapturePlaceholderText(textBox);
			UpdatePlaceholder(textBox);
		}
	}

	private static void CapturePlaceholderText(TextBox textBox)
	{
		if (!string.IsNullOrWhiteSpace(textBox.PlaceholderText))
		{
			SearchPlaceholders[textBox] = textBox.PlaceholderText;
			textBox.PlaceholderText = string.Empty;
		}
	}

	private static void UpdatePlaceholder(TextBox textBox)
	{
		if (TryScheduleOnUiThread(textBox, delegate
		{
			UpdatePlaceholder(textBox);
		}))
		{
			return;
		}
		Control parent = textBox.Parent;
		if (parent != null && !parent.IsDisposed && parent.Controls["__StoreSearchPlaceholderLabel"] is Label label)
		{
			CapturePlaceholderText(textBox);
			SearchPlaceholders.TryGetValue(textBox, out string value);
			label.Text = value ?? string.Empty;
			label.Visible = !textBox.Focused && string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrWhiteSpace(label.Text);
			if (label.Visible)
			{
				label.BringToFront();
			}
			else
			{
				textBox.BringToFront();
			}
		}
	}

	private static Label EnsurePlaceholderLabel(RoundedPanel shell, TextBox textBox)
	{
		if (shell.Controls["__StoreSearchPlaceholderLabel"] is Label result)
		{
			return result;
		}
		return new Label
		{
			Name = "__StoreSearchPlaceholderLabel",
			AutoEllipsis = true,
			BackColor = Color.Transparent,
			ForeColor = Color.FromArgb(146, 158, 176),
			Font = textBox.Font,
			Margin = Padding.Empty,
			TextAlign = ContentAlignment.MiddleLeft,
			Cursor = Cursors.IBeam
		};
	}

	private static bool TryScheduleOnUiThread(Control control, Action action)
	{
		if (control.IsDisposed || control.Disposing)
		{
			return true;
		}
		if (!control.IsHandleCreated || !control.InvokeRequired)
		{
			return false;
		}
		control.BeginInvoke(action);
		return true;
	}
}
