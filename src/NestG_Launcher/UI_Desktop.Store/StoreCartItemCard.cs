using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreCartItemCard : RoundedPanel
{
	private readonly DesktopCartLineItemResponse item;

	private readonly PictureBox artworkPictureBox;

	private readonly Label titleLabel;

	private readonly Label versionLabel;

	private readonly Label unitPriceCaptionLabel;

	private readonly Label unitPriceValueLabel;

	private readonly Label quantityCaptionLabel;

	private readonly RoundedPanel quantityShell;

	private readonly Button minusButton;

	private readonly Button plusButton;

	private readonly Label lineTotalCaptionLabel;

	private readonly Label lineTotalValueLabel;

	private readonly TextBox quantityTextBox;

	private readonly Button updateButton;

	private readonly Button removeButton;

	private Image? fallbackArtwork;

	private int artworkRequestId;

	private int quantity;

	private bool isBusy;

	public event Func<DesktopCartLineItemResponse, int, Task>? QuantityUpdateRequested;

	public event Func<DesktopCartLineItemResponse, Task>? RemoveRequested;

	public StoreCartItemCard(DesktopCartLineItemResponse item)
	{
		this.item = item;
		quantity = Math.Clamp(item.Quantity, 1, 99);
		BackColor = Color.Transparent;
		base.Margin = new Padding(0, 0, 0, 16);
		base.Padding = new Padding(18);
		base.Height = 226;
		MinimumSize = new Size(700, 226);
		base.SurfaceColor = Color.FromArgb(17, 24, 34);
		base.BorderColor = Color.FromArgb(55, 69, 89);
		base.BorderThickness = 1;
		base.CornerRadius = 12;
		artworkPictureBox = new PictureBox
		{
			Location = new Point(18, 18),
			Size = new Size(146, 178),
			BackColor = Color.FromArgb(10, 15, 22),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		titleLabel = new Label
		{
			AutoSize = false,
			AutoEllipsis = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 12.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.Name
		};
		versionLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(255, 132, 132),
			Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.VersionName
		};
		unitPriceCaptionLabel = CreateCaptionLabel("Đơn giá");
		unitPriceValueLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.UnitPriceText
		};
		quantityCaptionLabel = CreateCaptionLabel("Số lượng");
		quantityTextBox = new TextBox
		{
			Text = quantity.ToString(),
			Width = 58,
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(25, 34, 46),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
			TextAlign = HorizontalAlignment.Center
		};
		quantityShell = new RoundedPanel
		{
			SurfaceColor = Color.FromArgb(25, 34, 46),
			BorderColor = Color.FromArgb(66, 84, 109),
			BorderThickness = 1,
			CornerRadius = 8,
			Size = new Size(174, 48)
		};
		minusButton = CreateStepButton("-");
		plusButton = CreateStepButton("+");
		minusButton.Click += delegate
		{
			ChangeQuantity(-1);
		};
		plusButton.Click += delegate
		{
			ChangeQuantity(1);
		};
		quantityShell.Controls.Add(minusButton);
		quantityShell.Controls.Add(quantityTextBox);
		quantityShell.Controls.Add(plusButton);
		lineTotalCaptionLabel = CreateCaptionLabel("Thành tiền");
		lineTotalValueLabel = new Label
		{
			AutoSize = true,
			ForeColor = Color.FromArgb(255, 211, 136),
			Font = new Font("Segoe UI Semibold", 11.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = item.LineTotalText
		};
		updateButton = CreateActionButton("Cập nhật", Color.FromArgb(32, 44, 60), Color.FromArgb(95, 118, 151));
		removeButton = CreateActionButton("Xóa", Color.FromArgb(63, 34, 40), Color.FromArgb(154, 64, 74));
		updateButton.Click += async delegate
		{
			await TriggerUpdateAsync();
		};
		removeButton.Click += async delegate
		{
			await TriggerRemoveAsync();
		};
		quantityTextBox.Leave += delegate
		{
			NormalizeQuantityText();
		};
		base.Controls.Add(removeButton);
		base.Controls.Add(updateButton);
		base.Controls.Add(lineTotalCaptionLabel);
		base.Controls.Add(lineTotalValueLabel);
		base.Controls.Add(quantityCaptionLabel);
		base.Controls.Add(quantityShell);
		base.Controls.Add(unitPriceCaptionLabel);
		base.Controls.Add(unitPriceValueLabel);
		base.Controls.Add(versionLabel);
		base.Controls.Add(titleLabel);
		base.Controls.Add(artworkPictureBox);
		base.Resize += delegate
		{
			LayoutContent();
		};
		LayoutContent();
		UpdateArtwork();
	}

	public void UpdateItemState(DesktopCartLineItemResponse updatedItem)
	{
		quantity = Math.Clamp(updatedItem.Quantity, 1, 99);
		quantityTextBox.Text = quantity.ToString();
		unitPriceValueLabel.Text = updatedItem.UnitPriceText;
		lineTotalValueLabel.Text = updatedItem.LineTotalText;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			fallbackArtwork?.Dispose();
		}
		base.Dispose(disposing);
	}

	private static Button CreateStepButton(string text)
	{
		Button button = new Button();
		button.Text = text;
		button.Size = new Size(38, 32);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = Color.FromArgb(31, 42, 58);
		button.ForeColor = AppTheme.PrimaryText;
		button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point);
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderColor = Color.FromArgb(69, 87, 112);
		button.FlatAppearance.BorderSize = 1;
		return button;
	}

	private static Label CreateCaptionLabel(string text)
	{
		return new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = text,
			TextAlign = ContentAlignment.BottomLeft
		};
	}

	private static Button CreateActionButton(string text, Color backColor, Color borderColor)
	{
		Button button = new Button();
		button.Text = text;
		button.Size = new Size(112, 38);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = backColor;
		button.ForeColor = AppTheme.PrimaryText;
		button.Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold, GraphicsUnit.Point);
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderColor = borderColor;
		button.FlatAppearance.BorderSize = 1;
		return button;
	}

	private void LayoutContent()
	{
		int num = artworkPictureBox.Right + 18;
		int num2 = Math.Max(420, base.Width - num - 18);
		int num3 = num + num2;
		int num4 = num3 - quantityShell.Width;
		int num5 = num4 - 20;
		int num6 = Math.Max(260, num5 - num);
		int num7 = 18;
		int num8 = Math.Max(120, (num6 - num7) / 2);
		titleLabel.Location = new Point(num, 18);
		titleLabel.Size = new Size(num2, 32);
		versionLabel.Location = new Point(num, titleLabel.Bottom + 6);
		versionLabel.Size = new Size(num2, 20);
		int y = versionLabel.Bottom + 14;
		unitPriceCaptionLabel.Location = new Point(num, y);
		unitPriceCaptionLabel.Size = new Size(num8, 20);
		unitPriceValueLabel.Location = new Point(num, unitPriceCaptionLabel.Bottom + 2);
		unitPriceValueLabel.Size = new Size(num8, 24);
		quantityCaptionLabel.Location = new Point(num4, y);
		quantityCaptionLabel.Size = new Size(quantityShell.Width, 22);
		quantityShell.Location = new Point(num4, quantityCaptionLabel.Bottom + 4);
		LayoutQuantityShell();
		int x = num + num8 + num7;
		lineTotalCaptionLabel.Location = new Point(x, y);
		lineTotalCaptionLabel.Size = new Size(num8, 20);
		lineTotalValueLabel.Location = new Point(x, lineTotalCaptionLabel.Bottom + 2);
		lineTotalValueLabel.MaximumSize = new Size(num8, 0);
		int y2 = Math.Min(base.Height - updateButton.Height - 14, quantityShell.Bottom + 14);
		updateButton.Location = new Point(num3 - 236, y2);
		removeButton.Location = new Point(num3 - 118, y2);
	}

	private void LayoutQuantityShell()
	{
		int y = Math.Max(0, (quantityShell.Height - minusButton.Height) / 2);
		int num = 8;
		minusButton.Location = new Point(num, y);
		plusButton.Location = new Point(quantityShell.Width - plusButton.Width - num, y);
		quantityTextBox.Height = quantityTextBox.PreferredHeight;
		quantityTextBox.Location = new Point((quantityShell.Width - quantityTextBox.Width) / 2, Math.Max(0, (quantityShell.Height - quantityTextBox.Height) / 2));
	}

	private void ChangeQuantity(int delta)
	{
		quantity = Math.Clamp(quantity + delta, 1, 99);
		quantityTextBox.Text = quantity.ToString();
	}

	private void NormalizeQuantityText()
	{
		if (!int.TryParse(quantityTextBox.Text, out var result))
		{
			result = quantity;
		}
		quantity = Math.Clamp(result, 1, 99);
		quantityTextBox.Text = quantity.ToString();
	}

	private async Task TriggerUpdateAsync()
	{
		if (isBusy)
		{
			return;
		}
		NormalizeQuantityText();
		if (this.QuantityUpdateRequested == null)
		{
			return;
		}
		try
		{
			isBusy = true;
			SetBusyState(busy: true);
			await this.QuantityUpdateRequested(item, quantity);
		}
		finally
		{
			isBusy = false;
			SetBusyState(busy: false);
		}
	}

	private async Task TriggerRemoveAsync()
	{
		if (isBusy || this.RemoveRequested == null)
		{
			return;
		}
		try
		{
			isBusy = true;
			SetBusyState(busy: true);
			await this.RemoveRequested(item);
		}
		finally
		{
			isBusy = false;
			SetBusyState(busy: false);
		}
	}

	private void SetBusyState(bool busy)
	{
		updateButton.Enabled = !busy;
		removeButton.Enabled = !busy;
		minusButton.Enabled = !busy;
		plusButton.Enabled = !busy;
		quantityTextBox.Enabled = !busy;
	}

	private async void UpdateArtwork()
	{
		StoreGame game = CreatePreviewGame();
		fallbackArtwork?.Dispose();
		fallbackArtwork = StoreArtworkFactory.CreateCover(game, artworkPictureBox.Size);
		artworkPictureBox.Image = fallbackArtwork;
		if (string.IsNullOrWhiteSpace(item.PosterImageUrl))
		{
			return;
		}
		Size size = artworkPictureBox.Size;
		if (StoreRemoteImageCache.TryGetPrepared(item.PosterImageUrl, size, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			artworkPictureBox.Image = image;
			return;
		}
		int requestId = ++artworkRequestId;
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(item.PosterImageUrl, size, StoreImageResizeMode.Stretch);
		if (!base.IsDisposed && !artworkPictureBox.IsDisposed && requestId == artworkRequestId)
		{
			artworkPictureBox.Image = image2 ?? fallbackArtwork;
		}
	}

	private StoreGame CreatePreviewGame()
	{
		string text = item.CategoryNames.FirstOrDefault() ?? "Cart";
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed($"{item.GameId:N}:{item.Name}:{item.VersionName}");
		StoreGame obj = new StoreGame
		{
			GameId = item.GameId,
			Title = item.Name,
			Subtitle = item.VersionName,
			Category = text
		};
		List<string> list;
		if (item.CategoryNames.Count <= 0)
		{
			int num = 1;
			list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = text;
		}
		else
		{
			list = item.CategoryNames;
		}
		obj.Genres = list;
		obj.BadgeText = "CART";
		obj.PosterImageUrl = item.PosterImageUrl;
		obj.SpotlightImageUrl = item.PosterImageUrl;
		obj.OriginalPrice = item.UnitPrice;
		obj.SalePrice = item.UnitPrice;
		obj.Likes = 0;
		obj.Dislikes = 0;
		obj.PurchaseCount = 0;
		obj.PromoLabel = "CART";
		obj.IsTrending = false;
		obj.PrimaryColor = storeGamePalette.PrimaryColor;
		obj.SecondaryColor = storeGamePalette.SecondaryColor;
		obj.AccentColor = storeGamePalette.AccentColor;
		return obj;
	}
}
