using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreGalleryImageView : Control
{
	private Image? displayedImage;

	private Bitmap? renderedImageCache;

	private Size renderedImageCacheSize = Size.Empty;

	private Image? renderedImageSource;

	private StoreGalleryImageFit renderedImageFitMode;

	private string renderedImageObjectPosition = "center center";

	private StoreGalleryImageFit fitMode;

	private string objectPosition = "center center";

	private Color surfaceColor = Color.FromArgb(10, 15, 22);

	private Color overlayColor = Color.Transparent;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Image? DisplayedImage
	{
		get
		{
			return displayedImage;
		}
		set
		{
			if (displayedImage != value)
			{
				displayedImage = value;
				InvalidateRenderCache();
				Invalidate();
			}
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public StoreGalleryImageFit FitMode
	{
		get
		{
			return fitMode;
		}
		set
		{
			if (fitMode != value)
			{
				fitMode = value;
				InvalidateRenderCache();
				Invalidate();
			}
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string ObjectPosition
	{
		get
		{
			return objectPosition;
		}
		set
		{
			string b = (string.IsNullOrWhiteSpace(value) ? "center center" : value.Trim());
			if (!string.Equals(objectPosition, b, StringComparison.OrdinalIgnoreCase))
			{
				objectPosition = b;
				InvalidateRenderCache();
				Invalidate();
			}
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color SurfaceColor
	{
		get
		{
			return surfaceColor;
		}
		set
		{
			if (!(surfaceColor == value))
			{
				surfaceColor = value;
				Invalidate();
			}
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color OverlayColor
	{
		get
		{
			return overlayColor;
		}
		set
		{
			if (!(overlayColor == value))
			{
				overlayColor = value;
				Invalidate();
			}
		}
	}

	public StoreGalleryImageView()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = surfaceColor;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		e.Graphics.Clear(surfaceColor);
		if (displayedImage == null || base.ClientSize.Width <= 0 || base.ClientSize.Height <= 0)
		{
			return;
		}
		EnsureRenderedImageCache();
		if (renderedImageCache != null)
		{
			e.Graphics.DrawImageUnscaled(renderedImageCache, Point.Empty);
		}
		if (overlayColor.A <= 0)
		{
			return;
		}
		using SolidBrush brush = new SolidBrush(overlayColor);
		e.Graphics.FillRectangle(brush, base.ClientRectangle);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		InvalidateRenderCache();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			renderedImageCache?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void EnsureRenderedImageCache()
	{
		if (displayedImage != null && base.ClientSize.Width > 0 && base.ClientSize.Height > 0 && (renderedImageCache == null || !(renderedImageCacheSize == base.ClientSize) || renderedImageSource != displayedImage || renderedImageFitMode != fitMode || !string.Equals(renderedImageObjectPosition, objectPosition, StringComparison.OrdinalIgnoreCase)))
		{
			renderedImageCache?.Dispose();
			renderedImageCache = StoreImageRenderHelper.RenderToBitmap(displayedImage, base.ClientSize, (fitMode == StoreGalleryImageFit.Cover) ? StoreImageResizeMode.Cover : StoreImageResizeMode.Contain, objectPosition, null);
			renderedImageCacheSize = base.ClientSize;
			renderedImageSource = displayedImage;
			renderedImageFitMode = fitMode;
			renderedImageObjectPosition = objectPosition;
		}
	}

	private void InvalidateRenderCache()
	{
		renderedImageCache?.Dispose();
		renderedImageCache = null;
		renderedImageCacheSize = Size.Empty;
		renderedImageSource = null;
	}
}
