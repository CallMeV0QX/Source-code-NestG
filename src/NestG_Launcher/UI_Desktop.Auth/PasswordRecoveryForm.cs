using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop;
using app_desktop.Models;

namespace UI_Desktop.Auth;

internal class PasswordRecoveryForm : Form
{
	private bool isSubmitting;

	private IContainer components;

	private TableLayoutPanel rootLayout;

	private RoundedPanel heroPanel;

	private PictureBox heroLogoPictureBox;

	private Label heroAppNameLabel;

	private Label heroMetaLabel;

	private Label heroBadgeLabel;

	private Label heroTitleLabel;

	private Label heroDescriptionLabel;

	private Label heroBulletOneLabel;

	private Label heroBulletTwoLabel;

	private Label heroBulletThreeLabel;

	private RoundedPanel cardPanel;

	private PictureBox cardLogoPictureBox;

	private Label cardAppNameLabel;

	private Label cardMetaLabel;

	private Label titleLabel;

	private Label subtitleLabel;

	private Label descriptionLabel;

	private Label emailCaptionLabel;

	private RoundedPanel emailFieldPanel;

	private TextBox emailTextBox;

	private GradientButton sendCodeButton;

	private LinkLabel backToLoginLinkLabel;

	public PasswordRecoveryForm()
	{
		InitializeComponent();
		ConfigureView();
		BindEvents();
	}

	private void ConfigureView()
	{
		AuthUiHelper.ApplyWindowIcon(this);
		AuthUiHelper.EnableDoubleBuffer(this);
		AuthUiHelper.EnableDoubleBuffer(heroPanel);
		AuthUiHelper.EnableDoubleBuffer(cardPanel);
		AuthUiHelper.LoadLogo(heroLogoPictureBox);
		AuthUiHelper.LoadLogo(cardLogoPictureBox);
		AuthUiHelper.ConfigureInput(emailFieldPanel, emailTextBox);
		base.AcceptButton = sendCodeButton;
		UpdateResponsiveLayout();
	}

	private void BindEvents()
	{
		base.Resize += delegate
		{
			UpdateResponsiveLayout();
		};
		base.Shown += delegate
		{
			emailTextBox.Focus();
		};
	}

	private void UpdateResponsiveLayout()
	{
		AuthUiHelper.UpdateResponsiveLayout(this, rootLayout, heroPanel);
	}

