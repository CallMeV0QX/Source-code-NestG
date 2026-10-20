using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop;
using app_desktop.Models;

namespace UI_Desktop.Auth;

internal class RegisterForm : Form
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

	private Label displayNameCaptionLabel;

	private RoundedPanel displayNameFieldPanel;

	private TextBox displayNameTextBox;

	private Label usernameCaptionLabel;

	private RoundedPanel usernameFieldPanel;

	private TextBox usernameTextBox;

	private Label emailCaptionLabel;

	private RoundedPanel emailFieldPanel;

	private TextBox emailTextBox;

	private Label passwordCaptionLabel;

	private RoundedPanel passwordFieldPanel;

	private TextBox passwordTextBox;

	private Label passwordHintLabel;

	private GradientButton continueButton;

	private Label footerPromptLabel;

	private LinkLabel loginLinkLabel;

	public RegisterForm()
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
		AuthUiHelper.ConfigureInput(displayNameFieldPanel, displayNameTextBox);
		AuthUiHelper.ConfigureInput(usernameFieldPanel, usernameTextBox);
		AuthUiHelper.ConfigureInput(emailFieldPanel, emailTextBox);
		AuthUiHelper.ConfigureInput(passwordFieldPanel, passwordTextBox);
		usernameCaptionLabel.Text = "Số điện thoại";
		usernameTextBox.PlaceholderText = "Ví dụ: 0901234567 (tuỳ chọn)";
		base.AcceptButton = continueButton;
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
			displayNameTextBox.Focus();
		};
	}

	private void UpdateResponsiveLayout()
	{
		AuthUiHelper.UpdateResponsiveLayout(this, rootLayout, heroPanel);
	}

	private async void continueButton_Click(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(displayNameTextBox.Text) || string.IsNullOrWhiteSpace(emailTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text))
		{
			LauncherMessageBox.Show("Vui lòng điền đầy đủ tên hiển thị, email và mật khẩu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		else if (passwordTextBox.Text.Length < 6)
		{
			LauncherMessageBox.Show("Mật khẩu cần có ít nhất 6 ký tự.", "Mật khẩu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		else
		{
			if (isSubmitting)
			{
				return;
			}
			await RunBusyAsync("Đang tạo tài khoản...", async delegate
			{
				AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.RegisterAsync(displayNameTextBox.Text.Trim(), NormalizePhone(usernameTextBox.Text), emailTextBox.Text.Trim(), passwordTextBox.Text, DesktopAppContext.Settings.RememberSessionByDefault);
				if (authActionResult.HasSession)
				{
					AuthUiHelper.Navigate(this, DesktopAppContext.CreateStorefrontForm());
				}
				else
				{
					if (authActionResult.RequiresChallenge && authActionResult.Challenge != null)
					{
						using (OtpVerificationDialog otpVerificationDialog = new OtpVerificationDialog(authActionResult.Challenge, DesktopAppContext.Settings.RememberSessionByDefault))
						{
							if (otpVerificationDialog.ShowDialog(this) == DialogResult.OK)
							{
								AuthUiHelper.Navigate(this, DesktopAppContext.CreateStorefrontForm());
							}
							return;
						}
					}
					LauncherMessageBox.Show(this, authActionResult.Message, "Đăng ký thất bại", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			});
		}
	}

	private void loginLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		AuthUiHelper.Navigate(this, DesktopAppContext.CreateLoginForm());
	}

	private static string? NormalizePhone(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return null;
	}

	private async Task RunBusyAsync(string loadingText, Func<Task> action)
	{
		string originalText = continueButton.Text;
		isSubmitting = true;
		continueButton.Enabled = false;
		base.UseWaitCursor = true;
		continueButton.Text = loadingText;
		try
		{
			await action();
		}
		finally
		{
			isSubmitting = false;
			continueButton.Enabled = true;
			base.UseWaitCursor = false;
			continueButton.Text = originalText;
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
		this.loginLinkLabel = new System.Windows.Forms.LinkLabel();
		this.footerPromptLabel = new System.Windows.Forms.Label();
		this.continueButton = new UI_Desktop.GradientButton();
		this.passwordHintLabel = new System.Windows.Forms.Label();
		this.passwordFieldPanel = new UI_Desktop.RoundedPanel();
		this.passwordTextBox = new System.Windows.Forms.TextBox();
		this.passwordCaptionLabel = new System.Windows.Forms.Label();
		this.emailFieldPanel = new UI_Desktop.RoundedPanel();
		this.emailTextBox = new System.Windows.Forms.TextBox();
		this.emailCaptionLabel = new System.Windows.Forms.Label();
		this.usernameFieldPanel = new UI_Desktop.RoundedPanel();
		this.usernameTextBox = new System.Windows.Forms.TextBox();
		this.usernameCaptionLabel = new System.Windows.Forms.Label();
		this.displayNameFieldPanel = new UI_Desktop.RoundedPanel();
		this.displayNameTextBox = new System.Windows.Forms.TextBox();
		this.displayNameCaptionLabel = new System.Windows.Forms.Label();
		this.subtitleLabel = new System.Windows.Forms.Label();
		this.titleLabel = new System.Windows.Forms.Label();
		this.cardMetaLabel = new System.Windows.Forms.Label();
		this.cardAppNameLabel = new System.Windows.Forms.Label();
		this.cardLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.rootLayout.SuspendLayout();
		this.heroPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).BeginInit();
		this.cardPanel.SuspendLayout();
		this.passwordFieldPanel.SuspendLayout();
		this.emailFieldPanel.SuspendLayout();
		this.usernameFieldPanel.SuspendLayout();
		this.displayNameFieldPanel.SuspendLayout();
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
		this.heroBulletThreeLabel.Text = "• Hoàn tất đăng ký để đăng nhập và bắt đầu sử dụng ngay.";
		this.heroBulletTwoLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletTwoLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletTwoLabel.Location = new System.Drawing.Point(34, 462);
		this.heroBulletTwoLabel.Name = "heroBulletTwoLabel";
		this.heroBulletTwoLabel.Size = new System.Drawing.Size(396, 48);
		this.heroBulletTwoLabel.TabIndex = 7;
		this.heroBulletTwoLabel.Text = "• Dùng email đang hoạt động để nhận xác thực và hỗ trợ tài khoản.";
		this.heroBulletOneLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletOneLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletOneLabel.Location = new System.Drawing.Point(34, 402);
		this.heroBulletOneLabel.Name = "heroBulletOneLabel";
		this.heroBulletOneLabel.Size = new System.Drawing.Size(396, 48);
		this.heroBulletOneLabel.TabIndex = 6;
		this.heroBulletOneLabel.Text = "• Chọn tên hiển thị để mọi người dễ nhận ra bạn.";
		this.heroDescriptionLabel.Font = new System.Drawing.Font("Segoe UI", 10.5f);
		this.heroDescriptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.heroDescriptionLabel.Location = new System.Drawing.Point(34, 300);
		this.heroDescriptionLabel.Name = "heroDescriptionLabel";
		this.heroDescriptionLabel.Size = new System.Drawing.Size(396, 74);
		this.heroDescriptionLabel.TabIndex = 5;
		this.heroDescriptionLabel.Text = "Điền thông tin cơ bản để bắt đầu sử dụng launcher và nhận các thông báo cần thiết từ hệ thống.";
		this.heroTitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 24f, System.Drawing.FontStyle.Bold);
		this.heroTitleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroTitleLabel.Location = new System.Drawing.Point(34, 172);
		this.heroTitleLabel.Name = "heroTitleLabel";
		this.heroTitleLabel.Size = new System.Drawing.Size(390, 112);
		this.heroTitleLabel.TabIndex = 4;
		this.heroTitleLabel.Text = "Tạo tài khoản NestG";
		this.heroBadgeLabel.AutoSize = true;
		this.heroBadgeLabel.BackColor = System.Drawing.Color.FromArgb(30, 56, 101);
		this.heroBadgeLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold);
		this.heroBadgeLabel.ForeColor = System.Drawing.Color.FromArgb(185, 213, 255);
		this.heroBadgeLabel.Location = new System.Drawing.Point(34, 116);
		this.heroBadgeLabel.Name = "heroBadgeLabel";
		this.heroBadgeLabel.Padding = new System.Windows.Forms.Padding(14, 8, 14, 8);
		this.heroBadgeLabel.Size = new System.Drawing.Size(148, 36);
		this.heroBadgeLabel.TabIndex = 3;
		this.heroBadgeLabel.Text = "TẠO TÀI KHOẢN";
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
		this.cardPanel.Controls.Add(this.loginLinkLabel);
		this.cardPanel.Controls.Add(this.footerPromptLabel);
		this.cardPanel.Controls.Add(this.continueButton);
		this.cardPanel.Controls.Add(this.passwordHintLabel);
		this.cardPanel.Controls.Add(this.passwordFieldPanel);
		this.cardPanel.Controls.Add(this.passwordCaptionLabel);
		this.cardPanel.Controls.Add(this.emailFieldPanel);
		this.cardPanel.Controls.Add(this.emailCaptionLabel);
		this.cardPanel.Controls.Add(this.usernameFieldPanel);
		this.cardPanel.Controls.Add(this.usernameCaptionLabel);
		this.cardPanel.Controls.Add(this.displayNameFieldPanel);
		this.cardPanel.Controls.Add(this.displayNameCaptionLabel);
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
		this.loginLinkLabel.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.loginLinkLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
		this.loginLinkLabel.AutoSize = true;
		this.loginLinkLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.loginLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.loginLinkLabel.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.loginLinkLabel.Location = new System.Drawing.Point(311, 684);
		this.loginLinkLabel.Name = "loginLinkLabel";
		this.loginLinkLabel.Size = new System.Drawing.Size(161, 23);
		this.loginLinkLabel.TabIndex = 16;
		this.loginLinkLabel.TabStop = true;
		this.loginLinkLabel.Text = "Quay lại đăng nhập";
		this.loginLinkLabel.VisitedLinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.loginLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(loginLinkLabel_LinkClicked);
		this.footerPromptLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
		this.footerPromptLabel.AutoSize = true;
		this.footerPromptLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.footerPromptLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.footerPromptLabel.Location = new System.Drawing.Point(194, 684);
		this.footerPromptLabel.Name = "footerPromptLabel";
		this.footerPromptLabel.Size = new System.Drawing.Size(138, 23);
		this.footerPromptLabel.TabIndex = 15;
		this.footerPromptLabel.Text = "Đã có tài khoản?";
		this.continueButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.continueButton.BackColor = System.Drawing.Color.Transparent;
		this.continueButton.FlatAppearance.BorderSize = 0;
		this.continueButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.continueButton.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.continueButton.ForeColor = System.Drawing.Color.White;
		this.continueButton.Location = new System.Drawing.Point(38, 621);
		this.continueButton.MinimumSize = new System.Drawing.Size(0, 56);
		this.continueButton.Name = "continueButton";
		this.continueButton.Size = new System.Drawing.Size(618, 56);
		this.continueButton.TabIndex = 14;
		this.continueButton.Text = "Tiếp theo";
		this.continueButton.UseVisualStyleBackColor = true;
		this.continueButton.Click += new System.EventHandler(continueButton_Click);
		this.passwordHintLabel.AutoSize = true;
		this.passwordHintLabel.Font = new System.Drawing.Font("Segoe UI", 9.25f);
		this.passwordHintLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.passwordHintLabel.Location = new System.Drawing.Point(38, 668);
		this.passwordHintLabel.MaximumSize = new System.Drawing.Size(520, 0);
		this.passwordHintLabel.Name = "passwordHintLabel";
		this.passwordHintLabel.Size = new System.Drawing.Size(0, 21);
		this.passwordHintLabel.TabIndex = 13;
		this.passwordHintLabel.Visible = false;
		this.passwordFieldPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.passwordFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.passwordFieldPanel.Controls.Add(this.passwordTextBox);
		this.passwordFieldPanel.Location = new System.Drawing.Point(38, 558);
		this.passwordFieldPanel.Name = "passwordFieldPanel";
		this.passwordFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.passwordFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.passwordFieldPanel.TabIndex = 12;
		this.passwordTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.passwordTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.passwordTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.passwordTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.passwordTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.passwordTextBox.Location = new System.Drawing.Point(18, 15);
		this.passwordTextBox.Name = "passwordTextBox";
		this.passwordTextBox.PlaceholderText = "Tối thiểu 6 ký tự";
		this.passwordTextBox.Size = new System.Drawing.Size(582, 25);
		this.passwordTextBox.TabIndex = 0;
		this.passwordTextBox.UseSystemPasswordChar = true;
		this.passwordCaptionLabel.AutoSize = true;
		this.passwordCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.passwordCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.passwordCaptionLabel.Location = new System.Drawing.Point(38, 525);
		this.passwordCaptionLabel.Name = "passwordCaptionLabel";
		this.passwordCaptionLabel.Size = new System.Drawing.Size(84, 23);
		this.passwordCaptionLabel.TabIndex = 11;
		this.passwordCaptionLabel.Text = "Mật khẩu";
		this.emailFieldPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.emailFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.emailFieldPanel.Controls.Add(this.emailTextBox);
		this.emailFieldPanel.Location = new System.Drawing.Point(38, 453);
		this.emailFieldPanel.Name = "emailFieldPanel";
		this.emailFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.emailFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.emailFieldPanel.TabIndex = 10;
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
		this.emailCaptionLabel.Location = new System.Drawing.Point(38, 420);
		this.emailCaptionLabel.Name = "emailCaptionLabel";
		this.emailCaptionLabel.Size = new System.Drawing.Size(108, 23);
		this.emailCaptionLabel.TabIndex = 9;
		this.emailCaptionLabel.Text = "Địa chỉ email";
		this.usernameFieldPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.usernameFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.usernameFieldPanel.Controls.Add(this.usernameTextBox);
		this.usernameFieldPanel.Location = new System.Drawing.Point(38, 348);
		this.usernameFieldPanel.Name = "usernameFieldPanel";
		this.usernameFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.usernameFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.usernameFieldPanel.TabIndex = 8;
		this.usernameTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.usernameTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.usernameTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.usernameTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.usernameTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.usernameTextBox.Location = new System.Drawing.Point(18, 15);
		this.usernameTextBox.Name = "usernameTextBox";
		this.usernameTextBox.PlaceholderText = "Ví dụ: nestg123";
		this.usernameTextBox.Size = new System.Drawing.Size(582, 25);
		this.usernameTextBox.TabIndex = 0;
		this.usernameCaptionLabel.AutoSize = true;
		this.usernameCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.usernameCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.usernameCaptionLabel.Location = new System.Drawing.Point(38, 315);
		this.usernameCaptionLabel.Name = "usernameCaptionLabel";
		this.usernameCaptionLabel.Size = new System.Drawing.Size(124, 23);
		this.usernameCaptionLabel.TabIndex = 7;
		this.usernameCaptionLabel.Text = "Tên đăng nhập";
		this.displayNameFieldPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.displayNameFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.displayNameFieldPanel.Controls.Add(this.displayNameTextBox);
		this.displayNameFieldPanel.Location = new System.Drawing.Point(38, 243);
		this.displayNameFieldPanel.Name = "displayNameFieldPanel";
		this.displayNameFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.displayNameFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.displayNameFieldPanel.TabIndex = 6;
		this.displayNameTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.displayNameTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.displayNameTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.displayNameTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.displayNameTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.displayNameTextBox.Location = new System.Drawing.Point(18, 15);
		this.displayNameTextBox.Name = "displayNameTextBox";
		this.displayNameTextBox.PlaceholderText = "Ví dụ: Nguyễn Văn A";
		this.displayNameTextBox.Size = new System.Drawing.Size(582, 25);
		this.displayNameTextBox.TabIndex = 0;
		this.displayNameCaptionLabel.AutoSize = true;
		this.displayNameCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.displayNameCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.displayNameCaptionLabel.Location = new System.Drawing.Point(38, 210);
		this.displayNameCaptionLabel.Name = "displayNameCaptionLabel";
		this.displayNameCaptionLabel.Size = new System.Drawing.Size(99, 23);
		this.displayNameCaptionLabel.TabIndex = 5;
		this.displayNameCaptionLabel.Text = "Tên hiển thị";
		this.subtitleLabel.AutoSize = true;
		this.subtitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.subtitleLabel.ForeColor = System.Drawing.Color.FromArgb(82, 138, 255);
		this.subtitleLabel.Location = new System.Drawing.Point(38, 166);
		this.subtitleLabel.Name = "subtitleLabel";
		this.subtitleLabel.Size = new System.Drawing.Size(176, 28);
		this.subtitleLabel.TabIndex = 4;
		this.subtitleLabel.Text = "Bắt đầu với NestG";
		this.titleLabel.AutoSize = true;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 28f, System.Drawing.FontStyle.Bold);
		this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.titleLabel.Location = new System.Drawing.Point(38, 104);
		this.titleLabel.Name = "titleLabel";
		this.titleLabel.Size = new System.Drawing.Size(318, 62);
		this.titleLabel.TabIndex = 3;
		this.titleLabel.Text = "Tạo tài khoản";
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
		base.Name = "RegisterForm";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "NestG Launcher";
		this.rootLayout.ResumeLayout(false);
		this.heroPanel.ResumeLayout(false);
		this.heroPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).EndInit();
		this.cardPanel.ResumeLayout(false);
		this.cardPanel.PerformLayout();
		this.passwordFieldPanel.ResumeLayout(false);
		this.passwordFieldPanel.PerformLayout();
		this.emailFieldPanel.ResumeLayout(false);
		this.emailFieldPanel.PerformLayout();
		this.usernameFieldPanel.ResumeLayout(false);
		this.usernameFieldPanel.PerformLayout();
		this.displayNameFieldPanel.ResumeLayout(false);
		this.displayNameFieldPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.cardLogoPictureBox).EndInit();
		base.ResumeLayout(false);
	}
}
