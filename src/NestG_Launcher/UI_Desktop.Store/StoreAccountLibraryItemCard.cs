using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreAccountLibraryItemCard : RoundedPanel
{
	private readonly DesktopAccountLibraryItemResponse item;

	private readonly PictureBox artworkPictureBox;

	private readonly Label statusLabel;

	private readonly Label titleLabel;

	private readonly Label versionLabel;

	private readonly Label metaLabel;

	private readonly Label orderLabel;

	private Image? fallbackArtwork;

	private bool isHovered;

	private int artworkRequestId;

	public StoreAccountLibraryItemCard(DesktopAccountLibraryItemResponse item)
	{
		this.item = item;
		BackColor = Color.Transparent;
		base.Margin = new Padding(0, 0, 0, 14);
		base.Padding = new Padding(ScaleValue(14));
		base.Height = ScaleValue(124);
		MinimumSize = new Size(ScaleValue(520), ScaleValue(124));
		base.SurfaceColor = Color.FromArgb(18, 24, 33);
		base.BorderColor = AppTheme.CardBorder;
		base.BorderThickness = 1;
		base.CornerRadius = ScaleValue(16);
		artworkPictureBox = new PictureBox
		{
			Size = new Size(ScaleValue(96), ScaleValue(96)),
			BackColor = Color.FromArgb(10, 15, 22),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		statusLabel = new Label
		{
			AutoSize = false,
			Size = new Size(ScaleValue(110), ScaleValue(24)),
			BackColor = Color.FromArgb(132, 184, 40, 49),
			ForeColor = Color.FromArgb(255, 237, 238),
			Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.StatusText.ToUpperInvariant(),
			TextAlign = ContentAlignment.MiddleCenter
		};
		titleLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.1f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.GameName,
			AutoEllipsis = true
		};
		versionLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(255, 133, 133),
			Font = new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.VersionName,
			AutoEllipsis = true
		};
		metaLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = item.Category + "  |  " + item.AcquiredAtText,
			AutoEllipsis = true
		};
		orderLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(138, 156, 181),
			Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
			Text = "Order " + item.OrderCode,
			AutoEllipsis = true
		};
		base.Controls.Add(orderLabel);
		base.Controls.Add(metaLabel);
		base.Controls.Add(versionLabel);
		base.Controls.Add(titleLabel);
		base.Controls.Add(statusLabel);
		base.Controls.Add(artworkPictureBox);
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
		UpdateArtwork();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			fallbackArtwork?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void LayoutContent()
	{
		int num = ScaleValue(14);
		artworkPictureBox.Location = new Point(num, num);
		int num2 = artworkPictureBox.Right + ScaleValue(16);
		int width = Math.Max(ScaleValue(220), base.Width - num2 - num);
		statusLabel.Location = new Point(num2, num);
		titleLabel.Location = new Point(num2, statusLabel.Bottom + ScaleValue(10));
		titleLabel.Size = new Size(width, ScaleValue(26));
		versionLabel.Location = new Point(num2, titleLabel.Bottom + ScaleValue(4));
		versionLabel.Size = new Size(width, ScaleValue(22));
		metaLabel.Location = new Point(num2, versionLabel.Bottom + ScaleValue(6));
		metaLabel.Size = new Size(width, ScaleValue(18));
		orderLabel.Location = new Point(num2, metaLabel.Bottom + ScaleValue(4));
		orderLabel.Size = new Size(width, ScaleValue(18));
	}

	private async void UpdateArtwork()
	{
		StoreGame game = CreatePreviewGame();
		fallbackArtwork?.Dispose();
		fallbackArtwork = StoreArtworkFactory.CreateCover(game, artworkPictureBox.Size);
		artworkPictureBox.Image = fallbackArtwork;
		string text = ((!string.IsNullOrWhiteSpace(item.PosterImageUrl)) ? item.PosterImageUrl : item.SpotlightImageUrl);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		Size size = artworkPictureBox.Size;
		if (StoreRemoteImageCache.TryGetPrepared(text, size, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			artworkPictureBox.Image = image;
			return;
		}
		int requestId = ++artworkRequestId;
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(text, size, StoreImageResizeMode.Stretch);
		if (!base.IsDisposed && !artworkPictureBox.IsDisposed && requestId == artworkRequestId)
		{
			artworkPictureBox.Image = image2 ?? fallbackArtwork;
		}
	}

	private StoreGame CreatePreviewGame()
	{
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed($"{item.GameId:N}:{item.GameName}:{item.VersionName}");
		return new StoreGame
		{
			GameId = item.GameId,
			Title = item.GameName,
			Subtitle = item.VersionName,
			Category = item.Category,
			Genres = new _003C_003Ez__ReadOnlySingleElementList<string>(item.Category),
			BadgeText = item.StatusText,
			PosterImageUrl = item.PosterImageUrl,
			SpotlightImageUrl = item.SpotlightImageUrl,
			OriginalPrice = 0m,
			SalePrice = 0m,
			Likes = 0,
			Dislikes = 0,
			PurchaseCount = 0,
			PromoLabel = "OWNED",
			IsTrending = false,
			PrimaryColor = storeGamePalette.PrimaryColor,
			SecondaryColor = storeGamePalette.SecondaryColor,
			AccentColor = storeGamePalette.AccentColor
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