	private async void sendCodeButton_Click(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(emailTextBox.Text))
		{
			LauncherMessageBox.Show("Vui lòng nhập địa chỉ email để nhận mã xác thực.", "Thiếu email", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		else
		{
			if (isSubmitting)
			{
				return;
			}
			await RunBusyAsync("Đang gửi OTP...", async delegate
			{
				AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ForgotPasswordAsync(emailTextBox.Text.Trim());
				if (!authActionResult.RequiresChallenge || authActionResult.Challenge == null)
				{
					LauncherMessageBox.Show(this, authActionResult.Message, "Không thể gửi OTP", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return;
				}
				using ResetPasswordDialog resetPasswordDialog = new ResetPasswordDialog(authActionResult.Challenge, DesktopAppContext.Settings.RememberSessionByDefault);
				if (resetPasswordDialog.ShowDialog(this) == DialogResult.OK)
				{
					LauncherMessageBox.Show(this, "Mật khẩu đã được cập nhật và phiên đăng nhập mới đã được tạo.", "Khôi phục thành công", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					AuthUiHelper.Navigate(this, DesktopAppContext.CreateStorefrontForm());
				}
			});
		}
	}

	private void backToLoginLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		AuthUiHelper.Navigate(this, DesktopAppContext.CreateLoginForm());
	}

	private async Task RunBusyAsync(string loadingText, Func<Task> action)
	{
		string originalText = sendCodeButton.Text;
		isSubmitting = true;
		sendCodeButton.Enabled = false;
		base.UseWaitCursor = true;
		sendCodeButton.Text = loadingText;
		try
		{
			await action();
		}
		finally
		{
			isSubmitting = false;
			sendCodeButton.Enabled = true;
			base.UseWaitCursor = false;
			sendCodeButton.Text = originalText;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.rootLayout = new System.Windows.Forms.TableLayoutPanel();
		this.heroPanel = new UI_Desktop.RoundedPanel();
		this.heroBulletThreeLabel = new System.Windows.Forms.Label();
		this.heroBulletTwoLabel = new System.Windows.Forms.Label();
		this.heroBulletOneLabel = new System.Windows.Forms.Label();
		this.heroDescriptionLabel = new System.Windows.Forms.Label();
		this.heroTitleLabel = new System.Windows.Forms.Label();
		this.heroBadgeLabel = new System.Windows.Forms.Label();
		this.heroMetaLabel = new System.Windows.Forms.Label();
		this.heroAppNameLabel = new System.Windows.Forms.Label();
		this.heroLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.cardPanel = new UI_Desktop.RoundedPanel();
		this.backToLoginLinkLabel = new System.Windows.Forms.LinkLabel();
		this.sendCodeButton = new UI_Desktop.GradientButton();
		this.emailFieldPanel = new UI_Desktop.RoundedPanel();
		this.emailTextBox = new System.Windows.Forms.TextBox();
		this.emailCaptionLabel = new System.Windows.Forms.Label();
		this.descriptionLabel = new System.Windows.Forms.Label();
		this.subtitleLabel = new System.Windows.Forms.Label();
		this.titleLabel = new System.Windows.Forms.Label();
		this.cardMetaLabel = new System.Windows.Forms.Label();
		this.cardAppNameLabel = new System.Windows.Forms.Label();
		this.cardLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.rootLayout.SuspendLayout();
		this.heroPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).BeginInit();
		this.cardPanel.SuspendLayout();
		this.emailFieldPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.cardLogoPictureBox).BeginInit();
		base.SuspendLayout();
		this.rootLayout.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		this.rootLayout.ColumnCount = 2;
		this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 43f));
		this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 57f));
		this.rootLayout.Controls.Add(this.heroPanel, 0, 0);
		this.rootLayout.Controls.Add(this.cardPanel, 1, 0);
		this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rootLayout.Location = new System.Drawing.Point(0, 0);
		this.rootLayout.Name = "rootLayout";
		this.rootLayout.Padding = new System.Windows.Forms.Padding(24);
		this.rootLayout.RowCount = 1;
		this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.rootLayout.Size = new System.Drawing.Size(1264, 761);
		this.rootLayout.TabIndex = 0;
		this.heroPanel.BackColor = System.Drawing.Color.Transparent;
		this.heroPanel.Controls.Add(this.heroBulletThreeLabel);
		this.heroPanel.Controls.Add(this.heroBulletTwoLabel);
		this.heroPanel.Controls.Add(this.heroBulletOneLabel);
		this.heroPanel.Controls.Add(this.heroDescriptionLabel);
		this.heroPanel.Controls.Add(this.heroTitleLabel);
		this.heroPanel.Controls.Add(this.heroBadgeLabel);
		this.heroPanel.Controls.Add(this.heroMetaLabel);
		this.heroPanel.Controls.Add(this.heroAppNameLabel);
		this.heroPanel.Controls.Add(this.heroLogoPictureBox);
		this.heroPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.heroPanel.Location = new System.Drawing.Point(24, 24);
		this.heroPanel.Margin = new System.Windows.Forms.Padding(0, 0, 18, 0);
		this.heroPanel.Name = "heroPanel";
		this.heroPanel.Size = new System.Drawing.Size(504, 713);
		this.heroPanel.TabIndex = 0;
		this.heroBulletThreeLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletThreeLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletThreeLabel.Location = new System.Drawing.Point(34, 522);
		this.heroBulletThreeLabel.Name = "heroBulletThreeLabel";
		this.heroBulletThreeLabel.Size = new System.Drawing.Size(396, 48);
		this.heroBulletThreeLabel.TabIndex = 8;
		this.heroBulletThreeLabel.Text = "• Sau khi xác minh, bạn có thể quay lại đăng nhập ngay.";
		this.heroBulletTwoLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletTwoLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletTwoLabel.Location = new System.Drawing.Point(34, 462);
		this.heroBulletTwoLabel.Name = "heroBulletTwoLabel";
		this.heroBulletTwoLabel.Size = new System.Drawing.Size(396, 48);
		this.heroBulletTwoLabel.TabIndex = 7;
		this.heroBulletTwoLabel.Text = "• Mã OTP sẽ được gửi về hộp thư của bạn trong ít phút.";
		this.heroBulletOneLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletOneLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletOneLabel.Location = new System.Drawing.Point(34, 402);
		this.heroBulletOneLabel.Name = "heroBulletOneLabel";
		this.heroBulletOneLabel.Size = new System.Drawing.Size(396, 48);
		this.heroBulletOneLabel.TabIndex = 6;
		this.heroBulletOneLabel.Text = "• Kiểm tra đúng email bạn đã dùng khi tạo tài khoản.";
		this.heroDescriptionLabel.Font = new System.Drawing.Font("Segoe UI", 10.5f);
		this.heroDescriptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.heroDescriptionLabel.Location = new System.Drawing.Point(34, 300);
		this.heroDescriptionLabel.Name = "heroDescriptionLabel";
		this.heroDescriptionLabel.Size = new System.Drawing.Size(396, 74);
		this.heroDescriptionLabel.TabIndex = 5;
		this.heroDescriptionLabel.Text = "Nhập email đã đăng ký để hệ thống gửi mã xác thực và hướng dẫn bạn tạo mật khẩu mới.";
		this.heroTitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 24f, System.Drawing.FontStyle.Bold);
		this.heroTitleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroTitleLabel.Location = new System.Drawing.Point(34, 172);
		this.heroTitleLabel.Name = "heroTitleLabel";
		this.heroTitleLabel.Size = new System.Drawing.Size(390, 112);
		this.heroTitleLabel.TabIndex = 4;
		this.heroTitleLabel.Text = "Nhận mã OTP để đặt lại mật khẩu.";
		this.heroBadgeLabel.AutoSize = true;
		this.heroBadgeLabel.BackColor = System.Drawing.Color.FromArgb(30, 56, 101);
		this.heroBadgeLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold);
		this.heroBadgeLabel.ForeColor = System.Drawing.Color.FromArgb(185, 213, 255);
		this.heroBadgeLabel.Location = new System.Drawing.Point(34, 116);
		this.heroBadgeLabel.Name = "heroBadgeLabel";
		this.heroBadgeLabel.Padding = new System.Windows.Forms.Padding(14, 8, 14, 8);
		this.heroBadgeLabel.Size = new System.Drawing.Size(141, 36);
		this.heroBadgeLabel.TabIndex = 3;
		this.heroBadgeLabel.Text = "NHẬN MÃ OTP";
		this.heroMetaLabel.AutoSize = true;
		this.heroMetaLabel.Font = new System.Drawing.Font("Segoe UI", 9.5f);
		this.heroMetaLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.heroMetaLabel.Location = new System.Drawing.Point(94, 62);
		this.heroMetaLabel.Name = "heroMetaLabel";
		this.heroMetaLabel.Size = new System.Drawing.Size(162, 21);
		this.heroMetaLabel.TabIndex = 2;
		this.heroMetaLabel.Text = "Cổng tài khoản NestG";
		this.heroAppNameLabel.AutoSize = true;
		this.heroAppNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 13f, System.Drawing.FontStyle.Bold);
		this.heroAppNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroAppNameLabel.Location = new System.Drawing.Point(94, 36);
		this.heroAppNameLabel.Name = "heroAppNameLabel";
		this.heroAppNameLabel.Size = new System.Drawing.Size(171, 30);
		this.heroAppNameLabel.TabIndex = 1;
		this.heroAppNameLabel.Text = "NestG Launcher";
		this.heroLogoPictureBox.Location = new System.Drawing.Point(34, 34);
		this.heroLogoPictureBox.Name = "heroLogoPictureBox";
		this.heroLogoPictureBox.Size = new System.Drawing.Size(46, 46);
		this.heroLogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.heroLogoPictureBox.TabIndex = 0;
		this.heroLogoPictureBox.TabStop = false;
		this.cardPanel.BackColor = System.Drawing.Color.Transparent;
		this.cardPanel.Controls.Add(this.backToLoginLinkLabel);
		this.cardPanel.Controls.Add(this.sendCodeButton);
		this.cardPanel.Controls.Add(this.emailFieldPanel);
		this.cardPanel.Controls.Add(this.emailCaptionLabel);
		this.cardPanel.Controls.Add(this.descriptionLabel);
		this.cardPanel.Controls.Add(this.subtitleLabel);
		this.cardPanel.Controls.Add(this.titleLabel);
		this.cardPanel.Controls.Add(this.cardMetaLabel);
		this.cardPanel.Controls.Add(this.cardAppNameLabel);
		this.cardPanel.Controls.Add(this.cardLogoPictureBox);
		this.cardPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardPanel.Location = new System.Drawing.Point(546, 24);
		this.cardPanel.Margin = new System.Windows.Forms.Padding(0);
		this.cardPanel.Name = "cardPanel";
		this.cardPanel.Size = new System.Drawing.Size(694, 713);
		this.cardPanel.TabIndex = 1;
		this.backToLoginLinkLabel.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.backToLoginLinkLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.backToLoginLinkLabel.AutoSize = true;
		this.backToLoginLinkLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.backToLoginLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.backToLoginLinkLabel.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.backToLoginLinkLabel.Location = new System.Drawing.Point(271, 516);
		this.backToLoginLinkLabel.Name = "backToLoginLinkLabel";
		this.backToLoginLinkLabel.Size = new System.Drawing.Size(161, 23);
		this.backToLoginLinkLabel.TabIndex = 9;
		this.backToLoginLinkLabel.TabStop = true;
		this.backToLoginLinkLabel.Text = "Quay lại đăng nhập";
		this.backToLoginLinkLabel.VisitedLinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.backToLoginLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(backToLoginLinkLabel_LinkClicked);
		this.sendCodeButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sendCodeButton.BackColor = System.Drawing.Color.Transparent;
		this.sendCodeButton.FlatAppearance.BorderSize = 0;
		this.sendCodeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.sendCodeButton.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.sendCodeButton.ForeColor = System.Drawing.Color.White;
		this.sendCodeButton.Location = new System.Drawing.Point(38, 432);
		this.sendCodeButton.MinimumSize = new System.Drawing.Size(0, 56);
		this.sendCodeButton.Name = "sendCodeButton";
		this.sendCodeButton.Size = new System.Drawing.Size(618, 56);
		this.sendCodeButton.TabIndex = 8;
		this.sendCodeButton.Text = "Gửi mã OTP";
		this.sendCodeButton.UseVisualStyleBackColor = true;
		this.sendCodeButton.Click += new System.EventHandler(sendCodeButton_Click);
		this.emailFieldPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.emailFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.emailFieldPanel.Controls.Add(this.emailTextBox);
		this.emailFieldPanel.Location = new System.Drawing.Point(38, 338);
		this.emailFieldPanel.Name = "emailFieldPanel";
		this.emailFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.emailFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.emailFieldPanel.TabIndex = 7;
		this.emailTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.emailTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.emailTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.emailTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.emailTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.emailTextBox.Location = new System.Drawing.Point(18, 15);
		this.emailTextBox.Name = "emailTextBox";
		this.emailTextBox.PlaceholderText = "example@gmail.com";
		this.emailTextBox.Size = new System.Drawing.Size(582, 25);
		this.emailTextBox.TabIndex = 0;
		this.emailCaptionLabel.AutoSize = true;
		this.emailCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.emailCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.emailCaptionLabel.Location = new System.Drawing.Point(38, 305);
		this.emailCaptionLabel.Name = "emailCaptionLabel";
		this.emailCaptionLabel.Size = new System.Drawing.Size(108, 23);
		this.emailCaptionLabel.TabIndex = 6;
		this.emailCaptionLabel.Text = "Địa chỉ email";
		this.descriptionLabel.Font = new System.Drawing.Font("Segoe UI", 10.5f);
		this.descriptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.descriptionLabel.Location = new System.Drawing.Point(38, 226);
		this.descriptionLabel.Name = "descriptionLabel";
		this.descriptionLabel.Size = new System.Drawing.Size(552, 48);
		this.descriptionLabel.TabIndex = 5;
		this.descriptionLabel.Text = "Nhập email của bạn để nhận mã OTP đặt lại mật khẩu.";
		this.subtitleLabel.AutoSize = true;
		this.subtitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.subtitleLabel.ForeColor = System.Drawing.Color.FromArgb(82, 138, 255);
		this.subtitleLabel.Location = new System.Drawing.Point(38, 166);
		this.subtitleLabel.Name = "subtitleLabel";
		this.subtitleLabel.Size = new System.Drawing.Size(233, 28);
		this.subtitleLabel.TabIndex = 4;
		this.subtitleLabel.Text = "Nhận mã OTP qua email";
		this.titleLabel.AutoSize = true;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 28f, System.Drawing.FontStyle.Bold);
		this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.titleLabel.Location = new System.Drawing.Point(38, 104);
		this.titleLabel.Name = "titleLabel";
		this.titleLabel.Size = new System.Drawing.Size(240, 62);
		this.titleLabel.TabIndex = 3;
		this.titleLabel.Text = "Khôi phục";
		this.cardMetaLabel.AutoSize = true;
		this.cardMetaLabel.Font = new System.Drawing.Font("Segoe UI", 8.75f);
		this.cardMetaLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.cardMetaLabel.Location = new System.Drawing.Point(84, 58);
		this.cardMetaLabel.Name = "cardMetaLabel";
		this.cardMetaLabel.Size = new System.Drawing.Size(153, 20);
		this.cardMetaLabel.TabIndex = 2;
		this.cardMetaLabel.Text = "Cổng tài khoản NestG";
		this.cardAppNameLabel.AutoSize = true;
		this.cardAppNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 11.5f, System.Drawing.FontStyle.Bold);
		this.cardAppNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.cardAppNameLabel.Location = new System.Drawing.Point(84, 36);
		this.cardAppNameLabel.Name = "cardAppNameLabel";
		this.cardAppNameLabel.Size = new System.Drawing.Size(157, 28);
		this.cardAppNameLabel.TabIndex = 1;
		this.cardAppNameLabel.Text = "NestG Launcher";
		this.cardLogoPictureBox.Location = new System.Drawing.Point(38, 34);
		this.cardLogoPictureBox.Name = "cardLogoPictureBox";
		this.cardLogoPictureBox.Size = new System.Drawing.Size(34, 34);
		this.cardLogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.cardLogoPictureBox.TabIndex = 0;
		this.cardLogoPictureBox.TabStop = false;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		this.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		base.ClientSize = new System.Drawing.Size(1264, 761);
		base.Controls.Add(this.rootLayout);
		this.MinimumSize = new System.Drawing.Size(980, 720);
		base.Name = "PasswordRecoveryForm";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "NestG Launcher";
		this.rootLayout.ResumeLayout(false);
		this.heroPanel.ResumeLayout(false);
		this.heroPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).EndInit();
		this.cardPanel.ResumeLayout(false);
		this.cardPanel.PerformLayout();
		this.emailFieldPanel.ResumeLayout(false);
		this.emailFieldPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.cardLogoPictureBox).EndInit();
		base.ResumeLayout(false);
	}
}
