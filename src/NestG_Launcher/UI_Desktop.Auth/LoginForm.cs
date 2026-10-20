using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop;
using app_desktop.Models;

namespace UI_Desktop.Auth;

internal class LoginForm : Form
{
	private bool isSubmitting;

	private IContainer components;

	private TableLayoutPanel rootLayout;

	private RoundedPanel heroPanel;

	private TableLayoutPanel heroContentLayout;

	private TableLayoutPanel heroHeaderLayout;

	private Panel heroBrandTextPanel;

	private Label heroMetaLabel;

	private Label heroAppNameLabel;

	private PictureBox heroLogoPictureBox;

	private Label heroBadgeLabel;

	private Label heroTitleLabel;

	private Label heroDescriptionLabel;

	private Label heroBulletOneLabel;

	private Label heroBulletTwoLabel;

	private Label heroBulletThreeLabel;

	private RoundedPanel cardPanel;

	private TableLayoutPanel cardContentLayout;

	private TableLayoutPanel cardHeaderLayout;

	private Panel cardBrandTextPanel;

	private Label cardMetaLabel;

	private Label cardAppNameLabel;

	private PictureBox cardLogoPictureBox;

	private Label titleLabel;

	private Label subtitleLabel;

	private Label emailCaptionLabel;

	private RoundedPanel emailFieldPanel;

	private TextBox emailTextBox;

	private Label passwordCaptionLabel;

	private RoundedPanel passwordFieldPanel;

	private TextBox passwordTextBox;

	private TableLayoutPanel optionsLayout;

	private CheckBox rememberMeCheckBox;

	private LinkLabel forgotPasswordLink;

	private GradientButton loginButton;

	private FlowLayoutPanel footerFlowPanel;

	private Label footerPromptLabel;

	private LinkLabel registerLinkLabel;

	public LoginForm()
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
		AuthUiHelper.ConfigureInput(passwordFieldPanel, passwordTextBox);
		rememberMeCheckBox.Checked = DesktopAppContext.Settings.RememberSessionByDefault;
		base.AcceptButton = loginButton;
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

