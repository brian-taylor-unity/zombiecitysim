using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

public class TileSpawner_System : JobComponentSystem
{
    private enum TileKinds
    {
        BuildingTile,
        RoadTile
    }

    private BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    struct SpawnJob : IJobForEachWithEntity<TileSpawner_Data>
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int3> tilePositions;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<TileKinds> tileKinds;
        public EntityCommandBuffer.Concurrent CommandBuffer;

        public void Execute(Entity entity, int index, [ReadOnly] ref TileSpawner_Data tileSpawner)
        {
            for (int i = 0; i < tilePositions.Length; i++)
            {
                Entity instance;
                switch (tileKinds[i])
                {
                    case TileKinds.BuildingTile:
                        instance = CommandBuffer.Instantiate(index, tileSpawner.BuildingTile_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = tilePositions[i] });
                        break;
                    case TileKinds.RoadTile:
                        instance = CommandBuffer.Instantiate(index, tileSpawner.RoadTile_Prefab);
                        CommandBuffer.SetComponent(index, instance, new Translation { Value = new float3(tilePositions[i].x / 2.0f, 0, tilePositions[i].z / 2.0f ) });
                        CommandBuffer.AddComponent(index, instance, new Scale { Value = tilePositions[i].x / 10.0f - 0.1f });
                        break;
                }
            }

            CommandBuffer.DestroyEntity(index, entity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var tilePositions = new List<int3>();
        var tileKinds = new List<TileKinds>();

        // Road border boundary
        for (int y = 0; y < GameController.instance.numTilesY; y++)
        {
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(0, 0, y));
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(0, 1, y));

            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(GameController.instance.numTilesX - 1, 0, y));
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(GameController.instance.numTilesX - 1, 1, y));
        }
        
        // Road border boundary
        for (int x = 1; x < GameController.instance.numTilesX - 1; x++)
        {
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(x, 0, 0));
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(x, 1, 0));

            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(x, 0, GameController.instance.numTilesY - 1));
            tileKinds.Add(TileKinds.BuildingTile);
            tilePositions.Add(new int3(x, 1, GameController.instance.numTilesY - 1));
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
                    tileKinds.Add(TileKinds.BuildingTile);
                    tilePositions.Add(new int3(x, 1, y));
                }
            }
        }

        // Road Floor Plane
        tileKinds.Add(TileKinds.RoadTile);
        tilePositions.Add(new int3(GameController.instance.numTilesX, 0, GameController.instance.numTilesY));

        var tilePositionsNativeArray = new NativeArray<int3>(tilePositions.ToArray(), Allocator.TempJob);
        var tileKindsNativeArray = new NativeArray<TileKinds>(tileKinds.ToArray(), Allocator.TempJob);

        var job = new SpawnJob
        {
            tilePositions = tilePositionsNativeArray,
            tileKinds = tileKindsNativeArray,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
