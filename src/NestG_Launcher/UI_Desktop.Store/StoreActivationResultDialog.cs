using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using app_desktop.Services;
using app_desktop.Services.CloudRedirect;

namespace UI_Desktop.Store;

internal sealed class StoreActivationResultDialog : Form
{
	private enum ActivationDialogState
	{
		Success,
		Warning,
		Error
	}

	private sealed record DialogPalette(string BadgeText, Color BadgeBackColor, Color BadgeForeColor, Color AccentColor, Color BorderColor);

	private readonly DesktopLibraryDetailResponse detail;

	private readonly GameActivationResult? activationResult;

	private readonly string errorMessage;

	private readonly ActivationDialogState state;

	private readonly Button closeButton = new Button();

	public StoreActivationResultDialog(DesktopLibraryDetailResponse detail, GameActivationResult activationResult)
	{
		this.detail = detail;
		this.activationResult = activationResult;
		errorMessage = string.Empty;
		state = ((!activationResult.SteamInstall.ActivationSucceeded) ? ActivationDialogState.Warning : ActivationDialogState.Success);
		InitializeComponent();
	}

	public StoreActivationResultDialog(DesktopLibraryDetailResponse detail, string? errorMessage)
	{
		this.detail = detail;
		activationResult = null;
		this.errorMessage = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể active game lúc này." : errorMessage.Trim());
		state = ActivationDialogState.Error;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		bool flag = this.IsCompactSuccess();
		this.Text = "Kich hoat game";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.ClientSize = (flag ? new System.Drawing.Size(480, 320) : new System.Drawing.Size(820, 720));
		this.MinimumSize = base.ClientSize;
		this.BackColor = UI_Desktop.AppTheme.WindowBackground;
		this.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		UI_Desktop.Store.StoreUiHelper.ApplyWindowIcon(this);
		UI_Desktop.Store.StoreUiHelper.EnableDoubleBuffer(this);
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			ColumnCount = 1,
			RowCount = (flag ? 2 : 3),
			Padding = (flag ? new System.Windows.Forms.Padding(22) : new System.Windows.Forms.Padding(24)),
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		if (flag)
		{
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64f));
		}
		else
		{
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72f));
		}
		UI_Desktop.Store.StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(this.BuildHeroCard(), 0, 0);
		if (flag)
		{
			tableLayoutPanel.Controls.Add(this.BuildActionBar(), 0, 1);
		}
		else
		{
			tableLayoutPanel.Controls.Add(this.BuildContentCard(), 0, 1);
			tableLayoutPanel.Controls.Add(this.BuildActionBar(), 0, 2);
		}
		base.Controls.Add(tableLayoutPanel);
		this.closeButton.Text = ((this.state == UI_Desktop.Store.StoreActivationResultDialog.ActivationDialogState.Error) ? "Đóng" : "Hoàn tất");
		this.closeButton.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.closeButton.FlatAppearance.BorderSize = 1;
		this.closeButton.FlatAppearance.BorderColor = this.ResolvePalette().AccentColor;
		this.closeButton.BackColor = System.Drawing.Color.FromArgb(24, 33, 45);
		this.closeButton.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.closeButton.Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.closeButton.Cursor = System.Windows.Forms.Cursors.Hand;
		base.AcceptButton = this.closeButton;
		base.CancelButton = this.closeButton;
		base.Shown += delegate
		{
			this.closeButton.Focus();
		};
		base.ResumeLayout(false);
	}

	private Control BuildHeroCard()
	{
		DialogPalette dialogPalette = ResolvePalette();
		bool isCompactSuccess = IsCompactSuccess();
		RoundedPanel card = CreateCard(Color.FromArgb(16, 22, 31), dialogPalette.BorderColor, isCompactSuccess ? new Padding(24) : new Padding(22));
		card.AutoSize = true;
		card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		card.MinimumSize = new Size(0, isCompactSuccess ? 190 : 196);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Color.Transparent,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			RowCount = 1,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			RowCount = 1,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76f));
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		PictureBox pictureBox = new PictureBox
		{
			Dock = DockStyle.Top,
			Margin = Padding.Empty,
			Size = new Size(68, 68),
			SizeMode = PictureBoxSizeMode.Zoom
		};
		StoreUiHelper.LoadLogo(pictureBox);
		TableLayoutPanel textLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 3,
			Margin = new Padding(12, 0, 18, 0),
			BackColor = Color.Transparent
		};
		textLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		textLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		textLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		textLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		Label gameLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 2, 0, 2),
			ForeColor = dialogPalette.AccentColor,
			Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point),
			Text = detail.GameName,
			Visible = !isCompactSuccess
		};
		Label titleLabel = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold, GraphicsUnit.Point),
			Text = ResolveHeading()
		};
		Label metaLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 2, 0, 0),
			ForeColor = AppTheme.MutedText,
			Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
			Text = BuildMetaText(),
			Visible = !isCompactSuccess
		};
		RoundedPanel badgePanel = new RoundedPanel
		{
			Dock = DockStyle.Top,
			Width = 160,
			Height = 70,
			Margin = new Padding(0, 8, 0, 0),
			SurfaceColor = dialogPalette.BadgeBackColor,
			BorderColor = dialogPalette.BorderColor,
			BorderThickness = 1,
			CornerRadius = 20
		};
		Label value = new Label
		{
			Dock = DockStyle.Fill,
			ForeColor = dialogPalette.BadgeForeColor,
			Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point),
			Text = dialogPalette.BadgeText,
			TextAlign = ContentAlignment.MiddleCenter,
			Padding = new Padding(10, 0, 10, 0)
		};
		badgePanel.Controls.Add(value);
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Color.Transparent,
			Margin = Padding.Empty,
			Visible = !isCompactSuccess
		};
		panel.Controls.Add(badgePanel);
		tableLayoutPanel3.Controls.Add(pictureBox, 0, 0);
		tableLayoutPanel3.Controls.Add(textLayout, 1, 0);
		textLayout.Controls.Add(gameLabel, 0, 0);
		textLayout.Controls.Add(titleLabel, 0, 1);
		textLayout.Controls.Add(metaLabel, 0, 2);
		Label messageLabel = new Label
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			Margin = new Padding(0, 16, 0, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10.1f, FontStyle.Regular, GraphicsUnit.Point),
			Text = ResolveSummaryMessage()
		};
		tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 0, 0);
		if (!isCompactSuccess)
		{
			tableLayoutPanel2.Controls.Add(panel, 1, 0);
		}
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 0);
		tableLayoutPanel.Controls.Add(messageLabel, 0, 1);
		card.Controls.Add(tableLayoutPanel);
		card.Resize += delegate
		{
			ApplyHeroConstraints();
		};
		ApplyHeroConstraints();
		return card;
		void ApplyHeroConstraints()
		{
			int num = ((!isCompactSuccess) ? badgePanel.Width : 0);
			int num2 = Math.Max(isCompactSuccess ? 360 : 420, card.ClientSize.Width - card.Padding.Horizontal);
			int num3 = Math.Max(260, num2 - num - ((!isCompactSuccess) ? 28 : 0));
			int width = Math.Max(220, num3 - 76 - textLayout.Margin.Horizontal);
			gameLabel.MaximumSize = new Size(width, 0);
			titleLabel.MaximumSize = new Size(width, 0);
			metaLabel.MaximumSize = new Size(width, 0);
			messageLabel.MaximumSize = new Size(num2, 0);
		}
	}

	private Control BuildContentCard()
	{
		RoundedPanel roundedPanel = CreateCard(Color.FromArgb(17, 23, 31), Color.FromArgb(49, 62, 82), new Padding(18));
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 28,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold, GraphicsUnit.Point),
			Text = ((state == ActivationDialogState.Error) ? "Thông tin và gợi ý xử lý" : "Thông tin kích hoạt"),
			TextAlign = ContentAlignment.MiddleLeft
		};
		StoreScrollPanel storeScrollPanel = new StoreScrollPanel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Color.Transparent,
			Margin = new Padding(0, 10, 0, 0)
		};
		StoreUiHelper.EnableDoubleBuffer(storeScrollPanel);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(CreateSectionLabel("Tổng quan"));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Trạng thái", ResolveStatusMetric(), ResolveStatusColor()));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Phiên bản", NormalizeValue(detail.VersionName, "Mặc định"), AppTheme.PrimaryText));
		tableLayoutPanel.Controls.Add(CreateInfoLine("File đồng bộ", ResolveLocalFilesMetric(), Color.FromArgb(255, 214, 136)));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Steam sync", ResolveSteamSyncMetric(), ResolveSteamSyncColor()));
		tableLayoutPanel.Controls.Add(CreateSectionSpacer());
		tableLayoutPanel.Controls.Add(CreateSectionLabel((state == ActivationDialogState.Success) ? "Hướng dẫn tiếp theo" : "Cần lưu ý"));
		foreach (string item in BuildMessages())
		{
			tableLayoutPanel.Controls.Add(CreateMessageRow(item));
		}
		storeScrollPanel.Controls.Add(tableLayoutPanel);
		roundedPanel.Controls.Add(storeScrollPanel);
		roundedPanel.Controls.Add(value);
		return roundedPanel;
	}

	private Control BuildActionBar()
	{
		Panel actionPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Transparent,
			Margin = Padding.Empty,
			Padding = new Padding(0, 8, 0, 0)
		};
		closeButton.Size = new Size(132, 48);
		closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		closeButton.Location = new Point(Math.Max(0, actionPanel.Width - closeButton.Width), 8);
		actionPanel.Resize += delegate
		{
			closeButton.Location = new Point(Math.Max(0, actionPanel.ClientSize.Width - closeButton.Width), Math.Max(8, (actionPanel.ClientSize.Height - closeButton.Height) / 2));
		};
		actionPanel.Controls.Add(closeButton);
		return actionPanel;
	}

	private Label CreateSectionLabel(string text)
	{
		return new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 8),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = text
		};
	}

	private Control CreateSectionSpacer()
	{
		return new Panel
		{
			Height = 8,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
	}

	private Control CreateInfoLine(string caption, string value, Color valueColor)
	{
		TableLayoutPanel obj = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			Margin = new Padding(0, 0, 0, 12),
			Padding = Padding.Empty,
			BackColor = Color.Transparent,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.6f, FontStyle.Regular, GraphicsUnit.Point),
			Text = caption.ToUpperInvariant()
		};
		Label control2 = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 4, 0, 0),
			MaximumSize = new Size(704, 0),
			ForeColor = valueColor,
			Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = value
		};
		Panel control3 = new Panel
		{
			Height = 1,
			Width = 704,
			Margin = new Padding(0, 10, 0, 0),
			BackColor = Color.FromArgb(47, 60, 78)
		};
		obj.Controls.Add(control, 0, 0);
		obj.Controls.Add(control2, 0, 1);
		obj.Controls.Add(control3, 0, 2);
		return obj;
	}

	private Control CreateMessageRow(string message)
	{
		TableLayoutPanel obj = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			Margin = new Padding(0, 0, 0, 10),
			Padding = Padding.Empty,
			BackColor = Color.Transparent,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Absolute, 18f),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		Label control = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 0),
			ForeColor = AppTheme.Accent,
			Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "•"
		};
		Label control2 = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			MaximumSize = new Size(684, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.6f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message
		};
		obj.Controls.Add(control, 0, 0);
		obj.Controls.Add(control2, 1, 0);
		return obj;
	}

	private RoundedPanel CreateCard(Color surfaceColor, Color borderColor, Padding padding)
	{
		return new RoundedPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = padding,
			SurfaceColor = surfaceColor,
			BorderColor = borderColor,
			BorderThickness = 1,
			CornerRadius = 18
		};
	}

	private string ResolveHeading()
	{
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult != null && gameActivationResult.SteamInstall.VerificationPending)
		{
			return "Chưa hoàn tất Active";
		}
		GameActivationResult? gameActivationResult2 = activationResult;
		if ((object)gameActivationResult2 != null && gameActivationResult2.SteamInstall.ActivationSucceeded)
		{
			return "Active thành công";
		}
		GameActivationResult? gameActivationResult3 = activationResult;
		if ((object)gameActivationResult3 != null && gameActivationResult3.SteamInstall.BetterOnly)
		{
			return "Chưa hoàn tất Active";
		}
		GameActivationResult? gameActivationResult4 = activationResult;
		if ((object)gameActivationResult4 != null && gameActivationResult4.SteamInstall.WasRolledBack)
		{
			return "CloudRedirect đã được rollback";
		}
		SteamInstallResult steamInstallResult = activationResult?.SteamInstall;
		if ((object)steamInstallResult != null && steamInstallResult.IsFound && steamInstallResult.IsSteamRestarted)
		{
			CloudRedirectHealthResult cloudRedirectHealth = steamInstallResult.CloudRedirectHealth;
			if ((object)cloudRedirectHealth != null && !cloudRedirectHealth.IsReady)
			{
				return "Cần kiểm tra CloudRedirect";
			}
		}
		GameActivationResult? gameActivationResult5 = activationResult;
		if ((object)gameActivationResult5 != null && gameActivationResult5.SteamInstall.IsFound && !activationResult.SteamInstall.IsSteamRestarted)
		{
			return "Đã copy file, cần mở lại Steam";
		}
		return state switch
		{
			ActivationDialogState.Success => "Active thành công", 
			ActivationDialogState.Warning => "Chưa tìm thấy Steam", 
			_ => "Không thể kích hoạt game", 
		};
	}

	private string ResolveSummaryMessage()
	{
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult != null && gameActivationResult.SteamInstall.VerificationPending)
		{
			return activationResult.SteamInstall.RecoveryMessage;
		}
		GameActivationResult? gameActivationResult2 = activationResult;
		if ((object)gameActivationResult2 != null && gameActivationResult2.SteamInstall.ActivationSucceeded)
		{
			return "Game đã được active thành công.";
		}
		GameActivationResult? gameActivationResult3 = activationResult;
		if ((object)gameActivationResult3 != null && gameActivationResult3.SteamInstall.BetterOnly)
		{
			return "Chưa xác nhận được Steam và thành phần kích hoạt. Hãy thử Active lại.";
		}
		GameActivationResult? gameActivationResult4 = activationResult;
		if ((object)gameActivationResult4 != null && gameActivationResult4.SteamInstall.WasRolledBack)
		{
			return "CloudRedirect không thể khởi tạo nên launcher đã khôi phục DLL và cấu hình trước đó. Launcher không backup hoặc khôi phục save game; hãy kiểm tra bản backup save của bạn.";
		}
		SteamInstallResult steamInstallResult = activationResult?.SteamInstall;
		if ((object)steamInstallResult != null && steamInstallResult.IsFound && steamInstallResult.IsSteamRestarted)
		{
			CloudRedirectHealthResult cloudRedirectHealth = steamInstallResult.CloudRedirectHealth;
			if ((object)cloudRedirectHealth != null && !cloudRedirectHealth.IsReady)
			{
				return cloudRedirectHealth.Message;
			}
		}
		GameActivationResult? gameActivationResult5 = activationResult;
		if ((object)gameActivationResult5 != null && gameActivationResult5.SteamInstall.IsFound && !activationResult.SteamInstall.IsSteamRestarted)
		{
			return "Launcher đã đồng bộ file vào Steam, nhưng chưa thể khởi động lại Steam tự động trên máy này.";
		}
		return state switch
		{
			ActivationDialogState.Success => "Game đã được active thành công.", 
			ActivationDialogState.Warning => "Launcher đã tải gói kích hoạt tạm thời, nhưng Steam chưa được phát hiện trên máy này. File tạm đã được dọn dẹp sau khi thử active.", 
			_ => errorMessage, 
		};
	}

	private bool IsCompactSuccess()
	{
		return state == ActivationDialogState.Success;
	}

	private string BuildMetaText()
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.VersionName))
		{
			list.Add(detail.VersionName);
		}
		if (!string.IsNullOrWhiteSpace(detail.OrderCode))
		{
			list.Add("Order " + detail.OrderCode);
		}
		if (!string.IsNullOrWhiteSpace(detail.AcquiredAtText))
		{
			list.Add(detail.AcquiredAtText);
		}
		if (list.Count != 0)
		{
			return string.Join("  •  ", list);
		}
		return "Launcher đã sẵn sàng hoàn tất kích hoạt.";
	}

	private string ResolveStatusMetric()
	{
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult != null && gameActivationResult.SteamInstall.VerificationPending)
		{
			return "Chờ kiểm tra";
		}
		GameActivationResult? gameActivationResult2 = activationResult;
		if ((object)gameActivationResult2 != null && gameActivationResult2.SteamInstall.ActivationSucceeded)
		{
			return "Hoàn tất";
		}
		GameActivationResult? gameActivationResult3 = activationResult;
		if ((object)gameActivationResult3 != null && gameActivationResult3.SteamInstall.BetterOnly)
		{
			return "Cần kiểm tra";
		}
		GameActivationResult? gameActivationResult4 = activationResult;
		if ((object)gameActivationResult4 != null && gameActivationResult4.SteamInstall.WasRolledBack)
		{
			return "Đã rollback";
		}
		SteamInstallResult steamInstallResult = activationResult?.SteamInstall;
		if ((object)steamInstallResult != null && steamInstallResult.IsFound && steamInstallResult.IsSteamRestarted)
		{
			CloudRedirectHealthResult cloudRedirectHealth = steamInstallResult.CloudRedirectHealth;
			if ((object)cloudRedirectHealth != null && !cloudRedirectHealth.IsReady)
			{
				return "Kiểm tra Cloud";
			}
		}
		GameActivationResult? gameActivationResult5 = activationResult;
		if ((object)gameActivationResult5 != null && gameActivationResult5.SteamInstall.IsFound && !activationResult.SteamInstall.IsSteamRestarted)
		{
			return "Cần mở lại Steam";
		}
		return state switch
		{
			ActivationDialogState.Success => "Đã reset Steam", 
			ActivationDialogState.Warning => "Cần mở Steam", 
			_ => "Cần thử lại", 
		};
	}

	private string ResolveLocalFilesMetric()
	{
		if ((object)activationResult != null)
		{
			return $"{activationResult.WrittenFiles.Count} tep";
		}
		return "-";
	}

	private string ResolveSteamSyncMetric()
	{
		if ((object)activationResult != null)
		{
			if (!activationResult.SteamInstall.IsFound)
			{
				return "Chưa đồng bộ";
			}
			return $"{activationResult.SteamInstall.CopiedFiles.Count} tep";
		}
		return "-";
	}

	private string ResolveGameDirectoryValue()
	{
		return activationResult?.TargetDirectory ?? "Thư mục log sẽ xuất hiện sau khi launcher thử active.";
	}

	private string ResolveSteamDirectoryValue()
	{
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult != null && gameActivationResult.SteamInstall.IsFound && !string.IsNullOrWhiteSpace(activationResult.SteamInstall.SteamDirectory))
		{
			return activationResult.SteamInstall.SteamDirectory;
		}
		return "Không tìm thấy Steam trên máy này.";
	}

	private string ResolveLogFileValue()
	{
		return activationResult?.LogFilePath ?? "Chưa tạo file log vì quá trình active dừng sớm.";
	}

	private IReadOnlyList<string> BuildMessages()
	{
		List<string> list = new List<string>();
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult != null && gameActivationResult.SteamInstall.VerificationPending)
		{
			list.Add(activationResult.SteamInstall.RecoveryMessage);
			list.Add("Hãy kiểm tra Steam rồi bấm Active để tiếp tục xác minh.");
		}
		else if (state == ActivationDialogState.Success)
		{
			list.Add("Đã xác minh và cài thành phần kích hoạt.");
			list.Add("Lua activation đã được nạp vào Steam\\config\\lua.");
			list.Add("Launcher đã tự động đóng/mở lại Steam sau khi copy file active xong.");
			list.Add("Nếu game chưa hiện trong Steam, bạn có thể mở file log để kiểm tra các file đã được đồng bộ.");
		}
		else
		{
			GameActivationResult? gameActivationResult2 = activationResult;
			if ((object)gameActivationResult2 != null && gameActivationResult2.SteamInstall.BetterOnly)
			{
				list.Add("Chưa xác nhận được Steam và thành phần kích hoạt. Hãy thử Active lại.");
			}
			else
			{
				SteamInstallResult steamInstallResult = activationResult?.SteamInstall;
				if ((object)steamInstallResult != null && steamInstallResult.IsFound && steamInstallResult.IsSteamRestarted)
				{
					CloudRedirectHealthResult cloudRedirectHealth = steamInstallResult.CloudRedirectHealth;
					if ((object)cloudRedirectHealth != null && !cloudRedirectHealth.IsReady)
					{
						list.Add(cloudRedirectHealth.Message);
						list.Add("CloudRedirect log: " + cloudRedirectHealth.LogPath);
						goto IL_0190;
					}
				}
				GameActivationResult? gameActivationResult3 = activationResult;
				if ((object)gameActivationResult3 != null && gameActivationResult3.SteamInstall.IsFound)
				{
					list.Add("Đã copy runtime BetterSteamTools + CloudRedirect và Lua activation vào Steam.");
					list.Add("Lua target: Steam\\config\\lua.");
					list.Add("Launcher đã copy file vào Steam nhưng reset Steam chưa hoàn tất.");
					list.Add("Hãy đóng/mở lại Steam thủ công để thư viện nạp game vừa active.");
				}
				else if (state == ActivationDialogState.Warning)
				{
					list.Add("Launcher đã tải file active vào thư mục tạm, nhưng chưa copy tiếp sang Steam vì chưa tìm thấy Steam.");
					list.Add("File tạm đã được dọn dẹp sau lần thử active này.");
					list.Add("Hãy cài đặt hoặc mở Steam ít nhất một lần, sau đó bấm Active lại.");
				}
				else
				{
					list.Add(errorMessage);
					list.Add("Xem chi tiết lỗi để biết bước cần xử lý trước khi Active lại.");
				}
			}
		}
		goto IL_0190;
		IL_0190:
		if ((object)activationResult != null && state != 0)
		{
			foreach (string message in activationResult.SteamInstall.Messages)
			{
				if (!string.IsNullOrWhiteSpace(message) && !message.StartsWith("Steam:", StringComparison.OrdinalIgnoreCase) && !message.Contains("->", StringComparison.Ordinal))
				{
					list.Add(message);
				}
			}
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private DialogPalette ResolvePalette()
	{
		switch (state)
		{
		case ActivationDialogState.Success:
			return new DialogPalette("Steam đã sẵn sàng", Color.FromArgb(27, 61, 51), Color.FromArgb(172, 237, 208), Color.FromArgb(73, 209, 164), Color.FromArgb(53, 96, 84));
		case ActivationDialogState.Warning:
		{
			GameActivationResult? gameActivationResult = activationResult;
			return new DialogPalette(((object)gameActivationResult != null && gameActivationResult.SteamInstall.VerificationPending) ? "Chờ kiểm tra" : "Cần mở Steam", Color.FromArgb(72, 54, 28), Color.FromArgb(255, 224, 162), Color.FromArgb(255, 190, 92), Color.FromArgb(99, 78, 44));
		}
		default:
			return new DialogPalette("Thử lại sau", Color.FromArgb(78, 38, 44), Color.FromArgb(255, 205, 208), Color.FromArgb(233, 91, 104), Color.FromArgb(104, 54, 62));
		}
	}

	private Color ResolveStatusColor()
	{
		return state switch
		{
			ActivationDialogState.Success => Color.FromArgb(172, 237, 208), 
			ActivationDialogState.Warning => Color.FromArgb(255, 224, 162), 
			_ => Color.FromArgb(255, 205, 208), 
		};
	}

	private Color ResolveSteamSyncColor()
	{
		GameActivationResult? gameActivationResult = activationResult;
		if ((object)gameActivationResult == null || !gameActivationResult.SteamInstall.IsFound || !activationResult.SteamInstall.IsSteamRestarted)
		{
			if (state != ActivationDialogState.Error)
			{
				return Color.FromArgb(255, 224, 162);
			}
			return AppTheme.MutedText;
		}
		return Color.FromArgb(172, 237, 208);
	}

	private static string NormalizeValue(string? value, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return fallback;
	}
}
