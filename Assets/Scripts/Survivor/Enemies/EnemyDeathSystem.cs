using UnityEngine;

namespace Survivor
{
    // Death effect for every enemy: its sprite shatters into small pieces (a 3x3 grid, 4x4 for bosses) that
    // burst outward, spin, fall with gravity and fade. Played after the enemy is removed from EnemySystem, so it
    // no longer moves, hits or blocks anything. Purely cosmetic, stored in flat arrays.
    // When too many pieces are alive at once (huge area kills), new deaths just vanish like before.
    public sealed class EnemyDeathSystem
    {
        const int MaxPieces = 8000;
        const int Grid = 3, BossGrid = 4;
        const float Life = 0.45f, BossLife = 0.9f;
        const float FlashTime = 0.05f;
        const float Gravity = 9f;          // world units / s^2, pulls pieces down
        const float BurstSpeed = 2.6f;     // outward speed, scaled by the enemy's size
        const float UpKick = 2.2f;         // extra upward speed so pieces arc up then fall

        readonly GameConfig config;
        readonly SpriteCatalog sprites;
        readonly InstancedRenderer renderer;
        readonly Player player;
        readonly EffectSystem effects;
        readonly CameraShake shake;

        int count;
        readonly Vector2[] position = new Vector2[MaxPieces];
        readonly Vector2[] velocity = new Vector2[MaxPieces];
        readonly Vector2[] size = new Vector2[MaxPieces];
        readonly Vector4[] uv = new Vector4[MaxPieces];
        readonly float[] angle = new float[MaxPieces];
        readonly float[] spin = new float[MaxPieces];
        readonly float[] age = new float[MaxPieces];
        readonly float[] life = new float[MaxPieces];
        readonly float[] groundY = new float[MaxPieces];   // pieces stop falling a bit below the enemy's feet
        readonly bool[] mirror = new bool[MaxPieces];
        readonly Texture[] texture = new Texture[MaxPieces];
        readonly Color[] color = new Color[MaxPieces];      // white for sprite pieces, type color for circle pieces

        public int Count => count;

        public EnemyDeathSystem(GameConfig config, SpriteCatalog sprites, InstancedRenderer renderer, Player player,
            EffectSystem effects, CameraShake shake)
        {
            this.config = config;
            this.sprites = sprites;
            this.renderer = renderer;
            this.player = player;
            this.effects = effects;
            this.shake = shake;
        }

        // Called by EnemySystem for each enemy that died this frame. It shatters in the walk frame it died in.
        public void Add(Vector2 at, float radius, int enemyType, int enemyId, bool isBoss)
        {
            var n = isBoss ? BossGrid : Grid;
            if (count + n * n > MaxPieces) return;
            if (!renderer.IsVisible(at, radius * 3f)) return; // off screen: nothing to see

            var anim = enemyType < sprites.Enemies.Length ? sprites.Enemies[enemyType] : null;
            var hasSprite = anim != null && !anim.IsEmpty;
            var mirrored = at.x > player.Position.x; // same rule as EnemyDrawer: sprites face the hero
            var lifetime = isBoss ? BossLife : Life;

            // Size and center of the whole picture, matching how EnemyDrawer draws it.
            Vector2 full, center;
            Vector4 spriteUv = new Vector4(0f, 0f, 1f, 1f);
            Texture tex = null;
            if (hasSprite)
            {
                // Same frame formula as EnemyDrawer, so it breaks apart in the exact pose it had.
                var frame = anim.Frames[((int)(Time.time * anim.Fps) + enemyId % 7) % anim.Frames.Length];
                var height = radius * 2f * sprites.SpriteScale;
                full = new Vector2(height * frame.rect.width / frame.rect.height, height);
                center = at + new Vector2(0f, radius * 0.3f);
                spriteUv = renderer.SpriteUv(frame);
                tex = frame.texture;
            }
            else
            {
                full = new Vector2(radius * 2f, radius * 2f);
                center = at;
            }

            var piece = full / n;
            var tint = hasSprite ? Color.white : config.Enemies[enemyType].Color;
            var speedScale = Mathf.Sqrt(radius / 0.35f);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = count++;
                // Piece offset from the picture center (mirrored pictures mirror their pieces too).
                var local = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f);
                var offset = new Vector2(local.x * full.x * (mirrored ? -1f : 1f), local.y * full.y);

                position[i] = center + offset;
                var outward = offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.insideUnitCircle.normalized;
                velocity[i] = (outward * BurstSpeed * Random.Range(0.7f, 1.3f) + new Vector2(0f, UpKick * Random.Range(0.6f, 1.2f)))
                              * speedScale;
                size[i] = piece;
                uv[i] = new Vector4(spriteUv.x + spriteUv.z * x / n, spriteUv.y + spriteUv.w * y / n, spriteUv.z / n, spriteUv.w / n);
                angle[i] = 0f;
                spin[i] = Random.Range(-12f, 12f);
                age[i] = 0f;
                life[i] = lifetime * Random.Range(0.8f, 1.15f);
                groundY[i] = at.y - radius - Random.Range(0f, 0.25f);
                mirror[i] = mirrored;
                texture[i] = tex;
                color[i] = tint;
            }

            if (isBoss)
            {
                shake.Add(0.5f);
                effects.Burst(at, radius * 2.2f, new Color(1f, 1f, 1f, 0.7f), 0.4f);
            }
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                if (age[i] >= life[i])
                {
                    RemoveAt(i);
                    continue;
                }

                velocity[i].y -= Gravity * dt;
                position[i] += velocity[i] * dt;
                angle[i] += spin[i] * dt;
                if (position[i].y < groundY[i])
                {
                    // Land: a small bounce, then slide to a stop.
                    position[i].y = groundY[i];
                    velocity[i] = new Vector2(velocity[i].x * 0.5f, -velocity[i].y * 0.3f);
                    spin[i] *= 0.5f;
                }
            }
        }

        void RemoveAt(int i)
        {
            var last = --count;
            position[i] = position[last];
            velocity[i] = velocity[last];
            size[i] = size[last];
            uv[i] = uv[last];
            angle[i] = angle[last];
            spin[i] = spin[last];
            age[i] = age[last];
            life[i] = life[last];
            groundY[i] = groundY[last];
            mirror[i] = mirror[last];
            texture[i] = texture[last];
            color[i] = color[last];
            texture[last] = null;
        }

        public void Draw(InstancedRenderer target)
        {
            for (var i = 0; i < count; i++)
            {
                var t = age[i] / life[i];
                var c = color[i];
                c.a *= t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                var flash = age[i] < FlashTime ? 1f : 0f;
                var s = size[i] * (t < 0.6f ? 1f : 1f - (t - 0.6f) * 1.2f);   // shrink a little at the end

                if (texture[i] != null)
                {
                    // Mirroring a piece = rotate half a turn and flip vertically.
                    var a = mirror[i] ? Mathf.PI + angle[i] : angle[i];
                    target.TextureRotated(RenderLayer.Effects, texture[i], uv[i], position[i], new Vector2(s.x, mirror[i] ? -s.y : s.y),
                        a, c, flash);
                }
                else
                {
                    target.Rect(RenderLayer.Effects, position[i], s, c);
                }
            }
        }
    }
}
