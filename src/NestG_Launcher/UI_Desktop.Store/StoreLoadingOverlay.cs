using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreLoadingOverlay : IDisposable
{
	private readonly Control host;

	private readonly Panel overlayPanel = new Panel();

	private readonly RoundedPanel cardPanel = new RoundedPanel();

	private readonly PictureBox logoPictureBox = new PictureBox();

	private readonly Label titleLabel = new Label();

	private readonly Label subtitleLabel = new Label();

	private readonly Label statusLabel = new Label();

	private readonly RoundedPanel progressTrack = new RoundedPanel();

	private readonly RoundedPanel progressIndicator = new RoundedPanel();

	private readonly Timer animationTimer = new Timer
	{
		Interval = 18
	};

	private string statusBaseText = "Đang chuẩn bị dữ liệu";

	private DateTime shownAtUtc;

	private int animationTick;

	private bool isDisposed;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool Visible => overlayPanel.Visible;

	public StoreLoadingOverlay(Control host)
	{
		this.host = host;
		BuildShell();
		HookEvents();
		UpdateLayout();
	}

	public void Show(string title, string subtitle, string statusText)
	{
		if (!isDisposed && !host.IsDisposed)
		{
			titleLabel.Text = (string.IsNullOrWhiteSpace(title) ? "Đang tải dữ liệu" : title);
			subtitleLabel.Text = (string.IsNullOrWhiteSpace(subtitle) ? "Launcher đang chuẩn bị nội dung để bạn tiếp tục mượt hơn." : subtitle);
			statusBaseText = (string.IsNullOrWhiteSpace(statusText) ? "Đang chuẩn bị dữ liệu" : statusText);
			shownAtUtc = DateTime.UtcNow;
			animationTick = 0;
			UpdateStatusText();
			UpdateLayout();
			overlayPanel.Visible = true;
			overlayPanel.BringToFront();
			animationTimer.Start();
		}
	}

	public void UpdateStatus(string statusText)
	{
		if (!isDisposed)
		{
			statusBaseText = (string.IsNullOrWhiteSpace(statusText) ? "Đang chuẩn bị dữ liệu" : statusText);
			UpdateStatusText();
		}
	}

	public async Task RunAsync(string title, string subtitle, string statusText, Func<Task> action, int minimumVisibleMs = 420)
	{
		Show(title, subtitle, statusText);
		try
		{
			await action();
		}
		finally
		{
			await HideAsync(minimumVisibleMs);
		}
	}

	public async Task HideAsync(int minimumVisibleMs = 420)
	{
		if (!isDisposed && overlayPanel.Visible)
		{
			TimeSpan timeSpan = DateTime.UtcNow - shownAtUtc;
			TimeSpan timeSpan2 = TimeSpan.FromMilliseconds(Math.Max(0, minimumVisibleMs));
			if (timeSpan < timeSpan2)
			{
				await Task.Delay(timeSpan2 - timeSpan);
			}
			if (!isDisposed && !host.IsDisposed)
			{
				animationTimer.Stop();
				overlayPanel.Visible = false;
			}
		}
	}

	public void HideImmediate()
	{
		if (!isDisposed)
		{
			animationTimer.Stop();
			overlayPanel.Visible = false;
		}
	}

	public void Dispose()
	{
		if (!isDisposed)
		{
			isDisposed = true;
			animationTimer.Stop();
			animationTimer.Dispose();
			logoPictureBox.Image?.Dispose();
			overlayPanel.Dispose();
		}
	}

	private void BuildShell()
	{
		overlayPanel.Visible = false;
		overlayPanel.Dock = DockStyle.Fill;
		overlayPanel.Margin = Padding.Empty;
		overlayPanel.BackColor = Color.FromArgb(9, 13, 19);
		overlayPanel.Cursor = Cursors.AppStarting;
		StoreUiHelper.EnableDoubleBuffer(overlayPanel);
		cardPanel.SurfaceColor = Color.FromArgb(18, 24, 33);
		cardPanel.BorderColor = Color.FromArgb(58, 73, 94);
		cardPanel.BorderThickness = 1;
		cardPanel.CornerRadius = 28;
		cardPanel.Padding = new Padding(28, 24, 28, 24);
		logoPictureBox.BackColor = Color.Transparent;
		logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
		StoreUiHelper.LoadLogo(logoPictureBox);
		titleLabel.AutoSize = false;
		titleLabel.ForeColor = AppTheme.PrimaryText;
		titleLabel.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold, GraphicsUnit.Point);
		titleLabel.TextAlign = ContentAlignment.MiddleLeft;
		titleLabel.Text = "Đang tải dữ liệu";
		subtitleLabel.AutoSize = false;
		subtitleLabel.ForeColor = AppTheme.SecondaryText;
		subtitleLabel.Font = new Font("Segoe UI", 10.25f, FontStyle.Regular, GraphicsUnit.Point);
		subtitleLabel.TextAlign = ContentAlignment.TopLeft;
		subtitleLabel.Text = "Launcher đang chuẩn bị nội dung để bạn tiếp tục mượt hơn.";
		statusLabel.AutoSize = false;
		statusLabel.ForeColor = Color.FromArgb(157, 188, 255);
		statusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point);
		statusLabel.TextAlign = ContentAlignment.MiddleLeft;
		progressTrack.SurfaceColor = Color.FromArgb(24, 31, 43);
		progressTrack.BorderColor = Color.FromArgb(60, 77, 101);
		progressTrack.BorderThickness = 1;
		progressTrack.CornerRadius = 7;
		progressIndicator.SurfaceColor = Color.FromArgb(72, 122, 255);
		progressIndicator.BorderColor = Color.FromArgb(119, 160, 255);
		progressIndicator.BorderThickness = 0;
		progressIndicator.CornerRadius = 6;
		progressTrack.Controls.Add(progressIndicator);
		cardPanel.Controls.Add(logoPictureBox);
		cardPanel.Controls.Add(titleLabel);
		cardPanel.Controls.Add(subtitleLabel);
		cardPanel.Controls.Add(statusLabel);
		cardPanel.Controls.Add(progressTrack);
		overlayPanel.Controls.Add(cardPanel);
		host.Controls.Add(overlayPanel);
		overlayPanel.BringToFront();
	}

	private void HookEvents()
	{
		host.SizeChanged += Host_SizeChanged;
		overlayPanel.SizeChanged += delegate
		{
			UpdateLayout();
		};
		animationTimer.Tick += AnimationTimer_Tick;
	}

	private void Host_SizeChanged(object? sender, EventArgs e)
	{
		UpdateLayout();
	}

	private void AnimationTimer_Tick(object? sender, EventArgs e)
	{
		animationTick++;
		UpdateStatusText();
		UpdateProgressIndicatorLayout();
	}

	private void UpdateLayout()
	{
		if (isDisposed || host.IsDisposed)
		{
			return;
		}
		overlayPanel.Bounds = host.ClientRectangle;
		int width = overlayPanel.ClientSize.Width;
		int height = overlayPanel.ClientSize.Height;
		if (width > 0 && height > 0)
		{
			int width2 = Math.Min(Math.Max(380, width - 72), 540);
			if (width <= 420)
			{
				width2 = Math.Max(300, width - 32);
			}
			int val = ((width < 1220 || height < 820) ? 228 : 242);
			val = Math.Min(val, Math.Max(180, height - 32));
			cardPanel.Size = new Size(width2, val);
			cardPanel.Location = new Point(Math.Max(16, (width - cardPanel.Width) / 2), Math.Max(16, (height - cardPanel.Height) / 2));
			int left = cardPanel.Padding.Left;
			int top = cardPanel.Padding.Top;
			logoPictureBox.SetBounds(left, top, 50, 50);
			int num = logoPictureBox.Right + 16;
			int width3 = Math.Max(180, cardPanel.ClientSize.Width - num - left);
			int height2 = TextRenderer.MeasureText(titleLabel.Text, titleLabel.Font, new Size(width3, 0), TextFormatFlags.WordBreak).Height;
			titleLabel.SetBounds(num, top + 3, width3, Math.Max(34, height2));
			int y = Math.Max(logoPictureBox.Bottom, titleLabel.Bottom) + 18;
			int width4 = cardPanel.ClientSize.Width - left * 2;
			int height3 = TextRenderer.MeasureText(subtitleLabel.Text, subtitleLabel.Font, new Size(width4, 0), TextFormatFlags.WordBreak).Height;
			subtitleLabel.SetBounds(left, y, width4, Math.Max(42, height3));
			int y2 = subtitleLabel.Bottom + 18;
			statusLabel.SetBounds(left, y2, width4, 22);
			progressTrack.SetBounds(left, statusLabel.Bottom + 10, width4, 14);
			UpdateProgressIndicatorLayout();
			if (overlayPanel.Visible)
			{
				overlayPanel.BringToFront();
			}
		}
	}

	private void UpdateProgressIndicatorLayout()
	{
		if (progressTrack.ClientSize.Width > 8 && progressTrack.ClientSize.Height > 6)
		{
			int num = 2;
			int num2 = Math.Max(10, progressTrack.ClientSize.Width - num * 2);
			int num3 = Math.Max(72, Math.Min(144, num2 / 3));
			int num4 = Math.Max(1, num2 - num3);
			float num5 = (float)(animationTick % 48) / 47f;
			float num6 = ((num5 <= 0.5f) ? (num5 * 2f) : ((1f - num5) * 2f));
			int x = num + (int)Math.Round((float)num4 * num6);
			int height = Math.Max(6, progressTrack.ClientSize.Height - num * 2);
			progressIndicator.SetBounds(x, num, num3, height);
			progressIndicator.BringToFront();
		}
	}

	private void UpdateStatusText()
	{
		int count = animationTick / 10 % 4;
		statusLabel.Text = statusBaseText + new string('.', count);
	}
}
