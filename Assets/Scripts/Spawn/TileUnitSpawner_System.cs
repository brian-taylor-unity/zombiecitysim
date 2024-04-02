using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Rendering;
using UnityEngine;

public enum TileUnitKinds
{
    BuildingTile,
    RoadTile,
    HumanUnit,
    ZombieUnit
}

public struct SpawnWorld : IComponentData { }

[BurstCompile]
public partial struct SpawnJob : IJobEntity
{
    public int HumanTurnDelay;
    public int ZombieTurnDelay;

    public EntityCommandBuffer.ParallelWriter Ecb;

    [ReadOnly] public NativeList<int3> TileUnitPositionsNativeList;
    [ReadOnly] public NativeList<TileUnitKinds> TileUnitKindsNativeList;
    [ReadOnly] public NativeList<int> TileUnitHealthNativeList;
    [ReadOnly] public NativeList<int> TileUnitDamageNativeList;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, [ReadOnly] in TileUnitSpawner_Data tileUnitSpawner)
    {
        for (var i = 0; i < TileUnitPositionsNativeList.Length; i++)
        {
            Entity instance;
            switch (TileUnitKindsNativeList[i])
            {
                case TileUnitKinds.BuildingTile:
                    instance = Ecb.Instantiate(entityIndexInQuery, tileUnitSpawner.BuildingTile_Prefab);
                    Ecb.SetComponent(entityIndexInQuery, instance, LocalTransform.FromPosition(TileUnitPositionsNativeList[i]));
                    Ecb.AddComponent(entityIndexInQuery, instance, new URPMaterialPropertyBaseColor { Value = new float4(0.0f, 0.0f, 0.0f, 1.0f) });
                    Ecb.AddComponent(entityIndexInQuery, instance, new GridPosition { Value = new int3(TileUnitPositionsNativeList[i]) });
                    Ecb.AddComponent(entityIndexInQuery, instance, new StaticCollidable());
                    break;
                case TileUnitKinds.RoadTile:
                    instance = Ecb.Instantiate(entityIndexInQuery, tileUnitSpawner.RoadTile_Prefab);
                    Ecb.SetComponent(entityIndexInQuery, instance, LocalTransform.FromPositionRotationScale(
                        new float3(TileUnitPositionsNativeList[i].x / 2.0f, 0.5f, TileUnitPositionsNativeList[i].z / 2.0f),
                        Quaternion.identity,
                        (TileUnitPositionsNativeList[i].x >= TileUnitPositionsNativeList[i].z ? TileUnitPositionsNativeList[i].x : TileUnitPositionsNativeList[i].z) / 10.0f - 0.1f
                    ));
                    Ecb.AddComponent(entityIndexInQuery, instance, new URPMaterialPropertyBaseColor { Value = new float4(0.8f, 0.8f, 0.8f, 1.0f) });
                    Ecb.AddComponent(entityIndexInQuery, instance, new RoadSurface());
                    break;
                case TileUnitKinds.HumanUnit:
                    var turnsUntilActive = i % HumanTurnDelay + 1;
                    HumanCreator.CreateHuman(
                        ref Ecb,
                        entityIndexInQuery,
                        tileUnitSpawner.HumanUnit_Prefab,
                        TileUnitPositionsNativeList[i],
                        TileUnitHealthNativeList[i],
                        TileUnitDamageNativeList[i],
                        turnsUntilActive,
                        i == 0 ? 1 : (uint)i
                    );
                    break;
                case TileUnitKinds.ZombieUnit:
                    turnsUntilActive = i % ZombieTurnDelay + 1;
                    ZombieCreator.CreateZombie(
                        ref Ecb,
                        entityIndexInQuery,
                        tileUnitSpawner.ZombieUnit_Prefab,
                        TileUnitPositionsNativeList[i],
                        TileUnitHealthNativeList[i],
                        TileUnitDamageNativeList[i],
                        turnsUntilActive,
                        i == 0 ? 1 : (uint)i
                    );
                    break;
            }
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct TileUnitSpawner_System : ISystem
{
    private EntityQuery _spawnWorldComponentQuery;
    private EntityQuery _regenerateComponentsQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spawnWorldComponentQuery = state.GetEntityQuery(ComponentType.ReadOnly<SpawnWorld>());
        _regenerateComponentsQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAny<GridPosition, RoadSurface, HashDynamicCollidableSystemComponent, HashStaticCollidableSystemComponent>());
        state.RequireForUpdate<TileUnitSpawner_Data>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireForUpdate<SpawnWorld>();
        state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.DestroyEntity(_spawnWorldComponentQuery);
        state.EntityManager.DestroyEntity(_regenerateComponentsQuery);

        var staticComponentEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent(staticComponentEntity, ComponentType.ReadOnly<HashStaticCollidableSystemComponent>());

        var dynamicComponentEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent(dynamicComponentEntity, ComponentType.ReadOnly<HashDynamicCollidableSystemComponent>());

        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        var tileUnitPositions = new NativeList<int3>(Allocator.TempJob);
        var tileUnitKinds = new NativeList<TileUnitKinds>(Allocator.TempJob);
        var tileUnitHealth = new NativeList<int>(Allocator.TempJob);
        var tileUnitDamage = new NativeList<int>(Allocator.TempJob);

        var tileExists = new NativeArray<bool>(gameControllerComponent.numTilesY * gameControllerComponent.numTilesX, Allocator.Temp);
        for (var y = 0; y < gameControllerComponent.numTilesY; y++)
            for (var x = 0; x < gameControllerComponent.numTilesX; x++)
                tileExists[y * gameControllerComponent.numTilesX + x] = true;

        // Streets
        for (int i = 0, xPos = 1; i < gameControllerComponent.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 4);
            xPos += UnityEngine.Random.Range(0, 2 * (gameControllerComponent.numTilesX / (gameControllerComponent.numStreets / 2)));
            if (xPos >= gameControllerComponent.numTilesX - 1)
                break;

            while (roadSize >= 1 && xPos < gameControllerComponent.numTilesX - 1)
            {
                for (var yPos = 1; yPos < gameControllerComponent.numTilesY - 1; yPos++)
                {
                    if (tileExists[yPos * gameControllerComponent.numTilesX + xPos])
                    {
                        tileExists[yPos * gameControllerComponent.numTilesX + xPos] = false;
                    }
                }
                xPos++;
                roadSize--;
            }
        }
        for (int i = 0, yPos = 1; i < gameControllerComponent.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 4);
            yPos += UnityEngine.Random.Range(0, 2 * (gameControllerComponent.numTilesX / (gameControllerComponent.numStreets / 2)));
            if (yPos >= gameControllerComponent.numTilesX - 1)
                break;

            while (roadSize >= 1 && yPos < gameControllerComponent.numTilesY - 1)
            {
                for (var xPos = 1; xPos < gameControllerComponent.numTilesX - 1; xPos++)
                {
                    if (tileExists[yPos * gameControllerComponent.numTilesX + xPos])
                    {
                        tileExists[yPos * gameControllerComponent.numTilesX + xPos] = false;
                    }
                }
                yPos++;
                roadSize--;
            }
        }

        // Road border boundary
        for (var y = 0; y < gameControllerComponent.numTilesY; y++)
        {
            tileExists[y * gameControllerComponent.numTilesX] = true;
            tileExists[y * gameControllerComponent.numTilesX + gameControllerComponent.numTilesX - 1] = true;
        }

        // Road border boundary
        for (var x = 1; x < gameControllerComponent.numTilesX - 1; x++)
        {
            tileExists[x] = true;
            tileExists[(gameControllerComponent.numTilesY - 1) * gameControllerComponent.numTilesX] = true;
        }

        // Fill in buildings
        for (var y = 0; y < gameControllerComponent.numTilesY; y++)
        {
            for (var x = 0; x < gameControllerComponent.numTilesX; x++)
            {
                if (!tileExists[y * gameControllerComponent.numTilesX + x])
                    continue;

                tileUnitKinds.Add(TileUnitKinds.BuildingTile);
                tileUnitPositions.Add(new int3(x, 1, y));
                tileUnitHealth.Add(0);
                tileUnitDamage.Add(0);
            }
        }

        // Road Floor Plane
        tileUnitKinds.Add(TileUnitKinds.RoadTile);
        tileUnitPositions.Add(new int3(gameControllerComponent.numTilesX, 0, gameControllerComponent.numTilesY));
        tileUnitHealth.Add(0);
        tileUnitDamage.Add(0);

        // Human Units
        for (var i = 0; i < gameControllerComponent.numHumans; i++)
        {
            int xPos, yPos;

            do
            {
                xPos = UnityEngine.Random.Range(1, gameControllerComponent.numTilesX - 1);
                yPos = UnityEngine.Random.Range(1, gameControllerComponent.numTilesY - 1);
            } while (tileExists[yPos * gameControllerComponent.numTilesX + xPos]);

            tileExists[yPos * gameControllerComponent.numTilesX + xPos] = true;
            tileUnitKinds.Add(TileUnitKinds.HumanUnit);
            tileUnitPositions.Add(new int3(xPos, 1, yPos));
            tileUnitHealth.Add(gameControllerComponent.humanStartingHealth);
            tileUnitDamage.Add(gameControllerComponent.humanDamage);
        }

        // Zombie Units
        for (var i = 0; i < gameControllerComponent.numZombies; i++)
        {
            int xPos, yPos;

            do
            {
                xPos = UnityEngine.Random.Range(1, gameControllerComponent.numTilesX - 1);
                yPos = UnityEngine.Random.Range(1, gameControllerComponent.numTilesY - 1);
            } while (tileExists[yPos * gameControllerComponent.numTilesX + xPos]);

            tileExists[yPos * gameControllerComponent.numTilesX + xPos] = true;
            tileUnitKinds.Add(TileUnitKinds.ZombieUnit);
            tileUnitPositions.Add(new int3(xPos, 1, yPos));
            tileUnitHealth.Add(gameControllerComponent.zombieStartingHealth);
            tileUnitDamage.Add(gameControllerComponent.zombieDamage);
        }

        tileExists.Dispose();

        state.Dependency = new SpawnJob
        {
            HumanTurnDelay = gameControllerComponent.humanTurnDelay,
            ZombieTurnDelay = gameControllerComponent.zombieTurnDelay,

            Ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),

            TileUnitPositionsNativeList = tileUnitPositions,
            TileUnitKindsNativeList = tileUnitKinds,
            TileUnitHealthNativeList = tileUnitHealth,
            TileUnitDamageNativeList = tileUnitDamage
        }.ScheduleParallel(state.Dependency);

        tileUnitPositions.Dispose(state.Dependency);
        tileUnitKinds.Dispose(state.Dependency);
        tileUnitHealth.Dispose(state.Dependency);
        tileUnitDamage.Dispose(state.Dependency);
    }
}
