using UnityEngine;

namespace Survivor
{
    // Blood splashes (numbers in BloodSettings.asset). Droplets fly out from a hit with a little arc (a fake height),
    // land, and some leave a stain on the ground that fades. Structure of arrays, swap-remove, capped.
    // Enemies bleed from EnemySystem.Damage, the hero from Player.TryHurt.
    public sealed class BloodSystem
    {
        readonly BloodSettings s;
        readonly GameConfig config;

        int count;
        Vector2[] pos = new Vector2[128], vel = new Vector2[128];
        float[] z = new float[128], vz = new float[128], size = new float[128];
        Color[] color = new Color[128];

        int stains;
        Vector2[] stainPos = new Vector2[64];
        float[] stainSize = new float[64], stainAge = new float[64];
        Color[] stainColor = new Color[64];

        public BloodSystem(BloodSettings settings, GameConfig config)
        {
            s = settings;
            this.config = config;
        }

        // An enemy of `type` (radius r) was hit; `push` = the direction it was pushed (zero = all around).
        public void Enemy(Vector2 at, float r, int type, Vector2 push)
        {
            if (!s.EnemyBlood || s.EnemyDroplets <= 0) return;
            var c = s.EnemyColor;
            if (type >= 0 && type < config.Enemies.Length)
            {
                var def = config.Enemies[type];
                var ts = def.IsBoss ? null : def.Settings;
                if (ts != null && ts.OwnBloodColor) c = ts.BloodColor;
                else if (s.UseBodyColor) c = Color.Lerp(def.Color, Color.black, s.EnemyColorDarken);
            }
            c.a = 1f;
            var body = Mathf.Clamp(r / 0.35f, 0.7f, 3f); // bigger body: more and bigger droplets
            Spray(at, r, push, s.EnemySprayAngle, Mathf.RoundToInt(s.EnemyDroplets * Mathf.Sqrt(body)), Mathf.Sqrt(body), c);
        }

        // The hero got hurt: a burst all around him.
        public void Hero(Vector2 at, float r)
        {
            if (!s.HeroBlood || s.HeroDroplets <= 0) return;
            Spray(at, r, Vector2.zero, 360f, s.HeroDroplets, s.HeroSizeScale, s.HeroColor);
        }

        void Spray(Vector2 at, float r, Vector2 dir, float cone, int amount, float scale, Color c)
        {
            var hasDir = dir.sqrMagnitude > 0.0001f && cone < 360f;
            var baseAngle = hasDir ? Mathf.Atan2(dir.y, dir.x) : 0f;
            for (var k = 0; k < amount; k++)
            {
                if (count >= s.MaxDroplets) return;
                if (count == pos.Length) Grow();
                var a = hasDir ? baseAngle + Random.Range(-0.5f, 0.5f) * cone * Mathf.Deg2Rad : Random.value * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var i = count++;
                pos[i] = at + d * (r * 0.5f);
                vel[i] = d * s.Speed * Random.Range(0.4f, 1f);
                z[i] = s.StartHeight * Mathf.Max(0.5f, r / 0.35f);
                vz[i] = s.Lift * Random.Range(0.3f, 1f);
                size[i] = s.DropletSize * scale * Random.Range(0.6f, 1.4f);
                color[i] = c;
            }
        }

        public void Tick(float dt)
        {
            var slow = s.Drag > 0f ? Mathf.Exp(-s.Drag * dt) : 1f;
            for (var i = count - 1; i >= 0; i--)
            {
                pos[i] += vel[i] * dt;
                vel[i] *= slow;
                vz[i] -= s.Gravity * dt;
                z[i] += vz[i] * dt;
                if (z[i] > 0f) continue;
                if (Random.value < s.StainChance) AddStain(pos[i], size[i] * s.StainScale, color[i]);
                var last = --count;
                pos[i] = pos[last]; vel[i] = vel[last]; z[i] = z[last]; vz[i] = vz[last];
                size[i] = size[last]; color[i] = color[last];
            }

            for (var i = stains - 1; i >= 0; i--)
            {
                stainAge[i] += dt;
                if (stainAge[i] < s.StainLifetime) continue;
                var last = --stains;
                stainPos[i] = stainPos[last]; stainSize[i] = stainSize[last];
                stainAge[i] = stainAge[last]; stainColor[i] = stainColor[last];
            }
        }

        void AddStain(Vector2 at, float radius, Color c)
        {
            if (stains >= s.MaxStains) return;
            if (stains == stainPos.Length)
            {
                var n = stains * 2;
                System.Array.Resize(ref stainPos, n);
                System.Array.Resize(ref stainSize, n);
                System.Array.Resize(ref stainAge, n);
                System.Array.Resize(ref stainColor, n);
            }
            var i = stains++;
            stainPos[i] = at;
            stainSize[i] = radius;
            stainAge[i] = 0f;
            stainColor[i] = Color.Lerp(c, Color.black, 0.2f);
        }

        void Grow()
        {
            var n = pos.Length * 2;
            System.Array.Resize(ref pos, n);
            System.Array.Resize(ref vel, n);
            System.Array.Resize(ref z, n);
            System.Array.Resize(ref vz, n);
            System.Array.Resize(ref size, n);
            System.Array.Resize(ref color, n);
        }

        // Boss fight starts / run restarts: nothing left on the ground.
        public void Clear() => count = stains = 0;

        public void Draw(InstancedRenderer renderer)
        {
            var life = Mathf.Max(0.1f, s.StainLifetime);
            for (var i = 0; i < stains; i++)
            {
                var t = stainAge[i] / life;
                var c = stainColor[i];
                c.a = s.StainAlpha * (t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f);
                var r = stainSize[i] * Mathf.Min(1f, 0.6f + stainAge[i] * 8f); // spreads a little as it lands
                renderer.Ellipse(RenderLayer.Decorations, stainPos[i], new Vector2(r * 2f, r * 1.3f), c);
            }
            for (var i = 0; i < count; i++)
                renderer.Circle(RenderLayer.Effects, pos[i] + new Vector2(0f, z[i]), size[i], color[i]);
        }
    }
}