	private async void loginButton_Click(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(emailTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text))
		{
			LauncherMessageBox.Show("Vui lòng nhập đầy đủ địa chỉ email và mật khẩu trước khi tiếp tục.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		else
		{
			if (isSubmitting)
			{
				return;
			}
			await RunBusyAsync("Đang đăng nhập...", async delegate
			{
				bool rememberSession = rememberMeCheckBox.Checked;
				AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.LoginAsync(emailTextBox.Text.Trim(), passwordTextBox.Text, rememberSession);
				if (authActionResult.HasSession)
				{
					AuthUiHelper.Navigate(this, DesktopAppContext.CreateStorefrontForm());
				}
				else
				{
					if (authActionResult.RequiresChallenge && authActionResult.Challenge != null)
					{
						using (OtpVerificationDialog otpVerificationDialog = new OtpVerificationDialog(authActionResult.Challenge, rememberSession))
						{
							if (otpVerificationDialog.ShowDialog(this) == DialogResult.OK)
							{
								AuthUiHelper.Navigate(this, DesktopAppContext.CreateStorefrontForm());
							}
							return;
						}
					}
					LauncherMessageBox.Show(this, authActionResult.Message, "Đăng nhập thất bại", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			});
		}
	}

	private void forgotPasswordLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		AuthUiHelper.Navigate(this, new PasswordRecoveryForm());
	}

	private void registerLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		AuthUiHelper.Navigate(this, new RegisterForm());
	}

	private async Task RunBusyAsync(string loadingText, Func<Task> action)
	{
		string originalText = loginButton.Text;
		isSubmitting = true;
		loginButton.Enabled = false;
		base.UseWaitCursor = true;
		loginButton.Text = loadingText;
		try
		{
			await action();
		}
		finally
		{
			isSubmitting = false;
			loginButton.Enabled = true;
			base.UseWaitCursor = false;
			loginButton.Text = originalText;
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
		this.heroContentLayout = new System.Windows.Forms.TableLayoutPanel();
		this.heroHeaderLayout = new System.Windows.Forms.TableLayoutPanel();
		this.heroBrandTextPanel = new System.Windows.Forms.Panel();
		this.heroMetaLabel = new System.Windows.Forms.Label();
		this.heroAppNameLabel = new System.Windows.Forms.Label();
		this.heroLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.heroBadgeLabel = new System.Windows.Forms.Label();
		this.heroTitleLabel = new System.Windows.Forms.Label();
		this.heroDescriptionLabel = new System.Windows.Forms.Label();
		this.heroBulletOneLabel = new System.Windows.Forms.Label();
		this.heroBulletTwoLabel = new System.Windows.Forms.Label();
		this.heroBulletThreeLabel = new System.Windows.Forms.Label();
		this.cardPanel = new UI_Desktop.RoundedPanel();
		this.cardContentLayout = new System.Windows.Forms.TableLayoutPanel();
		this.cardHeaderLayout = new System.Windows.Forms.TableLayoutPanel();
		this.cardBrandTextPanel = new System.Windows.Forms.Panel();
		this.cardMetaLabel = new System.Windows.Forms.Label();
		this.cardAppNameLabel = new System.Windows.Forms.Label();
		this.cardLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.titleLabel = new System.Windows.Forms.Label();
		this.subtitleLabel = new System.Windows.Forms.Label();
		this.emailCaptionLabel = new System.Windows.Forms.Label();
		this.emailFieldPanel = new UI_Desktop.RoundedPanel();
		this.emailTextBox = new System.Windows.Forms.TextBox();
		this.passwordCaptionLabel = new System.Windows.Forms.Label();
		this.passwordFieldPanel = new UI_Desktop.RoundedPanel();
		this.passwordTextBox = new System.Windows.Forms.TextBox();
		this.optionsLayout = new System.Windows.Forms.TableLayoutPanel();
		this.rememberMeCheckBox = new System.Windows.Forms.CheckBox();
		this.forgotPasswordLink = new System.Windows.Forms.LinkLabel();
		this.loginButton = new UI_Desktop.GradientButton();
		this.footerFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
		this.footerPromptLabel = new System.Windows.Forms.Label();
		this.registerLinkLabel = new System.Windows.Forms.LinkLabel();
		this.rootLayout.SuspendLayout();
		this.heroPanel.SuspendLayout();
		this.heroContentLayout.SuspendLayout();
		this.heroHeaderLayout.SuspendLayout();
		this.heroBrandTextPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).BeginInit();
		this.cardPanel.SuspendLayout();
		this.cardContentLayout.SuspendLayout();
		this.cardHeaderLayout.SuspendLayout();
		this.cardBrandTextPanel.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.cardLogoPictureBox).BeginInit();
		this.emailFieldPanel.SuspendLayout();
		this.passwordFieldPanel.SuspendLayout();
		this.optionsLayout.SuspendLayout();
		this.footerFlowPanel.SuspendLayout();
		base.SuspendLayout();
		this.rootLayout.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		this.rootLayout.ColumnCount = 2;
		this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 43f));
		this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 57f));
		this.rootLayout.Controls.Add(this.heroPanel, 0, 0);
		this.rootLayout.Controls.Add(this.cardPanel, 1, 0);
		this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rootLayout.Location = new System.Drawing.Point(0, 0);
		this.rootLayout.Margin = new System.Windows.Forms.Padding(0);
		this.rootLayout.Name = "rootLayout";
		this.rootLayout.Padding = new System.Windows.Forms.Padding(24);
		this.rootLayout.RowCount = 1;
		this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.rootLayout.Size = new System.Drawing.Size(1264, 761);
		this.rootLayout.TabIndex = 0;
		this.heroPanel.BackColor = System.Drawing.Color.Transparent;
		this.heroPanel.Controls.Add(this.heroContentLayout);
		this.heroPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.heroPanel.Location = new System.Drawing.Point(24, 24);
		this.heroPanel.Margin = new System.Windows.Forms.Padding(0, 0, 18, 0);
		this.heroPanel.Name = "heroPanel";
		this.heroPanel.Padding = new System.Windows.Forms.Padding(34);
		this.heroPanel.Size = new System.Drawing.Size(504, 713);
		this.heroPanel.TabIndex = 0;
		this.heroContentLayout.BackColor = System.Drawing.Color.Transparent;
		this.heroContentLayout.ColumnCount = 1;
		this.heroContentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.heroContentLayout.Controls.Add(this.heroHeaderLayout, 0, 0);
		this.heroContentLayout.Controls.Add(this.heroBadgeLabel, 0, 1);
		this.heroContentLayout.Controls.Add(this.heroTitleLabel, 0, 2);
		this.heroContentLayout.Controls.Add(this.heroDescriptionLabel, 0, 3);
		this.heroContentLayout.Controls.Add(this.heroBulletOneLabel, 0, 4);
		this.heroContentLayout.Controls.Add(this.heroBulletTwoLabel, 0, 5);
		this.heroContentLayout.Controls.Add(this.heroBulletThreeLabel, 0, 6);
		this.heroContentLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.heroContentLayout.Location = new System.Drawing.Point(34, 34);
		this.heroContentLayout.Margin = new System.Windows.Forms.Padding(0);
		this.heroContentLayout.Name = "heroContentLayout";
		this.heroContentLayout.RowCount = 8;
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.heroContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.heroContentLayout.Size = new System.Drawing.Size(436, 645);
		this.heroContentLayout.TabIndex = 0;
		this.heroHeaderLayout.AutoSize = true;
		this.heroHeaderLayout.ColumnCount = 2;
		this.heroHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.heroHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.heroHeaderLayout.Controls.Add(this.heroBrandTextPanel, 1, 0);
		this.heroHeaderLayout.Controls.Add(this.heroLogoPictureBox, 0, 0);
		this.heroHeaderLayout.Dock = System.Windows.Forms.DockStyle.Top;
		this.heroHeaderLayout.Location = new System.Drawing.Point(0, 0);
		this.heroHeaderLayout.Margin = new System.Windows.Forms.Padding(0);
		this.heroHeaderLayout.Name = "heroHeaderLayout";
		this.heroHeaderLayout.RowCount = 1;
		this.heroHeaderLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.heroHeaderLayout.Size = new System.Drawing.Size(436, 51);
		this.heroHeaderLayout.TabIndex = 0;
		this.heroBrandTextPanel.AutoSize = true;
		this.heroBrandTextPanel.BackColor = System.Drawing.Color.Transparent;
		this.heroBrandTextPanel.Controls.Add(this.heroMetaLabel);
		this.heroBrandTextPanel.Controls.Add(this.heroAppNameLabel);
		this.heroBrandTextPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.heroBrandTextPanel.Location = new System.Drawing.Point(60, 0);
		this.heroBrandTextPanel.Margin = new System.Windows.Forms.Padding(0);
		this.heroBrandTextPanel.Name = "heroBrandTextPanel";
		this.heroBrandTextPanel.Size = new System.Drawing.Size(376, 51);
		this.heroBrandTextPanel.TabIndex = 1;
		this.heroMetaLabel.AutoSize = true;
		this.heroMetaLabel.Dock = System.Windows.Forms.DockStyle.Top;
		this.heroMetaLabel.Font = new System.Drawing.Font("Segoe UI", 9.5f);
		this.heroMetaLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.heroMetaLabel.Location = new System.Drawing.Point(0, 30);
		this.heroMetaLabel.Margin = new System.Windows.Forms.Padding(0);
		this.heroMetaLabel.Name = "heroMetaLabel";
		this.heroMetaLabel.Size = new System.Drawing.Size(162, 21);
		this.heroMetaLabel.TabIndex = 1;
		this.heroMetaLabel.Text = "Cổng tài khoản NestG";
		this.heroAppNameLabel.AutoSize = true;
		this.heroAppNameLabel.Dock = System.Windows.Forms.DockStyle.Top;
		this.heroAppNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 13f, System.Drawing.FontStyle.Bold);
		this.heroAppNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroAppNameLabel.Location = new System.Drawing.Point(0, 0);
		this.heroAppNameLabel.Margin = new System.Windows.Forms.Padding(0);
		this.heroAppNameLabel.Name = "heroAppNameLabel";
		this.heroAppNameLabel.Size = new System.Drawing.Size(171, 30);
		this.heroAppNameLabel.TabIndex = 0;
		this.heroAppNameLabel.Text = "NestG Launcher";
		this.heroLogoPictureBox.Location = new System.Drawing.Point(0, 0);
		this.heroLogoPictureBox.Margin = new System.Windows.Forms.Padding(0, 0, 14, 0);
		this.heroLogoPictureBox.Name = "heroLogoPictureBox";
		this.heroLogoPictureBox.Size = new System.Drawing.Size(46, 46);
		this.heroLogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.heroLogoPictureBox.TabIndex = 0;
		this.heroLogoPictureBox.TabStop = false;
		this.heroBadgeLabel.AutoSize = true;
		this.heroBadgeLabel.BackColor = System.Drawing.Color.FromArgb(30, 56, 101);
		this.heroBadgeLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold);
		this.heroBadgeLabel.ForeColor = System.Drawing.Color.FromArgb(185, 213, 255);
		this.heroBadgeLabel.Location = new System.Drawing.Point(0, 81);
		this.heroBadgeLabel.Margin = new System.Windows.Forms.Padding(0, 30, 0, 0);
		this.heroBadgeLabel.Name = "heroBadgeLabel";
		this.heroBadgeLabel.Padding = new System.Windows.Forms.Padding(14, 8, 14, 8);
		this.heroBadgeLabel.Size = new System.Drawing.Size(208, 36);
		this.heroBadgeLabel.TabIndex = 1;
		this.heroBadgeLabel.Text = "ĐĂNG NHẬP TÀI KHOẢN";
		this.heroTitleLabel.AutoSize = true;
		this.heroTitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 24f, System.Drawing.FontStyle.Bold);
		this.heroTitleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroTitleLabel.Location = new System.Drawing.Point(0, 135);
		this.heroTitleLabel.Margin = new System.Windows.Forms.Padding(0, 18, 0, 0);
		this.heroTitleLabel.MaximumSize = new System.Drawing.Size(390, 0);
		this.heroTitleLabel.Name = "heroTitleLabel";
		this.heroTitleLabel.Size = new System.Drawing.Size(369, 108);
		this.heroTitleLabel.TabIndex = 2;
		this.heroTitleLabel.Text = "Đăng nhập để tiếp tục với NestG.";
		this.heroDescriptionLabel.AutoSize = true;
		this.heroDescriptionLabel.Font = new System.Drawing.Font("Segoe UI", 10.5f);
		this.heroDescriptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.heroDescriptionLabel.Location = new System.Drawing.Point(0, 259);
		this.heroDescriptionLabel.Margin = new System.Windows.Forms.Padding(0, 16, 0, 0);
		this.heroDescriptionLabel.MaximumSize = new System.Drawing.Size(398, 0);
		this.heroDescriptionLabel.Name = "heroDescriptionLabel";
		this.heroDescriptionLabel.Size = new System.Drawing.Size(387, 50);
		this.heroDescriptionLabel.TabIndex = 3;
		this.heroDescriptionLabel.Text = "Nhập email và mật khẩu để vào launcher, kiểm tra tài khoản và tiếp tục phiên làm việc của bạn.";
		this.heroBulletOneLabel.AutoSize = true;
		this.heroBulletOneLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletOneLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletOneLabel.Location = new System.Drawing.Point(0, 337);
		this.heroBulletOneLabel.Margin = new System.Windows.Forms.Padding(0, 28, 0, 0);
		this.heroBulletOneLabel.MaximumSize = new System.Drawing.Size(400, 0);
		this.heroBulletOneLabel.Name = "heroBulletOneLabel";
		this.heroBulletOneLabel.Size = new System.Drawing.Size(396, 50);
		this.heroBulletOneLabel.TabIndex = 4;
		this.heroBulletOneLabel.Text = "• Theo dõi nhanh tình trạng tài khoản và thiết bị của bạn.";
		this.heroBulletTwoLabel.AutoSize = true;
		this.heroBulletTwoLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletTwoLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletTwoLabel.Location = new System.Drawing.Point(0, 403);
		this.heroBulletTwoLabel.Margin = new System.Windows.Forms.Padding(0, 16, 0, 0);
		this.heroBulletTwoLabel.MaximumSize = new System.Drawing.Size(400, 0);
		this.heroBulletTwoLabel.Name = "heroBulletTwoLabel";
		this.heroBulletTwoLabel.Size = new System.Drawing.Size(396, 50);
		this.heroBulletTwoLabel.TabIndex = 5;
		this.heroBulletTwoLabel.Text = "• Ghi nhớ đăng nhập an toàn để lần sau truy cập nhanh hơn.";
		this.heroBulletThreeLabel.AutoSize = true;
		this.heroBulletThreeLabel.Font = new System.Drawing.Font("Segoe UI", 10.25f);
		this.heroBulletThreeLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.heroBulletThreeLabel.Location = new System.Drawing.Point(0, 469);
		this.heroBulletThreeLabel.Margin = new System.Windows.Forms.Padding(0, 16, 0, 0);
		this.heroBulletThreeLabel.MaximumSize = new System.Drawing.Size(400, 0);
		this.heroBulletThreeLabel.Name = "heroBulletThreeLabel";
		this.heroBulletThreeLabel.Size = new System.Drawing.Size(398, 50);
		this.heroBulletThreeLabel.TabIndex = 6;
		this.heroBulletThreeLabel.Text = "• Nếu quên mật khẩu, bạn có thể yêu cầu mã xác thực ngay tại đây.";
		this.cardPanel.BackColor = System.Drawing.Color.Transparent;
		this.cardPanel.Controls.Add(this.cardContentLayout);
		this.cardPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardPanel.Location = new System.Drawing.Point(546, 24);
		this.cardPanel.Margin = new System.Windows.Forms.Padding(0);
		this.cardPanel.Name = "cardPanel";
		this.cardPanel.Padding = new System.Windows.Forms.Padding(38, 34, 38, 34);
		this.cardPanel.Size = new System.Drawing.Size(694, 713);
		this.cardPanel.TabIndex = 1;
		this.cardContentLayout.AutoScroll = true;
		this.cardContentLayout.BackColor = System.Drawing.Color.Transparent;
		this.cardContentLayout.ColumnCount = 1;
		this.cardContentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.cardContentLayout.Controls.Add(this.cardHeaderLayout, 0, 0);
		this.cardContentLayout.Controls.Add(this.titleLabel, 0, 1);
		this.cardContentLayout.Controls.Add(this.subtitleLabel, 0, 2);
		this.cardContentLayout.Controls.Add(this.emailCaptionLabel, 0, 3);
		this.cardContentLayout.Controls.Add(this.emailFieldPanel, 0, 4);
		this.cardContentLayout.Controls.Add(this.passwordCaptionLabel, 0, 5);
		this.cardContentLayout.Controls.Add(this.passwordFieldPanel, 0, 6);
		this.cardContentLayout.Controls.Add(this.optionsLayout, 0, 7);
		this.cardContentLayout.Controls.Add(this.loginButton, 0, 8);
		this.cardContentLayout.Controls.Add(this.footerFlowPanel, 0, 9);
		this.cardContentLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardContentLayout.Location = new System.Drawing.Point(38, 34);
		this.cardContentLayout.Margin = new System.Windows.Forms.Padding(0);
		this.cardContentLayout.Name = "cardContentLayout";
		this.cardContentLayout.RowCount = 11;
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.cardContentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.cardContentLayout.Size = new System.Drawing.Size(618, 645);
		this.cardContentLayout.TabIndex = 0;
		this.cardHeaderLayout.AutoSize = true;
		this.cardHeaderLayout.ColumnCount = 2;
		this.cardHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.cardHeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.cardHeaderLayout.Controls.Add(this.cardBrandTextPanel, 1, 0);
		this.cardHeaderLayout.Controls.Add(this.cardLogoPictureBox, 0, 0);
		this.cardHeaderLayout.Dock = System.Windows.Forms.DockStyle.Top;
		this.cardHeaderLayout.Location = new System.Drawing.Point(0, 0);
		this.cardHeaderLayout.Margin = new System.Windows.Forms.Padding(0);
		this.cardHeaderLayout.Name = "cardHeaderLayout";
		this.cardHeaderLayout.RowCount = 1;
		this.cardHeaderLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.cardHeaderLayout.Size = new System.Drawing.Size(618, 48);
		this.cardHeaderLayout.TabIndex = 0;
		this.cardBrandTextPanel.AutoSize = true;
		this.cardBrandTextPanel.BackColor = System.Drawing.Color.Transparent;
		this.cardBrandTextPanel.Controls.Add(this.cardMetaLabel);
		this.cardBrandTextPanel.Controls.Add(this.cardAppNameLabel);
		this.cardBrandTextPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cardBrandTextPanel.Location = new System.Drawing.Point(46, 0);
		this.cardBrandTextPanel.Margin = new System.Windows.Forms.Padding(0);
		this.cardBrandTextPanel.Name = "cardBrandTextPanel";
		this.cardBrandTextPanel.Size = new System.Drawing.Size(572, 48);
		this.cardBrandTextPanel.TabIndex = 1;
		this.cardMetaLabel.AutoSize = true;
		this.cardMetaLabel.Dock = System.Windows.Forms.DockStyle.Top;
		this.cardMetaLabel.Font = new System.Drawing.Font("Segoe UI", 8.75f);
		this.cardMetaLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.cardMetaLabel.Location = new System.Drawing.Point(0, 28);
		this.cardMetaLabel.Margin = new System.Windows.Forms.Padding(0);
		this.cardMetaLabel.Name = "cardMetaLabel";
		this.cardMetaLabel.Size = new System.Drawing.Size(153, 20);
		this.cardMetaLabel.TabIndex = 1;
		this.cardMetaLabel.Text = "Cổng tài khoản NestG";
		this.cardAppNameLabel.AutoSize = true;
		this.cardAppNameLabel.Dock = System.Windows.Forms.DockStyle.Top;
		this.cardAppNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 11.5f, System.Drawing.FontStyle.Bold);
		this.cardAppNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.cardAppNameLabel.Location = new System.Drawing.Point(0, 0);
		this.cardAppNameLabel.Margin = new System.Windows.Forms.Padding(0);
		this.cardAppNameLabel.Name = "cardAppNameLabel";
		this.cardAppNameLabel.Size = new System.Drawing.Size(157, 28);
		this.cardAppNameLabel.TabIndex = 0;
		this.cardAppNameLabel.Text = "NestG Launcher";
		this.cardLogoPictureBox.Location = new System.Drawing.Point(0, 0);
		this.cardLogoPictureBox.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
		this.cardLogoPictureBox.Name = "cardLogoPictureBox";
		this.cardLogoPictureBox.Size = new System.Drawing.Size(34, 34);
		this.cardLogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.cardLogoPictureBox.TabIndex = 0;
		this.cardLogoPictureBox.TabStop = false;
		this.titleLabel.AutoSize = true;
		this.titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 28f, System.Drawing.FontStyle.Bold);
		this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.titleLabel.Location = new System.Drawing.Point(0, 74);
		this.titleLabel.Margin = new System.Windows.Forms.Padding(0, 26, 0, 0);
		this.titleLabel.Name = "titleLabel";
		this.titleLabel.Size = new System.Drawing.Size(261, 62);
		this.titleLabel.TabIndex = 1;
		this.titleLabel.Text = "Đăng nhập";
		this.subtitleLabel.AutoSize = true;
		this.subtitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.subtitleLabel.ForeColor = System.Drawing.Color.FromArgb(82, 138, 255);
		this.subtitleLabel.Location = new System.Drawing.Point(0, 140);
		this.subtitleLabel.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
		this.subtitleLabel.Name = "subtitleLabel";
		this.subtitleLabel.Size = new System.Drawing.Size(234, 28);
		this.subtitleLabel.TabIndex = 2;
		this.subtitleLabel.Text = "Chào mừng bạn quay lại";
		this.emailCaptionLabel.AutoSize = true;
		this.emailCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.emailCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.emailCaptionLabel.Location = new System.Drawing.Point(0, 202);
		this.emailCaptionLabel.Margin = new System.Windows.Forms.Padding(0, 34, 0, 10);
		this.emailCaptionLabel.Name = "emailCaptionLabel";
		this.emailCaptionLabel.Size = new System.Drawing.Size(108, 23);
		this.emailCaptionLabel.TabIndex = 3;
		this.emailCaptionLabel.Text = "Địa chỉ email";
		this.emailFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.emailFieldPanel.Controls.Add(this.emailTextBox);
		this.emailFieldPanel.Dock = System.Windows.Forms.DockStyle.Top;
		this.emailFieldPanel.Location = new System.Drawing.Point(0, 235);
		this.emailFieldPanel.Margin = new System.Windows.Forms.Padding(0);
		this.emailFieldPanel.Name = "emailFieldPanel";
		this.emailFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.emailFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.emailFieldPanel.TabIndex = 4;
		this.emailTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.emailTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.emailTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.emailTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.emailTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.emailTextBox.Location = new System.Drawing.Point(18, 15);
		this.emailTextBox.Margin = new System.Windows.Forms.Padding(0);
		this.emailTextBox.Name = "emailTextBox";
		this.emailTextBox.PlaceholderText = "example@gmail.com";
		this.emailTextBox.Size = new System.Drawing.Size(582, 25);
		this.emailTextBox.TabIndex = 0;
		this.passwordCaptionLabel.AutoSize = true;
		this.passwordCaptionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.passwordCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.passwordCaptionLabel.Location = new System.Drawing.Point(0, 317);
		this.passwordCaptionLabel.Margin = new System.Windows.Forms.Padding(0, 28, 0, 10);
		this.passwordCaptionLabel.Name = "passwordCaptionLabel";
		this.passwordCaptionLabel.Size = new System.Drawing.Size(84, 23);
		this.passwordCaptionLabel.TabIndex = 5;
		this.passwordCaptionLabel.Text = "Mật khẩu";
		this.passwordFieldPanel.BackColor = System.Drawing.Color.Transparent;
		this.passwordFieldPanel.Controls.Add(this.passwordTextBox);
		this.passwordFieldPanel.Dock = System.Windows.Forms.DockStyle.Top;
		this.passwordFieldPanel.Location = new System.Drawing.Point(0, 350);
		this.passwordFieldPanel.Margin = new System.Windows.Forms.Padding(0);
		this.passwordFieldPanel.Name = "passwordFieldPanel";
		this.passwordFieldPanel.Padding = new System.Windows.Forms.Padding(18, 15, 18, 15);
		this.passwordFieldPanel.Size = new System.Drawing.Size(618, 54);
		this.passwordFieldPanel.TabIndex = 6;
		this.passwordTextBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.passwordTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.passwordTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.passwordTextBox.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.passwordTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.passwordTextBox.Location = new System.Drawing.Point(18, 15);
		this.passwordTextBox.Margin = new System.Windows.Forms.Padding(0);
		this.passwordTextBox.Name = "passwordTextBox";
		this.passwordTextBox.PlaceholderText = "Nhập mật khẩu của bạn";
		this.passwordTextBox.Size = new System.Drawing.Size(582, 25);
		this.passwordTextBox.TabIndex = 0;
		this.passwordTextBox.UseSystemPasswordChar = true;
		this.optionsLayout.ColumnCount = 2;
		this.optionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.optionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.optionsLayout.Controls.Add(this.rememberMeCheckBox, 0, 0);
		this.optionsLayout.Controls.Add(this.forgotPasswordLink, 1, 0);
		this.optionsLayout.Dock = System.Windows.Forms.DockStyle.Top;
		this.optionsLayout.Location = new System.Drawing.Point(0, 424);
		this.optionsLayout.Margin = new System.Windows.Forms.Padding(0, 20, 0, 0);
		this.optionsLayout.Name = "optionsLayout";
		this.optionsLayout.RowCount = 1;
		this.optionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.optionsLayout.Size = new System.Drawing.Size(618, 28);
		this.optionsLayout.TabIndex = 7;
		this.rememberMeCheckBox.AutoSize = true;
		this.rememberMeCheckBox.Cursor = System.Windows.Forms.Cursors.Hand;
		this.rememberMeCheckBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.rememberMeCheckBox.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.rememberMeCheckBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.rememberMeCheckBox.Location = new System.Drawing.Point(0, 0);
		this.rememberMeCheckBox.Margin = new System.Windows.Forms.Padding(0);
		this.rememberMeCheckBox.Name = "rememberMeCheckBox";
		this.rememberMeCheckBox.Size = new System.Drawing.Size(89, 27);
		this.rememberMeCheckBox.TabIndex = 0;
		this.rememberMeCheckBox.Text = "Ghi nhớ";
		this.rememberMeCheckBox.UseVisualStyleBackColor = true;
		this.forgotPasswordLink.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.forgotPasswordLink.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.forgotPasswordLink.AutoSize = true;
		this.forgotPasswordLink.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.forgotPasswordLink.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.forgotPasswordLink.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.forgotPasswordLink.Location = new System.Drawing.Point(480, 0);
		this.forgotPasswordLink.Margin = new System.Windows.Forms.Padding(0);
		this.forgotPasswordLink.Name = "forgotPasswordLink";
		this.forgotPasswordLink.Size = new System.Drawing.Size(138, 23);
		this.forgotPasswordLink.TabIndex = 1;
		this.forgotPasswordLink.TabStop = true;
		this.forgotPasswordLink.Text = "Quên mật khẩu?";
		this.forgotPasswordLink.VisitedLinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.forgotPasswordLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(forgotPasswordLink_LinkClicked);
		this.loginButton.BackColor = System.Drawing.Color.Transparent;
		this.loginButton.Dock = System.Windows.Forms.DockStyle.Top;
		this.loginButton.FlatAppearance.BorderSize = 0;
		this.loginButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.loginButton.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold);
		this.loginButton.ForeColor = System.Drawing.Color.White;
		this.loginButton.Location = new System.Drawing.Point(0, 480);
		this.loginButton.Margin = new System.Windows.Forms.Padding(0, 28, 0, 0);
		this.loginButton.MinimumSize = new System.Drawing.Size(0, 56);
		this.loginButton.Name = "loginButton";
		this.loginButton.Size = new System.Drawing.Size(618, 56);
		this.loginButton.TabIndex = 8;
		this.loginButton.Text = "Đăng nhập";
		this.loginButton.UseVisualStyleBackColor = true;
		this.loginButton.Click += new System.EventHandler(loginButton_Click);
		this.footerFlowPanel.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.footerFlowPanel.AutoSize = true;
		this.footerFlowPanel.Controls.Add(this.footerPromptLabel);
		this.footerFlowPanel.Controls.Add(this.registerLinkLabel);
		this.footerFlowPanel.Location = new System.Drawing.Point(155, 562);
		this.footerFlowPanel.Margin = new System.Windows.Forms.Padding(0, 26, 0, 0);
		this.footerFlowPanel.Name = "footerFlowPanel";
		this.footerFlowPanel.Size = new System.Drawing.Size(307, 27);
		this.footerFlowPanel.TabIndex = 9;
		this.footerFlowPanel.WrapContents = false;
		this.footerPromptLabel.AutoSize = true;
		this.footerPromptLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.footerPromptLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.footerPromptLabel.Location = new System.Drawing.Point(0, 4);
		this.footerPromptLabel.Margin = new System.Windows.Forms.Padding(0, 4, 4, 0);
		this.footerPromptLabel.Name = "footerPromptLabel";
		this.footerPromptLabel.Size = new System.Drawing.Size(157, 23);
		this.footerPromptLabel.TabIndex = 0;
		this.footerPromptLabel.Text = "Chưa có tài khoản?";
		this.registerLinkLabel.ActiveLinkColor = System.Drawing.Color.FromArgb(192, 216, 255);
		this.registerLinkLabel.AutoSize = true;
		this.registerLinkLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.registerLinkLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.registerLinkLabel.LinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.registerLinkLabel.Location = new System.Drawing.Point(161, 3);
		this.registerLinkLabel.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
		this.registerLinkLabel.Name = "registerLinkLabel";
		this.registerLinkLabel.Size = new System.Drawing.Size(146, 23);
		this.registerLinkLabel.TabIndex = 1;
		this.registerLinkLabel.TabStop = true;
		this.registerLinkLabel.Text = "Đăng ký miễn phí";
		this.registerLinkLabel.VisitedLinkColor = System.Drawing.Color.FromArgb(220, 232, 255);
		this.registerLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(registerLinkLabel_LinkClicked);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		this.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		base.ClientSize = new System.Drawing.Size(1264, 761);
		base.Controls.Add(this.rootLayout);
		this.MinimumSize = new System.Drawing.Size(980, 720);
		base.Name = "LoginForm";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "NestG Launcher";
		this.rootLayout.ResumeLayout(false);
		this.heroPanel.ResumeLayout(false);
		this.heroContentLayout.ResumeLayout(false);
		this.heroContentLayout.PerformLayout();
		this.heroHeaderLayout.ResumeLayout(false);
		this.heroHeaderLayout.PerformLayout();
		this.heroBrandTextPanel.ResumeLayout(false);
		this.heroBrandTextPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.heroLogoPictureBox).EndInit();
		this.cardPanel.ResumeLayout(false);
		this.cardContentLayout.ResumeLayout(false);
		this.cardContentLayout.PerformLayout();
		this.cardHeaderLayout.ResumeLayout(false);
		this.cardHeaderLayout.PerformLayout();
		this.cardBrandTextPanel.ResumeLayout(false);
		this.cardBrandTextPanel.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.cardLogoPictureBox).EndInit();
		this.emailFieldPanel.ResumeLayout(false);
		this.emailFieldPanel.PerformLayout();
		this.passwordFieldPanel.ResumeLayout(false);
		this.passwordFieldPanel.PerformLayout();
		this.optionsLayout.ResumeLayout(false);
		this.optionsLayout.PerformLayout();
		this.footerFlowPanel.ResumeLayout(false);
		this.footerFlowPanel.PerformLayout();
		base.ResumeLayout(false);
	}
}
