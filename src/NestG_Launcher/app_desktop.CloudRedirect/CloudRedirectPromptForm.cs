using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Desktop;
using app_desktop.Services;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.CloudRedirect;

internal sealed class CloudRedirectPromptForm : Form
{
	private readonly CloudRedirectIntegrationService integrationService;

	private readonly Label titleLabel = new Label();

	private readonly Label descriptionLabel = new Label();

	private readonly Label statusLabel = new Label();

	private readonly ProgressBar progressBar = new ProgressBar();

	private readonly Button fixButton = new Button();

	private readonly Button closeButton = new Button();

	public CloudRedirectPromptForm(StorefrontApiClient storefrontApiClient)
	{
		integrationService = new CloudRedirectIntegrationService(storefrontApiClient);
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		this.Text = "Sửa lỗi Steam Cloud";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.ClientSize = new System.Drawing.Size(520, 300);
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
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56f));
		this.titleLabel.AutoSize = true;
		this.titleLabel.Margin = System.Windows.Forms.Padding.Empty;
		this.titleLabel.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 17f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.titleLabel.Text = "Sửa lỗi Steam Cloud khi mở Steam";
		this.descriptionLabel.AutoSize = false;
		this.descriptionLabel.Dock = System.Windows.Forms.DockStyle.Top;
		this.descriptionLabel.Height = 72;
		this.descriptionLabel.Margin = new System.Windows.Forms.Padding(0, 12, 0, 0);
		this.descriptionLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.descriptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.descriptionLabel.Text = "Launcher sẽ kích hoạt CloudRedirect, ghi cấu hình local folder và mở lại Steam để hạn chế lỗi Steam Cloud.";
		this.statusLabel.AutoSize = false;
		this.statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.statusLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
		this.statusLabel.ForeColor = UI_Desktop.AppTheme.MutedText;
		this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 9.3f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.statusLabel.Text = "Bấm nút bên dưới để bắt đầu. Steam có thể bị đóng và mở lại trong quá trình sửa lỗi.";
		this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
		this.progressBar.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
		this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
		this.progressBar.MarqueeAnimationSpeed = 0;
		this.progressBar.Visible = false;
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 128f));
		tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 196f));
		tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.closeButton.Text = "Bỏ qua";
		this.closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.closeButton.Dock = System.Windows.Forms.DockStyle.Fill;
		this.closeButton.Margin = new System.Windows.Forms.Padding(0, 8, 12, 0);
		app_desktop.CloudRedirect.CloudRedirectPromptForm.ConfigureButton(this.closeButton, System.Drawing.Color.FromArgb(21, 29, 40), UI_Desktop.AppTheme.CardBorder, UI_Desktop.AppTheme.PrimaryText);
		this.fixButton.Text = "Sửa lỗi Steam Cloud";
		this.fixButton.Dock = System.Windows.Forms.DockStyle.Fill;
		this.fixButton.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
		app_desktop.CloudRedirect.CloudRedirectPromptForm.ConfigureButton(this.fixButton, UI_Desktop.AppTheme.Accent, UI_Desktop.AppTheme.Accent, System.Drawing.Color.White);
		this.fixButton.Click += async delegate
		{
			await this.RunCloudRedirectFixAsync();
		};
		tableLayoutPanel2.Controls.Add(this.closeButton, 1, 0);
		tableLayoutPanel2.Controls.Add(this.fixButton, 2, 0);
		tableLayoutPanel.Controls.Add(this.titleLabel, 0, 0);
		tableLayoutPanel.Controls.Add(this.descriptionLabel, 0, 1);
		tableLayoutPanel.Controls.Add(this.statusLabel, 0, 2);
		tableLayoutPanel.Controls.Add(this.progressBar, 0, 3);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 4);
		base.Controls.Add(tableLayoutPanel);
		base.AcceptButton = this.fixButton;
		base.CancelButton = this.closeButton;
		base.ResumeLayout(false);
	}

	private async Task RunCloudRedirectFixAsync()
	{
		SetBusy(isBusy: true);
		statusLabel.ForeColor = AppTheme.SecondaryText;
		statusLabel.Text = "Đang chuẩn bị sửa lỗi Steam Cloud...";
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			Progress<CloudRedirectInstallProgress> progress = new Progress<CloudRedirectInstallProgress>(delegate(CloudRedirectInstallProgress item)
			{
				statusLabel.Text = item.Message;
			});
			CloudRedirectInstallResult cloudRedirectInstallResult = await integrationService.InstallAsync(text ?? string.Empty, progress);
			statusLabel.ForeColor = (cloudRedirectInstallResult.Succeeded ? AppTheme.SuccessText : Color.FromArgb(255, 163, 178));
			statusLabel.Text = (string.IsNullOrWhiteSpace(cloudRedirectInstallResult.LogFilePath) ? cloudRedirectInstallResult.Message : (cloudRedirectInstallResult.Message + "\nLog: " + cloudRedirectInstallResult.LogFilePath));
			if (cloudRedirectInstallResult.Succeeded)
			{
				fixButton.Enabled = false;
				fixButton.Text = "Đã kích hoạt";
				closeButton.Text = "Đóng";
				closeButton.DialogResult = DialogResult.OK;
				base.AcceptButton = closeButton;
			}
			else
			{
				fixButton.Enabled = true;
			}
		}
		catch (Exception ex)
		{
			statusLabel.ForeColor = Color.FromArgb(255, 163, 178);
			statusLabel.Text = ex.Message;
			fixButton.Enabled = true;
		}
		finally
		{
			SetBusy(isBusy: false);
		}
	}

	private void SetBusy(bool isBusy)
	{
		base.UseWaitCursor = isBusy;
		progressBar.Visible = isBusy;
		progressBar.MarqueeAnimationSpeed = (isBusy ? 30 : 0);
		closeButton.Enabled = !isBusy;
		fixButton.Enabled = !isBusy && fixButton.Text != "Đã kích hoạt";
	}

	private static void ConfigureButton(Button button, Color backColor, Color borderColor, Color foreColor)
	{
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = borderColor;
		button.BackColor = backColor;
		button.ForeColor = foreColor;
		button.Cursor = Cursors.Hand;
		button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point);
	}
}
