using System;
using System.Drawing;
using System.Windows.Forms;
using UI_Desktop;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.CloudRedirect;

internal sealed class CloudRedirectManagementForm : Form
{
	private readonly CloudRedirectRuntimeStatus status;

	internal CloudRedirectManagementAction SelectedAction { get; private set; }

	internal CloudRedirectManagementForm(CloudRedirectRuntimeStatus status)
	{
		this.status = status;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		this.Text = "Quản lý CloudRedirect";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.ClientSize = new System.Drawing.Size(680, 390);
		this.BackColor = UI_Desktop.AppTheme.WindowBackground;
		this.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		UI_Desktop.AppIconProvider.ApplyIcon(this);
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 5,
			Padding = new System.Windows.Forms.Padding(24),
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58f));
		System.Windows.Forms.Label control = new System.Windows.Forms.Label
		{
			AutoSize = true,
			Text = "BetterSteamTools + CloudRedirect",
			ForeColor = UI_Desktop.AppTheme.PrimaryText,
			Font = new System.Drawing.Font("Segoe UI Semibold", 17f, System.Drawing.FontStyle.Bold)
		};
		System.Windows.Forms.Label control2 = new System.Windows.Forms.Label
		{
			AutoSize = true,
			MaximumSize = new System.Drawing.Size(620, 0),
			Margin = new System.Windows.Forms.Padding(0, 12, 0, 0),
			Text = this.status.Message,
			ForeColor = (this.status.IsEnabled ? UI_Desktop.AppTheme.SuccessText : System.Drawing.Color.FromArgb(255, 190, 100)),
			Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold)
		};
		System.Windows.Forms.Label control3 = new System.Windows.Forms.Label
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			Margin = new System.Windows.Forms.Padding(0, 16, 0, 0),
			ForeColor = UI_Desktop.AppTheme.SecondaryText,
			Text = this.BuildDetails(),
			Font = new System.Drawing.Font("Segoe UI", 10f)
		};
		System.Windows.Forms.Label control4 = new System.Windows.Forms.Label
		{
			AutoSize = true,
			MaximumSize = new System.Drawing.Size(620, 0),
			Margin = new System.Windows.Forms.Padding(0, 12, 0, 10),
			ForeColor = System.Drawing.Color.FromArgb(255, 190, 100),
			Text = "CloudRedirect là tính năng thử nghiệm. Hãy sao lưu save game trước khi Repair, Disable hoặc Rollback. Backup của launcher chỉ bảo vệ DLL và file cấu hình, không thay thế backup save."
		};
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			ColumnCount = 4,
			RowCount = 1,
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130f));
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130f));
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130f));
		System.Windows.Forms.Button button = this.CreateButton("Đóng", app_desktop.CloudRedirect.CloudRedirectManagementAction.None, false);
		button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		System.Windows.Forms.Button button2 = this.CreateButton("Rollback", app_desktop.CloudRedirect.CloudRedirectManagementAction.Rollback, false);
		button2.Enabled = this.status.CanRollback;
		System.Windows.Forms.Button button3 = this.CreateButton("Disable", app_desktop.CloudRedirect.CloudRedirectManagementAction.Disable, false);
		button3.Enabled = this.status.RuntimeInstalled && this.status.IsEnabled;
		System.Windows.Forms.Button button4 = this.CreateButton("Repair", app_desktop.CloudRedirect.CloudRedirectManagementAction.Repair, true);
		button4.Enabled = this.status.SteamFound;
		tableLayoutPanel2.Controls.Add(button, 0, 0);
		tableLayoutPanel2.Controls.Add(button2, 1, 0);
		tableLayoutPanel2.Controls.Add(button3, 2, 0);
		tableLayoutPanel2.Controls.Add(button4, 3, 0);
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(control2, 0, 1);
		tableLayoutPanel.Controls.Add(control3, 0, 2);
		tableLayoutPanel.Controls.Add(control4, 0, 3);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 4);
		base.Controls.Add(tableLayoutPanel);
		base.CancelButton = button;
	}

	private string BuildDetails()
	{
		string value = (string.IsNullOrWhiteSpace(status.Provider) ? "chưa cấu hình" : status.Provider);
		string value2 = (string.IsNullOrWhiteSpace(status.SteamDirectory) ? "không tìm thấy" : status.SteamDirectory);
		return $"Steam: {value2}{Environment.NewLine}Runtime: {(status.RuntimeInstalled ? "đã cài" : "chưa cài đủ")}{Environment.NewLine}Cloud host: {(status.IsEnabled ? "đang bật" : "đang tắt")}{Environment.NewLine}Provider: {value} ({(status.ProviderReady ? "sẵn sàng" : "cần cấu hình")}){Environment.NewLine}Rollback: {(status.CanRollback ? "có transaction khả dụng" : "chưa có backup khả dụng")}";
	}

	private Button CreateButton(string text, CloudRedirectManagementAction action, bool primary)
	{
		Button button = new Button
		{
			Text = text,
			Dock = DockStyle.Fill,
			Margin = new Padding(8, 8, 0, 0),
			FlatStyle = FlatStyle.Flat,
			BackColor = (primary ? AppTheme.Accent : Color.FromArgb(21, 29, 40)),
			ForeColor = AppTheme.PrimaryText,
			Cursor = Cursors.Hand
		};
		button.FlatAppearance.BorderColor = (primary ? AppTheme.Accent : AppTheme.CardBorder);
		button.FlatAppearance.BorderSize = 1;
		if (action != 0)
		{
			button.Click += delegate
			{
				SelectedAction = action;
				base.DialogResult = DialogResult.OK;
				Close();
			};
		}
		return button;
	}
}
