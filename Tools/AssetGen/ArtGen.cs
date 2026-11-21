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
    static void Player(int f)
    {
        using (var c = new Canvas(128, 128, 3f))
        {
            var bob = f % 2 == 1 ? -3f : 0f;
            var step = f == 0 ? 5f : f == 2 ? -5f : 0f;

            // Back: bandana tails and legs
            c.Add(Paint.Poly(30, 40 + bob, 16, 34 + bob, 20, 48 + bob), Paint.Hex("1FB5A8"));
            c.Add(Paint.Poly(30, 44 + bob, 14, 52 + bob, 24, 56 + bob), Paint.Hex("1FB5A8"));
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
            c.Eye(58, 55 + bob, 6.5f, 8.5f, 0.7f, 0.1f);
            c.Eye(76, 55 + bob, 6.5f, 8.5f, 0.7f, 0.1f);
            c.Blush(50, 66 + bob);
            c.Blush(84, 66 + bob);
            c.Smile(68, 66 + bob, 14, 6);
            c.Save(P("player_" + f));
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
