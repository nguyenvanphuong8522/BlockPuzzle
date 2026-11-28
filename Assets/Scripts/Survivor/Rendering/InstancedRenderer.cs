using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor
{
    // Draw order: earlier layers are drawn below later ones.
    // World is depth-sorted: player, gun, enemies and tall props are ordered by their feet (sortY), so whatever is
    // lower on screen (closer to the viewer) is drawn on top.
    public enum RenderLayer { Decorations, Shadows, Gems, Pickups, World, Projectiles, Weapons, Effects, Overlay, Text }

    // Collects shapes and sprites during the frame and draws them with GPU instancing.
    // Shapes share one material; sprites get one material per texture. Put all sprites in one
    // SpriteAtlas so they share a texture and everything in a layer stays in a few draw calls.
    public sealed class InstancedRenderer : IDisposable
    {
        const int BatchSize = 1023;
        const float ShapeCircle = 0f, ShapeRect = 1f, ShapeRing = 2f, ShapeSprite = 3f, ShapeSector = 4f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int UvId = Shader.PropertyToID("_UV");
        static readonly Vector4 FullUv = new Vector4(0f, 0f, 1f, 1f);

        internal sealed class Batch
        {
            public readonly Matrix4x4[] Matrices = new Matrix4x4[BatchSize];
            public readonly Vector4[] Colors = new Vector4[BatchSize];
            public readonly Vector4[] Shapes = new Vector4[BatchSize];
            public readonly Vector4[] Uvs = new Vector4[BatchSize];
            public readonly float[] Keys = new float[BatchSize]; // sortY, only used by sorted layers
        }

        // Everything drawn with one material (= one texture) in one layer.
        internal sealed class Group
        {
            public Material Material;
            public List<Batch> Batches = new List<Batch>();
            public List<Batch> Scratch = new List<Batch>(); // reordered copy while sorting
            public int Used;
        }

        sealed class Layer
        {
            public Group Shapes;
            public readonly Dictionary<Texture, Group> Textured = new Dictionary<Texture, Group>();
            public readonly List<Group> TexturedOrder = new List<Group>();
            public int RenderQueue;
            public bool Sorted;
        }

        readonly Layer[] layers;
        readonly Mesh quad;
        readonly Material baseMaterial;
        readonly Material groundMaterial;
        readonly List<Material> createdMaterials = new List<Material>();
        readonly Dictionary<Sprite, Vector4> spriteUvs = new Dictionary<Sprite, Vector4>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        NativeArray<SortItem> sortItems;

        // Visible world rectangle. Shapes outside it are dropped before they cost anything.
        float viewMinX = float.MinValue, viewMaxX = float.MaxValue, viewMinY = float.MinValue, viewMaxY = float.MaxValue;

        public int LastFrameInstances { get; private set; }
        public int LastFrameDrawCalls { get; private set; }

        public InstancedRenderer()
        {
            // Material assets (not Shader.Find) so the instancing variant is included in builds.
            baseMaterial = Resources.Load<Material>("Shaders/InstancedShape");
            groundMaterial = CreateMaterial(Resources.Load<Material>("Shaders/Ground"), null, 1000);

            var count = Enum.GetValues(typeof(RenderLayer)).Length;
            layers = new Layer[count];
            for (var i = 0; i < count; i++)
            {
                var layer = new Layer { RenderQueue = 3000 + i, Sorted = i == (int)RenderLayer.World };
                layer.Shapes = new Group { Material = CreateMaterial(baseMaterial, null, layer.RenderQueue) };
                layers[i] = layer;
            }
            quad = CreateQuad();
        }

        // Stretched circle, e.g. a soft shadow under a unit.
        public void Ellipse(RenderLayer layer, Vector2 center, Vector2 size, Color color) =>
            Add(layers[(int)layer].Shapes, center, size, color, new Vector4(ShapeCircle, 0f, 0f, 0f), FullUv, center.y);

        // Call once per frame before drawing.
        public void SetView(Camera camera)
        {
            var halfH = camera.orthographicSize;
            var halfW = halfH * camera.aspect;
            var p = camera.transform.position;
            viewMinX = p.x - halfW;
            viewMaxX = p.x + halfW;
            viewMinY = p.y - halfH;
            viewMaxY = p.y + halfH;
        }

        // (minX, minY, maxX, maxY) of the visible world rectangle.
        public Vector4 ViewRect => new Vector4(viewMinX, viewMinY, viewMaxX, viewMaxY);

        // extent = half of the object's largest dimension.
        public bool IsVisible(Vector2 center, float extent) =>
            center.x + extent >= viewMinX && center.x - extent <= viewMaxX &&
            center.y + extent >= viewMinY && center.y - extent <= viewMaxY;

        // sortY: depth key on the World layer (usually the feet). NaN = use the center.
        public void Circle(RenderLayer layer, Vector2 center, float radius, Color color, float flash = 0f, float sortY = float.NaN) =>
            Add(layers[(int)layer].Shapes, center, new Vector2(radius * 2f, radius * 2f), color,
                new Vector4(ShapeCircle, 0f, flash, 0f), FullUv, float.IsNaN(sortY) ? center.y : sortY);

        // thickness is a fraction of the radius (0..1).
        public void Ring(RenderLayer layer, Vector2 center, float radius, float thickness, Color color) =>
            Add(layers[(int)layer].Shapes, center, new Vector2(radius * 2f, radius * 2f), color,
                new Vector4(ShapeRing, 1f - Mathf.Clamp01(thickness), 0f, 0f), FullUv, center.y);

        public void Rect(RenderLayer layer, Vector2 center, Vector2 size, Color color) =>
            Add(layers[(int)layer].Shapes, center, size, color, new Vector4(ShapeRect, 0f, 0f, 0f), FullUv, center.y);

        // Bar anchored on its left edge, e.g. a health bar. fill is 0..1.
        public void Bar(RenderLayer layer, Vector2 center, Vector2 size, float fill, Color background, Color foreground)
        {
            Rect(layer, center, size, background);
            var w = size.x * Mathf.Clamp01(fill);
            Rect(layer, new Vector2(center.x - size.x / 2f + w / 2f, center.y), new Vector2(w, size.y), foreground);
        }

        // Draws a sprite with the given world height; width follows the sprite's aspect ratio.
        public void Sprite(RenderLayer layer, Sprite sprite, Vector2 center, float height, Color tint,
            bool flipX = false, float flash = 0f, float sortY = float.NaN)
        {
            var rect = sprite.rect;
            var width = height * rect.width / rect.height;
            if (!IsVisible(center, Mathf.Max(width, height) * 0.5f)) return;
            var uv = SpriteUv(sprite);
            var trim = SpriteTrim(sprite);
            var drawCenter = center + new Vector2((flipX ? -trim.x : trim.x) * width, trim.y * height);
            var w = width * trim.z;
            Add(GetTexturedGroup(layers[(int)layer], sprite.texture), drawCenter,
                new Vector2(flipX ? -w : w, height * trim.w), tint, new Vector4(ShapeSprite, 0f, flash, 0f), uv,
                float.IsNaN(sortY) ? center.y : sortY);
        }

        // The atlas packer cuts away transparent borders, so sprite.uv only covers the visible part of the image.
        // This says where that part sits inside the full image: (center x, center y) as a fraction of the full size
        // measured from the image center, and (width, height) as a fraction of the full size. Untrimmed = (0, 0, 1, 1).
        readonly Dictionary<Sprite, Vector4> spriteTrims = new Dictionary<Sprite, Vector4>();
        public Vector4 SpriteTrim(Sprite sprite)
        {
            if (spriteTrims.TryGetValue(sprite, out var trim)) return trim;
            var ppu = sprite.pixelsPerUnit;
            var rect = sprite.rect;
            var fullMin = -sprite.pivot / ppu;
            var fullSize = rect.size / ppu;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var v in sprite.vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            if (fullSize.x <= 0f || fullSize.y <= 0f || min.x > max.x) trim = new Vector4(0f, 0f, 1f, 1f);
            else
            {
                var c = ((min + max) * 0.5f - fullMin) / fullSize - new Vector2(0.5f, 0.5f);
                var s = (max - min) / fullSize;
                trim = new Vector4(c.x, c.y, s.x, s.y);
            }
            spriteTrims[sprite] = trim;
            return trim;
        }

        // Rotated sprite (angle in radians, 0 = pointing right). flipY mirrors it vertically, e.g. a gun aimed left
        // stays upright instead of upside down.
        public void SpriteRotated(RenderLayer layer, Sprite sprite, Vector2 center, float height, float angle, Color tint,
            bool flipY = false, float flash = 0f, float sortY = float.NaN)
        {
            var rect = sprite.rect;
            var width = height * rect.width / rect.height;
            if (!IsVisible(center, Mathf.Max(width, height) * 0.5f)) return;
            var trim = SpriteTrim(sprite);
            var local = new Vector2(trim.x * width, (flipY ? -trim.y : trim.y) * height);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            var drawCenter = center + new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
            var h = height * trim.w;
            AddRotated(GetTexturedGroup(layers[(int)layer], sprite.texture), drawCenter, new Vector2(width * trim.z, flipY ? -h : h),
                angle, tint, new Vector4(ShapeSprite, 0f, flash, 0f), SpriteUv(sprite), float.IsNaN(sortY) ? center.y : sortY);
        }

        // Any part of a texture (uv = x, y, width, height in 0..1) as a rotated quad: e.g. one piece of a shattered sprite.
        // A negative size.y flips it vertically.
        public void TextureRotated(RenderLayer layer, Texture texture, Vector4 uv, Vector2 center, Vector2 size, float angle,
            Color tint, float flash = 0f)
        {
            if (!IsVisible(center, Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f)) return;
            AddRotated(GetTexturedGroup(layers[(int)layer], texture), center, size, angle, tint,
                new Vector4(ShapeSprite, 0f, flash, 0f), uv, center.y);
        }

        // Fast path for drawing many copies of one sprite (gems, death pieces...): texture group, uv and aspect are
        // looked up once here instead of once per copy. Get a brush, then call Draw/DrawRotated in the loop.
        public readonly struct SpriteBrush
        {
            internal readonly Group Group;
            public readonly Vector4 Uv;
            public readonly float Aspect; // width / height
            public readonly Vector4 Trim; // where the visible (uv) part sits in the full image, see SpriteTrim
            internal SpriteBrush(Group group, Vector4 uv, float aspect, Vector4 trim)
            {
                Group = group;
                Uv = uv;
                Aspect = aspect;
                Trim = trim;
            }
            public bool IsValid => Group != null;
        }

        public SpriteBrush Brush(RenderLayer layer, Sprite sprite)
        {
            if (sprite == null) return default;
            var rect = sprite.rect;
            return new SpriteBrush(GetTexturedGroup(layers[(int)layer], sprite.texture), SpriteUv(sprite), rect.width / rect.height,
                SpriteTrim(sprite));
        }

        // Brush for any part of a texture (uv set per draw call).
        public SpriteBrush Brush(RenderLayer layer, Texture texture) =>
            texture == null ? default : new SpriteBrush(GetTexturedGroup(layers[(int)layer], texture), FullUv, 1f, new Vector4(0f, 0f, 1f, 1f));

        public void Draw(in SpriteBrush brush, Vector2 center, float height, Color tint, float flash = 0f, float sortY = float.NaN)
        {
            var width = height * brush.Aspect;
            var t = brush.Trim;
            Add(brush.Group, center + new Vector2(t.x * width, t.y * height), new Vector2(width * t.z, height * t.w), tint,
                new Vector4(ShapeSprite, 0f, flash, 0f), brush.Uv, float.IsNaN(sortY) ? center.y : sortY);
        }

        // Unrotated quad with its own uv (e.g. one digit of a damage number).
        public void Draw(in SpriteBrush brush, Vector4 uv, Vector2 center, Vector2 size, Color tint) =>
            Add(brush.Group, center, size, tint, new Vector4(ShapeSprite, 0f, 0f, 0f), uv, center.y);

        // Rotated quad with its own uv (e.g. one piece of a shattered sprite). Negative size.y flips it vertically.
        public void DrawRotated(in SpriteBrush brush, Vector4 uv, Vector2 center, Vector2 size, float angle, Color tint,
            float flash = 0f)
        {
            if (!IsVisible(center, Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f)) return;
            AddRotated(brush.Group, center, size, angle, tint, new Vector4(ShapeSprite, 0f, flash, 0f), uv, center.y);
        }

        // Stretched, rotated soft ellipse: bullet tracers, motion streaks.
        public void Streak(RenderLayer layer, Vector2 center, float length, float width, float angle, Color color)
        {
            if (!IsVisible(center, length * 0.5f)) return;
            AddRotated(layers[(int)layer].Shapes, center, new Vector2(length, width), angle, color,
                new Vector4(ShapeCircle, 0f, 0f, 0f), FullUv, center.y);
        }

        // Pie slice: center, outer radius, inner radius as a fraction of it (0 = solid to the center), facing
        // `angle` (radians), spanning `halfAngle` to each side (radians, up to PI = full circle).
        public void Sector(RenderLayer layer, Vector2 center, float radius, float inner, float angle, float halfAngle, Color color)
        {
            if (!IsVisible(center, radius)) return;
            AddRotated(layers[(int)layer].Shapes, center, new Vector2(radius * 2f, radius * 2f), angle, color,
                new Vector4(ShapeSector, Mathf.Clamp01(inner), 0f, Mathf.Clamp(halfAngle, 0f, Mathf.PI)), FullUv, center.y);
        }

        // Straight line from a to b (debug outlines).
        public void Line(RenderLayer layer, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            var length = d.magnitude;
            if (length < 0.0001f) return;
            var center = (a + b) * 0.5f;
            if (!IsVisible(center, length * 0.5f)) return;
            AddRotated(layers[(int)layer].Shapes, center, new Vector2(length, width), Mathf.Atan2(d.y, d.x), color,
                new Vector4(ShapeRect, 0f, 0f, 0f), FullUv, center.y);
        }

        // Draws a sub-rectangle of a texture (used by the damage-number digit font).
        public void TextureRect(RenderLayer layer, Texture texture, Vector4 uv, Vector2 center, Vector2 size, Color tint) =>
            Add(GetTexturedGroup(layers[(int)layer], texture), center, size, tint,
                new Vector4(ShapeSprite, 0f, 0f, 0f), uv, center.y);

        // Sprite bounds inside its texture. Works for rect- and tight-packed atlas sprites.
        public Vector4 SpriteUv(Sprite sprite)
        {
            if (spriteUvs.TryGetValue(sprite, out var uv)) return uv;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in sprite.uv)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            uv = new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
            spriteUvs[sprite] = uv;
            return uv;
        }

        Group GetTexturedGroup(Layer layer, Texture texture)
        {
            if (layer.Textured.TryGetValue(texture, out var group)) return group;
            group = new Group { Material = CreateMaterial(baseMaterial, texture, layer.RenderQueue) };
            layer.Textured[texture] = group;
            layer.TexturedOrder.Add(group);
            return group;
        }

        void AddRotated(Group group, Vector2 center, Vector2 size, float angle, Color color, Vector4 shape, Vector4 uv, float key)
        {
            var batchIndex = group.Used / BatchSize;
            if (batchIndex == group.Batches.Count) group.Batches.Add(new Batch());
            var batch = group.Batches[batchIndex];
            var i = group.Used % BatchSize;
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);

            ref var m = ref batch.Matrices[i];
            m = Matrix4x4.identity;
            m.m00 = c * size.x;
            m.m01 = -s * size.y;
            m.m10 = s * size.x;
            m.m11 = c * size.y;
            m.m03 = center.x;
            m.m13 = center.y;

            batch.Colors[i] = color;
            batch.Shapes[i] = shape;
            batch.Uvs[i] = uv;
            batch.Keys[i] = key;
            group.Used++;
        }

        // Bulk path for Burst-generated instance data: copies whole blocks into the GPU batches (memcpy),
        // no per-instance C# work. texture = null draws with the shape material. keys = sortY per instance (World layer).
        public void AddBulk(RenderLayer layerId, Texture texture, NativeArray<Matrix4x4> matrices, NativeArray<Vector4> colors,
            NativeArray<Vector4> shapes, NativeArray<Vector4> uvs, int count, NativeArray<float> keys = default)
        {
            var layer = layers[(int)layerId];
            var group = texture == null ? layer.Shapes : GetTexturedGroup(layer, texture);
            var copied = 0;
            while (copied < count)
            {
                var batchIndex = group.Used / BatchSize;
                if (batchIndex == group.Batches.Count) group.Batches.Add(new Batch());
                var batch = group.Batches[batchIndex];
                var offset = group.Used % BatchSize;
                var n = Mathf.Min(BatchSize - offset, count - copied);
                NativeArray<Matrix4x4>.Copy(matrices, copied, batch.Matrices, offset, n);
                NativeArray<Vector4>.Copy(colors, copied, batch.Colors, offset, n);
                NativeArray<Vector4>.Copy(shapes, copied, batch.Shapes, offset, n);
                NativeArray<Vector4>.Copy(uvs, copied, batch.Uvs, offset, n);
                if (keys.IsCreated) NativeArray<float>.Copy(keys, copied, batch.Keys, offset, n);
                group.Used += n;
                copied += n;
            }
        }

        void Add(Group group, Vector2 center, Vector2 size, Color color, Vector4 shape, Vector4 uv, float key)
        {
            if (!IsVisible(center, Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f)) return;
            var batchIndex = group.Used / BatchSize;
            if (batchIndex == group.Batches.Count) group.Batches.Add(new Batch());
            var batch = group.Batches[batchIndex];
            var i = group.Used % BatchSize;

            ref var m = ref batch.Matrices[i];
            m = Matrix4x4.identity;
            m.m00 = size.x;
            m.m11 = size.y;
            m.m03 = center.x;
            m.m13 = center.y;

            batch.Colors[i] = color;
            batch.Shapes[i] = shape;
            batch.Uvs[i] = uv;
            batch.Keys[i] = key;
            group.Used++;
        }

        // Tiles this texture over the endless ground (null = debug checkerboard).
        public void SetGroundTexture(Texture texture, float tileSize)
        {
            if (texture == null) return;
            texture.wrapMode = TextureWrapMode.Repeat;
            groundMaterial.mainTexture = texture;
            groundMaterial.SetFloat("_UseTexture", 1f);
            groundMaterial.SetFloat("_TileSize", tileSize);
        }

        // Ground color: tint (multiplies the tile) and saturation (0 = gray, 1 = original).
        public void SetGroundLook(Color tint, float saturation)
        {
            groundMaterial.SetColor("_Tint", tint);
            groundMaterial.SetFloat("_Saturation", saturation);
        }

        public void DrawGround(Camera camera)
        {
            var h = camera.orthographicSize * 2f + 2f;
            var w = h * camera.aspect + 2f;
            var pos = camera.transform.position;
            var matrix = Matrix4x4.TRS(new Vector3(pos.x, pos.y, 1f), Quaternion.identity, new Vector3(w, h, 1f));
            Graphics.DrawMesh(quad, matrix, groundMaterial, 0);
        }

        // Sends everything collected this frame to the GPU, then clears for the next frame.
        public void Flush()
        {
            var instances = 0;
            var drawCalls = 0;
            foreach (var layer in layers)
            {
                if (layer.Sorted)
                {
                    SortGroup(layer.Shapes);
                    foreach (var group in layer.TexturedOrder) SortGroup(group);
                }
                FlushGroup(layer.Shapes, ref instances, ref drawCalls);
                foreach (var group in layer.TexturedOrder) FlushGroup(group, ref instances, ref drawCalls);
            }
            LastFrameInstances = instances;
            LastFrameDrawCalls = drawCalls;
        }

        // Reorders a group back-to-front by its keys (higher sortY = farther away = drawn first).
        // Instances inside one instanced draw call are rendered in buffer order, so this fixes overlap.
        void SortGroup(Group group)
        {
            var n = group.Used;
            if (n < 2) return;
            if (!sortItems.IsCreated || sortItems.Length < n)
            {
                if (sortItems.IsCreated) sortItems.Dispose();
                sortItems = new NativeArray<SortItem>(Mathf.NextPowerOfTwo(n), Allocator.Persistent);
            }

            for (var i = 0; i < n; i++)
                sortItems[i] = new SortItem { Key = -group.Batches[i / BatchSize].Keys[i % BatchSize], Index = i };
            new SortJob { Items = sortItems.GetSubArray(0, n) }.Run();

            while (group.Scratch.Count < group.Batches.Count) group.Scratch.Add(new Batch());
            for (var i = 0; i < n; i++)
            {
                var src = sortItems[i].Index;
                var from = group.Batches[src / BatchSize];
                var to = group.Scratch[i / BatchSize];
                int s = src % BatchSize, d = i % BatchSize;
                to.Matrices[d] = from.Matrices[s];
                to.Colors[d] = from.Colors[s];
                to.Shapes[d] = from.Shapes[s];
                to.Uvs[d] = from.Uvs[s];
            }
            (group.Batches, group.Scratch) = (group.Scratch, group.Batches);
        }

        struct SortItem : IComparable<SortItem>
        {
            public float Key;
            public int Index;

            public int CompareTo(SortItem other)
            {
                var c = Key.CompareTo(other.Key);
                return c != 0 ? c : Index.CompareTo(other.Index); // stable for equal depth
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SortJob : IJob
        {
            public NativeArray<SortItem> Items;
            public void Execute() => Items.Sort();
        }

        void FlushGroup(Group group, ref int instances, ref int drawCalls)
        {
            for (var start = 0; start < group.Used; start += BatchSize)
            {
                var batch = group.Batches[start / BatchSize];
                var n = Mathf.Min(BatchSize, group.Used - start);
                block.Clear();
                block.SetVectorArray(ColorId, batch.Colors);
                block.SetVectorArray(ShapeId, batch.Shapes);
                block.SetVectorArray(UvId, batch.Uvs);
                Graphics.DrawMeshInstanced(quad, 0, group.Material, batch.Matrices, n, block, ShadowCastingMode.Off, false);
                drawCalls++;
            }
            instances += group.Used;
            group.Used = 0;
        }

        Material CreateMaterial(Material source, Texture texture, int renderQueue)
        {
            var material = new Material(source) { enableInstancing = true, renderQueue = renderQueue };
            if (texture != null) material.mainTexture = texture;
            createdMaterials.Add(material);
            return material;
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        public void Dispose()
        {
            foreach (var material in createdMaterials) UnityEngine.Object.Destroy(material);
            UnityEngine.Object.Destroy(quad);
            if (sortItems.IsCreated) sortItems.Dispose();
        }
    }
}
