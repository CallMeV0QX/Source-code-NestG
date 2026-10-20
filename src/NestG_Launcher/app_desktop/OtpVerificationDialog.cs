using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Desktop;
using UI_Desktop.Auth;
using app_desktop.Models;

namespace app_desktop;

internal sealed class OtpVerificationDialog : Form
{
	private readonly bool persistSession;

	private OtpChallengeInfo challenge;

	private bool busy;

	private readonly RoundedPanel cardPanel = new RoundedPanel();

	private readonly Label titleLabel = new Label();

	private readonly Label subtitleLabel = new Label();

	private readonly Label messageLabel = new Label();

	private readonly Label emailCaptionLabel = new Label();

	private readonly Label emailValueLabel = new Label();

	private readonly Label expiryLabel = new Label();

	private readonly RoundedPanel otpFieldPanel = new RoundedPanel();

	private readonly TextBox otpTextBox = new TextBox();

	private readonly GradientButton verifyButton = new GradientButton();

	private readonly LinkLabel resendLinkLabel = new LinkLabel();

	private readonly PictureBox logoPictureBox = new PictureBox();

	public OtpVerificationDialog(OtpChallengeInfo challenge, bool persistSession)
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
		base.ClientSize = new System.Drawing.Size(520, 500);
		this.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "OtpVerificationDialog";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Xác thực email";
		this.cardPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardPanel.Padding = new System.Windows.Forms.Padding(28);
		this.logoPictureBox.Location = new System.Drawing.Point(28, 28);
		this.logoPictureBox.Size = new System.Drawing.Size(36, 36);
		this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.titleLabel.AutoSize = true;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 22f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.titleLabel.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.titleLabel.Location = new System.Drawing.Point(28, 80);
		this.titleLabel.Text = "Xác thực email";
		this.subtitleLabel.AutoSize = true;
		this.subtitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 11f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.subtitleLabel.ForeColor = UI_Desktop.AppTheme.Accent;
		this.subtitleLabel.Location = new System.Drawing.Point(28, 126);
		this.subtitleLabel.Text = "Nhập mã OTP để hoàn tất đăng nhập";
		this.messageLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.messageLabel.Location = new System.Drawing.Point(28, 166);
		this.messageLabel.Size = new System.Drawing.Size(458, 48);
		this.emailCaptionLabel.AutoSize = true;
		this.emailCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.emailCaptionLabel.ForeColor = UI_Desktop.AppTheme.SecondaryText;
		this.emailCaptionLabel.Location = new System.Drawing.Point(28, 226);
		this.emailCaptionLabel.Text = "Email xác thực";
		this.emailValueLabel.AutoSize = true;
		this.emailValueLabel.Font = new System.Drawing.Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.emailValueLabel.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.emailValueLabel.Location = new System.Drawing.Point(28, 252);
		this.expiryLabel.AutoSize = true;
		this.expiryLabel.Font = new System.Drawing.Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.expiryLabel.ForeColor = UI_Desktop.AppTheme.MutedText;
		this.expiryLabel.Location = new System.Drawing.Point(28, 278);
		this.otpFieldPanel.Location = new System.Drawing.Point(28, 318);
		this.otpFieldPanel.Size = new System.Drawing.Size(458, 54);
		this.otpFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.otpFieldPanel.Controls.Add(this.otpTextBox);
		this.otpTextBox.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		this.otpTextBox.PlaceholderText = "Nhập mã OTP";
		this.otpTextBox.MaxLength = 12;
		this.verifyButton.Location = new System.Drawing.Point(28, 402);
		this.verifyButton.Size = new System.Drawing.Size(320, 56);
		this.verifyButton.Text = "Xác thực";
		this.verifyButton.Click += new System.EventHandler(verifyButton_Click);
		this.resendLinkLabel.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.resendLinkLabel.AutoSize = true;
		this.resendLinkLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.resendLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.resendLinkLabel.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.resendLinkLabel.Location = new System.Drawing.Point(368, 418);
		this.resendLinkLabel.Text = "Gửi lại OTP";
		this.resendLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(resendLinkLabel_LinkClicked);
		this.cardPanel.Controls.Add(this.logoPictureBox);
		this.cardPanel.Controls.Add(this.titleLabel);
		this.cardPanel.Controls.Add(this.subtitleLabel);
		this.cardPanel.Controls.Add(this.messageLabel);
		this.cardPanel.Controls.Add(this.emailCaptionLabel);
		this.cardPanel.Controls.Add(this.emailValueLabel);
		this.cardPanel.Controls.Add(this.expiryLabel);
		this.cardPanel.Controls.Add(this.otpFieldPanel);
		this.cardPanel.Controls.Add(this.verifyButton);
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
		base.AcceptButton = verifyButton;
		base.Shown += delegate
		{
			otpTextBox.Focus();
		};
	}

	private void UpdateChallengeSummary()
	{
		messageLabel.Text = (challenge.EmailDispatched ? challenge.Message : (challenge.Message + " Bạn có thể bấm gửi lại OTP sau."));
		emailValueLabel.Text = challenge.Email;
		expiryLabel.Text = $"OTP hết hạn lúc {challenge.ExpiresAtUtc.ToLocalTime():HH:mm dd/MM/yyyy}";
	}

	private async void verifyButton_Click(object? sender, EventArgs e)
	{
		if (busy)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(otpTextBox.Text))
		{
			LauncherMessageBox.Show(this, "Vui lòng nhập mã OTP.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		await RunBusyAsync("Đang xác thực...", async delegate
		{
			AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.VerifyEmailOtpAsync(challenge.Email, otpTextBox.Text.Trim(), persistSession);
			if (authActionResult.HasSession)
			{
				base.DialogResult = DialogResult.OK;
				Close();
			}
			else
			{
				LauncherMessageBox.Show(this, authActionResult.Message, "Không thể xác thực", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
			AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ResendEmailOtpAsync(challenge.Email);
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
		string originalText = verifyButton.Text;
		busy = true;
		verifyButton.Enabled = false;
		resendLinkLabel.Enabled = false;
		base.UseWaitCursor = true;
		verifyButton.Text = loadingText;
		try
		{
			await action();
		}
		finally
		{
			busy = false;
			verifyButton.Enabled = true;
			resendLinkLabel.Enabled = true;
			base.UseWaitCursor = false;
			verifyButton.Text = originalText;
		}
	}
}
