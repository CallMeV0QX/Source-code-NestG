using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreCatalogGridPanel : Panel
{
	public StoreCatalogGridPanel()
	{
		SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		DoubleBuffered = true;
		base.Margin = Padding.Empty;
		base.Padding = Padding.Empty;
		AutoSize = false;
	}

	internal void ApplyGridLayout(IReadOnlyList<StoreGameCard> cards, int availableWidth, int cardWidth, int columns, int gap)
	{
		SuspendLayout();
		base.Width = Math.Max(320, availableWidth);
		if (cards.Count == 0)
		{
			base.Height = 0;
			ResumeLayout(performLayout: false);
			return;
		}
		int count = Math.Max(1, columns);
		int num = 0;
		int num2 = 0;
		while (num2 < cards.Count)
		{
			StoreGameCard[] array = cards.Skip(num2).Take(count).ToArray();
			int num3 = 0;
			int num4 = 0;
			StoreGameCard[] array2 = array;
			foreach (StoreGameCard storeGameCard in array2)
			{
				storeGameCard.SetCardWidth(cardWidth);
				storeGameCard.Location = new Point(num3, num);
				num3 += cardWidth + gap;
				num4 = Math.Max(num4, storeGameCard.Height);
			}
			num += num4;
			num2 += array.Length;
			if (num2 < cards.Count)
			{
				num += gap;
			}
		}
		base.Height = num;
		ResumeLayout(performLayout: false);
		Invalidate();
	}
}
