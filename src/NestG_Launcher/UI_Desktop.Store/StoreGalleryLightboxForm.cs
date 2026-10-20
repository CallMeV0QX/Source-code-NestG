using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreGalleryLightboxForm : Form
{
	private readonly IReadOnlyList<StoreProductGalleryItem> gallery;

	private readonly StoreGalleryImageView imageView = new StoreGalleryImageView();

	private readonly StoreGalleryGlyphButton prevButton;

	private readonly StoreGalleryGlyphButton nextButton;

	private readonly StoreGalleryGlyphButton closeButton;

	private readonly Label counterLabel = new Label();

	private int activeIndex;

	private int imageRequestId;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int SelectedIndex => activeIndex;

	public StoreGalleryLightboxForm(IReadOnlyList<StoreProductGalleryItem> gallery, int startIndex, string title)
	{
		this.gallery = gallery;
		activeIndex = ((gallery.Count != 0) ? Math.Clamp(startIndex, 0, gallery.Count - 1) : 0);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.Black;
		base.FormBorderStyle = FormBorderStyle.None;
		base.KeyPreview = true;
		MinimumSize = new Size(760, 520);
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		Text = title + " - Gallery";
		base.WindowState = FormWindowState.Maximized;
		StoreUiHelper.ApplyWindowIcon(this);
		imageView.Dock = DockStyle.Fill;
		imageView.FitMode = StoreGalleryImageFit.Contain;
		imageView.ObjectPosition = "center center";
		imageView.SurfaceColor = Color.Black;
		prevButton = CreateOverlayButton("‹", new Size(54, 98), 18, 28f);
		nextButton = CreateOverlayButton("›", new Size(54, 98), 18, 28f);
		closeButton = CreateOverlayButton("✕", new Size(52, 52), 26, 14f);
		counterLabel.AutoSize = true;
		counterLabel.BackColor = Color.Transparent;
		counterLabel.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
		counterLabel.ForeColor = Color.FromArgb(228, 232, 239);
		base.Controls.Add(imageView);
		base.Controls.Add(prevButton);
		base.Controls.Add(nextButton);
		base.Controls.Add(closeButton);
		base.Controls.Add(counterLabel);
		prevButton.Visible = gallery.Count > 1;
		nextButton.Visible = gallery.Count > 1;
		base.SizeChanged += delegate
		{
			LayoutChrome();
		};
		base.Shown += async delegate
		{
			await ApplyGalleryAsync(activeIndex);
		};
		base.KeyDown += StoreGalleryLightboxForm_KeyDown;
		prevButton.Click += async delegate
		{
			await ApplyGalleryAsync(activeIndex - 1);
		};
		nextButton.Click += async delegate
		{
			await ApplyGalleryAsync(activeIndex + 1);
		};
		closeButton.Click += delegate
		{
			Close();
		};
		LayoutChrome();
	}

	private void StoreGalleryLightboxForm_KeyDown(object? sender, KeyEventArgs e)
	{
		switch (e.KeyCode)
		{
		case Keys.Escape:
			Close();
			e.Handled = true;
			break;
		case Keys.Left:
			if (gallery.Count > 1)
			{
				ApplyGalleryAsync(activeIndex - 1);
				e.Handled = true;
			}
			break;
		case Keys.Right:
			if (gallery.Count > 1)
			{
				ApplyGalleryAsync(activeIndex + 1);
				e.Handled = true;
			}
			break;
		}
	}

	private void LayoutChrome()
	{
		int y = Math.Max(24, (base.ClientSize.Height - prevButton.Height) / 2);
		prevButton.Location = new Point(24, y);
		nextButton.Location = new Point(Math.Max(24, base.ClientSize.Width - nextButton.Width - 24), y);
		closeButton.Location = new Point(Math.Max(16, base.ClientSize.Width - closeButton.Width - 18), 18);
		counterLabel.Location = new Point(22, 22);
	}

	private async Task ApplyGalleryAsync(int index)
	{
		if (gallery.Count != 0)
		{
			activeIndex = (index + gallery.Count) % gallery.Count;
			counterLabel.Text = $"{activeIndex + 1} / {gallery.Count}";
			int requestId = ++imageRequestId;
			Image image = await StoreRemoteImageCache.GetAsync(gallery[activeIndex].FullUrl);
			if (!base.IsDisposed && !imageView.IsDisposed && requestId == imageRequestId && image != null)
			{
				imageView.DisplayedImage = image;
			}
		}
	}

	private static StoreGalleryGlyphButton CreateOverlayButton(string text, Size size, int cornerRadius, float fontSize)
	{
		return new StoreGalleryGlyphButton
		{
			Size = size,
			CornerRadius = cornerRadius,
			IconKind = ((text == "‹") ? StoreGalleryButtonIcon.ChevronLeft : ((text == "›") ? StoreGalleryButtonIcon.ChevronRight : StoreGalleryButtonIcon.Text)),
			BackColor = Color.FromArgb(132, 12, 12, 12),
			HoverBackColor = Color.FromArgb(164, 20, 20, 20),
			PressedBackColor = Color.FromArgb(186, 28, 28, 28),
			ForeColor = Color.FromArgb(245, 247, 250),
			Font = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold),
			Text = text
		};
	}
}
