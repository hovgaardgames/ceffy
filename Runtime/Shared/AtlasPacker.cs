using System.Collections.Generic;
using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Simple shelf atlas packer with free-rect reuse after <see cref="Free"/>.
    /// </summary>
    internal sealed class AtlasPacker
    {
        private readonly int atlasWidth;
        private readonly int atlasHeight;
        private readonly List<RectInt> freeRects = new();
        private int shelfY;
        private int shelfHeight;
        private int shelfX;

        public AtlasPacker(int width, int height)
        {
            atlasWidth = Mathf.Max(1, width);
            atlasHeight = Mathf.Max(1, height);
        }

        public bool TryAllocate(int width, int height, out RectInt rect)
        {
            rect = default;
            if (width <= 0 || height <= 0 || width > atlasWidth || height > atlasHeight)
                return false;

            if (TryAllocateFromFreeList(width, height, out rect))
                return true;

            // Fit on the current shelf (same row).
            if (shelfHeight > 0 && height <= shelfHeight && shelfX + width <= atlasWidth)
            {
                rect = new RectInt(shelfX, shelfY, width, height);
                shelfX += width;
                return true;
            }

            // Open a new shelf.
            int nextY = shelfHeight > 0 ? shelfY + shelfHeight : 0;
            if (nextY + height > atlasHeight)
                return false;

            shelfY = nextY;
            shelfX = 0;
            shelfHeight = height;
            rect = new RectInt(shelfX, shelfY, width, height);
            shelfX += width;
            return true;
        }

        public void Free(RectInt rect)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return;
            freeRects.Add(rect);
        }

        private bool TryAllocateFromFreeList(int width, int height, out RectInt rect)
        {
            rect = default;
            int bestIndex = -1;
            int bestArea = int.MaxValue;

            for (int i = 0; i < freeRects.Count; i++)
            {
                var free = freeRects[i];
                if (free.width < width || free.height < height)
                    continue;

                int area = free.width * free.height;
                if (area >= bestArea)
                    continue;

                bestArea = area;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return false;

            var chosen = freeRects[bestIndex];
            freeRects.RemoveAt(bestIndex);
            rect = new RectInt(chosen.x, chosen.y, width, height);

            int rightW = chosen.width - width;
            int bottomH = chosen.height - height;
            if (rightW > 0)
                freeRects.Add(new RectInt(chosen.x + width, chosen.y, rightW, height));
            if (bottomH > 0)
                freeRects.Add(new RectInt(chosen.x, chosen.y + height, chosen.width, bottomH));

            return true;
        }
    }
}
