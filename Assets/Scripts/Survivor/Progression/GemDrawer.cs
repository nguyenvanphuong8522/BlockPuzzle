using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Survivor
{
    // Builds the GPU instance data for every experience gem in one Burst job (like EnemyDrawer does for enemies),
    // then hands it to the renderer as whole blocks. Thousands of gems cost a few microseconds.
    // Owned by GemSystem, which copies its positions / values in each frame.
    public sealed class GemDrawer : IDisposable
    {
        NativeArray<float2> positions;
        NativeArray<int> values;
        NativeArray<Matrix4x4> matrices;
        NativeArray<Vector4> colors, shapes, uvs;
        NativeArray<int> written;

        public GemDrawer(int capacity)
        {
            written = new NativeArray<int>(1, Allocator.Persistent);
            Allocate(capacity);
        }

        // pos / value are GemSystem's managed arrays; only the first `count` entries are used.
        public void Draw(InstancedRenderer renderer, Sprite sprite, Vector2[] pos, int[] value, int count)
        {
            if (count == 0) return;
            if (positions.Length < count) Allocate(Mathf.NextPowerOfTwo(count));

            // Managed -> native copy of just the live gems (a memcpy).
            NativeArray<Vector2>.Copy(pos, 0, positions.Reinterpret<Vector2>(), 0, count);
            NativeArray<int>.Copy(value, 0, values, 0, count);

            var hasSprite = sprite != null;
            var aspect = 1f;
            var uv = new Vector4(0f, 0f, 1f, 1f);
            if (hasSprite)
            {
                var rect = sprite.rect;
                aspect = rect.width / rect.height;
                uv = renderer.SpriteUv(sprite);
            }

            new DrawJob
            {
                Pos = positions, Value = values, Count = count, View = renderer.ViewRect,
                Sprite = hasSprite, Aspect = aspect, Uv = uv,
                Matrices = matrices.Reinterpret<float4x4>(), Colors = colors.Reinterpret<float4>(),
                Shapes = shapes.Reinterpret<float4>(), Uvs = uvs.Reinterpret<float4>(), Written = written,
            }.Run();

            renderer.AddBulk(RenderLayer.Gems, hasSprite ? sprite.texture : null, matrices, colors, shapes, uvs, written[0]);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        struct DrawJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Pos;
            [ReadOnly] public NativeArray<int> Value;
            public int Count;
            public float4 View;          // minX, minY, maxX, maxY
            public bool Sprite;          // false = draw colored circles (no gem art)
            public float Aspect;
            public float4 Uv;

            public NativeArray<float4x4> Matrices;
            public NativeArray<float4> Colors, Shapes, Uvs;
            public NativeArray<int> Written;

            const float ShapeCircle = 0f, ShapeSprite = 3f;

            public void Execute()
            {
                // Same look as before: value 5+ = big red, 2+ = green, 1 = blue.
                var big = new float4(1f, 0.35f, 0.35f, 1f);
                var mid = new float4(0.4f, 1f, 0.5f, 1f);
                var small = new float4(0.4f, 0.75f, 1f, 1f);
                var n = 0;
                for (var i = 0; i < Count; i++)
                {
                    var p = Pos[i];
                    if (p.x < View.x - 0.5f || p.x > View.z + 0.5f || p.y < View.y - 0.5f || p.y > View.w + 0.5f) continue;

                    var v = Value[i];
                    var radius = v >= 5 ? 0.2f : 0.14f;
                    float2 size;
                    if (Sprite)
                    {
                        var height = radius * 2.6f;
                        size = new float2(height * Aspect, height);
                    }
                    else
                    {
                        size = new float2(radius * 2f, radius * 2f);
                    }

                    Matrices[n] = new float4x4(
                        new float4(size.x, 0f, 0f, 0f),
                        new float4(0f, size.y, 0f, 0f),
                        new float4(0f, 0f, 1f, 0f),
                        new float4(p.x, p.y, 0f, 1f));
                    Colors[n] = v >= 5 ? big : v >= 2 ? mid : small;
                    Shapes[n] = new float4(Sprite ? ShapeSprite : ShapeCircle, 0f, 0f, 0f);
                    Uvs[n] = Sprite ? Uv : new float4(0f, 0f, 1f, 1f);
                    n++;
                }
                Written[0] = n;
            }
        }

        void Allocate(int size)
        {
            DisposeBuffers();
            positions = new NativeArray<float2>(size, Allocator.Persistent);
            values = new NativeArray<int>(size, Allocator.Persistent);
            matrices = new NativeArray<Matrix4x4>(size, Allocator.Persistent);
            colors = new NativeArray<Vector4>(size, Allocator.Persistent);
            shapes = new NativeArray<Vector4>(size, Allocator.Persistent);
            uvs = new NativeArray<Vector4>(size, Allocator.Persistent);
        }

        void DisposeBuffers()
        {
            if (positions.IsCreated) positions.Dispose();
            if (values.IsCreated) values.Dispose();
            if (matrices.IsCreated) matrices.Dispose();
            if (colors.IsCreated) colors.Dispose();
            if (shapes.IsCreated) shapes.Dispose();
            if (uvs.IsCreated) uvs.Dispose();
        }

        public void Dispose()
        {
            DisposeBuffers();
            if (written.IsCreated) written.Dispose();
        }
    }
}
