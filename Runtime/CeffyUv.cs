using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Centralizes CEF top-left vs Unity bottom-left UV conventions for RawImage displays.
    /// </summary>
    internal static class CeffyUv
    {
        /// <summary>
        /// UV rect covering the entire browser texture (Y-flipped).
        /// </summary>
        public static Rect FullTexture => new Rect(0f, 1f, 1f, -1f);

        /// <summary>
        /// Builds a Y-flipped UV rect for a pixel rectangle in CEF/top-left coordinates.
        /// </summary>
        public static Rect GetUvRect(RectInt pixels, int textureWidth, int textureHeight)
        {
            float u = pixels.x / (float)textureWidth;
            float w = pixels.width / (float)textureWidth;
            float h = pixels.height / (float)textureHeight;

            // The shared texture keeps CEF row 0 at v=0, so a slot's lower CEF edge is its
            // larger v. RawImage maps uvRect.yMin to the quad's bottom edge, hence the
            // negative height.
            float vBottom = (pixels.y + pixels.height) / (float)textureHeight;
            return new Rect(u, vBottom, w, -h);
        }
    }
}
