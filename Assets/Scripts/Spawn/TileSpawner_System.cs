using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class TileSpawner_System : JobComponentSystem
{
    private BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    struct SpawnJob : IJobForEachWithEntity<TileSpawner_Data, LocalToWorld>
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int3> buildingTilePositions;
        public EntityCommandBuffer.Concurrent CommandBuffer;

        public void Execute(Entity entity, int index, [ReadOnly] ref TileSpawner_Data tileSpawner, [ReadOnly] ref LocalToWorld location)
        {
            for (int i = 0; i < buildingTilePositions.Length; i++)
            {
                var instance = CommandBuffer.Instantiate(index, tileSpawner.BuildingTile_Prefab);
                CommandBuffer.SetComponent(index, instance, new Translation { Value = buildingTilePositions[i] });
            }

            CommandBuffer.DestroyEntity(index, entity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        int numBuildingTiles = 10 + 10 + 8 + 8;
        NativeArray<int3> buildingTilePositions = new NativeArray<int3>(numBuildingTiles, Allocator.TempJob);
        int index = 0;
        for (int y = 0; y < 10; y++)
        {
            buildingTilePositions[index++] = new int3(0, 0, y);
            buildingTilePositions[index++] = new int3(9, 0, y);
        }

        for (int x = 1; x < 9; x++)
        {
            buildingTilePositions[index++] = new int3(x, 0, 0);
            buildingTilePositions[index++] = new int3(x, 0, 9);
        }

        var job = new SpawnJob
        {
            buildingTilePositions = buildingTilePositions,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
