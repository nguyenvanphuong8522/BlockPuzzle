using UnityEngine;

namespace Survivor
{
    // Endless beach props (palms, rocks, shells). Nothing is stored: each world cell hashes to the same
    // decoration every frame, so the world looks consistent however far the player walks.
    public sealed class DecorationSystem
    {
        const float CellSize = 5f;
        const float Density = 0.3f; // chance that a cell has a decoration

        readonly SpriteCatalog sprites;

        public DecorationSystem(SpriteCatalog sprites) => this.sprites = sprites;

        public void Draw(InstancedRenderer renderer, Camera camera)
        {
            var count = sprites.Decorations?.Length ?? 0;
            if (count == 0) return;

            var halfH = camera.orthographicSize + 3f;
            var halfW = halfH * camera.aspect + 3f;
            var c = (Vector2)camera.transform.position;
            int x0 = Mathf.FloorToInt((c.x - halfW) / CellSize), x1 = Mathf.FloorToInt((c.x + halfW) / CellSize);
            int y0 = Mathf.FloorToInt((c.y - halfH) / CellSize), y1 = Mathf.FloorToInt((c.y + halfH) / CellSize);

            for (var x = x0; x <= x1; x++)
            for (var y = y0; y <= y1; y++)
            {
                var h = Hash(x, y);
                if (Rand(h, 0) > Density) continue;

                var index = (int)(Rand(h, 1) * count) % count;
                var sprite = sprites.Decorations[index];
                if (sprite == null) continue;

                var height = index < sprites.DecorationHeights.Length ? sprites.DecorationHeights[index] : 1f;
                var pos = new Vector2((x + 0.15f + Rand(h, 2) * 0.7f) * CellSize, (y + 0.15f + Rand(h, 3) * 0.7f) * CellSize);
                renderer.Sprite(RenderLayer.Decorations, sprite, pos, height, Color.white, flipX: Rand(h, 4) > 0.5f);
            }
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        // Deterministic 0..1 value for this cell; salt picks a different value per property.
        static float Rand(uint hash, uint salt)
        {
            unchecked
            {
                var h = hash ^ (salt * 2654435761u);
                h = (h ^ (h >> 15)) * 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
