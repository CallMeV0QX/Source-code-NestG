using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreSpotlightSidebarItem : RoundedPanel
{
	private readonly StoreGame game;

	private readonly PictureBox thumbnailPictureBox;

	private readonly Label titleLabel;

	private Image? fallbackImage;

	private int requestId;

	private bool isActive;

	public event EventHandler? Selected;

	public StoreSpotlightSidebarItem(StoreGame game)
	{
		this.game = game;
		BackColor = Color.Transparent;
		base.SurfaceColor = Color.FromArgb(24, 29, 37);
		base.BorderColor = Color.FromArgb(42, 52, 66);
		base.BorderThickness = 1;
		base.CornerRadius = 18;
		Cursor = Cursors.Hand;
		base.Height = 96;
		base.Margin = new Padding(0, 0, 0, 12);
		base.Padding = new Padding(12);
		thumbnailPictureBox = new PictureBox
		{
			Location = new Point(12, 12),
			Size = new Size(86, 72),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		titleLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.White,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
			Location = new Point(110, 14),
			Text = game.Title
		};
		base.Controls.Add(titleLabel);
		base.Controls.Add(thumbnailPictureBox);
		AttachSelection(this);
		base.MouseEnter += delegate
		{
			ApplyChrome(hover: true);
		};
		base.MouseLeave += delegate
		{
			ApplyChrome(hover: false);
		};
	}

	public void SetActive(bool active)
	{
		isActive = active;
		ApplyChrome(hover: false);
	}

	public void SetItemWidth(int width)
	{
		base.Width = Math.Max(260, width);
		titleLabel.Size = new Size(base.Width - 122, 64);
		UpdateArtwork();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			fallbackImage?.Dispose();
		}
		base.Dispose(disposing);
	}

	private async void UpdateArtwork()
	{
		if (thumbnailPictureBox.Width <= 0 || thumbnailPictureBox.Height <= 0)
		{
			return;
		}
		fallbackImage?.Dispose();
		fallbackImage = StoreArtworkFactory.CreateCover(game, thumbnailPictureBox.Size);
		thumbnailPictureBox.Image = fallbackImage;
		string text = (string.IsNullOrWhiteSpace(game.PosterImageUrl) ? game.SpotlightImageUrl : game.PosterImageUrl);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		Size size = thumbnailPictureBox.Size;
		if (StoreRemoteImageCache.TryGetPrepared(text, size, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			thumbnailPictureBox.Image = image;
			return;
		}
		int currentRequest = ++requestId;
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(text, size, StoreImageResizeMode.Stretch);
		if (!base.IsDisposed && !thumbnailPictureBox.IsDisposed && currentRequest == requestId)
		{
			thumbnailPictureBox.Image = image2 ?? fallbackImage;
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

	private void ApplyChrome(bool hover)
	{
		base.SurfaceColor = (isActive ? Color.FromArgb(36, 49, 69) : (hover ? Color.FromArgb(28, 36, 48) : Color.FromArgb(24, 29, 37)));
		base.BorderColor = (isActive ? Color.FromArgb(78, 129, 255) : (hover ? Color.FromArgb(68, 90, 120) : Color.FromArgb(42, 52, 66)));
		Invalidate();
	}
}
