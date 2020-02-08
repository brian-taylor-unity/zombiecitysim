using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

public class TileUnitSpawner_System : JobComponentSystem
{
    private enum TileUnitKinds
    {
        BuildingTile,
        RoadTile,
        HumanUnit,
        ZombieUnit
    }

    private BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    struct SpawnJob : IJobForEachWithEntity<TileUnitSpawner_Data>
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int3> tileUnitPositions;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<TileUnitKinds> tileUnitKinds;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> tileUnitHealth;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> tileUnitDamage;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> tileUnitTurnsUntilActive;

        public EntityCommandBuffer.Concurrent CommandBuffer;

        public void Execute(Entity entity, int index, [ReadOnly] ref TileUnitSpawner_Data tileUnitSpawner)
        {
            for (int i = 0; i < tileUnitPositions.Length; i++)
            {
                Entity instance;
                switch (tileUnitKinds[i])
                {
                    case TileUnitKinds.BuildingTile:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.BuildingTile_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tileUnitPositions[i] });
                        CommandBuffer.AddComponent(index, instance, new GridPosition { Value = new int3(tileUnitPositions[i]) });
                        CommandBuffer.AddComponent(index, instance, new StaticCollidable());
                        break;
                    case TileUnitKinds.RoadTile:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.RoadTile_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = new float3(tileUnitPositions[i].x / 2.0f, 0.5f, tileUnitPositions[i].z / 2.0f ) });
                        CommandBuffer.AddComponent(index, instance, new Scale { Value = tileUnitPositions[i].x / 10.0f - 0.1f });
                        break;
                    case TileUnitKinds.HumanUnit:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.HumanUnit_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tileUnitPositions[i] });
                        CommandBuffer.AddComponent(index, instance, new GridPosition { Value = new int3(tileUnitPositions[i]) });
                        CommandBuffer.AddComponent(index, instance, new NextGridPosition { Value = new int3(tileUnitPositions[i]) });
                        CommandBuffer.AddComponent(index, instance, new Health { Value = tileUnitHealth[i] });
                        CommandBuffer.AddComponent(index, instance, new HealthRange { Value = 100 });
                        CommandBuffer.AddComponent(index, instance, new Damage { Value = tileUnitDamage[i] });
                        CommandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = index % tileUnitTurnsUntilActive[0] });
                        CommandBuffer.AddComponent(index, instance, new Human());
                        CommandBuffer.AddComponent(index, instance, new DynamicCollidable());
                        CommandBuffer.AddComponent(index, instance, new FollowTarget());
                        CommandBuffer.AddComponent(index, instance, new MoveRandomly());
                        break;
                    case TileUnitKinds.ZombieUnit:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.ZombieUnit_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tileUnitPositions[i] });
                        CommandBuffer.AddComponent(index, instance, new GridPosition { Value = new int3(tileUnitPositions[i]) });
                        CommandBuffer.AddComponent(index, instance, new NextGridPosition { Value = new int3(tileUnitPositions[i]) });
                        CommandBuffer.AddComponent(index, instance, new Health { Value = tileUnitHealth[i] });
                        CommandBuffer.AddComponent(index, instance, new HealthRange { Value = 100 });
                        CommandBuffer.AddComponent(index, instance, new Damage { Value = tileUnitDamage[i] });
                        CommandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = index % tileUnitTurnsUntilActive[1] });
                        CommandBuffer.AddComponent(index, instance, new Zombie());
                        CommandBuffer.AddComponent(index, instance, new DynamicCollidable());
                        CommandBuffer.AddComponent(index, instance, new MoveTowardsTarget());
                        break;
                }
            }

            CommandBuffer.DestroyEntity(index, entity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var tileUnitPositions = new List<int3>();
        var tileUnitKinds = new List<TileUnitKinds>();
        var tileUnitHealth = new List<int>();
        var tileUnitDamage = new List<int>();
        var tileUnitTurnsUntilActive = new List<int>();
        tileUnitTurnsUntilActive.Add(GameController.instance.humanTurnDelay);
        tileUnitTurnsUntilActive.Add(GameController.instance.zombieTurnDelay);

        var tileExists = new bool[GameController.instance.numTilesY, GameController.instance.numTilesX];

        // Road border boundary
        for (int y = 0; y < GameController.instance.numTilesY; y++)
        {
            //tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            //tileUnitPositions.Add(new int3(0, 0, y));
            //tileUnitHealth.Add(0);
            //tileUnitDamage.Add(0);
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(0, 1, y));
            tileUnitHealth.Add(0);
            tileUnitDamage.Add(0);

            tileExists[y, 0] = true;

            //tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            //tileUnitPositions.Add(new int3(GameController.instance.numTilesX - 1, 0, y));
            //tileUnitHealth.Add(0);
            //tileUnitDamage.Add(0);
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(GameController.instance.numTilesX - 1, 1, y));
            tileUnitHealth.Add(0);
            tileUnitDamage.Add(0);

            tileExists[y, GameController.instance.numTilesX - 1] = true;
        }

        // Road border boundary
        for (int x = 1; x < GameController.instance.numTilesX - 1; x++)
        {
            //tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            //tileUnitPositions.Add(new int3(x, 0, 0));
            //tileUnitHealth.Add(0);
            //tileUnitDamage.Add(0);
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 1, 0));
            tileUnitHealth.Add(0);
            tileUnitDamage.Add(0);

            tileExists[0, x] = true;

            //tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            //tileUnitPositions.Add(new int3(x, 0, GameController.instance.numTilesY - 1));
            //tileUnitHealth.Add(0);
            //tileUnitDamage.Add(0);
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 1, GameController.instance.numTilesY - 1));
            tileUnitHealth.Add(0);
            tileUnitDamage.Add(0);

            tileExists[GameController.instance.numTilesY - 1, 0] = true;
        }

        // Streets
        for (int i = 0, xPos = 1; i < GameController.instance.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 3);
            xPos += UnityEngine.Random.Range(0, 2 * (GameController.instance.numTilesX / (GameController.instance.numStreets / 2)));
            if (xPos >= GameController.instance.numTilesX - 1)
                break;

            while (roadSize >= 1 && xPos <= GameController.instance.numTilesX - 1)
            {
                for (int yPos = 1; yPos < GameController.instance.numTilesY - 1; yPos++)
                {
                    if (!tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = true;
                    }
                }
                xPos++;
                roadSize--;
            }
        }
        for (int i = 0, yPos = 1; i < GameController.instance.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 3);
            yPos += UnityEngine.Random.Range(0, 2 * (GameController.instance.numTilesX / (GameController.instance.numStreets / 2)));
            if (yPos >= GameController.instance.numTilesX - 1)
                break;

            while (roadSize >= 1 && yPos <= GameController.instance.numTilesX - 1)
            {
                for (var xPos = 1; xPos < GameController.instance.numTilesY - 1; xPos++)
                {
                    if (!tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = true;
                    }
                }
                yPos++;
                roadSize--;
            }
        }

        // Fill in buildings
        for (var y = 0; y < GameController.instance.numTilesY; y++)
        {
            for (var x = 0; x < GameController.instance.numTilesX; x++)
            {
                if (!tileExists[y, x])
                {
                    tileUnitKinds.Add(TileUnitKinds.BuildingTile);
                    tileUnitPositions.Add(new int3(x, 1, y));
                    tileUnitHealth.Add(0);
                    tileUnitDamage.Add(0);

                    tileExists[y, x] = true;
                }
            }
        }

        // Road Floor Plane
        tileUnitKinds.Add(TileUnitKinds.RoadTile);
        tileUnitPositions.Add(new int3(GameController.instance.numTilesX, 0, GameController.instance.numTilesY));
        tileUnitHealth.Add(0);
        tileUnitDamage.Add(0);

        // Human Units
        for (var i = 0; i < GameController.instance.numHumans; i++)
        {
            int xPos, yPos;

            do { 
                xPos = UnityEngine.Random.Range(1, GameController.instance.numTilesX - 1);
                yPos = UnityEngine.Random.Range(1, GameController.instance.numTilesY - 1);
            } while (!tileExists[yPos, xPos]);

            tileExists[yPos, xPos] = true;
            tileUnitKinds.Add(TileUnitKinds.HumanUnit);
            tileUnitPositions.Add(new int3(xPos, 1, yPos));
            tileUnitHealth.Add(GameController.instance.humanStartingHealth);
            tileUnitDamage.Add(GameController.instance.humanDamage);
        }

        // Zombie Units
        for (var i = 0; i < GameController.instance.numZombies; i++)
        {
            int xPos, yPos;

            do
            {
                xPos = UnityEngine.Random.Range(1, GameController.instance.numTilesX - 1);
                yPos = UnityEngine.Random.Range(1, GameController.instance.numTilesY - 1);
            } while (!tileExists[yPos, xPos]);

            tileExists[yPos, xPos] = true;
            tileUnitKinds.Add(TileUnitKinds.ZombieUnit);
            tileUnitPositions.Add(new int3(xPos, 1, yPos));
            tileUnitHealth.Add(GameController.instance.zombieStartingHealth);
            tileUnitDamage.Add(GameController.instance.zombieDamage);
        }

        // Spawn Tiles and Units
        var tileUnitPositionsNativeArray = new NativeArray<int3>(tileUnitPositions.ToArray(), Allocator.TempJob);
        var tileKindsNativeArray = new NativeArray<TileUnitKinds>(tileUnitKinds.ToArray(), Allocator.TempJob);
        var tileUnitHealthNativeArray = new NativeArray<int>(tileUnitHealth.ToArray(), Allocator.TempJob);
        var tileUnitDamagehNativeArray = new NativeArray<int>(tileUnitDamage.ToArray(), Allocator.TempJob);
        var tileUnitTurnsUntilActiveNativeArray = new NativeArray<int>(tileUnitTurnsUntilActive.ToArray(), Allocator.TempJob);

        var job = new SpawnJob
        {
            tileUnitPositions = tileUnitPositionsNativeArray,
            tileUnitKinds = tileKindsNativeArray,
            tileUnitHealth = tileUnitHealthNativeArray,
            tileUnitDamage = tileUnitDamagehNativeArray,
            tileUnitTurnsUntilActive = tileUnitTurnsUntilActiveNativeArray,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
