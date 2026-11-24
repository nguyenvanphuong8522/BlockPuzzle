using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Survivor
{
    // Builds the per-instance GPU data for all enemies (shadow + sprite or circle) in Burst jobs,
    // then hands it to the renderer as whole blocks. Thousands of enemies cost well under a millisecond.
    public sealed class EnemyDrawer : IDisposable
    {
        readonly GameConfig config;
        readonly SpriteCatalog sprites;

        // Lookup tables: frames of all enemy types, flattened.
        SpriteAnim[] tableSource;
        bool tablesBuilt;
        readonly List<Texture> textures = new List<Texture>();
        NativeArray<float4> frameUv;
        NativeArray<float> frameAspect;
        NativeArray<float4> frameTrim;
        NativeArray<int> frameTexture;
        NativeArray<int> typeFrameStart;
        NativeArray<int> typeFrameCount;
        NativeArray<float> typeFps;
        NativeArray<float4> typeColor;

        // Per-frame inputs / outputs
        NativeArray<int> typeNative;
        NativeArray<int> idNative;
        NativeArray<Matrix4x4> matrices, shadowMatrices;
        NativeArray<Vector4> colors, shapes, uvs, shadowColors, shadowShapes, shadowUvs;
        NativeArray<float> keys; // depth (feet y) per instance for the sorted World layer
        NativeArray<int> counts; // [0] = instances written, [1] = shadows written

        public EnemyDrawer(GameConfig config, SpriteCatalog sprites)
        {
            this.config = config;
            this.sprites = sprites;
            counts = new NativeArray<int>(2, Allocator.Persistent);
            Allocate(1024);
        }

        public void Draw(InstancedRenderer renderer, NativeArray<Vector2> position, NativeArray<float> radius,
            NativeArray<float> flash, int[] type, int[] id, int count, float playerX)
        {
            if (count == 0) return;
            if (matrices.Length < count) Allocate(Mathf.NextPowerOfTwo(count));
            if (!tablesBuilt || tableSource != sprites.Enemies) BuildTables(renderer);

            NativeArray<int>.Copy(type, typeNative, count);
            NativeArray<int>.Copy(id, idNative, count);

            // One pass for shadows + plain circles (shape material), one pass per sprite texture
            // (normally just one: all enemy sprites live in the same atlas).
            for (var pass = -1; pass < textures.Count; pass++)
            {
                new DrawJob
                {
                    Pos = position.Reinterpret<float2>(), Radius = radius, Flash = flash, Type = typeNative, Id = idNative,
                    FrameUv = frameUv, FrameAspect = frameAspect, FrameTrim = frameTrim, FrameTexture = frameTexture,
                    TypeFrameStart = typeFrameStart, TypeFrameCount = typeFrameCount, TypeFps = typeFps, TypeColor = typeColor,
                    Count = count, View = renderer.ViewRect, Time = Time.time, SpriteScale = sprites.SpriteScale,
                    PlayerX = playerX, Texture = pass,
                    Matrices = matrices.Reinterpret<float4x4>(), Colors = colors.Reinterpret<float4>(),
                    Shapes = shapes.Reinterpret<float4>(), Uvs = uvs.Reinterpret<float4>(), Keys = keys,
                    ShadowMatrices = shadowMatrices.Reinterpret<float4x4>(), ShadowColors = shadowColors.Reinterpret<float4>(),
                    ShadowShapes = shadowShapes.Reinterpret<float4>(), ShadowUvs = shadowUvs.Reinterpret<float4>(),
                    Counts = counts,
                }.Run();

                var texture = pass < 0 ? null : textures[pass];
                if (pass < 0) renderer.AddBulk(RenderLayer.Shadows, null, shadowMatrices, shadowColors, shadowShapes, shadowUvs, counts[1]);
                renderer.AddBulk(RenderLayer.World, texture, matrices, colors, shapes, uvs, counts[0], keys);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        struct DrawJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Pos;
            [ReadOnly] public NativeArray<float> Radius;
            [ReadOnly] public NativeArray<float> Flash;
            [ReadOnly] public NativeArray<int> Type;
            [ReadOnly] public NativeArray<int> Id;
            [ReadOnly] public NativeArray<float4> FrameUv;
            [ReadOnly] public NativeArray<float> FrameAspect;
            [ReadOnly] public NativeArray<float4> FrameTrim; // visible part inside the full image (atlas trims borders)
            [ReadOnly] public NativeArray<int> FrameTexture;
            [ReadOnly] public NativeArray<int> TypeFrameStart;
            [ReadOnly] public NativeArray<int> TypeFrameCount;
            [ReadOnly] public NativeArray<float> TypeFps;
            [ReadOnly] public NativeArray<float4> TypeColor;

            public int Count;
            public float4 View; // minX, minY, maxX, maxY
            public float Time, SpriteScale, PlayerX;
            public int Texture; // -1 = shapes pass (shadows and plain circles), otherwise texture index

            public NativeArray<float4x4> Matrices;
            public NativeArray<float4> Colors, Shapes, Uvs;
            public NativeArray<float> Keys;
            public NativeArray<float4x4> ShadowMatrices;
            public NativeArray<float4> ShadowColors, ShadowShapes, ShadowUvs;
            public NativeArray<int> Counts;

            public void Execute()
            {
                int n = 0, s = 0;
                var fullUv = new float4(0f, 0f, 1f, 1f);
                for (var i = 0; i < Count; i++)
                {
                    var p = Pos[i];
                    var r = Radius[i];
                    var e = r * 3f;
                    if (p.x + e < View.x || p.x - e > View.z || p.y + e < View.y || p.y - e > View.w) continue;

                    var t = Type[i];
                    var frames = TypeFrameCount[t];
                    var flash = Flash[i] > 0f ? 1f : 0f;

                    if (Texture < 0)
                    {
                        ShadowMatrices[s] = Quad(new float2(p.x, p.y - r * 0.85f), new float2(r * 1.9f, r * 0.6f));
                        ShadowColors[s] = new float4(0f, 0f, 0f, 0.22f);
                        ShadowShapes[s] = float4.zero;
                        ShadowUvs[s] = fullUv;
                        s++;

                        if (frames > 0) continue;
                        Matrices[n] = Quad(p, new float2(r * 2f, r * 2f)); // no sprite for this type: colored circle
                        Colors[n] = TypeColor[t];
                        Shapes[n] = new float4(0f, 0f, flash, 0f);
                        Uvs[n] = fullUv;
                        Keys[n] = p.y - r;
                        n++;
                        continue;
                    }

                    if (frames == 0) continue;
                    // Offset by id so a crowd does not animate in lockstep.
                    var f = ((int)(Time * TypeFps[t]) + Id[i] % 7) % frames;
                    var frame = TypeFrameStart[t] + f;
                    if (FrameTexture[frame] != Texture) continue;

                    var height = r * 2f * SpriteScale;
                    var width = height * FrameAspect[frame];
                    if (p.x > PlayerX) width = -width; // sprites face right: flip to face the player
                    var trim = FrameTrim[frame];
                    Matrices[n] = Quad(new float2(p.x + trim.x * width, p.y + r * 0.3f + trim.y * height),
                        new float2(width * trim.z, height * trim.w));
                    Colors[n] = new float4(1f, 1f, 1f, 1f);
                    Shapes[n] = new float4(3f, 0f, flash, 0f);
                    Uvs[n] = FrameUv[frame];
                    Keys[n] = p.y - r; // feet: lower on screen = drawn later = in front
                    n++;
                }
                Counts[0] = n;
                Counts[1] = s;
            }

            static float4x4 Quad(float2 center, float2 size) => new float4x4(
                new float4(size.x, 0f, 0f, 0f),
                new float4(0f, size.y, 0f, 0f),
                new float4(0f, 0f, 1f, 0f),
                new float4(center.x, center.y, 0f, 1f));
        }

        // Flattens SpriteCatalog.Enemies into arrays the job can read. Rebuilt if the catalog changes.
        void BuildTables(InstancedRenderer renderer)
        {
            tableSource = sprites.Enemies;
            tablesBuilt = true;
            textures.Clear();
            var uvList = new List<float4>();
            var aspectList = new List<float>();
            var trimList = new List<float4>();
            var texList = new List<int>();
            var typeCount = config.Enemies.Length;
            var start = new int[typeCount];
            var frameCount = new int[typeCount];
            var fps = new float[typeCount];
            var color = new float4[typeCount];

            for (var t = 0; t < typeCount; t++)
            {
                var c = config.Enemies[t].Color;
                color[t] = new float4(c.r, c.g, c.b, c.a);
                start[t] = uvList.Count;
                var anim = sprites.Enemy(t);
                if (anim == null) continue;
                fps[t] = anim.Fps;
                foreach (var sprite in anim.Frames)
                {
                    if (sprite == null) continue;
                    var texIndex = textures.IndexOf(sprite.texture);
                    if (texIndex < 0)
                    {
                        texIndex = textures.Count;
                        textures.Add(sprite.texture);
                    }
                    var uv = renderer.SpriteUv(sprite);
                    uvList.Add(new float4(uv.x, uv.y, uv.z, uv.w));
                    aspectList.Add(sprite.rect.width / sprite.rect.height);
                    var trim = renderer.SpriteTrim(sprite);
                    trimList.Add(new float4(trim.x, trim.y, trim.z, trim.w));
                    texList.Add(texIndex);
                    frameCount[t]++;
                }
            }

            DisposeTables();
            frameUv = new NativeArray<float4>(Math.Max(1, uvList.Count), Allocator.Persistent);
            frameAspect = new NativeArray<float>(Math.Max(1, uvList.Count), Allocator.Persistent);
            frameTrim = new NativeArray<float4>(Math.Max(1, uvList.Count), Allocator.Persistent);
            frameTexture = new NativeArray<int>(Math.Max(1, uvList.Count), Allocator.Persistent);
            for (var i = 0; i < uvList.Count; i++)
            {
                frameUv[i] = uvList[i];
                frameAspect[i] = aspectList[i];
                frameTrim[i] = trimList[i];
                frameTexture[i] = texList[i];
            }
            typeFrameStart = new NativeArray<int>(start, Allocator.Persistent);
            typeFrameCount = new NativeArray<int>(frameCount, Allocator.Persistent);
            typeFps = new NativeArray<float>(fps, Allocator.Persistent);
            typeColor = new NativeArray<float4>(color, Allocator.Persistent);
        }

        void Allocate(int size)
        {
            DisposeBuffers();
            typeNative = new NativeArray<int>(size, Allocator.Persistent);
            idNative = new NativeArray<int>(size, Allocator.Persistent);
            matrices = new NativeArray<Matrix4x4>(size, Allocator.Persistent);
            colors = new NativeArray<Vector4>(size, Allocator.Persistent);
            shapes = new NativeArray<Vector4>(size, Allocator.Persistent);
            uvs = new NativeArray<Vector4>(size, Allocator.Persistent);
            keys = new NativeArray<float>(size, Allocator.Persistent);
            shadowMatrices = new NativeArray<Matrix4x4>(size, Allocator.Persistent);
            shadowColors = new NativeArray<Vector4>(size, Allocator.Persistent);
            shadowShapes = new NativeArray<Vector4>(size, Allocator.Persistent);
            shadowUvs = new NativeArray<Vector4>(size, Allocator.Persistent);
        }

        void DisposeBuffers()
        {
            if (!typeNative.IsCreated) return;
            typeNative.Dispose();
            idNative.Dispose();
            matrices.Dispose();
            colors.Dispose();
            shapes.Dispose();
            uvs.Dispose();
            keys.Dispose();
            shadowMatrices.Dispose();
            shadowColors.Dispose();
            shadowShapes.Dispose();
            shadowUvs.Dispose();
        }

        void DisposeTables()
        {
            if (!frameUv.IsCreated) return;
            frameUv.Dispose();
            frameAspect.Dispose();
            frameTrim.Dispose();
            frameTexture.Dispose();
            typeFrameStart.Dispose();
            typeFrameCount.Dispose();
            typeFps.Dispose();
            typeColor.Dispose();
        }

        public void Dispose()
        {
            DisposeBuffers();
            DisposeTables();
            counts.Dispose();
        }
    }
}
