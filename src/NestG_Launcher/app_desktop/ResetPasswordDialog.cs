using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Desktop;
using UI_Desktop.Auth;
using app_desktop.Models;

namespace app_desktop;

internal sealed class ResetPasswordDialog : Form
{
	private readonly bool persistSession;

	private OtpChallengeInfo challenge;

	private bool busy;

	private readonly RoundedPanel cardPanel = new RoundedPanel();

	private readonly PictureBox logoPictureBox = new PictureBox();

	private readonly Label titleLabel = new Label();

	private readonly Label subtitleLabel = new Label();

	private readonly Label messageLabel = new Label();

	private readonly Label otpCaptionLabel = new Label();

	private readonly RoundedPanel otpFieldPanel = new RoundedPanel();

	private readonly TextBox otpTextBox = new TextBox();

	private readonly Label passwordCaptionLabel = new Label();

	private readonly RoundedPanel passwordFieldPanel = new RoundedPanel();

	private readonly TextBox passwordTextBox = new TextBox();

	private readonly Label confirmCaptionLabel = new Label();

	private readonly RoundedPanel confirmFieldPanel = new RoundedPanel();

	private readonly TextBox confirmPasswordTextBox = new TextBox();

	private readonly Label expiryLabel = new Label();

	private readonly GradientButton resetButton = new GradientButton();

	private readonly LinkLabel resendLinkLabel = new LinkLabel();

	public ResetPasswordDialog(OtpChallengeInfo challenge, bool persistSession)
	{
		this.challenge = challenge;
		this.persistSession = persistSession;
		InitializeComponent();
		ConfigureView();
		UpdateChallengeSummary();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		this.BackColor = UI_Desktop.AppTheme.WindowBackground;
		base.ClientSize = new System.Drawing.Size(560, 680);
		this.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "ResetPasswordDialog";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Đặt lại mật khẩu";
		this.cardPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardPanel.Padding = new System.Windows.Forms.Padding(28);
		this.logoPictureBox.Location = new System.Drawing.Point(28, 28);
		this.logoPictureBox.Size = new System.Drawing.Size(36, 36);
		this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.titleLabel.AutoSize = true;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 22f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.titleLabel.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.titleLabel.Location = new System.Drawing.Point(28, 80);
		this.titleLabel.Text = "Đặt lại mật khẩu";
		this.subtitleLabel.AutoSize = true;
		this.subtitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 11f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.subtitleLabel.ForeColor = UI_Desktop.AppTheme.Accent;
		this.subtitleLabel.Location = new System.Drawing.Point(28, 126);
		this.subtitleLabel.Text = "Nhập OTP và mật khẩu mới";
		this.messageLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.messageLabel.Location = new System.Drawing.Point(28, 164);
		this.messageLabel.Size = new System.Drawing.Size(490, 48);
		this.otpCaptionLabel.AutoSize = true;
		this.otpCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.otpCaptionLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.otpCaptionLabel.Location = new System.Drawing.Point(28, 230);
		this.otpCaptionLabel.Text = "Mã OTP";
		this.otpFieldPanel.Location = new System.Drawing.Point(28, 258);
		this.otpFieldPanel.Size = new System.Drawing.Size(490, 54);
		this.otpFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.otpFieldPanel.Controls.Add(this.otpTextBox);
		this.otpTextBox.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.otpTextBox.PlaceholderText = "Nhập mã OTP từ email";
		this.otpTextBox.MaxLength = 12;
		this.passwordCaptionLabel.AutoSize = true;
		this.passwordCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.passwordCaptionLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.passwordCaptionLabel.Location = new System.Drawing.Point(28, 330);
		this.passwordCaptionLabel.Text = "Mật khẩu mới";
		this.passwordFieldPanel.Location = new System.Drawing.Point(28, 358);
		this.passwordFieldPanel.Size = new System.Drawing.Size(490, 54);
		this.passwordFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.passwordFieldPanel.Controls.Add(this.passwordTextBox);
		this.passwordTextBox.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.passwordTextBox.PlaceholderText = "Tối thiểu 6 ký tự";
		this.passwordTextBox.UseSystemPasswordChar = true;
		this.confirmCaptionLabel.AutoSize = true;
		this.confirmCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.confirmCaptionLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.confirmCaptionLabel.Location = new System.Drawing.Point(28, 430);
		this.confirmCaptionLabel.Text = "Nhập lại mật khẩu";
		this.confirmFieldPanel.Location = new System.Drawing.Point(28, 458);
		this.confirmFieldPanel.Size = new System.Drawing.Size(490, 54);
		this.confirmFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.confirmFieldPanel.Controls.Add(this.confirmPasswordTextBox);
		this.confirmPasswordTextBox.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.confirmPasswordTextBox.PlaceholderText = "Nhập lại mật khẩu mới";
		this.confirmPasswordTextBox.UseSystemPasswordChar = true;
		this.expiryLabel.AutoSize = true;
		this.expiryLabel.Font = new System.Drawing.Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.expiryLabel.ForeColor = UI_Desktop.AppTheme.MutedText;
		this.expiryLabel.Location = new System.Drawing.Point(28, 532);
		this.resetButton.Location = new System.Drawing.Point(28, 574);
		this.resetButton.Size = new System.Drawing.Size(340, 56);
		this.resetButton.Text = "Đặt lại mật khẩu";
		this.resetButton.Click += new System.EventHandler(resetButton_Click);
		this.resendLinkLabel.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.resendLinkLabel.AutoSize = true;
		this.resendLinkLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.resendLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.resendLinkLabel.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.resendLinkLabel.Location = new System.Drawing.Point(386, 590);
		this.resendLinkLabel.Text = "Gửi lại OTP";
		this.resendLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(resendLinkLabel_LinkClicked);
		this.cardPanel.Controls.Add(this.logoPictureBox);
		this.cardPanel.Controls.Add(this.titleLabel);
		this.cardPanel.Controls.Add(this.subtitleLabel);
		this.cardPanel.Controls.Add(this.messageLabel);
		this.cardPanel.Controls.Add(this.otpCaptionLabel);
		this.cardPanel.Controls.Add(this.otpFieldPanel);
		this.cardPanel.Controls.Add(this.passwordCaptionLabel);
		this.cardPanel.Controls.Add(this.passwordFieldPanel);
		this.cardPanel.Controls.Add(this.confirmCaptionLabel);
		this.cardPanel.Controls.Add(this.confirmFieldPanel);
		this.cardPanel.Controls.Add(this.expiryLabel);
		this.cardPanel.Controls.Add(this.resetButton);
		this.cardPanel.Controls.Add(this.resendLinkLabel);
		base.Controls.Add(this.cardPanel);
		base.ResumeLayout(false);
	}

