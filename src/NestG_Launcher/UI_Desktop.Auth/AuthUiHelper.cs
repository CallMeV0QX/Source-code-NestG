using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using UI_Desktop.Store;

namespace UI_Desktop.Auth;

internal static class AuthUiHelper
{
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

	internal static void ApplyWindowIcon(Form form)
	{
		AppIconProvider.ApplyIcon(form);
	}

	internal static void ConfigureInput(RoundedPanel shell, TextBox textBox)
	{
		shell.SurfaceColor = AppTheme.InputSurface;
		shell.BorderColor = AppTheme.InputBorder;
		shell.BorderThickness = 1;
		shell.CornerRadius = 24;
		textBox.Dock = DockStyle.None;
		textBox.Multiline = false;
		textBox.BackColor = AppTheme.InputSurface;
		textBox.BorderStyle = BorderStyle.None;
		textBox.ForeColor = AppTheme.PrimaryText;
		textBox.Margin = Padding.Empty;
		textBox.Enter += delegate
		{
			UpdateState(focused: true);
		};
		textBox.Leave += delegate
		{
			UpdateState(focused: false);
		};
		shell.Click += delegate
		{
			textBox.Focus();
		};
		shell.Resize += delegate
		{
			LayoutTextBox();
		};
		foreach (Control control in shell.Controls)
		{
			control.Click += delegate
			{
				textBox.Focus();
			};
		}
		LayoutTextBox();
		void LayoutTextBox()
		{
			int preferredHeight = textBox.PreferredHeight;
			int y = Math.Max(8, (shell.ClientSize.Height - preferredHeight) / 2);
			int width = Math.Max(10, shell.ClientSize.Width - 40);
			textBox.SetBounds(20, y, width, preferredHeight + 1);
		}
		void UpdateState(bool focused)
		{
			Color color = (focused ? Color.FromArgb(36, 46, 61) : AppTheme.InputSurface);
			shell.SurfaceColor = color;
			shell.BorderColor = (focused ? AppTheme.InputBorderFocus : AppTheme.InputBorder);
			textBox.BackColor = color;
		}
	}

	internal static void UpdateResponsiveLayout(Form form, TableLayoutPanel rootLayout, Control heroPanel)
	{
		bool flag2 = (heroPanel.Visible = form.ClientSize.Width >= 1120);
		rootLayout.ColumnStyles[0].Width = (flag2 ? 43f : 0f);
		rootLayout.ColumnStyles[1].Width = (flag2 ? 57f : 100f);
	}

	internal static void Navigate(Form current, Form next)
	{
		if (next is StoreWebHomeForm)
		{
			next.StartPosition = FormStartPosition.CenterScreen;
			next.WindowState = FormWindowState.Maximized;
		}
		else if ((next is StoreForm || next is LoginWebViewForm) ? true : false)
		{
			next.StartPosition = FormStartPosition.CenterScreen;
			next.WindowState = FormWindowState.Normal;
		}
		else
		{
			next.StartPosition = FormStartPosition.Manual;
			next.Bounds = current.Bounds;
			next.WindowState = current.WindowState;
		}
		next.FormClosed += delegate
		{
			current.Close();
		};
		current.Hide();
		next.Show();
	}
}
