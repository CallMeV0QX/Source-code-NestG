using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreLibraryEntryCard : RoundedPanel
{
	private readonly DesktopLibraryListItemResponse item;

	private readonly RoundedPanel artworkHost;

	private readonly PictureBox artworkPictureBox;

	private readonly Label badgeLabel;

	private Image? fallbackArtwork;

	private bool isHovered;

	private bool isSelected;

	private int artworkRequestId;

	private Size lastArtworkSize = Size.Empty;

	public DesktopLibraryListItemResponse Item => item;

	public event EventHandler? Selected;

	public StoreLibraryEntryCard(DesktopLibraryListItemResponse item)
	{
		this.item = item;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.Margin = new Padding(8, 0, 8, 12);
		base.Padding = new Padding(12);
		base.Height = 92;
		MinimumSize = new Size(248, 92);
		base.SurfaceColor = Color.FromArgb(18, 24, 33);
		base.BorderColor = AppTheme.CardBorder;
		base.BorderThickness = 1;
		base.CornerRadius = 14;
		artworkPictureBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(10, 15, 22),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		artworkHost = new RoundedPanel
		{
			Dock = DockStyle.None,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			SurfaceColor = Color.FromArgb(10, 15, 22),
			BorderColor = Color.FromArgb(42, 56, 73),
			BorderThickness = 1,
			CornerRadius = 12
		};
		artworkHost.Controls.Add(artworkPictureBox);
		badgeLabel = new Label
		{
			AutoSize = true,
			BackColor = Color.FromArgb(146, 34, 110, 72),
			ForeColor = Color.FromArgb(236, 255, 244),
			Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = (string.IsNullOrWhiteSpace(item.Badge) ? "ĐÃ SỞ HỮU" : item.Badge),
			Padding = new Padding(10, 5, 10, 5),
			Margin = Padding.Empty
		};
		base.Controls.Add(badgeLabel);
		base.Controls.Add(artworkHost);
		AttachSelection(this);
		base.Resize += delegate
		{
			LayoutCard(refreshArtwork: true);
		};
		base.MouseEnter += delegate
		{
			UpdateHover(hovered: true);
		};
		base.MouseLeave += delegate
		{
			UpdateHover(hovered: false);
		};
		LayoutCard(refreshArtwork: true);
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		ApplyChrome();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			fallbackArtwork?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void LayoutCard(bool refreshArtwork)
	{
		int width = Math.Max(180, base.ClientSize.Width - base.Padding.Horizontal);
		int height = Math.Max(56, base.ClientSize.Height - base.Padding.Vertical);
		artworkHost.Bounds = new Rectangle(base.Padding.Left, base.Padding.Top, width, height);
		badgeLabel.Location = new Point(artworkHost.Left + 12, artworkHost.Top + 10);
		badgeLabel.BringToFront();
		Size clientSize = artworkHost.ClientSize;
		if (refreshArtwork && clientSize.Width > 0 && clientSize.Height > 0 && !(clientSize == lastArtworkSize))
		{
			lastArtworkSize = clientSize;
			UpdateArtwork();
		}
	}

	private async void UpdateArtwork()
	{
		Size size = new Size(Math.Max(180, artworkHost.ClientSize.Width), Math.Max(56, artworkHost.ClientSize.Height));
		StoreGame game = CreatePreviewGame();
		fallbackArtwork?.Dispose();
		fallbackArtwork = StoreArtworkFactory.CreateBanner(game, size);
		artworkPictureBox.Image = fallbackArtwork;
		string text = ((!string.IsNullOrWhiteSpace(item.SpotlightImageUrl)) ? item.SpotlightImageUrl : item.PosterImageUrl);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (StoreRemoteImageCache.TryGetPrepared(text, size, StoreImageResizeMode.Cover, out Image image) && image != null)
		{
			artworkPictureBox.Image = image;
			return;
		}
		int requestId = ++artworkRequestId;
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(text, size, StoreImageResizeMode.Cover);
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
			BadgeText = item.Badge,
			PosterImageUrl = item.PosterImageUrl,
			SpotlightImageUrl = item.SpotlightImageUrl,
			OriginalPrice = 0m,
			SalePrice = 0m,
			Likes = 0,
			Dislikes = 0,
			PurchaseCount = 0,
			PromoLabel = "OWNED",
			IsTrending = item.IsTrending,
			PrimaryColor = storeGamePalette.PrimaryColor,
			SecondaryColor = storeGamePalette.SecondaryColor,
			AccentColor = storeGamePalette.AccentColor
		};
	}

	private void AttachSelection(Control control)
	{
		control.Click += delegate
		{
			this.Selected?.Invoke(this, EventArgs.Empty);
		};
		foreach (Control control2 in control.Controls)
		{
			AttachSelection(control2);
		}
	}

	private void UpdateHover(bool hovered)
	{
		isHovered = hovered;
		ApplyChrome();
	}

	private void ApplyChrome()
	{
		base.SurfaceColor = (isSelected ? Color.FromArgb(24, 36, 45) : (isHovered ? Color.FromArgb(21, 31, 40) : Color.FromArgb(18, 24, 33)));
		base.BorderColor = (isSelected ? Color.FromArgb(62, 176, 115) : (isHovered ? Color.FromArgb(90, 114, 148) : AppTheme.CardBorder));
		Invalidate();
	}
}
