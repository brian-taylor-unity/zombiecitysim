using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct CityStreetBuilderRuleJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    // City street builder should be mostly straight, with decent numbers of intersections
    public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref BuilderLifetime builderLifetime, [ReadOnly] in HighwayBuilderAgent highwayBuilderAgent)
    {
        // Build street

        // Decrease lifetime

        // 80% to keep moving forward,
            // 5% chance to spawn intersection
                // 50% chance to spawn one direction (left or right), set lower lifetime on child
                // 50% chance to spawn both directions (left and right), set lower lifetime on child
        // 10% to turn right,
        // 10% to turn left

        // Check if movement direction available
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CityStreetBuilderRuleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CityStreetBuilderAgent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get all builders of this type

        // Run a step of their build process
        //  inputs: roads already generated

    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}