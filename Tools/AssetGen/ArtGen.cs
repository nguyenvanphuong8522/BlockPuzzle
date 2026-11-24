// Placeholder art generator: draws bright cartoon sprites (thick ink outline, gradient shading) to PNG.
// Written for the C# 5 compiler that ships with Windows (csc.exe), so no newer syntax here.
// Run Tools/AssetGen/generate.ps1. Replace any PNG later with real art using the same file name.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class Paint
{
    public static readonly Color Ink = Color.FromArgb(255, 45, 30, 40);

    public static Color Lighter(Color c, float k)
    {
        return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * k), (int)(c.G + (255 - c.G) * k), (int)(c.B + (255 - c.B) * k));
    }

    public static Color Darker(Color c, float k)
    {
        return Color.FromArgb(c.A, (int)(c.R * (1 - k)), (int)(c.G * (1 - k)), (int)(c.B * (1 - k)));
    }

    public static Color Hex(string hex)
    {
        return ColorTranslator.FromHtml("#" + hex);
    }

    public static GraphicsPath Ellipse(float cx, float cy, float rx, float ry)
    {
        var p = new GraphicsPath();
        p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        return p;
    }

    public static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        var d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static GraphicsPath Poly(params float[] xy)
    {
        var pts = new PointF[xy.Length / 2];
        for (var i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
        var p = new GraphicsPath();
        p.AddPolygon(pts);
        return p;
    }

    public static GraphicsPath Smooth(params float[] xy)
    {
        var pts = new PointF[xy.Length / 2];
        for (var i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
        var p = new GraphicsPath();
        p.AddClosedCurve(pts, 0.5f);
        return p;
    }

    public static GraphicsPath Star(float cx, float cy, float outer, float inner, int points, float rotationDeg)
    {
        var pts = new PointF[points * 2];
        for (var i = 0; i < pts.Length; i++)
        {
            var a = (rotationDeg + i * 180.0 / points) * Math.PI / 180.0;
            var r = i % 2 == 0 ? outer : inner;
            pts[i] = new PointF(cx + (float)(Math.Cos(a) * r), cy + (float)(Math.Sin(a) * r));
        }
        var p = new GraphicsPath();
        p.AddClosedCurve(pts, 0.15f);
        return p;
    }

    public static GraphicsPath Pie(float cx, float cy, float r, float start, float sweep)
    {
        var p = new GraphicsPath();
        p.AddPie(cx - r, cy - r, r * 2, r * 2, start, sweep);
        return p;
    }
}

// One sprite canvas. Parts are collected into groups; Flush() draws the ink outline of the
// whole group first and the fills on top, so overlapping parts share one clean outer outline.
sealed class Canvas : IDisposable
{
    sealed class Part
    {
        public GraphicsPath Path;
        public Color Color;
        public bool Shade;
    }

    public readonly Bitmap Bitmap;
    public readonly Graphics G;
    readonly float outline;
    readonly List<Part> parts = new List<Part>();

    public Canvas(int w, int h, float outlineWidth)
    {
        Bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        G = Graphics.FromImage(Bitmap);
        G.SmoothingMode = SmoothingMode.AntiAlias;
        G.PixelOffsetMode = PixelOffsetMode.HighQuality;
        G.CompositingQuality = CompositingQuality.HighQuality;
        G.Clear(Color.Transparent);
        outline = outlineWidth;
    }

    public void Add(GraphicsPath path, Color color)
    {
        parts.Add(new Part { Path = path, Color = color, Shade = true });
    }

    public void AddFlat(GraphicsPath path, Color color)
    {
        parts.Add(new Part { Path = path, Color = color, Shade = false });
    }

    public void Flush()
    {
        using (var pen = new Pen(Paint.Ink, outline * 2) { LineJoin = LineJoin.Round })
            foreach (var part in parts) G.DrawPath(pen, part.Path);
        foreach (var part in parts)
        {
            var b = part.Path.GetBounds();
            if (part.Shade && b.Height > 1)
            {
                // Soft cartoon shading: light on top, darker at the bottom.
                var rect = new RectangleF(b.X, b.Y - 1, b.Width + 1, b.Height + 2);
                using (var brush = new LinearGradientBrush(rect, Paint.Lighter(part.Color, 0.3f), Paint.Darker(part.Color, 0.18f), 90f))
                    G.FillPath(brush, part.Path);
            }
            else
            {
                using (var brush = new SolidBrush(part.Color)) G.FillPath(brush, part.Path);
            }
        }
        parts.Clear();
    }

    // Glossy highlight blob.
    public void Shine(float cx, float cy, float rx, float ry, int alpha)
    {
        using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            G.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    public void Fill(GraphicsPath p, Color c)
    {
        using (var b = new SolidBrush(c)) G.FillPath(b, p);
    }

    public void Line(Color c, float width, params float[] xy)
    {
        using (var pen = new Pen(c, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            var pts = new PointF[xy.Length / 2];
            for (var i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            if (pts.Length == 2) G.DrawLine(pen, pts[0], pts[1]);
            else G.DrawCurve(pen, pts, 0.5f);
        }
    }

    // Outlined thick stroke (tentacles, stalks, arms).
    public void Limb(Color c, float width, params float[] xy)
    {
        Line(Paint.Ink, width + outline * 2, xy);
        Line(c, width, xy);
    }

    // Big cartoon eye looking slightly in (lookX, lookY).
    public void Eye(float cx, float cy, float rx, float ry, float lookX, float lookY)
    {
        using (var ink = new SolidBrush(Paint.Ink)) G.FillEllipse(ink, cx - rx - 2, cy - ry - 2, rx * 2 + 4, ry * 2 + 4);
        using (var white = new SolidBrush(Color.White)) G.FillEllipse(white, cx - rx, cy - ry, rx * 2, ry * 2);
        var pr = rx * 0.58f;
        var px = cx + lookX * rx * 0.35f;
        var py = cy + lookY * ry * 0.3f;
        using (var ink = new SolidBrush(Paint.Ink)) G.FillEllipse(ink, px - pr, py - pr * 1.1f, pr * 2, pr * 2.2f);
        Shine(px - pr * 0.35f, py - pr * 0.45f, pr * 0.4f, pr * 0.4f, 255);
    }

    // Angry eyebrow above an eye.
    public void Brow(float x1, float y1, float x2, float y2)
    {
        Line(Paint.Ink, 4f, x1, y1, x2, y2);
    }

    public void Smile(float cx, float cy, float w, float h)
    {
        using (var pen = new Pen(Paint.Ink, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            G.DrawArc(pen, cx - w / 2, cy - h, w, h * 2, 20, 140);
    }

    public void Blush(float cx, float cy)
    {
        using (var b = new SolidBrush(Color.FromArgb(110, 255, 110, 130))) G.FillEllipse(b, cx - 6, cy - 3.5f, 12, 7);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Bitmap.Save(path, ImageFormat.Png);
    }

    public void Dispose()
    {
        G.Dispose();
        Bitmap.Dispose();
    }
}

static class ArtGen
{
    static string dir;

    public static void Run(string outputDir)
    {
        dir = outputDir;
        for (var f = 0; f < 4; f++) Player(f);
        for (var f = 0; f < 5; f++) PlayerIdle(f);
        for (var f = 0; f < 2; f++) Crab("enemy0_crab_" + f, f, 128, Paint.Hex("FF6B4A"), false);
        for (var f = 0; f < 2; f++) FlyingFish(f);
        for (var f = 0; f < 3; f++) Jelly(f);
        for (var f = 0; f < 2; f++) Starfish(f);
        for (var f = 0; f < 3; f++) Octopus(f);
        for (var f = 0; f < 2; f++) Hermit(f);
        for (var f = 0; f < 2; f++) Crab("enemy6_kingcrab_" + f, f, 256, Paint.Hex("E8333A"), true);

        Pearl();
        Chest();
        Meat();
        Magnet();
        Cannonball();
        Gun();
        for (var f = 0; f < 6; f++) Monkey(f);
        for (var f = 0; f < 7; f++) Otter(f);
        for (var f = 0; f < 3; f++) Slash(f);
        for (var f = 0; f < 6; f++) Turtle(f);
        for (var f = 0; f < 2; f++) MagicOrb(f);
        for (var f = 0; f < 4; f++) Blast(f);
        HeroMarker();
        DigitAtlas();
        for (var f = 0; f < 5; f++) Gunner(f);
        Smg();
        for (var f = 0; f < 6; f++) Frog(f);
        Pellet();
        BarbedFence();
        HealPlus();
        Banana();
        for (var f = 0; f < 2; f++) MuzzleFlash(f);
        Anchor();
        Palm();
        Rock();
        Shell();
        Sand();
    }

    static string P(string name)
    {
        return Path.Combine(dir, "Sprites", name + ".png");
    }

    // ---------- Player: young pirate (teal bandana, white shirt, blue shorts) ----------
    // Walk cycle: player_0..3.
    static void Player(int f)
    {
        var bob = f % 2 == 1 ? -3f : 0f;
        var step = f == 0 ? 5f : f == 2 ? -5f : 0f;
        DrawPlayer("player_" + f, bob, step, 0f, false);
    }

    // Standing still: hero_idle_0..3 = breathing loop (head and body sink a little, bandana tails flutter), 4 = blink.
    static void PlayerIdle(int f)
    {
        float[] breathe = { 0f, 1.2f, 2.4f, 1.2f };
        float[] flutter = { 0f, 2f, 3f, 1f };
        var i = f == 4 ? 0 : f;
        DrawPlayer("hero_idle_" + f, breathe[i], 0f, flutter[i], f == 4);
    }

    static void DrawPlayer(string name, float bob, float step, float flutter, bool blink)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            // Back: bandana tails and legs
            c.Add(Paint.Poly(30, 40 + bob, 16, 34 + bob - flutter, 20, 48 + bob - flutter * 0.5f), Paint.Hex("1FB5A8"));
            c.Add(Paint.Poly(30, 44 + bob, 14 - flutter * 0.5f, 52 + bob + flutter * 0.4f, 24, 56 + bob), Paint.Hex("1FB5A8"));
            c.Add(Paint.Ellipse(54 + step, 112, 10, 7), Paint.Hex("7A4A2A"));
            c.Add(Paint.Ellipse(74 - step, 112, 10, 7), Paint.Hex("7A4A2A"));
            c.Add(Paint.RoundRect(50 + step * 0.6f, 96, 9, 14, 3), Paint.Hex("F6C9A0"));
            c.Add(Paint.RoundRect(69 - step * 0.6f, 96, 9, 14, 3), Paint.Hex("F6C9A0"));
            c.Flush();

            // Body: arms, shorts, shirt with a red sash
            c.Add(Paint.Ellipse(40, 84 + bob + step * 0.3f, 7, 9), Paint.Hex("F6C9A0"));
            c.Add(Paint.Ellipse(88, 84 + bob - step * 0.3f, 7, 9), Paint.Hex("F6C9A0"));
            c.Add(Paint.RoundRect(46, 88 + bob, 36, 14, 5), Paint.Hex("3A6FD8"));
            c.Add(Paint.RoundRect(44, 70 + bob, 40, 24, 9), Paint.Hex("FFFFFF"));
            c.Flush();
            c.Fill(Paint.RoundRect(45, 85 + bob, 38, 6, 3), Paint.Hex("E8443A"));

            // Head
            c.Add(Paint.Ellipse(64, 50 + bob, 30, 27), Paint.Hex("F6C9A0"));
            c.Flush();
            // Spiky dark hair under the bandana
            c.Add(Paint.Poly(36, 46 + bob, 40, 58 + bob, 46, 48 + bob, 52, 56 + bob, 58, 46 + bob, 50, 38 + bob), Paint.Hex("3B2A2A"));
            c.Add(Paint.Poly(92, 46 + bob, 88, 58 + bob, 84, 48 + bob, 80, 40 + bob), Paint.Hex("3B2A2A"));
            c.Flush();
            // Bandana
            var band = new GraphicsPath();
            band.AddArc(33, 20 + bob, 62, 48, 180, 180);
            band.CloseFigure();
            c.Add(band, Paint.Hex("1FB5A8"));
            c.Flush();
            c.Fill(Paint.Ellipse(50, 30 + bob, 3, 3), Color.White);
            c.Fill(Paint.Ellipse(64, 26 + bob, 3, 3), Color.White);
            c.Fill(Paint.Ellipse(78, 30 + bob, 3, 3), Color.White);

            // Face, looking right
            if (blink)
            {
                c.Smile(58, 53 + bob, 13, 4);   // closed eyes
                c.Smile(76, 53 + bob, 13, 4);
            }
            else
            {
                c.Eye(58, 55 + bob, 6.5f, 8.5f, 0.7f, 0.1f);
                c.Eye(76, 55 + bob, 6.5f, 8.5f, 0.7f, 0.1f);
            }
            c.Blush(50, 66 + bob);
            c.Blush(84, 66 + bob);
            c.Smile(68, 66 + bob, 14, 6);
            c.Save(P(name));
        }
    }

    // ---------- Crab (also the boss with crown and spikes) ----------
    static void Crab(string name, int f, int size, Color shell, bool king)
    {
        var s = size / 128f;
        using (var c = new Canvas(size, size, 3f * s))
        {
            c.G.ScaleTransform(s, s);
            var claw = f == 0 ? 0f : -6f;
            var leg = f == 0 ? 0f : 3f;

            // Legs
            for (var i = 0; i < 3; i++)
            {
                var y = 78 + i * 8;
                var l = (i % 2 == 0 ? leg : -leg);
                c.Limb(Paint.Darker(shell, 0.1f), 5, 34, y, 18, y + 8 + l, 14, y + 18 + l);
                c.Limb(Paint.Darker(shell, 0.1f), 5, 94, y, 110, y + 8 - l, 114, y + 18 - l);
            }
            // Eye stalks
            c.Limb(shell, 5, 54, 56, 50, 36);
            c.Limb(shell, 5, 74, 56, 78, 36);
            // Arms + claws
            c.Limb(shell, 8, 34, 70, 24, 58 + claw);
            c.Limb(shell, 8, 94, 70, 104, 58 + claw);
            var open = f == 0 ? 40f : 15f;
            // Pincers: a circle with a wedge cut out at the top (GDI angles: 270 = up).
            c.Add(Paint.Pie(20, 48 + claw, 17, 270 + open / 2, 360 - open), shell);
            c.Add(Paint.Pie(108, 48 + claw, 17, 270 + open / 2, 360 - open), shell);
            c.Flush();

            // Body
            c.Add(Paint.Ellipse(64, 76, 42, 27), shell);
            if (king)
            {
                c.Add(Paint.Poly(30, 62, 26, 50, 40, 58), shell);
                c.Add(Paint.Poly(98, 62, 102, 50, 88, 58), shell);
            }
            c.Flush();
            c.Shine(50, 64, 12, 6, 90);
            c.Fill(Paint.Ellipse(44, 84, 3, 3), Paint.Lighter(shell, 0.4f));
            c.Fill(Paint.Ellipse(84, 86, 3, 3), Paint.Lighter(shell, 0.4f));

            // Eyes on stalks
            c.Eye(50, 34, 8, 9, 0.5f, 0.2f);
            c.Eye(78, 34, 8, 9, 0.5f, 0.2f);
            c.Brow(42, 22, 56, 27);
            c.Brow(86, 22, 72, 27);
            c.Smile(64, 84, 18, 6);

            if (king)
            {
                // Golden crown with jewels
                c.Add(Paint.Poly(44, 58, 44, 40, 52, 48, 58, 34, 64, 46, 70, 34, 76, 48, 84, 40, 84, 58), Paint.Hex("FFC93C"));
                c.Flush();
                c.Fill(Paint.Ellipse(64, 53, 4, 4), Paint.Hex("3A9BFF"));
                c.Fill(Paint.Ellipse(52, 54, 3, 3), Paint.Hex("FF4FA0"));
                c.Fill(Paint.Ellipse(76, 54, 3, 3), Paint.Hex("FF4FA0"));
            }
            c.Save(P(name));
        }
    }

    // ---------- Flying fish ----------
    static void FlyingFish(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var wing = f == 0 ? -18f : 8f;
            var body = Paint.Hex("3AA7F2");
            c.Add(Paint.Poly(26, 66, 6, 48, 12, 66, 6, 84), Paint.Hex("2B86D6"));
            c.Add(Paint.Poly(60, 58, 40, 34 + wing, 84, 52), Paint.Hex("8FE3FF"));
            c.Flush();
            c.Add(Paint.Ellipse(64, 66, 42, 19), body);
            c.Flush();
            c.Fill(Paint.Ellipse(66, 74, 30, 8), Paint.Hex("CFF4FF"));
            c.Add(Paint.Poly(56, 70, 48, 90 - wing * 0.4f, 70, 76), Paint.Hex("8FE3FF"));
            c.Flush();
            c.Shine(56, 58, 14, 5, 110);
            c.Eye(88, 62, 7, 8, 0.8f, 0f);
            c.Smile(100, 70, 8, 3);
            c.Save(P("enemy1_flyfish_" + f));
        }
    }

    // ---------- Jellyfish ----------
    static void Jelly(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var squash = new[] { 0f, 4f, -3f }[f];
            var wave = new[] { 0f, 5f, -5f }[f];
            var color = Paint.Hex("B567F0");
            for (var i = 0; i < 4; i++)
            {
                var x = 44 + i * 13;
                c.Limb(Paint.Hex("F29BE8"), 5, x, 70, x + wave, 86, x - wave, 100 - squash, x + wave * 0.5f, 114 - squash);
            }
            var dome = new GraphicsPath();
            dome.AddArc(26 - squash, 26 + squash, 76 + squash * 2, 72 - squash * 2, 180, 180);
            dome.AddCurve(new[]
            {
                new PointF(102 + squash, 62 + squash * 0.5f), new PointF(90, 70), new PointF(77, 64), new PointF(64, 71),
                new PointF(51, 64), new PointF(38, 70), new PointF(26 - squash, 62 + squash * 0.5f)
            }, 0.5f);
            dome.CloseFigure();
            c.Add(dome, Color.FromArgb(240, color.R, color.G, color.B));
            c.Flush();
            c.Shine(48, 42 + squash, 10, 7, 140);
            c.Fill(Paint.Ellipse(80, 38 + squash, 4, 4), Color.FromArgb(150, 255, 255, 255));
            c.Eye(54, 54, 6, 7, 0f, 0.3f);
            c.Eye(74, 54, 6, 7, 0f, 0.3f);
            c.Blush(46, 62);
            c.Blush(82, 62);
            c.Save(P("enemy2_jelly_" + f));
        }
    }

    // ---------- Spiky starfish ----------
    static void Starfish(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var rot = -90f + (f == 0 ? -8f : 8f);
            c.Add(Paint.Star(64, 68, 54, 24, 5, rot), Paint.Hex("FFA43A"));
            c.Flush();
            var rnd = new Random(7);
            for (var i = 0; i < 14; i++)
            {
                var a = rnd.NextDouble() * Math.PI * 2;
                var r = 14 + rnd.NextDouble() * 30;
                c.Fill(Paint.Ellipse(64 + (float)(Math.Cos(a) * r), 68 + (float)(Math.Sin(a) * r), 2.5f, 2.5f), Paint.Hex("FFE08A"));
            }
            c.Eye(55, 64, 6, 7.5f, 0f, 0.2f);
            c.Eye(73, 64, 6, 7.5f, 0f, 0.2f);
            c.Brow(48, 54, 59, 58);
            c.Brow(80, 54, 69, 58);
            c.Smile(64, 76, 12, 4);
            c.Save(P("enemy3_starfish_" + f));
        }
    }

    // ---------- Octopus ----------
    static void Octopus(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var color = Paint.Hex("FF7EB6");
            var w = new[] { 0f, 6f, -6f }[f];
            for (var i = 0; i < 5; i++)
            {
                var x = 36 + i * 14;
                var dir = (i - 2) * 6;
                var sway = (i % 2 == 0 ? w : -w);
                c.Limb(color, 9, x, 70, x + dir * 0.5f + sway, 92, x + dir + sway * 0.4f, 106, x + dir * 1.4f - sway, 116);
            }
            c.Add(Paint.Ellipse(64, 50, 34, 32), color);
            c.Flush();
            c.Shine(52, 34, 11, 7, 120);
            c.Fill(Paint.Ellipse(80, 34, 4, 4), Paint.Lighter(color, 0.45f));
            c.Fill(Paint.Ellipse(86, 46, 3, 3), Paint.Lighter(color, 0.45f));
            c.Eye(54, 56, 7, 9, 0.2f, 0.2f);
            c.Eye(74, 56, 7, 9, 0.2f, 0.2f);
            using (var b = new SolidBrush(Paint.Ink)) c.G.FillEllipse(b, 60, 68, 8, 7);
            c.Save(P("enemy4_octopus_" + f));
        }
    }

    // ---------- Hermit crab in a spiral shell (tank) ----------
    static void Hermit(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var leg = f == 0 ? 0f : 3f;
            var body = Paint.Hex("FF8A4A");
            c.Limb(body, 5, 30, 94, 20, 108 + leg);
            c.Limb(body, 5, 44, 98, 40, 114 - leg);
            c.Limb(body, 4, 28, 74, 26, 58);
            c.Flush();
            c.Add(Paint.Ellipse(36, 88, 18, 14), body);
            c.Add(Paint.Pie(14, 78, 11, 210, 300), body);
            c.Flush();
            // Shell
            c.Add(Paint.Smooth(44, 92, 46, 50, 70, 28, 104, 38, 116, 70, 104, 100, 74, 106), Paint.Hex("F2D2A2"));
            c.Flush();
            using (var pen = new Pen(Paint.Hex("B07A4A"), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                c.G.DrawArc(pen, 58, 42, 48, 48, 180, 270);
                c.G.DrawArc(pen, 70, 54, 26, 26, 90, 270);
            }
            c.Shine(70, 42, 12, 6, 110);
            c.Eye(26, 56, 6, 7, -0.6f, 0.2f);
            c.Brow(18, 46, 32, 50);
            c.Save(P("enemy5_hermit_" + f));
        }
    }

    // ---------- Pickups ----------
    static void Pearl()
    {
        using (var c = new Canvas(64, 64, 2.5f))
        {
            c.Add(Paint.Ellipse(32, 32, 22, 22), Color.FromArgb(255, 236, 240, 250));
            c.Flush();
            c.Shine(25, 24, 8, 6, 230);
            c.Fill(Paint.Ellipse(40, 41, 3, 3), Color.FromArgb(160, 255, 255, 255));
            c.Save(P("pickup_pearl"));
        }
    }

    static void Chest()
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var wood = Paint.Hex("A8642E");
            var gold = Paint.Hex("FFC93C");
            c.Add(Paint.RoundRect(18, 58, 92, 52, 8), wood);
            var lid = new GraphicsPath();
            lid.AddArc(18, 26, 92, 60, 180, 180);
            lid.CloseFigure();
            c.Add(lid, Paint.Lighter(wood, 0.1f));
            c.Flush();
            c.Add(Paint.RoundRect(16, 54, 96, 10, 4), gold);
            c.Add(Paint.RoundRect(28, 30, 10, 80, 4), gold);
            c.Add(Paint.RoundRect(90, 30, 10, 80, 4), gold);
            c.Add(Paint.RoundRect(52, 52, 24, 26, 6), gold);
            c.Flush();
            using (var b = new SolidBrush(Paint.Ink)) c.G.FillEllipse(b, 61, 59, 6, 6);
            c.Fill(Paint.Poly(62, 63, 66, 63, 65, 71, 63, 71), Paint.Ink);
            c.Shine(44, 40, 14, 5, 90);
            c.Save(P("pickup_chest"));
        }
    }

    static void Meat()
    {
        using (var c = new Canvas(96, 96, 3f))
        {
            var bone = Paint.Hex("FFF6E0");
            c.Limb(bone, 10, 22, 72, 50, 46);
            c.Add(Paint.Ellipse(16, 74, 8, 8), bone);
            c.Add(Paint.Ellipse(24, 80, 8, 8), bone);
            c.Flush();
            c.Add(Paint.Ellipse(58, 40, 26, 22), Paint.Hex("C8553A"));
            c.Flush();
            c.Shine(52, 30, 10, 6, 120);
            c.Fill(Paint.Ellipse(66, 46, 6, 4), Paint.Hex("E8836A"));
            c.Save(P("pickup_meat"));
        }
    }

    static void Magnet()
    {
        using (var c = new Canvas(96, 96, 3f))
        {
            var u = new GraphicsPath();
            u.AddArc(18, 18, 60, 60, 0, 180);
            u.AddLine(18, 48, 18, 30);
            u.AddLine(18, 30, 34, 30);
            u.AddLine(34, 30, 34, 48);
            u.AddArc(34, 34, 28, 28, 180, -180);
            u.AddLine(62, 48, 62, 30);
            u.AddLine(62, 30, 78, 30);
            u.AddLine(78, 30, 78, 48);
            u.CloseFigure();
            c.Add(u, Paint.Hex("E8333A"));
            c.Flush();
            c.Add(Paint.RoundRect(17, 14, 18, 18, 3), Paint.Hex("D8DEE8"));
            c.Add(Paint.RoundRect(61, 14, 18, 18, 3), Paint.Hex("D8DEE8"));
            c.Flush();
            c.Shine(26, 58, 4, 9, 110);
            c.Save(P("pickup_magnet"));
        }
    }

    // ---------- Flintlock pistol (points right; barrel tip at x=156, y=29 of a 160x80 canvas) ----------
    static void Gun()
    {
        using (var c = new Canvas(160, 80, 3f))
        {
            var wood = Paint.Hex("8A4B22");
            var steel = Paint.Hex("7D8796");
            var brass = Paint.Hex("E3B341");
            // Grip: curved wooden handle down-left
            c.Add(Paint.Smooth(44, 26, 70, 30, 66, 46, 44, 70, 26, 74, 20, 64, 30, 44), wood);
            // Trigger guard
            c.Flush();
            using (var pen = new Pen(brass, 5f)) c.G.DrawArc(pen, 52, 34, 22, 22, 0, 180);
            c.Line(Paint.Ink, 3f, 63, 36, 61, 46);
            // Barrel and stock
            c.Add(Paint.RoundRect(40, 18, 70, 20, 6), wood);
            c.Add(Paint.RoundRect(64, 22, 92, 14, 5), steel);
            c.Add(Paint.RoundRect(146, 19, 11, 20, 4), steel);
            c.Add(Paint.RoundRect(96, 20, 8, 18, 2), brass);
            c.Flush();
            // Hammer / flint
            c.Add(Paint.Poly(48, 20, 44, 8, 52, 6, 56, 18), steel);
            c.Add(Paint.Ellipse(24, 70, 7, 6), brass);
            c.Flush();
            c.Shine(110, 25, 30, 2.5f, 120);
            c.Shine(56, 23, 10, 2, 70);
            c.Save(P("weapon_gun"));
        }
    }

    // ---------- Companion: monkey ----------
    // Frames 0-3 = idle loop (breathing, tail sway), 4 = throwing pose with the arm up, 5 = blink.
    static void Monkey(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var fur = Paint.Hex("A0612E");
            var face = Paint.Hex("F2C99A");
            var throwing = f == 4;
            var blink = f == 5;
            var idle = throwing ? 0 : (blink ? 0 : f);
            float[] breathe = { 0f, 1.5f, 3f, 1.5f };   // head/body sink a little on the out-breath
            float[] tailSway = { 0f, -3f, -5f, -3f };
            var bob = throwing ? -3f : breathe[idle];
            var sway = throwing ? 2f : tailSway[idle];
            var widen = throwing ? 0f : breathe[idle] * 0.5f;  // belly gets a bit wider as it sinks
            // Curly tail behind
            c.Limb(fur, 7, 40, 96, 22 + sway * 0.4f, 92, 16 + sway, 76, 26 + sway, 66 + sway * 0.3f, 34 + sway * 0.6f, 72);
            // Body and feet
            c.Add(Paint.Ellipse(50, 112, 9, 6), fur);
            c.Add(Paint.Ellipse(78, 112, 9, 6), fur);
            c.Add(Paint.Ellipse(64, 92 + bob * 0.5f, 24 + widen, 22 - widen * 0.5f), fur);
            c.Flush();
            c.Fill(Paint.Ellipse(64, 96 + bob * 0.5f, 14 + widen * 0.5f, 13), face);
            // Arm: down holding the banana (swinging a little), or raised to throw
            if (!throwing) c.Limb(fur, 9, 84, 86 + bob * 0.5f, 96, 98 + bob * 0.4f);
            else c.Limb(fur, 9, 82, 80 + bob, 98, 56);
            c.Flush();
            // Head with big ears
            c.Add(Paint.Ellipse(36, 50 + bob, 11, 11), fur);
            c.Add(Paint.Ellipse(92, 50 + bob, 11, 11), fur);
            c.Add(Paint.Ellipse(64, 52 + bob, 28, 25), fur);
            c.Flush();
            c.Fill(Paint.Ellipse(36, 50 + bob, 6, 6), face);
            c.Fill(Paint.Ellipse(92, 50 + bob, 6, 6), face);
            c.Fill(Paint.Smooth(44, 50 + bob, 64, 40 + bob, 84, 50 + bob, 80, 70 + bob, 64, 76 + bob, 48, 70 + bob), face);
            if (blink)
            {
                c.Smile(56, 52 + bob, 12, 4);   // closed eyes: happy little arcs
                c.Smile(72, 52 + bob, 12, 4);
            }
            else
            {
                c.Eye(56, 54 + bob, 6, 7.5f, 0.5f, 0f);
                c.Eye(72, 54 + bob, 6, 7.5f, 0.5f, 0f);
            }
            c.Smile(66, 66 + bob, 12, 5);
            c.Blush(48, 64 + bob);
            c.Blush(82, 64 + bob);
            // Banana in the hand
            var bx = throwing ? 100f : 98f;
            var by = throwing ? 50f : 100f + bob * 0.4f;
            c.Add(BananaPath(bx, by, 0.55f), Paint.Hex("FFD93A"));
            c.Flush();
            c.Save(P("companion_monkey_" + f));
        }
    }

    // ---------- Companion: otter swordsman (faces right) ----------
    // 0-3 = idle loop (breathing, sword on the shoulder), 4 = dash (leaning in, sword held back),
    // 5 = slash (sword swung forward), 6 = blink.
    static void Otter(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var fur = Paint.Hex("8A6248");
            var belly = Paint.Hex("F3E2C4");
            var band = Paint.Hex("E8443A");
            var dash = f == 4;
            var slash = f == 5;
            var blink = f == 6;
            float[] breathe = { 0f, 1.2f, 2.4f, 1.2f };
            var bob = f <= 3 ? breathe[f] : 0f;
            var lean = dash ? 7f : slash ? 4f : 0f;   // upper body leans toward the enemy

            if (dash) Sword(c, 46, 86, 195f, 36);    // held back, behind the body
            // Tail and feet
            c.Limb(fur, 10, 48, 104, 32, 109 - bob * 0.5f, 18, 100 - bob);
            c.Add(Paint.Ellipse(52, 114, 10, 6), fur);
            c.Add(Paint.Ellipse(76, 114, 10, 6), fur);
            // Body
            c.Add(Paint.Ellipse(64 + lean * 0.5f, 92 + bob * 0.5f, 22 + bob * 0.4f, 22 - bob * 0.2f), fur);
            c.Flush();
            c.Fill(Paint.Ellipse(67 + lean * 0.5f, 95 + bob * 0.5f, 14, 15), belly);

            // Head with small round ears
            var hx = 62 + lean;
            var hy = 52 + bob;
            c.Add(Paint.Ellipse(hx - 21, hy - 19, 8, 8), fur);
            c.Add(Paint.Ellipse(hx + 21, hy - 19, 8, 8), fur);
            c.Add(Paint.Ellipse(hx, hy, 29, 25), fur);
            c.Flush();
            c.Fill(Paint.Ellipse(hx - 21, hy - 19, 4, 4), belly);
            c.Fill(Paint.Ellipse(hx + 21, hy - 19, 4, 4), belly);
            // Red headband with tails flying behind
            c.Fill(Paint.Poly(hx - 26, hy - 12, hx - 42, hy - 20 - (dash ? 5 : 0), hx - 38, hy - 6), band);
            c.Fill(Paint.Poly(hx - 26, hy - 10, hx - 40, hy - 2 - (dash ? 3 : 0), hx - 32, hy + 2), band);
            c.Fill(Paint.RoundRect(hx - 28, hy - 17, 56, 9, 4), band);
            // Muzzle, eyes, whiskers
            c.Fill(Paint.Ellipse(hx + 5, hy + 10, 15, 11), belly);
            if (blink)
            {
                c.Smile(hx - 4, hy - 1, 12, 4);
                c.Smile(hx + 13, hy - 1, 12, 4);
            }
            else
            {
                var look = dash || slash ? 0.9f : 0.5f;
                c.Eye(hx - 4, hy + 1, 5.5f, 7f, look, 0f);
                c.Eye(hx + 13, hy + 1, 5.5f, 7f, look, 0f);
            }
            c.Fill(Paint.Ellipse(hx + 6, hy + 7, 4, 3), Paint.Ink);
            c.Smile(hx + 6, hy + 14, 9, 3);
            c.Line(Paint.Ink, 1.5f, hx + 16, hy + 9, hx + 27, hy + 6);
            c.Line(Paint.Ink, 1.5f, hx + 16, hy + 12, hx + 27, hy + 13);
            c.Blush(hx - 12, hy + 10);

            // Arm and sword in front
            if (slash)
            {
                Sword(c, hx + 18, 82, 8f, 36);
                c.Limb(fur, 9, 70 + lean * 0.5f, 84, hx + 16, 82);
            }
            else if (dash)
            {
                c.Limb(fur, 9, 58, 86, 48, 86);
            }
            else
            {
                Sword(c, 86, 94 + bob * 0.5f, 62f, 44);  // resting on the shoulder
                c.Limb(fur, 9, 76, 88 + bob * 0.5f, 86, 94 + bob * 0.5f);
            }
            c.Save(P("companion_otter_" + f));
        }
    }

    // Sword from its hilt at (x, y) pointing at deg (0 = right, 90 = up), blade length len.
    static void Sword(Canvas c, float x, float y, float deg, float len)
    {
        var a = deg * Math.PI / 180.0;
        float dx = (float)Math.Cos(a), dy = -(float)Math.Sin(a);
        float px = -dy, py = dx;
        const float w = 4.5f;
        c.Limb(Paint.Hex("6B3E22"), 5, x - dx * 10, y - dy * 10, x, y);  // handle
        float bx = x + dx * 4, by = y + dy * 4;
        float mx = bx + dx * (len - 9), my = by + dy * (len - 9);
        c.Add(Paint.Poly(bx + px * w, by + py * w, mx + px * w, my + py * w, bx + dx * len, by + dy * len,
            mx - px * w, my - py * w, bx - px * w, by - py * w), Paint.Hex("DCE6F2"));
        c.Flush();
        c.Line(Color.FromArgb(200, 255, 255, 255), 1.6f, bx + dx * 4 + px * 1.2f, by + dy * 4 + py * 1.2f,
            mx + px * 1.2f, my + py * 1.2f);
        c.Limb(Paint.Hex("F2B632"), 5, x + px * 9, y + py * 9, x - px * 9, y - py * 9);  // guard
    }

    // ---------- Slash effect: a 120-degree crescent centered on the image, pointing right ----------
    // White so the game can tint it. 0 = first half of the swing, 1 = full arc, 2 = fading thin arc.
    static void Slash(int f)
    {
        using (var c = new Canvas(256, 256, 0f))
        {
            const float cx = 128, cy = 128, outer = 120;
            var end = f == 0 ? 0f : 60f;          // swing goes from -60 (bottom) to +60 (top)
            var thick = f == 2 ? 0.55f : 1f;
            var alpha = f == 2 ? 150 : 235;
            c.Fill(Crescent(cx, cy, outer + 4, -60, end, 78 * thick), Color.FromArgb(alpha / 3, 255, 255, 255)); // glow
            c.Fill(Crescent(cx, cy, outer, -60, end, 58 * thick), Color.FromArgb(alpha, 230, 245, 255));
            c.Fill(Crescent(cx, cy, outer - 5, -60, end, 22 * thick), Color.FromArgb(Math.Min(255, alpha + 20), 255, 255, 255));
            c.Save(P("fx_slash_" + f));
        }
    }

    // Arc band between angles a0..a1 (degrees, 0 = right, up positive); thickest in the middle of the full 120-degree swing.
    static GraphicsPath Crescent(float cx, float cy, float outer, float a0, float a1, float maxWidth)
    {
        const int steps = 32;
        var pts = new List<PointF>();
        var inner = new List<PointF>();
        for (var i = 0; i <= steps; i++)
        {
            var deg = a0 + (a1 - a0) * i / steps;
            var t = (deg + 60f) / 120f;
            var width = maxWidth * (float)Math.Pow(Math.Sin(Math.PI * Math.Min(1f, Math.Max(0f, t))), 0.7) + 1f;
            var r = deg * Math.PI / 180.0;
            float cos = (float)Math.Cos(r), sin = (float)Math.Sin(r);
            pts.Add(new PointF(cx + cos * outer, cy - sin * outer));
            inner.Add(new PointF(cx + cos * (outer - width), cy - sin * (outer - width)));
        }
        inner.Reverse();
        pts.AddRange(inner);
        var path = new GraphicsPath();
        path.AddPolygon(pts.ToArray());
        return path;
    }

    // ---------- Companion: turtle mage (faces right) ----------
    // 0-3 = idle loop (breathing, gem glow pulsing), 4 = cast (staff thrust forward, gem flaring), 5 = blink.
    static void Turtle(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var skin = Paint.Hex("8FD48A");
            var shell = Paint.Hex("3E9B5A");
            var plate = Paint.Hex("6CC27A");
            var hat = Paint.Hex("7A4FD8");
            var gem = Paint.Hex("D86BFF");
            var cast = f == 4;
            var blink = f == 5;
            float[] breathe = { 0f, 1.2f, 2.4f, 1.2f };
            int[] glow = { 70, 100, 130, 100 };
            var bob = f <= 3 ? breathe[f] : 0f;
            var lean = cast ? 3f : 0f;

            // Staff behind the arm: upright when idle, thrust forward when casting.
            float sx0 = cast ? 98 : 108, sy0 = 112, sx1 = cast ? 118 : 110, sy1 = cast ? 46 : 50 + bob;
            c.Limb(Paint.Hex("8A5A30"), 5, sx0, sy0, sx1, sy1 + 6);

            // Feet, shell, belly rim
            c.Add(Paint.Ellipse(40, 110, 9, 6), skin);
            c.Add(Paint.Ellipse(74, 112, 9, 6), skin);
            c.Add(Paint.Ellipse(26, 96 + bob * 0.5f, 7, 5), skin);  // little tail
            c.Add(Paint.Ellipse(56, 90 + bob * 0.5f, 32, 24 - bob * 0.3f), shell);
            c.Flush();
            c.Fill(Paint.Ellipse(56, 82 + bob * 0.5f, 10, 8), plate);
            c.Fill(Paint.Ellipse(40, 90 + bob * 0.5f, 8, 7), plate);
            c.Fill(Paint.Ellipse(72, 90 + bob * 0.5f, 8, 7), plate);
            c.Fill(Paint.Ellipse(56, 99 + bob * 0.5f, 9, 5), plate);
            c.Fill(Paint.RoundRect(26, 104 + bob * 0.5f, 62, 6, 3), Paint.Hex("F2E2A6"));

            // Head
            var hx = 88 + lean;
            var hy = 66 + bob;
            c.Add(Paint.Ellipse(hx, hy, 18, 16), skin);
            c.Flush();
            if (blink)
            {
                c.Smile(hx + 4, hy - 2, 10, 3.5f);
            }
            else
            {
                c.Eye(hx + 4, hy - 1, 5f, 6.5f, cast ? 0.9f : 0.5f, 0f);
            }
            c.Smile(hx + 8, hy + 8, 9, 3);
            c.Blush(hx - 6, hy + 6);

            // Wizard hat with a star, tip flopping back
            c.Add(Paint.Smooth(hx - 16, hy - 10, hx - 4, hy - 34, hx - 14, hy - 50, hx - 2, hy - 44, hx + 8, hy - 26, hx + 16, hy - 10), hat);
            c.Add(Paint.Ellipse(hx, hy - 10, 21, 5), hat);
            c.Flush();
            c.Fill(Paint.Star(hx + 1, hy - 24, 6, 2.6f, 5, -90), Paint.Hex("FFD93A"));

            // Arm holding the staff
            c.Limb(skin, 8, 74 + lean, 86 + bob * 0.5f, cast ? 104 : 108, cast ? 80 : 86 + bob * 0.5f);

            // Gem on top of the staff with a glow
            var ga = cast ? 200 : glow[Math.Min(f, 3)];
            using (var b = new SolidBrush(Color.FromArgb(ga, 230, 150, 255)))
            {
                var gr = cast ? 15f : 10f;
                c.G.FillEllipse(b, sx1 - gr, sy1 - gr, gr * 2, gr * 2);
            }
            c.Add(Paint.Ellipse(sx1, sy1, 6, 6), gem);
            c.Flush();
            c.Shine(sx1 - 2, sy1 - 2, 2, 2, 230);
            if (cast)
            {
                c.Fill(Paint.Star(sx1 - 14, sy1 + 12, 4, 1.5f, 4, 0), Color.White);
                c.Fill(Paint.Star(sx1 + 2, sy1 - 16 + 4, 3, 1.2f, 4, 0), Color.White);
            }
            c.Save(P("companion_turtle_" + f));
        }
    }

    // Magic orb: glowing pink-violet ball with a white core. Two frames shimmer.
    static void MagicOrb(int f)
    {
        using (var c = new Canvas(64, 64, 0f))
        {
            var g = f == 0 ? 0f : 3f;
            c.Fill(Paint.Ellipse(32, 32, 28 + g * 0.5f, 28 + g * 0.5f), Color.FromArgb(70, 210, 120, 255));
            c.Fill(Paint.Ellipse(32, 32, 20, 20), Color.FromArgb(220, 200, 100, 255));
            c.Fill(Paint.Ellipse(32, 32, 13, 13), Color.FromArgb(255, 245, 180, 255));
            c.Fill(Paint.Ellipse(32, 32, 7, 7), Color.White);
            c.Fill(Paint.Star(f == 0 ? 18 : 46, f == 0 ? 18 : 20, 6, 1.6f, 4, 0), Color.White);
            c.Fill(Paint.Star(f == 0 ? 46 : 20, f == 0 ? 44 : 46, 4, 1.2f, 4, 0), Color.FromArgb(220, 255, 255, 255));
            c.Save(P("projectile_orb_" + f));
        }
    }

    // Magic explosion, centered, filling the image at its largest:
    // 0 = bright flash, 1 = big starry burst, 2 = ring of puffs, 3 = thin fading ring with sparkles.
    static void Blast(int f)
    {
        using (var c = new Canvas(256, 256, 0f))
        {
            const float cx = 128, cy = 128;
            var violet = Color.FromArgb(200, 190, 90, 255);
            var pink = Color.FromArgb(230, 255, 140, 230);
            switch (f)
            {
                case 0:
                    c.Fill(Paint.Ellipse(cx, cy, 70, 70), Color.FromArgb(120, 210, 130, 255));
                    c.Fill(Paint.Star(cx, cy, 64, 26, 8, -90), pink);
                    c.Fill(Paint.Ellipse(cx, cy, 30, 30), Color.White);
                    break;
                case 1:
                    c.Fill(Paint.Ellipse(cx, cy, 118, 118), Color.FromArgb(90, 190, 90, 255));
                    c.Fill(Paint.Star(cx, cy, 116, 62, 12, -90), violet);
                    c.Fill(Paint.Ellipse(cx, cy, 64, 64), pink);
                    c.Fill(Paint.Ellipse(cx, cy, 34, 34), Color.FromArgb(240, 255, 255, 255));
                    break;
                case 2:
                    for (var i = 0; i < 10; i++)
                    {
                        var a = i * Math.PI * 2 / 10;
                        float px = cx + (float)Math.Cos(a) * 92, py = cy + (float)Math.Sin(a) * 92;
                        c.Fill(Paint.Ellipse(px, py, 22, 22), Color.FromArgb(150, 200, 110, 255));
                        c.Fill(Paint.Ellipse(px, py, 11, 11), Color.FromArgb(170, 255, 190, 245));
                    }
                    c.Fill(Paint.Ellipse(cx, cy, 70, 70), Color.FromArgb(70, 230, 160, 255));
                    break;
                default:
                    using (var pen = new Pen(Color.FromArgb(160, 230, 170, 255), 7f))
                        c.G.DrawEllipse(pen, cx - 116, cy - 116, 232, 232);
                    for (var i = 0; i < 8; i++)
                    {
                        var a = (i + 0.5) * Math.PI * 2 / 8;
                        c.Fill(Paint.Star(cx + (float)Math.Cos(a) * 100, cy + (float)Math.Sin(a) * 100, 9, 3, 4, 0),
                            Color.FromArgb(200, 255, 255, 255));
                    }
                    break;
            }
            c.Save(P("fx_blast_" + f));
        }
    }

    // ---------- Companion: little gunner (faces right; the gun is a separate sprite that aims) ----------
    // 0-3 = idle loop (breathing), 4 = blink.
    static void Gunner(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var skin = Paint.Hex("F6C9A0");
            var camo = Paint.Hex("5A86C8");
            var camoDark = Paint.Hex("3E5F9A");
            var camoLight = Paint.Hex("9DBFEA");
            var boots = Paint.Hex("4A3A32");
            float[] breathe = { 0f, 1.2f, 2.4f, 1.2f };
            var bob = f <= 3 ? breathe[f] : 0f;
            var blink = f == 4;

            // Backpack, legs, boots
            c.Add(Paint.RoundRect(34, 74 + bob, 16, 24, 5), Paint.Hex("7A6A4A"));
            c.Add(Paint.Ellipse(54, 114, 10, 6), boots);
            c.Add(Paint.Ellipse(76, 114, 10, 6), boots);
            c.Add(Paint.RoundRect(49, 98, 10, 14, 3), camoDark);
            c.Add(Paint.RoundRect(69, 98, 10, 14, 3), camoDark);
            c.Flush();
            // Body: camo shirt with belt
            c.Add(Paint.RoundRect(44, 72 + bob, 40, 30 - bob * 0.5f, 10), camo);
            c.Flush();
            c.Fill(Paint.Ellipse(54, 82 + bob, 5, 4), camoDark);
            c.Fill(Paint.Ellipse(72, 90 + bob, 6, 4), camoDark);
            c.Fill(Paint.Ellipse(62, 94 + bob, 4, 3), camoLight);
            c.Fill(Paint.RoundRect(44, 96 + bob * 0.5f, 40, 5, 2), Paint.Hex("4A3A32"));

            // Head
            var hy = 50 + bob;
            c.Add(Paint.Ellipse(64, hy, 28, 25), skin);
            c.Flush();
            // Camo helmet: dome + brim, with patches and a chin strap
            var helmet = new GraphicsPath();
            helmet.AddArc(34, hy - 34, 60, 50, 180, 180);
            helmet.CloseFigure();
            c.Add(helmet, camo);
            c.Add(Paint.RoundRect(32, hy - 12, 66, 8, 4), camoDark);
            c.Flush();
            c.Fill(Paint.Ellipse(50, hy - 22, 7, 5), camoDark);
            c.Fill(Paint.Ellipse(72, hy - 26, 6, 4), camoLight);
            c.Fill(Paint.Ellipse(82, hy - 17, 5, 4), camoDark);
            c.Fill(Paint.Ellipse(62, hy - 16, 4, 3), camoLight);
            c.Line(Paint.Ink, 2f, 38, hy - 4, 42, hy + 14);

            // Face, looking right
            if (blink)
            {
                c.Smile(60, hy + 4, 11, 4);
                c.Smile(77, hy + 4, 11, 4);
            }
            else
            {
                c.Eye(60, hy + 5, 5.5f, 7.5f, 0.7f, 0.1f);
                c.Eye(77, hy + 5, 5.5f, 7.5f, 0.7f, 0.1f);
            }
            c.Blush(52, hy + 15);
            c.Blush(86, hy + 15);
            c.Smile(70, hy + 15, 10, 4);

            // Hands in front, where the gun is held
            c.Add(Paint.Ellipse(80, 86 + bob, 6, 6), skin);
            c.Add(Paint.Ellipse(90, 84 + bob, 6, 6), skin);
            c.Flush();
            c.Save(P("companion_gunner_" + f));
        }
    }

    // Small machine gun pointing right; the grip sits a bit left of center.
    static void Smg()
    {
        using (var c = new Canvas(128, 64, 3f))
        {
            var metal = Paint.Hex("4C5566");
            var dark = Paint.Hex("2F3540");
            c.Add(Paint.RoundRect(10, 22, 22, 10, 3), dark);          // folded stock
            c.Add(Paint.RoundRect(44, 34, 11, 22, 3), dark);          // magazine
            c.Add(Paint.RoundRect(30, 36, 10, 16, 3), dark);          // grip
            c.Add(Paint.RoundRect(28, 18, 62, 20, 6), metal);         // body
            c.Add(Paint.RoundRect(88, 23, 30, 9, 3), dark);           // barrel
            c.Flush();
            c.Fill(Paint.RoundRect(110, 21, 8, 13, 2), Paint.Hex("1F242C")); // muzzle
            c.Fill(Paint.RoundRect(52, 14, 14, 5, 2), dark);                  // sight
            c.Shine(56, 24, 18, 3, 90);
            c.Save(P("weapon_smg"));
        }
    }

    // ---------- Companion: frog with a slingshot (faces right) ----------
    // 0-3 = idle loop (breathing, slingshot held up), 4 = shot (arm forward, band snapped), 5 = blink.
    static void Frog(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var skin = Paint.Hex("6FCF5B");
            var belly = Paint.Hex("E9F5B8");
            var wood = Paint.Hex("9A6234");
            float[] breathe = { 0f, 1.2f, 2.4f, 1.2f };
            var bob = f <= 3 ? breathe[f] : 0f;
            var shot = f == 4;
            var blink = f == 5;

            // Back leg + feet
            c.Add(Paint.Ellipse(42, 108, 14, 9), skin);
            c.Add(Paint.Ellipse(50, 116, 11, 5), skin);
            c.Add(Paint.Ellipse(78, 116, 11, 5), skin);
            // Round body
            c.Add(Paint.Ellipse(62, 92 + bob * 0.5f, 26, 22 - bob * 0.3f), skin);
            c.Flush();
            c.Fill(Paint.Ellipse(66, 96 + bob * 0.5f, 16, 14), belly);
            // Little red scarf
            c.Fill(Paint.RoundRect(42, 70 + bob, 42, 7, 3), Paint.Hex("E8443A"));
            c.Fill(Paint.Poly(46, 72 + bob, 34, 80 + bob, 40, 84 + bob), Paint.Hex("E8443A"));

            // Head: wide with two eye bumps on top
            var hy = 54 + bob;
            c.Add(Paint.Ellipse(64, hy, 30, 20), skin);
            c.Add(Paint.Ellipse(52, hy - 16, 11, 11), skin);
            c.Add(Paint.Ellipse(78, hy - 16, 11, 11), skin);
            c.Flush();
            // Aviator goggles pushed up on the forehead strap
            c.Fill(Paint.RoundRect(36, hy - 8, 56, 5, 2), Paint.Hex("5A3A22"));
            if (blink)
            {
                c.Smile(52, hy - 17, 11, 4);
                c.Smile(78, hy - 17, 11, 4);
            }
            else
            {
                c.Eye(52, hy - 16, 6, 7, shot ? 0.9f : 0.5f, 0f);
                c.Eye(78, hy - 16, 6, 7, shot ? 0.9f : 0.5f, 0f);
            }
            c.Smile(68, hy + 6, 13, 5);   // small cheerful smile
            c.Blush(44, hy + 4);
            c.Blush(88, hy + 4);

            // Slingshot: big Y-shaped wooden fork with a rubber band, held in front (s = size; 1 = the old tiny one).
            const float s = 2.5f;
            float hx = shot ? 104 : 98, hy2 = shot ? 96 : 100 + bob * 0.5f;    // hand on the handle
            float jy = hy2 - 11 * s;                                           // where the fork splits
            float ty = hy2 - 22 * s;                                           // top of the prongs
            float half = 7 * s;                                                // prong spread
            c.Limb(wood, 4 * s * 0.9f, hx, hy2 + 4, hx, jy);
            c.Limb(wood, 3.2f * s * 0.9f, hx, jy, hx - half, ty);
            c.Limb(wood, 3.2f * s * 0.9f, hx, jy, hx + half, ty);
            using (var band = new Pen(Paint.Hex("C8302A"), 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (shot) c.G.DrawLine(band, hx - half, ty, hx + half, ty);   // snapped straight after the shot
                else c.G.DrawLines(band, new[] { new PointF(hx - half, ty), new PointF(hx - 15, jy + 6), new PointF(hx + half, ty) }); // pulled back to the chest
            }
            if (!shot) c.Fill(Paint.Ellipse(hx - 15, jy + 6, 5, 5), Paint.Hex("FF6A3A")); // pellet in the pouch
            // Hand holding the handle
            c.Add(Paint.Ellipse(hx, hy2, 8, 8), skin);
            c.Flush();
            c.Save(P("companion_frog_" + f));
        }
    }

    // Rubber pellet: small glossy orange-red ball.
    static void Pellet()
    {
        using (var c = new Canvas(32, 32, 2.5f))
        {
            c.Add(Paint.Ellipse(16, 16, 11, 11), Paint.Hex("FF6A3A"));
            c.Flush();
            c.Shine(12, 12, 4, 3, 200);
            c.Save(P("projectile_pellet"));
        }
    }

    // Heal effect: a chunky green plus sign with a soft glow (the game tints and fades it).
    static void HealPlus()
    {
        using (var c = new Canvas(64, 64, 3f))
        {
            c.Fill(Paint.Ellipse(32, 32, 28, 28), Color.FromArgb(60, 140, 255, 140));          // glow
            var plus = new GraphicsPath();
            plus.AddPath(Paint.RoundRect(24, 10, 16, 44, 5), false);
            plus.AddPath(Paint.RoundRect(10, 24, 44, 16, 5), false);
            plus.FillMode = FillMode.Winding;
            c.Add(plus, Paint.Hex("4CD964"));
            c.Flush();
            c.Shine(28, 18, 4, 5, 170);
            c.Save(P("fx_heal_plus"));
        }
    }

    // Barbed-wire fence segment for the boss arena wall: a wooden post on the left, two sagging wires with barbs.
    // Tiles left-to-right (the next segment's post closes the wires), drawn along each side of the arena.
    static void BarbedFence()
    {
        using (var c = new Canvas(256, 96, 3f))
        {
            var wood = Paint.Hex("8A5A30");
            var wire = Paint.Hex("5E6672");
            var dark = Paint.Hex("3A3F48");
            // Two wires with a slight sag, from this post to the next one (x = 256 is the next post).
            float[] wireY = { 34, 62 };
            foreach (var y in wireY)
            {
                c.Line(Paint.Ink, 6f, 0, y, 128, y + 6, 256, y);
                c.Line(wire, 3.5f, 0, y, 128, y + 6, 256, y);
                // Barbs: little twisted X knots along the wire
                for (var bx = 40; bx < 256; bx += 54)
                {
                    var sag = 6f * (float)Math.Sin(bx / 256.0 * Math.PI);
                    var by = y + sag;
                    c.Line(Paint.Ink, 4.5f, bx - 7, by - 7, bx + 7, by + 7);
                    c.Line(Paint.Ink, 4.5f, bx - 7, by + 7, bx + 7, by - 7);
                    c.Line(dark, 2.5f, bx - 7, by - 7, bx + 7, by + 7);
                    c.Line(dark, 2.5f, bx - 7, by + 7, bx + 7, by - 7);
                    c.Fill(Paint.Ellipse(bx, by, 3, 3), wire);
                }
            }
            // Wooden post (rough top), drawn over the wire ends
            c.Add(Paint.Poly(14, 90, 14, 14, 20, 8, 28, 12, 32, 16, 32, 90), wood);
            c.Flush();
            c.Fill(Paint.RoundRect(17, 22, 3, 60, 1), Paint.Hex("A8743F"));   // grain highlight
            c.Fill(Paint.Ellipse(23, 50, 3, 2), Paint.Hex("6B4424"));          // knot
            c.Save(P("fx_barbed_fence"));
        }
    }

    // Damage number font: digits 0-9 side by side, chunky white with a dark outline (the game tints them).
    // Saved to Assets/Resources/DigitFont.png, loaded by DigitFont.cs.
    static void DigitAtlas()
    {
        const int cellW = 48, cellH = 64;
        using (var bmp = new Bitmap(cellW * 10, cellH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        using (var family = new FontFamily("Arial Black"))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (var outline = new Pen(Paint.Ink, 9f) { LineJoin = LineJoin.Round })
        using (var white = new SolidBrush(Color.White))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (var d = 0; d < 10; d++)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddString(d.ToString(), family, (int)FontStyle.Regular, 50f,
                        new RectangleF(d * cellW, 3, cellW, cellH), format);
                    g.DrawPath(outline, path);
                    g.FillPath(white, path);
                }
            }
            var file = Path.Combine(dir, "..", "Resources", "DigitFont.png");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            bmp.Save(file, ImageFormat.Png);
        }
    }

    // Blue upside-down triangle shown over the hero's head.
    static void HeroMarker()
    {
        using (var c = new Canvas(64, 64, 3.5f))
        {
            c.Add(Paint.Poly(8, 12, 56, 12, 32, 54), Paint.Hex("2E8BFF"));
            c.Flush();
            c.Shine(24, 20, 8, 3.5f, 150);
            c.Save(P("ui_hero_marker"));
        }
    }

    // Crescent banana centered on (cx, cy).
    static GraphicsPath BananaPath(float cx, float cy, float s)
    {
        return Paint.Smooth(cx - 30 * s, cy - 18 * s, cx - 4 * s, cy + 10 * s, cx + 26 * s, cy + 2 * s, cx + 34 * s, cy - 14 * s,
            cx + 20 * s, cy - 4 * s, cx - 6 * s, cy - 2 * s);
    }

    static void Banana()
    {
        using (var c = new Canvas(64, 64, 2.5f))
        {
            c.Add(BananaPath(30, 34, 0.9f), Paint.Hex("FFD93A"));
            c.Flush();
            c.Fill(Paint.Ellipse(4, 18, 3, 3), Paint.Hex("6B4A22"));
            c.Fill(Paint.Ellipse(60, 21, 2.5f, 2.5f), Paint.Hex("6B4A22"));
            c.Shine(28, 34, 10, 2.5f, 120);
            c.Save(P("projectile_banana"));
        }
    }

    // ---------- Muzzle flash (cone pointing right, starts at the left edge) ----------
    static void MuzzleFlash(int f)
    {
        using (var c = new Canvas(128, 64, 0f))
        {
            var len = f == 0 ? 118f : 92f;
            var spread = f == 0 ? 26f : 18f;
            c.Fill(Paint.Smooth(4, 32, len * 0.55f, 32 - spread, len, 32, len * 0.55f, 32 + spread), Color.FromArgb(230, 255, 140, 30));
            c.Fill(Paint.Smooth(4, 32, len * 0.45f, 32 - spread * 0.55f, len * 0.8f, 32, len * 0.45f, 32 + spread * 0.55f), Color.FromArgb(255, 255, 220, 80));
            c.Fill(Paint.Ellipse(14, 32, 12, 10), Color.FromArgb(255, 255, 255, 220));
            // Side sparks
            c.Fill(Paint.Poly(30, 32, 46, 8 + f * 6, 40, 30), Color.FromArgb(220, 255, 180, 60));
            c.Fill(Paint.Poly(30, 32, 46, 56 - f * 6, 40, 34), Color.FromArgb(220, 255, 180, 60));
            c.Save(P("fx_muzzle_" + f));
        }
    }

    static void Cannonball()
    {
        using (var c = new Canvas(64, 64, 2.5f))
        {
            c.Add(Paint.Ellipse(32, 32, 22, 22), Paint.Hex("3A3F52"));
            c.Flush();
            c.Shine(25, 24, 7, 5, 170);
            c.Save(P("weapon_cannonball"));
        }
    }

    static void Anchor()
    {
        using (var c = new Canvas(96, 96, 3f))
        {
            var steel = Paint.Hex("9AA8BC");
            c.Limb(steel, 8, 48, 26, 48, 80);
            c.Limb(steel, 7, 30, 36, 66, 36);
            c.Limb(steel, 7, 20, 62, 30, 80, 48, 86, 66, 80, 76, 62);
            c.Add(Paint.Poly(14, 58, 26, 58, 20, 70), steel);
            c.Add(Paint.Poly(70, 58, 82, 58, 76, 70), steel);
            c.Add(Paint.Ellipse(48, 18, 9, 9), steel);
            c.Flush();
            using (var b = new SolidBrush(Paint.Ink)) c.G.FillEllipse(b, 45, 15, 6, 6);
            c.Save(P("weapon_anchor"));
        }
    }

    // ---------- Beach decorations ----------
    static void Palm()
    {
        using (var c = new Canvas(256, 256, 3.5f))
        {
            var trunk = Paint.Hex("B98552");
            for (var i = 0; i < 9; i++)
            {
                var t = i / 8f;
                var x = 128 + (float)Math.Sin(t * 1.6) * 30;
                var y = 240 - t * 150;
                c.Add(Paint.Ellipse(x, y, 13 - t * 3, 11), Paint.Lighter(trunk, i % 2 == 0 ? 0f : 0.12f));
            }
            c.Flush();
            var topX = 128 + (float)Math.Sin(1.6) * 30;
            const float topY = 90;
            var leaf = Paint.Hex("3DBE5A");
            float[][] leaves =
            {
                new[] { -95f, 20f }, new[] { -60f, -38f }, new[] { 5f, -55f }, new[] { 70f, -30f }, new[] { 100f, 25f }
            };
            foreach (var l in leaves)
            {
                var ex = topX + l[0];
                var ey = topY + l[1];
                var mx = (topX + ex) / 2;
                var my = (topY + ey) / 2 - 18;
                c.Add(Paint.Smooth(topX, topY, mx - 16, my - 18, ex, ey, mx + 12, my + 16), leaf);
            }
            c.Flush();
            c.Add(Paint.Ellipse(topX - 10, topY + 10, 9, 9), Paint.Hex("7A4A2A"));
            c.Add(Paint.Ellipse(topX + 8, topY + 12, 9, 9), Paint.Hex("7A4A2A"));
            c.Flush();
            c.Save(P("deco_palm"));
        }
    }

    static void Rock()
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            c.Add(Paint.Smooth(18, 96, 26, 58, 52, 38, 88, 42, 110, 70, 108, 100, 64, 108), Paint.Hex("9CA3B0"));
            c.Flush();
            c.Shine(52, 54, 18, 8, 80);
            c.Save(P("deco_rock"));
        }
    }

    static void Shell()
    {
        using (var c = new Canvas(64, 64, 2.5f))
        {
            var fan = new GraphicsPath();
            fan.AddPie(8, 10, 48, 48, 200, 140);
            c.Add(fan, Paint.Hex("FFB3C7"));
            c.Add(Paint.RoundRect(26, 32, 12, 8, 3), Paint.Hex("FFB3C7"));
            c.Flush();
            for (var i = 0; i < 5; i++)
            {
                var a = (210 + i * 30) * Math.PI / 180;
                c.Line(Paint.Hex("E07A98"), 2f, 32, 34, 32 + (float)Math.Cos(a) * 20, 34 + (float)Math.Sin(a) * 20);
            }
            c.Save(P("deco_shell"));
        }
    }

    // ---------- Seamless sand tile ----------
    static void Sand()
    {
        const int size = 256;
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Paint.Hex("F6DDA4"));
            var rnd = new Random(42);
            // Soft blotches and speckles, drawn 3x3 times with wrap-around so the tile repeats seamlessly.
            for (var i = 0; i < 26; i++)
            {
                var x = (float)rnd.NextDouble() * size;
                var y = (float)rnd.NextDouble() * size;
                var r = 18 + (float)rnd.NextDouble() * 30;
                var col = rnd.Next(2) == 0 ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(22, 190, 140, 70);
                Wrapped(g, col, x, y, r, r * 0.7f, size);
            }
            for (var i = 0; i < 420; i++)
            {
                var x = (float)rnd.NextDouble() * size;
                var y = (float)rnd.NextDouble() * size;
                var r = 0.8f + (float)rnd.NextDouble() * 1.6f;
                var col = rnd.Next(3) == 0 ? Color.FromArgb(120, 255, 250, 235) : Color.FromArgb(90, 196, 150, 90);
                Wrapped(g, col, x, y, r, r, size);
            }
            var path = Path.Combine(dir, "Ground", "ground_sand.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bmp.Save(path, ImageFormat.Png);
        }
    }

    static void Wrapped(Graphics g, Color col, float x, float y, float rx, float ry, int size)
    {
        using (var b = new SolidBrush(col))
            for (var ox = -1; ox <= 1; ox++)
            for (var oy = -1; oy <= 1; oy++)
                g.FillEllipse(b, x + ox * size - rx, y + oy * size - ry, rx * 2, ry * 2);
    }
}
