using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct HighwayBuilderRuleJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;
    public uint Seed;
    [ReadOnly] public NativeParallelHashMap<uint, int> RoadHashMap;
    public Entity RoadPrefab;

    // Highway builder should create largely straight, wide roads
    public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref RandomGenerator random, ref BuilderLifetime builderLifetime, ref GridPosition gridPosition, ref Direction direction, [ReadOnly] in HighwayBuilderAgent highwayBuilderAgent)
    {
        // Build street
        TileCreator.CreateRoadTile(ref Ecb, entityIndexInQuery, RoadPrefab, gridPosition.Value);

        // Decrease lifetime
        builderLifetime.Value--;

        // 95% to keep moving forward,
        if (random.Value.NextFloat() < 0.95f)
        {
            // 2% chance to spawn intersection
            if (random.Value.NextFloat() < 0.02f)
            {
                // 50% chance to spawn one direction (left or right), set lower lifetime
                if (random.Value.NextFloat() < 0.5f)
                {
                    var childDirection = direction;
                    if (random.Value.NextFloat() < 0.5f)
                    {
                        childDirection.Value.x = direction.Value.z == 0 ? 0 : -direction.Value.z;
                        childDirection.Value.z = direction.Value.x == 0 ? 0 : direction.Value.x;
                    }
                    else
                    {
                        childDirection.Value.x = direction.Value.z == 0 ? 0 : direction.Value.z;
                        childDirection.Value.z = direction.Value.x == 0 ? 0 : -direction.Value.x;
                    }

                    if (!RoadHashMap.TryGetValue(math.hash(gridPosition.Value + childDirection.Value), out _))
                    {
                        BuilderAgentCreator.CreateHighwayBuilderAgent(ref Ecb, entityIndexInQuery, gridPosition.Value + childDirection.Value,
                            childDirection.Value, builderLifetime.Value - 10, Seed + (uint)(entityIndexInQuery + builderLifetime.Value + 1));
                    }
                }
                // 50% chance to spawn both directions (left and right), set lower lifetime
                else
                {
                    var childDirection = direction;
                    childDirection.Value.x = direction.Value.z == 0 ? 0 : -direction.Value.z;
                    childDirection.Value.z = direction.Value.x == 0 ? 0 : direction.Value.x;
                    if (!RoadHashMap.TryGetValue(math.hash(gridPosition.Value + childDirection.Value), out _))
                    {
                        BuilderAgentCreator.CreateHighwayBuilderAgent(ref Ecb, entityIndexInQuery, gridPosition.Value + childDirection.Value,
                            childDirection.Value, builderLifetime.Value - 10, Seed + (uint)(entityIndexInQuery + builderLifetime.Value + 2));
                    }

                    childDirection.Value.x = direction.Value.z == 0 ? 0 : direction.Value.z;
                    childDirection.Value.z = direction.Value.x == 0 ? 0 : -direction.Value.x;
                    if (!RoadHashMap.TryGetValue(math.hash(gridPosition.Value + childDirection.Value), out _))
                    {
                        BuilderAgentCreator.CreateHighwayBuilderAgent(ref Ecb, entityIndexInQuery, gridPosition.Value + childDirection.Value,
                            childDirection.Value, builderLifetime.Value - 10, Seed + (uint)(entityIndexInQuery + builderLifetime.Value + 3));
                    }
                }

            }
        }
        // 2.5% to turn right,
        else if (random.Value.NextFloat() < 0.5f)
        {
            var x = direction.Value.z == 0 ? 0 : -direction.Value.z;
            direction.Value.z = direction.Value.x == 0 ? 0 : direction.Value.x;
            direction.Value.x = x;
        }
        // 2.5% to turn left
        else
        {
            var x = direction.Value.z == 0 ? 0 : direction.Value.z;
            direction.Value.z = direction.Value.x == 0 ? 0 : -direction.Value.x;
            direction.Value.x = x;
        }

        // Check if movement direction available
        if (RoadHashMap.TryGetValue(math.hash(gridPosition.Value + direction.Value), out _))
        {
            builderLifetime.Value = 0;
        }
        else
        {
            gridPosition.Value += direction.Value;
        }
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(InitialGroup))]
[UpdateAfter(typeof(HashRoadsSystem))]
public partial struct HighwayBuilderRuleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HighwayBuilderAgent>();
        state.RequireForUpdate<TileUnitSpawner_Data>();
        state.RequireForUpdate<HashRoadsSystemComponent>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var hashRoadSystemComponent = SystemAPI.GetSingleton<HashRoadsSystemComponent>();
        var unitSpawner = SystemAPI.GetSingleton<TileUnitSpawner_Data>();

        // Get all builders of this type

        // Run a step of their build process
        //  inputs: roads already generated
        var seed = (uint)SystemAPI.Time.ElapsedTime == 0 ? 1 : (uint)SystemAPI.Time.ElapsedTime;
        state.Dependency = JobHandle.CombineDependencies(state.Dependency, hashRoadSystemComponent.Handle);
        state.Dependency = new HighwayBuilderRuleJob
        {
            Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            Seed = seed,
            RoadHashMap = hashRoadSystemComponent.HashMap,
            RoadPrefab = unitSpawner.RoadTile_Prefab,
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}
