using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
                        CommandBuffer.AddComponent(index, instance, new NonUniformScale { Value = tilePositions[i] });
                        break;
                }
            }

            CommandBuffer.DestroyEntity(index, entity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        int numBuildingTiles = (GameController.instance.numTilesX * 2 + (GameController.instance.numTilesY - 2) * 2) * 2;
        int numRoadTiles = 1;
        NativeArray<int3> tilePositions = new NativeArray<int3>(numBuildingTiles + numRoadTiles, Allocator.TempJob);
        NativeArray<TileKinds> tileKinds = new NativeArray<TileKinds>(numBuildingTiles + numRoadTiles, Allocator.TempJob);

        int index = 0;
        for (int y = 0; y < GameController.instance.numTilesY; y++)
        {
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(0, 0, y);
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(0, 1, y);

            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(GameController.instance.numTilesX - 1, 0, y);
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(GameController.instance.numTilesX - 1, 1, y);
        }

        for (int x = 1; x < GameController.instance.numTilesX - 1; x++)
        {
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(x, 0, 0);
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(x, 1, 0);

            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(x, 0, GameController.instance.numTilesY - 1);
            tileKinds[index] = TileKinds.BuildingTile;
            tilePositions[index++] = new int3(x, 1, GameController.instance.numTilesY - 1);
        }

        // Road Tile
        tileKinds[index] = TileKinds.RoadTile;
        tilePositions[index++] = new int3(GameController.instance.numTilesX, 0, GameController.instance.numTilesY);

        var job = new SpawnJob
        {
            tilePositions = tilePositions,
            tileKinds = tileKinds,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
