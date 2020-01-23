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
                        break;
                    case TileUnitKinds.RoadTile:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.RoadTile_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = new float3(tileUnitPositions[i].x / 2.0f, 0.5f, tileUnitPositions[i].z / 2.0f ) });
                        CommandBuffer.AddComponent(index, instance, new Scale { Value = tileUnitPositions[i].x / 10.0f - 0.1f });
                        break;
                    case TileUnitKinds.HumanUnit:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.HumanUnit_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tileUnitPositions[i] });
                        CommandBuffer.AddComponent(index, instance, new GridPosition { Value = new int3(tileUnitPositions[i].x, 1, tileUnitPositions[i].y) });
                        CommandBuffer.AddComponent(index, instance, new NextGridPosition { Value = new int3(tileUnitPositions[i].x, 1, tileUnitPositions[i].y) });
                        // CommandBuffer.SetComponent(index, instance, new Health { Value = health });
                        // CommandBuffer.SetComponent(index, instance, new HealthRange { Value = 100 });
                        // CommandBuffer.SetComponent(index, instance, new Damage { Value = damage });
                        // CommandBuffer.SetComponent(index, instance, new TurnsUntilMove { Value = rand.NextInt(turnDelay + 1) });
                        break;
                    case TileUnitKinds.ZombieUnit:
                        instance = CommandBuffer.Instantiate(index, tileUnitSpawner.ZombieUnit_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tileUnitPositions[i] });
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

        // Road border boundary
        for (int y = 0; y < GameController.instance.numTilesY; y++)
        {
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(0, 0, y));
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(0, 1, y));

            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(GameController.instance.numTilesX - 1, 0, y));
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(GameController.instance.numTilesX - 1, 1, y));
        }
        
        // Road border boundary
        for (int x = 1; x < GameController.instance.numTilesX - 1; x++)
        {
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 0, 0));
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 1, 0));

            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 0, GameController.instance.numTilesY - 1));
            tileUnitKinds.Add(TileUnitKinds.BuildingTile);
            tileUnitPositions.Add(new int3(x, 1, GameController.instance.numTilesY - 1));
        }

        // Streets
        var tileExists = new bool[GameController.instance.numTilesY, GameController.instance.numTilesX];
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
                }
            }
        }

        // Road Floor Plane
        tileUnitKinds.Add(TileUnitKinds.RoadTile);
        tileUnitPositions.Add(new int3(GameController.instance.numTilesX, 0, GameController.instance.numTilesY));

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
        }

        // Spawn Tiles and Units
        var tileUnitPositionsNativeArray = new NativeArray<int3>(tileUnitPositions.ToArray(), Allocator.TempJob);
        var tileKindsNativeArray = new NativeArray<TileUnitKinds>(tileUnitKinds.ToArray(), Allocator.TempJob);

        var job = new SpawnJob
        {
            tileUnitPositions = tileUnitPositionsNativeArray,
            tileUnitKinds = tileKindsNativeArray,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
