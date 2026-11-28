using System;
using System.Collections.Generic;
using MessagePipe;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Survivor
{
    // All enemies live in parallel arrays (structure of arrays): no GameObject, Collider or Rigidbody.
    // Index i in every array is the same enemy. Removal swaps the last enemy into the hole.
    //
    // Per frame:
    //   1. Burst job builds the spatial grid.
    //   2. Burst parallel job moves every enemy (chase + separation + knockback), using all CPU cores.
    //      Enemies outside the screen update every other frame and skip separation.
    //   3. Burst job: hit flash, strongest contact damage, boss lookup. Main thread only runs the boss ability.
    // On WebGL the jobs simply run on the main thread with the same results.
    public sealed class EnemySystem : IDisposable
    {
        public const float MaxRadius = 1.3f;
        const int MaxNeighborScan = 24;
        const int MaxOverlaps = 6;
        const int InitialCapacity = 1024;

        public int Count { get; private set; }

        // Read by weapons and projectiles (main thread, while no job is running).
        public NativeArray<Vector2> Position;
        public NativeArray<float> Hp;
        public NativeArray<float> Radius;
        public int[] Id = new int[InitialCapacity]; // unique per spawn, survives index swaps

        // Job data
        NativeArray<Vector2> positionNext;
        NativeArray<Vector2> knockback;
        NativeArray<Vector2> knockbackNext;
        NativeArray<float> moveSpeed; // base speed x current multiplier (bosses charge)
        NativeArray<float> flash;     // hit flash timer
        NativeArray<float> squash;    // hit squash timer (only when the hit did not kill)
        NativeArray<float> damage;
        NativeArray<byte> isBoss;
        NativeArray<float> contactDamage; // [0] = strongest enemy touching the player this frame
        NativeArray<int> bossFound;       // [0] = index of a living boss, or -1
        NativeArray<Vector2> fly;         // flyer (swarm): fixed velocity; zero = normal enemy chasing the hero
        NativeArray<float> keep;          // ranged enemy: distance it keeps from the hero (sign = circling side); 0 = melee
        NativeArray<byte> elite;          // 1 = elite (EliteSystem), drawn tinted

        // Main-thread-only data
        float[] maxHp = new float[InitialCapacity];
        float[] speed = new float[InitialCapacity];
        int[] xp = new int[InitialCapacity];
        int[] type = new int[InitialCapacity];
        float[] flyTime = new float[InitialCapacity];   // flyers: seconds left before they leave the map
        int flyerCount;

        // Ranged enemies: seconds until the next shot (EnemyBulletSystem counts it down and fires).
        public float[] ShootTimer = new float[InitialCapacity];
        public bool IsRanged(int i) => keep[i] != 0f;
        // Elite: a bigger, tougher copy of a normal enemy (EliteSystem). Tinted and with its own health bar.
        public bool IsElite(int i) => elite[i] != 0;
        public int TypeOf(int i) => type[i];
        public float MaxHp(int i) => maxHp[i];

        readonly GameConfig config;
        readonly Player player;
        readonly IPublisher<EnemyKilledEvent> killedPublisher;
        readonly EnemyDeathSystem deaths;
        readonly DamageNumberSystem damageNumbers;
        readonly BloodSystem blood;
        readonly BossRoster bosses;    // the boss's hit flash rule (its Settings)
        readonly ChapterService chapters; // chapter difficulty: every enemy's HP x Enemy Hp Scale
        readonly EnemyDrawer drawer;
        readonly AudioService audio;
        readonly EnemySettings tuning; // ranged / elite look / squash / knockback rules
        readonly SpatialGrid grid = new SpatialGrid(1f);
        readonly List<int> candidates = new List<int>(128);
        readonly List<int> dead = new List<int>(128);
        int nextId = 1;
        int frame;

        // Current boss, for the HUD health bar. BossIndex is -1 when no boss is alive.
        public int BossIndex { get; private set; } = -1;
        // Multiplies the boss walking speed (1 = normal, 0 = stands still). Set by BossSkillSystem.
        public float BossSpeedScale { get; set; } = 1f;
        public float BossHpRatio => BossIndex >= 0 ? Mathf.Clamp01(Hp[BossIndex] / maxHp[BossIndex]) : 0f;
        public string BossName => BossIndex >= 0 ? NameOf(type[BossIndex]) : "";

        // Translated enemy name (Localization.csv "enemy.<Id>"), the code name when there is none.
        public string NameOf(int typeIndex) => Loc.Get("enemy." + config.Enemies[typeIndex].Id, config.Enemies[typeIndex].Name);

        public EnemySystem(GameConfig config, Player player, IPublisher<EnemyKilledEvent> killedPublisher,
            DamageNumberSystem damageNumbers, EnemyDrawer drawer, AudioService audio, EnemyDeathSystem deaths, EnemySettings tuning,
            BloodSystem blood, BossRoster bosses, ChapterService chapters)
        {
            this.tuning = tuning;
            this.deaths = deaths;
            this.config = config;
            this.player = player;
            this.killedPublisher = killedPublisher;
            this.damageNumbers = damageNumbers;
            this.blood = blood;
            this.bosses = bosses;
            this.chapters = chapters;
            this.drawer = drawer;
            this.audio = audio;

            Position = new NativeArray<Vector2>(InitialCapacity, Allocator.Persistent);
            Hp = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            Radius = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            positionNext = new NativeArray<Vector2>(InitialCapacity, Allocator.Persistent);
            knockback = new NativeArray<Vector2>(InitialCapacity, Allocator.Persistent);
            knockbackNext = new NativeArray<Vector2>(InitialCapacity, Allocator.Persistent);
            moveSpeed = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            flash = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            squash = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            damage = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            isBoss = new NativeArray<byte>(InitialCapacity, Allocator.Persistent);
            contactDamage = new NativeArray<float>(1, Allocator.Persistent);
            bossFound = new NativeArray<int>(1, Allocator.Persistent);
            fly = new NativeArray<Vector2>(InitialCapacity, Allocator.Persistent);
            keep = new NativeArray<float>(InitialCapacity, Allocator.Persistent);
            elite = new NativeArray<byte>(InitialCapacity, Allocator.Persistent);
        }

        public bool IsAlive(int i) => Hp[i] > 0f;

        public void Spawn(int typeIndex, Vector2 position, float hpMultiplier)
        {
            var def = config.Enemies[typeIndex];
            if (Count >= config.MaxEnemies && !def.IsBoss) return;
            if (Count == Position.Length) Grow();

            var i = Count++;
            Position[i] = position;
            Hp[i] = maxHp[i] = def.Hp * hpMultiplier * chapters.Current.EnemyHpScale;
            Radius[i] = def.Radius;
            Id[i] = nextId++;
            knockback[i] = Vector2.zero;
            speed[i] = def.IsBoss ? def.Speed : def.Speed * UnityEngine.Random.Range(0.9f, 1.1f);
            moveSpeed[i] = speed[i];
            damage[i] = def.Damage;
            xp[i] = def.Xp;
            type[i] = typeIndex;
            isBoss[i] = (byte)(def.IsBoss ? 1 : 0);
            flash[i] = 0f;
            squash[i] = 0f;
            fly[i] = Vector2.zero;
            // Ranged: keeps its distance, circling one way or the other (random side per enemy).
            keep[i] = def.IsRanged ? def.Settings.KeepDistance * (UnityEngine.Random.value < 0.5f ? -1f : 1f) : 0f;
            ShootTimer[i] = def.IsRanged ? UnityEngine.Random.Range(0.6f, 1f) * def.Settings.ShotInterval : 0f;
            elite[i] = 0;
        }

        // Overrides the walking speed and touch damage of enemy i (the boss uses BossStats).
        public void SetSpeedAndDamage(int i, float newSpeed, float newDamage)
        {
            speed[i] = newSpeed;
            moveSpeed[i] = newSpeed;
            damage[i] = newDamage;
        }

        // Swarm member: flies in a straight line at `velocity` (ignores the hero) and quietly leaves the map after
        // `life` seconds (no reward). It still hurts the hero on contact and can be killed like any enemy.
        public void SpawnFlyer(int typeIndex, Vector2 position, float hpMultiplier, Vector2 velocity, float life)
        {
            var before = Count;
            Spawn(typeIndex, position, hpMultiplier);
            if (Count == before) return; // enemy cap reached
            var i = Count - 1;
            fly[i] = velocity;
            flyTime[i] = life;
            flyerCount++;
        }

        // Elite: a bigger, tougher copy of a normal enemy (numbers: EnemySettings "Quái tinh anh"). Returns its index,
        // or -1 when the enemy cap is reached.
        public int SpawnElite(int typeIndex, Vector2 position, float hpMultiplier)
        {
            var before = Count;
            var es = config.Enemies[typeIndex].Stats; // this type's elite multipliers
            Spawn(typeIndex, position, hpMultiplier * es.EliteHpMultiplier);
            if (Count == before) return -1;
            var i = Count - 1;
            elite[i] = 1;
            Radius[i] *= es.EliteSizeMultiplier;
            damage[i] *= es.EliteDamageMultiplier;
            speed[i] *= es.EliteSpeedMultiplier;
            moveSpeed[i] = speed[i];
            xp[i] = Mathf.RoundToInt(xp[i] * es.EliteXpMultiplier);
            return i;
        }

        public void Tick(float dt)
        {
            deaths.Tick(dt);
            frame++;
            var ringRadius = SpawnRingRadius();
            var visibleRadius = ringRadius - 1.5f; // half diagonal of the screen

            var positions = Position.Reinterpret<float2>();
            var build = grid.ScheduleBuild(positions, Count, default);
            var move = new MoveJob
            {
                Pos = positions,
                KnockIn = knockback.Reinterpret<float2>(),
                Hp = Hp,
                Radius = Radius,
                MoveSpeed = moveSpeed,
                Fly = fly.Reinterpret<float2>(),
                Keep = keep,
                KeepSlack = Mathf.Max(0.05f, tuning.RangedDistanceSlack),
                StrafeSpeed = tuning.RangedStrafeSpeed,
                RetreatSpeed = tuning.RangedRetreatSpeed,
                BucketStart = grid.BucketStart,
                Items = grid.Items,
                PosOut = positionNext.Reinterpret<float2>(),
                KnockOut = knockbackNext.Reinterpret<float2>(),
                Player = player.Position,
                Dt = dt,
                DecayNear = Mathf.Exp(-tuning.KnockbackDecay * dt),
                DecayFar = Mathf.Exp(-tuning.KnockbackDecay * 2f * dt),
                InverseStunSpeed = tuning.KnockbackStunSpeed > 0.01f ? 1f / tuning.KnockbackStunSpeed : 0f,
                RingRadius = ringRadius,
                // Never closer than the spawn ring + a margin, so a wide camera can't make enemies despawn as they spawn.
                DespawnSq = Mathf.Pow(Mathf.Max(config.DespawnDistance, ringRadius + 8f), 2f),
                FarSq = visibleRadius * visibleRadius,
                InverseCellSize = grid.InverseCellSize,
                Parity = frame & 1,
            }.Schedule(Count, 64, build);
            var post = new PostJob
            {
                Pos = positionNext.Reinterpret<float2>(), // the job above wrote the new positions here
                Hp = Hp, Radius = Radius, Damage = damage, IsBoss = isBoss, Flash = flash, Squash = squash,
                Player = player.Position, PlayerRadius = player.Radius, Dt = dt, Count = Count,
                ContactDamage = contactDamage, BossFound = bossFound,
            }.Schedule(move);
            post.Complete();

            (Position, positionNext) = (positionNext, Position);
            (knockback, knockbackNext) = (knockbackNext, knockback);

            BossIndex = bossFound[0];
            // Boss walking speed for the next frame; BossSkillSystem stops it while casting / moves it itself while dashing.
            if (BossIndex >= 0) moveSpeed[BossIndex] = speed[BossIndex] * BossSpeedScale;
            if (contactDamage[0] > 0f) player.TryHurt(contactDamage[0]);

            // Flyers whose time is up leave the map (no reward, no kill).
            if (flyerCount > 0)
                for (var i = Count - 1; i >= 0; i--)
                {
                    if (fly[i] == Vector2.zero) continue;
                    flyTime[i] -= dt;
                    if (flyTime[i] <= 0f && Hp[i] > 0f) RemoveAt(i);
                }
        }

        // Single-threaded Burst pass over all enemies: hit flash, strongest contact damage, boss lookup.
        [BurstCompile(CompileSynchronously = true)]
        struct PostJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Pos;
            [ReadOnly] public NativeArray<float> Hp;
            [ReadOnly] public NativeArray<float> Radius;
            [ReadOnly] public NativeArray<float> Damage;
            [ReadOnly] public NativeArray<byte> IsBoss;
            public NativeArray<float> Flash;
            public NativeArray<float> Squash;
            public NativeArray<float> ContactDamage;
            public NativeArray<int> BossFound;
            public float2 Player;
            public float PlayerRadius, Dt;
            public int Count;

            public void Execute()
            {
                var contact = 0f;
                var boss = -1;
                for (var i = 0; i < Count; i++)
                {
                    if (Hp[i] <= 0f) continue;
                    Flash[i] = Flash[i] - Dt;
                    Squash[i] = math.max(0f, Squash[i] - Dt);
                    if (IsBoss[i] != 0) boss = i;
                    var reach = Radius[i] + PlayerRadius;
                    if (math.lengthsq(Pos[i] - Player) < reach * reach) contact = math.max(contact, Damage[i]);
                }
                ContactDamage[0] = contact;
                BossFound[0] = boss;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        struct MoveJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float2> Pos;
            [ReadOnly] public NativeArray<float2> KnockIn;
            [ReadOnly] public NativeArray<float> Hp;
            [ReadOnly] public NativeArray<float> Radius;
            [ReadOnly] public NativeArray<float> MoveSpeed;
            [ReadOnly] public NativeArray<float2> Fly;
            [ReadOnly] public NativeArray<float> Keep;
            public float KeepSlack, StrafeSpeed, RetreatSpeed;
            [ReadOnly] public NativeArray<int> BucketStart;
            [ReadOnly] public NativeArray<int> Items;
            [WriteOnly] public NativeArray<float2> PosOut;
            [WriteOnly] public NativeArray<float2> KnockOut;

            public float2 Player;
            public float Dt, DecayNear, DecayFar, RingRadius, DespawnSq, FarSq, InverseCellSize;
            public float InverseStunSpeed; // while being pushed fast, the enemy stops walking toward the hero
            public int Parity;

            public void Execute(int i)
            {
                var p = Pos[i];
                var kb = KnockIn[i];
                if (Hp[i] <= 0f)
                {
                    PosOut[i] = p;
                    KnockOut[i] = kb;
                    return;
                }

                // Flyer (swarm): straight line at its own velocity: no chasing, no wrap-around, no separation.
                var fv = Fly[i];
                if (fv.x != 0f || fv.y != 0f)
                {
                    kb *= DecayNear;
                    PosOut[i] = p + (fv + kb) * Dt;
                    KnockOut[i] = kb;
                    return;
                }

                var toPlayer = Player - p;
                var distSq = math.lengthsq(toPlayer);

                // Too far behind: bring it back on the other side of the player instead of letting it walk forever.
                if (distSq > DespawnSq)
                {
                    PosOut[i] = Player + toPlayer * (RingRadius * math.rsqrt(distSq));
                    KnockOut[i] = float2.zero;
                    return;
                }

                // Off screen: update every other frame with a double step, and skip separation (nobody sees it).
                var far = distSq > FarSq;
                if (far && ((i + Parity) & 1) == 1)
                {
                    PosOut[i] = p;
                    KnockOut[i] = kb;
                    return;
                }
                var step = far ? Dt * 2f : Dt;
                kb *= far ? DecayFar : DecayNear;

                var dist = math.sqrt(distSq);
                var velocity = dist > 0.001f ? toPlayer * (MoveSpeed[i] / dist) : float2.zero;
                var keepSigned = Keep[i];
                if (keepSigned != 0f && dist > 0.001f)
                {
                    // Ranged: walk in when too far, back off when too close, circle the hero inside the comfort zone.
                    var keepDist = math.abs(keepSigned);
                    var dir = toPlayer / dist;
                    var along = math.clamp((dist - keepDist) / KeepSlack, -1f, 1f);   // +1 = come closer, -1 = back off
                    var tangent = new float2(-dir.y, dir.x) * math.sign(keepSigned);
                    var strafe = (1f - math.abs(along)) * StrafeSpeed;
                    var walk = along > 0f ? along : along * RetreatSpeed;
                    velocity = (dir * walk + tangent * strafe) * MoveSpeed[i];
                }
                velocity *= math.saturate(1f - math.length(kb) * InverseStunSpeed); // knocked back: no chasing
                var push = far ? float2.zero : Separation(i, p);

                PosOut[i] = p + (velocity + kb) * step + push * 0.5f;
                KnockOut[i] = kb;
            }

            // Push away from overlapping neighbours in the 3x3 cells around the enemy.
            // Capped, so a dense crowd costs the same per enemy as a sparse one.
            float2 Separation(int i, float2 p)
            {
                var push = float2.zero;
                var r = Radius[i];
                var cx = SpatialGrid.Cell(p.x, InverseCellSize);
                var cy = SpatialGrid.Cell(p.y, InverseCellSize);
                int scanned = 0, overlaps = 0;
                for (var gx = cx - 1; gx <= cx + 1; gx++)
                for (var gy = cy - 1; gy <= cy + 1; gy++)
                {
                    var b = SpatialGrid.Hash(gx, gy);
                    var end = BucketStart[b + 1];
                    for (var k = BucketStart[b]; k < end; k++)
                    {
                        if (++scanned > MaxNeighborScan || overlaps >= MaxOverlaps) return push;
                        var j = Items[k];
                        if (j == i) continue;
                        var d = p - Pos[j];
                        var rj = Radius[j];
                        var minDist = r + rj;
                        var sq = math.lengthsq(d);
                        if (sq >= minDist * minDist) continue;
                        overlaps++;
                        if (sq < 0.000001f)
                        {
                            push += new float2((i & 1) == 0 ? 0.01f : -0.01f, 0.01f);
                            continue;
                        }
                        var len = math.sqrt(sq);
                        // Bigger neighbours push harder: small enemies can't shove a boss around.
                        push += d * ((minDist - len) / len * (2f * rj / minDist));
                    }
                }
                return push;
            }
        }

        public void Damage(int i, float amount, Vector2 knockDirection = default, float knockForce = 0f)
        {
            if (Hp[i] <= 0f) return;
            Hp[i] -= amount;
            var boss = isBoss[i] != 0;
            if (!boss) flash[i] = 0.08f;
            else
            {
                // Boss: white for its Hit Flash Time, then at least Hit Flash Gap seconds of normal look before it can
                // flash again, so a stream of hits blinks instead of keeping it white. One timer: white while above the gap.
                var b = bosses.FindType(type[i])?.Settings;
                if (b != null && flash[i] <= 0f && b.HitFlashTime > 0f) flash[i] = b.HitFlashTime + b.HitFlashGap;
            }
            damageNumbers.Spawn(Position[i], amount, boss, type[i]);
            // Blood flies the way the hit pushes it (or away from the hero when the hit has no direction).
            blood.Enemy(Position[i], Radius[i], type[i], knockDirection.sqrMagnitude > 0.0001f ? knockDirection : Position[i] - player.Position);
            audio.Play(Sfx.Hit, 0.35f, 0.15f, 0.06f);
            if (Hp[i] <= 0f)
            {
                dead.Add(i); // killing blow: no knockback, it shatters where it stands
                return;
            }
            // Survived the hit: a quick squash (EnemySettings "Co rúm khi trúng đòn"), and a push back.
            squash[i] = tuning.HitSquashTime;
            // Survived the hit: pushed back (bosses ignore it). Capped so fast weapons can't launch it across the map.
            knockForce *= config.Enemies[type[i]].Settings.KnockbackScale; // per type (its Settings)
            if (elite[i] != 0) knockForce *= tuning.EliteKnockbackScale; // elites barely budge
            if (!boss && knockForce > 0f)
            {
                var kb = (Vector2)knockback[i] + knockDirection * (knockForce * tuning.KnockbackStrength);
                knockback[i] = Vector2.ClampMagnitude(kb, tuning.KnockbackMaxSpeed);
            }
        }

        // Boss fight starts: every non-boss enemy shatters and is removed, without rewards or kill count.
        public void ClearAllButBoss()
        {
            for (var i = Count - 1; i >= 0; i--)
            {
                if (isBoss[i] != 0) continue;
                if (Hp[i] > 0f) deaths.Add(Position[i], Radius[i], type[i], Id[i], false);
                RemoveAt(i); // swap-remove from the end, so the loop stays valid (BossIndex is kept up to date)
            }
            dead.Clear();
        }

        // Debug: kills every enemy (they still drop their rewards).
        public void KillAll()
        {
            for (var i = 0; i < Count; i++)
            {
                if (Hp[i] <= 0f) continue;
                Hp[i] = 0f;
                dead.Add(i);
            }
        }

        // Deaths are applied after all weapons ran this frame, so indices stay valid while weapons iterate.
        public void RemoveDead()
        {
            if (dead.Count == 0) return;
            dead.Sort();
            for (var k = dead.Count - 1; k >= 0; k--) // highest index first keeps the swap-remove correct
            {
                var i = dead[k];
                killedPublisher.Publish(new EnemyKilledEvent(Position[i], xp[i], config.Enemies[type[i]].IsBoss, type[i], elite[i] != 0));
                deaths.Add(Position[i], Radius[i], type[i], Id[i], isBoss[i] != 0); // death animation
                if (i == BossIndex) BossIndex = -1;
                RemoveAt(i);
            }
            dead.Clear();
        }

        void RemoveAt(int i)
        {
            if (fly[i] != Vector2.zero) flyerCount--;
            var last = --Count;
            if (i == last) return;
            if (BossIndex == last) BossIndex = i;
            Position[i] = Position[last];
            Hp[i] = Hp[last];
            Radius[i] = Radius[last];
            Id[i] = Id[last];
            knockback[i] = knockback[last];
            moveSpeed[i] = moveSpeed[last];
            maxHp[i] = maxHp[last];
            speed[i] = speed[last];
            damage[i] = damage[last];
            xp[i] = xp[last];
            type[i] = type[last];
            flash[i] = flash[last];
            squash[i] = squash[last];
            isBoss[i] = isBoss[last];
            fly[i] = fly[last];
            keep[i] = keep[last];
            elite[i] = elite[last];
            ShootTimer[i] = ShootTimer[last];
            flyTime[i] = flyTime[last];
        }

        // Alive enemies whose body touches the circle.
        public void QueryRadius(Vector2 center, float radius, List<int> results)
        {
            grid.Query(center, radius + MaxRadius, candidates);
            results.Clear();
            foreach (var i in candidates)
            {
                if (i >= Count || Hp[i] <= 0f) continue;
                var reach = radius + Radius[i];
                if ((Position[i] - center).sqrMagnitude <= reach * reach) results.Add(i);
            }
        }

        // The k closest alive enemies within range, closest first.
        public void FindNearest(Vector2 center, float range, int k, List<int> results)
        {
            results.Clear();
            var maxSq = range * range;
            Span<float> bestSq = stackalloc float[8];
            k = Mathf.Min(k, 8);
            for (var i = 0; i < Count; i++)
            {
                if (Hp[i] <= 0f) continue;
                var sq = (Position[i] - center).sqrMagnitude;
                if (sq > maxSq) continue;

                var pos = results.Count;
                while (pos > 0 && bestSq[pos - 1] > sq) pos--;
                if (pos >= k) continue;

                if (results.Count < k) results.Add(0);
                for (var s = results.Count - 1; s > pos; s--)
                {
                    results[s] = results[s - 1];
                    bestSq[s] = bestSq[s - 1];
                }
                results[pos] = i;
                bestSq[pos] = sq;
            }
        }

        // Up to count random alive enemies within range (used by lightning).
        public void PickRandom(Vector2 center, float range, int count, List<int> results)
        {
            results.Clear();
            if (Count == 0) return;
            var maxSq = range * range;
            for (var attempt = 0; attempt < count * 10 && results.Count < count; attempt++)
            {
                var i = UnityEngine.Random.Range(0, Count);
                if (Hp[i] <= 0f || results.Contains(i)) continue;
                if ((Position[i] - center).sqrMagnitude <= maxSq) results.Add(i);
            }
        }

        public float SpawnRingRadius()
        {
            var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 9f / 16f;
            var halfWidth = config.ViewWidth * 0.5f;
            var halfHeight = config.CameraSizeFor(aspect);
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight) + 1.5f;
        }

        public void Draw(InstancedRenderer renderer)
        {
            var bossLook = BossIndex >= 0 ? bosses.FindType(type[BossIndex])?.Settings : null;
            drawer.Draw(renderer, Position, Radius, flash, BossIndex, bossLook != null ? bossLook.HitFlashGap : 0f,
                bossLook != null ? bossLook.HitFlashStrength : 1f, squash,
                tuning.HitSquashTime, tuning.HitSquashAmount,
                type, Id, Count, player.Position.x, fly, elite, tuning.EliteTint);
            deaths.Draw(renderer);
        }

        void Grow()
        {
            var size = Position.Length * 2;
            GrowNative(ref Position, size);
            GrowNative(ref Hp, size);
            GrowNative(ref Radius, size);
            GrowNative(ref positionNext, size);
            GrowNative(ref knockback, size);
            GrowNative(ref knockbackNext, size);
            GrowNative(ref moveSpeed, size);
            GrowNative(ref flash, size);
            GrowNative(ref squash, size);
            GrowNative(ref damage, size);
            GrowNative(ref isBoss, size);
            GrowNative(ref fly, size);
            GrowNative(ref keep, size);
            GrowNative(ref elite, size);
            Array.Resize(ref Id, size);
            Array.Resize(ref flyTime, size);
            Array.Resize(ref ShootTimer, size);
            Array.Resize(ref maxHp, size);
            Array.Resize(ref speed, size);
            Array.Resize(ref xp, size);
            Array.Resize(ref type, size);
        }

        void GrowNative<T>(ref NativeArray<T> array, int size) where T : struct
        {
            var bigger = new NativeArray<T>(size, Allocator.Persistent);
            NativeArray<T>.Copy(array, bigger, Count);
            array.Dispose();
            array = bigger;
        }

        public void Dispose()
        {
            grid.Dispose();
            Position.Dispose();
            Hp.Dispose();
            Radius.Dispose();
            positionNext.Dispose();
            knockback.Dispose();
            knockbackNext.Dispose();
            moveSpeed.Dispose();
            flash.Dispose();
            squash.Dispose();
            damage.Dispose();
            isBoss.Dispose();
            contactDamage.Dispose();
            bossFound.Dispose();
            fly.Dispose();
            keep.Dispose();
            elite.Dispose();
        }
    }
}
