using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreGameCard : RoundedPanel
{
	private readonly StoreGame game;

	private readonly PictureBox artworkPictureBox;

	private readonly Label titleLabel;

	private readonly Label originalPriceLabel;

	private readonly Label salePriceLabel;

	private readonly Label purchaseLabel;

	private readonly Label likesLabel;

	private readonly Label dislikesLabel;

	private readonly StorePromoBadge promoBadge;

	private Image? coverImage;

	private bool isSelected;

	private bool isHovered;

	private int artworkRequestId;

	private Size renderedSize;

	public StoreGame Game => game;

	public event EventHandler? Selected;

	public StoreGameCard(StoreGame game)
	{
		this.game = game;
		BackColor = Color.Transparent;
		base.SurfaceColor = AppTheme.CardSurface;
		base.BorderColor = AppTheme.CardBorder;
		base.BorderThickness = 1;
		base.CornerRadius = 20;
		Cursor = Cursors.Hand;
		base.Margin = new Padding(0, 0, 18, 18);
		base.Padding = new Padding(0);
		artworkPictureBox = new PictureBox
		{
			Dock = DockStyle.Top,
			Height = 178,
			Margin = new Padding(0),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		promoBadge = new StorePromoBadge
		{
			Location = new Point(14, 14),
			BadgeText = game.PromoLabel
		};
		titleLabel = new Label
		{
			AutoSize = false,
			AutoEllipsis = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point),
			Location = new Point(18, 194),
			Size = new Size(280, 54),
			Text = game.Title
		};
		originalPriceLabel = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.MutedText,
			Font = new Font("Segoe UI", 9f, FontStyle.Strikeout, GraphicsUnit.Point),
			Location = new Point(18, 252),
			Text = ((game.OriginalPrice > game.SalePrice) ? game.OriginalPriceText : string.Empty),
			Visible = (game.OriginalPrice > game.SalePrice)
		};
		salePriceLabel = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.Accent,
			Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point),
			Location = new Point(18, 278),
			Text = game.SalePriceText
		};
		purchaseLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.MutedText,
			Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
			AutoEllipsis = true,
			TextAlign = ContentAlignment.MiddleRight,
			Text = game.PurchaseText
		};
		likesLabel = new Label
		{
			AutoSize = true,
			ForeColor = Color.FromArgb(171, 215, 52),
			Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold, GraphicsUnit.Point),
			Text = $"+ {game.Likes}"
		};
		dislikesLabel = new Label
		{
			AutoSize = true,
			ForeColor = Color.FromArgb(255, 96, 96),
			Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold, GraphicsUnit.Point),
			Text = $"- {game.Dislikes}"
		};
		base.Controls.Add(dislikesLabel);
		base.Controls.Add(likesLabel);
		base.Controls.Add(purchaseLabel);
		base.Controls.Add(salePriceLabel);
		base.Controls.Add(originalPriceLabel);
		base.Controls.Add(titleLabel);
		base.Controls.Add(artworkPictureBox);
		artworkPictureBox.Controls.Add(promoBadge);
		promoBadge.BringToFront();
		AttachSelection(this);
		base.MouseEnter += delegate
		{
			UpdateHover(hovered: true);
		};
		base.MouseLeave += delegate
		{
			UpdateHover(hovered: false);
		};
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		ApplyChrome();
	}

	public void SetCardWidth(int width)
	{
		base.Width = Math.Max(268, width);
		int height = Math.Max(146, (int)((float)base.Width * 0.56f));
		artworkPictureBox.Height = height;
		promoBadge.Location = new Point(14, 14);
		int width2 = base.Width - 36;
		titleLabel.Size = new Size(width2, 56);
		titleLabel.Location = new Point(18, artworkPictureBox.Bottom + 14);
		int num = titleLabel.Bottom + 8;
		originalPriceLabel.Location = new Point(18, num);
		salePriceLabel.Location = new Point(18, num + (originalPriceLabel.Visible ? 20 : 10));
		int num2 = titleLabel.Bottom + 48 + 18;
		likesLabel.Location = new Point(18, num2 + 2);
		dislikesLabel.Location = new Point(likesLabel.Right + 14, num2 + 2);
		int num3 = Math.Min(156, Math.Max(110, base.Width / 3));
		purchaseLabel.Size = new Size(num3, 22);
		purchaseLabel.Location = new Point(base.Width - num3 - 18, num2);
		base.Height = num2 + purchaseLabel.Height + 22;
		UpdateArtwork();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			coverImage?.Dispose();
		}
		base.Dispose(disposing);
	}

	private async void UpdateArtwork()
	{
		if (artworkPictureBox.Width <= 0 || artworkPictureBox.Height <= 0)
		{
			return;
		}
		Size size = artworkPictureBox.Size;
		if (size == renderedSize && coverImage != null)
		{
			return;
		}
		coverImage?.Dispose();
		coverImage = StoreArtworkFactory.CreateCover(game, size);
		artworkPictureBox.Image = coverImage;
		renderedSize = size;
		string text = (string.IsNullOrWhiteSpace(game.PosterImageUrl) ? game.SpotlightImageUrl : game.PosterImageUrl);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (StoreRemoteImageCache.TryGetPrepared(text, size, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			artworkPictureBox.Image = image;
			return;
		}
		int requestId = ++artworkRequestId;
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(text, size, StoreImageResizeMode.Stretch);
		if (!base.IsDisposed && !artworkPictureBox.IsDisposed && requestId == artworkRequestId)
		{
			artworkPictureBox.Image = image2 ?? coverImage;
		}
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
		base.BorderColor = (isSelected ? AppTheme.Accent : (isHovered ? Color.FromArgb(86, 118, 180) : AppTheme.CardBorder));
		Invalidate();
	}
}