	private void ConfigureView()
	{
		AppIconProvider.ApplyIcon(this);
		AuthUiHelper.EnableDoubleBuffer(this);
		AuthUiHelper.EnableDoubleBuffer(cardPanel);
		AuthUiHelper.LoadLogo(logoPictureBox);
		AuthUiHelper.ConfigureInput(otpFieldPanel, otpTextBox);
		AuthUiHelper.ConfigureInput(passwordFieldPanel, passwordTextBox);
		AuthUiHelper.ConfigureInput(confirmFieldPanel, confirmPasswordTextBox);
		base.AcceptButton = resetButton;
		base.Shown += delegate
		{
			otpTextBox.Focus();
		};
	}

	private void UpdateChallengeSummary()
	{
		messageLabel.Text = (challenge.EmailDispatched ? challenge.Message : (challenge.Message + " Bạn có thể tạo mã mới từ nút gửi lại OTP."));
		expiryLabel.Text = $"OTP cho {challenge.Email} hết hạn lúc {challenge.ExpiresAtUtc.ToLocalTime():HH:mm dd/MM/yyyy}";
	}

	private async void resetButton_Click(object? sender, EventArgs e)
	{
		if (busy)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(otpTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text) || string.IsNullOrWhiteSpace(confirmPasswordTextBox.Text))
		{
			LauncherMessageBox.Show(this, "Vui lòng nhập đầy đủ OTP và mật khẩu mới.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		if (passwordTextBox.Text.Length < 6)
		{
			LauncherMessageBox.Show(this, "Mật khẩu mới phải có ít nhất 6 ký tự.", "Mật khẩu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		if (!string.Equals(passwordTextBox.Text, confirmPasswordTextBox.Text, StringComparison.Ordinal))
		{
			LauncherMessageBox.Show(this, "Mật khẩu nhập lại chưa khớp.", "Sai xác nhận", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		await RunBusyAsync("Đang đặt lại...", async delegate
		{
			AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ResetPasswordAsync(challenge.Email, otpTextBox.Text.Trim(), passwordTextBox.Text, persistSession);
			if (authActionResult.HasSession)
			{
				base.DialogResult = DialogResult.OK;
				Close();
			}
			else
			{
				LauncherMessageBox.Show(this, authActionResult.Message, "Không thể đặt lại mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		});
	}

	private async void resendLinkLabel_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
	{
		if (busy)
		{
			return;
		}
		await RunBusyAsync("Đang gửi lại...", async delegate
		{
			AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ForgotPasswordAsync(challenge.Email);
			if (!authActionResult.RequiresChallenge || authActionResult.Challenge == null)
			{
				LauncherMessageBox.Show(this, authActionResult.Message, "Không thể gửi lại OTP", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			else
			{
				challenge = authActionResult.Challenge;
				UpdateChallengeSummary();
				LauncherMessageBox.Show(this, challenge.Message, "OTP mới đã được tạo", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		});
	}

	private async Task RunBusyAsync(string loadingText, Func<Task> action)
	{
		string originalText = resetButton.Text;
		busy = true;
		resetButton.Enabled = false;
		resendLinkLabel.Enabled = false;
		base.UseWaitCursor = true;
		resetButton.Text = loadingText;
		try
		{
			await action();
		}
		finally
		{
			busy = false;
			resetButton.Enabled = true;
			resendLinkLabel.Enabled = true;
			base.UseWaitCursor = false;
			resetButton.Text = originalText;
		}
	}
}
