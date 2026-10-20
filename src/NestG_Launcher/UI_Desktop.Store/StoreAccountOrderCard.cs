using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreAccountOrderCard : RoundedPanel
{
	private readonly DesktopAccountOrderResponse order;

	private readonly Func<DesktopAccountOrderResponse, Task>? openOrderAsync;

	private readonly Label orderCodeLabel;

	private readonly Label summaryLabel;

	private readonly Label statusLabel;

	private readonly Label createdAtLabel;

	private readonly Label totalAmountLabel;

	private readonly Button actionButton;

	private bool isHovered;

	public StoreAccountOrderCard(DesktopAccountOrderResponse order, Func<DesktopAccountOrderResponse, Task>? openOrderAsync = null)
	{
		this.order = order;
		this.openOrderAsync = openOrderAsync;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.Margin = new Padding(0, 0, 0, 14);
		base.Padding = new Padding(ScaleValue(18));
		base.Height = ScaleValue(140);
		MinimumSize = new Size(ScaleValue(520), ScaleValue(140));
		base.SurfaceColor = Color.FromArgb(18, 24, 33);
		base.BorderColor = AppTheme.CardBorder;
		base.BorderThickness = 1;
		base.CornerRadius = ScaleValue(16);
		orderCodeLabel = new Label
		{
			AutoSize = true,
			ForeColor = Color.FromArgb(132, 153, 182),
			Font = new Font("Segoe UI", 8.7f, FontStyle.Regular, GraphicsUnit.Point),
			Text = order.OrderCode
		};
		summaryLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.3f, FontStyle.Bold, GraphicsUnit.Point),
			Text = order.LineSummary,
			AutoEllipsis = true
		};
		statusLabel = new Label
		{
			AutoSize = false,
			Size = new Size(ScaleValue(136), ScaleValue(30)),
			BackColor = ResolveStatusBackColor(),
			ForeColor = ResolveStatusForeColor(),
			Font = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold, GraphicsUnit.Point),
			Text = order.StatusText.ToUpperInvariant(),
			TextAlign = ContentAlignment.MiddleCenter
		};
		createdAtLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = order.CreatedAtText,
			AutoEllipsis = true
		};
		totalAmountLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(255, 214, 137),
			Font = new Font("Segoe UI Semibold", 12.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = order.TotalAmountText,
			TextAlign = ContentAlignment.MiddleRight,
			AutoEllipsis = true
		};
		actionButton = new Button
		{
			Visible = !string.IsNullOrWhiteSpace(order.ActionUrl),
			Text = (string.IsNullOrWhiteSpace(order.ActionLabel) ? "Mở chi tiết" : order.ActionLabel),
			Width = ScaleValue(144),
			Height = ScaleValue(40),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(31, 42, 58),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point),
			Cursor = Cursors.Hand
		};
		actionButton.FlatAppearance.BorderSize = 1;
		actionButton.FlatAppearance.BorderColor = Color.FromArgb(84, 103, 129);
		actionButton.Click += async delegate
		{
			await HandleActionAsync();
		};
		base.Controls.Add(actionButton);
		base.Controls.Add(totalAmountLabel);
		base.Controls.Add(createdAtLabel);
		base.Controls.Add(statusLabel);
		base.Controls.Add(summaryLabel);
		base.Controls.Add(orderCodeLabel);
		base.MouseEnter += delegate
		{
			UpdateHover(hovered: true);
		};
		base.MouseLeave += delegate
		{
			UpdateHover(hovered: false);
		};
		base.Resize += delegate
		{
			LayoutContent();
		};
		AttachHover(this);
		LayoutContent();
	}

	private void LayoutContent()
	{
		int num = ScaleValue(18);
		int num2 = Math.Max(ScaleValue(320), base.Width - num * 2);
		int num3 = ScaleValue(192);
		orderCodeLabel.Location = new Point(num, num);
		summaryLabel.Location = new Point(num, orderCodeLabel.Bottom + ScaleValue(6));
		summaryLabel.Size = new Size(Math.Max(ScaleValue(240), num2 - ScaleValue(196)), ScaleValue(28));
		statusLabel.Location = new Point(base.Width - statusLabel.Width - num, num);
		createdAtLabel.Location = new Point(num, summaryLabel.Bottom + ScaleValue(14));
		createdAtLabel.Size = new Size(Math.Max(ScaleValue(220), num2 / 2), ScaleValue(22));
		totalAmountLabel.Location = new Point(base.Width - num3 - num, summaryLabel.Bottom + ScaleValue(10));
		totalAmountLabel.Size = new Size(num3, ScaleValue(28));
		actionButton.Location = new Point(base.Width - actionButton.Width - num, base.Height - actionButton.Height - num);
	}

	private async Task HandleActionAsync()
	{
		if (openOrderAsync != null)
		{
			await openOrderAsync(order);
		}
		else
		{
			OpenActionUrl();
		}
	}

	private void OpenActionUrl()
	{
		if (string.IsNullOrWhiteSpace(order.ActionUrl))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = order.ActionUrl,
				UseShellExecute = true
			});
		}
		catch
		{
			LauncherMessageBox.Show("Không mở được liên kết thanh toán lúc này.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private Color ResolveStatusBackColor()
	{
		return order.Status.ToLowerInvariant() switch
		{
			"paid" => Color.FromArgb(34, 72, 58), 
			"pending_payment" => Color.FromArgb(78, 56, 28), 
			"processing" => Color.FromArgb(56, 58, 82), 
			"failed" => Color.FromArgb(88, 37, 42), 
			"cancelled" => Color.FromArgb(68, 44, 47), 
			"refunded" => Color.FromArgb(52, 58, 74), 
			_ => Color.FromArgb(48, 55, 70), 
		};
	}

	private Color ResolveStatusForeColor()
	{
		return order.Status.ToLowerInvariant() switch
		{
			"paid" => Color.FromArgb(188, 238, 205), 
			"pending_payment" => Color.FromArgb(255, 223, 162), 
			"processing" => Color.FromArgb(202, 208, 255), 
			"failed" => Color.FromArgb(255, 199, 203), 
			"cancelled" => Color.FromArgb(232, 204, 206), 
			"refunded" => Color.FromArgb(208, 219, 238), 
			_ => AppTheme.PrimaryText, 
		};
	}

	private void AttachHover(Control control)
	{
		control.MouseEnter += delegate
		{
			UpdateHover(hovered: true);
		};
		control.MouseLeave += delegate
		{
			UpdateHover(hovered: false);
		};
		foreach (Control control2 in control.Controls)
		{
			AttachHover(control2);
		}
	}

	private void UpdateHover(bool hovered)
	{
		isHovered = hovered;
		base.SurfaceColor = (isHovered ? Color.FromArgb(22, 30, 41) : Color.FromArgb(18, 24, 33));
		base.BorderColor = (isHovered ? Color.FromArgb(93, 115, 147) : AppTheme.CardBorder);
		Invalidate();
	}

	private int ScaleValue(int value)
	{
		return (int)Math.Round((float)value * ((float)base.DeviceDpi / 96f));
	}
}
