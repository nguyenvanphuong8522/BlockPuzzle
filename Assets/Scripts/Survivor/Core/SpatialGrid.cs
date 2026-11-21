using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Survivor
{
    // Spatial hash rebuilt every frame with a counting sort (Burst job): no allocations, no physics engine.
    // Lets thousands of enemies find their neighbours by looking only at nearby cells.
    // Items of one cell are Items[BucketStart[b] .. BucketStart[b + 1]).
    public sealed class SpatialGrid : IDisposable
    {
        public const int TableSize = 8192; // must be a power of two
        const int Mask = TableSize - 1;
        const int MaxCellsPerQuery = 256;

        public readonly float InverseCellSize;
        public NativeArray<int> BucketStart;
        public NativeArray<int> Items;

        NativeArray<int> cursor;
        NativeArray<int> itemBucket;
        readonly List<int> visitedBuckets = new List<int>(64);
        int count;

        public SpatialGrid(float cellSize)
        {
            InverseCellSize = 1f / cellSize;
            BucketStart = new NativeArray<int>(TableSize + 1, Allocator.Persistent);
            cursor = new NativeArray<int>(TableSize, Allocator.Persistent);
            Items = new NativeArray<int>(1024, Allocator.Persistent);
            itemBucket = new NativeArray<int>(1024, Allocator.Persistent);
        }

        public static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & Mask;

        public static int Cell(float v, float inverseCellSize) => (int)math.floor(v * inverseCellSize);

        public JobHandle ScheduleBuild(NativeArray<float2> positions, int itemCount, JobHandle dependency)
        {
            count = itemCount;
            if (Items.Length < itemCount)
            {
                var size = Mathf.NextPowerOfTwo(itemCount);
                Items.Dispose();
                itemBucket.Dispose();
                Items = new NativeArray<int>(size, Allocator.Persistent);
                itemBucket = new NativeArray<int>(size, Allocator.Persistent);
            }
            return new BuildJob
            {
                Positions = positions, Count = itemCount, InverseCellSize = InverseCellSize,
                BucketStart = BucketStart, Cursor = cursor, Items = Items, ItemBucket = itemBucket
            }.Schedule(dependency);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct BuildJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Positions;
            public int Count;
            public float InverseCellSize;
            public NativeArray<int> BucketStart;
            public NativeArray<int> Cursor;
            public NativeArray<int> Items;
            public NativeArray<int> ItemBucket;

            public void Execute()
            {
                for (var b = 0; b < TableSize; b++) Cursor[b] = 0;
                for (var i = 0; i < Count; i++)
                {
                    var p = Positions[i];
                    var b = Hash(Cell(p.x, InverseCellSize), Cell(p.y, InverseCellSize));
                    ItemBucket[i] = b;
                    Cursor[b] = Cursor[b] + 1;
                }

                var start = 0;
                for (var b = 0; b < TableSize; b++)
                {
                    BucketStart[b] = start;
                    start += Cursor[b];
                    Cursor[b] = BucketStart[b];
                }
                BucketStart[TableSize] = start;

                for (var i = 0; i < Count; i++)
                {
                    var b = ItemBucket[i];
                    Items[Cursor[b]] = i;
                    Cursor[b] = Cursor[b] + 1;
                }
            }
        }

        // Main thread, after the build job completed: adds candidate indices near the circle to results.
        // Callers still check the exact distance.
        public void Query(Vector2 center, float radius, List<int> results)
        {
            results.Clear();
            int x0 = Cell(center.x - radius, InverseCellSize), x1 = Cell(center.x + radius, InverseCellSize);
            int y0 = Cell(center.y - radius, InverseCellSize), y1 = Cell(center.y + radius, InverseCellSize);

            if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxCellsPerQuery)
            {
                for (var i = 0; i < count; i++) results.Add(i);
                return;
            }

            visitedBuckets.Clear();
            for (var x = x0; x <= x1; x++)
            for (var y = y0; y <= y1; y++)
            {
                var b = Hash(x, y);
                if (visitedBuckets.Contains(b)) continue; // two cells can share a bucket
                visitedBuckets.Add(b);
                for (var k = BucketStart[b]; k < BucketStart[b + 1]; k++) results.Add(Items[k]);
            }
        }

        public void Dispose()
        {
            BucketStart.Dispose();
            cursor.Dispose();
            Items.Dispose();
            itemBucket.Dispose();
        }
    }
}
