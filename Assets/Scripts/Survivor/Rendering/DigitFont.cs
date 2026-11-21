using UnityEngine;

namespace Survivor
{
    // Tiny 3x5 pixel font for damage numbers, baked into a texture with a black outline.
    // Each digit is drawn as one instanced quad, so thousands of numbers cost almost nothing.
    public static class DigitFont
    {
        const int GlyphW = 3, GlyphH = 5, CellW = GlyphW + 2, CellH = GlyphH + 2;

        static readonly string[] Glyphs =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        static Texture2D texture;

        public static Texture2D Texture => texture != null ? texture : texture = Build();

        // Width / height of one digit cell.
        public const float Aspect = CellW / (float)CellH;

        public static Vector4 Uv(int digit)
        {
            const float w = 1f / 10f;
            return new Vector4(digit * w, 0f, w, 1f);
        }

        static Texture2D Build()
        {
            var tex = new Texture2D(CellW * 10, CellH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "DigitFont"
            };
            var pixels = new Color32[tex.width * tex.height];
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (var d = 0; d < 10; d++)
            {
                // Glyph strings are written top row first; texture rows go bottom-up.
                bool On(int gx, int gy) =>
                    gx >= 0 && gx < GlyphW && gy >= 0 && gy < GlyphH && Glyphs[d][(GlyphH - 1 - gy) * GlyphW + gx] == '1';

                for (var cy = 0; cy < CellH; cy++)
                for (var cx = 0; cx < CellW; cx++)
                {
                    int gx = cx - 1, gy = cy - 1;
                    var index = cy * tex.width + d * CellW + cx;
                    if (On(gx, gy)) pixels[index] = white;
                    else if (On(gx - 1, gy) || On(gx + 1, gy) || On(gx, gy - 1) || On(gx, gy + 1) ||
                             On(gx - 1, gy - 1) || On(gx + 1, gy + 1) || On(gx - 1, gy + 1) || On(gx + 1, gy - 1))
                        pixels[index] = black;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
