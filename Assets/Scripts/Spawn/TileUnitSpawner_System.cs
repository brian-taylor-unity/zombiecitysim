﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

public class TileUnitSpawner_System : SystemBase
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

    protected override void OnUpdate()
    {
        var tileUnitPositions = new List<int3>();
        var tileUnitKinds = new List<TileUnitKinds>();
        var tileUnitHealth = new List<int>();
        var tileUnitDamage = new List<int>();

        var tileExists = new bool[GameController.instance.numTilesY, GameController.instance.numTilesX];
        for (int y = 0; y < GameController.instance.numTilesY; y++)
            for (int x = 0; x < GameController.instance.numTilesX; x++)
                tileExists[y, x] = true;

        // Streets
        for (int i = 0, xPos = 1; i < GameController.instance.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 4);
            xPos += UnityEngine.Random.Range(0, 2 * (GameController.instance.numTilesX / (GameController.instance.numStreets / 2)));
            if (xPos >= GameController.instance.numTilesX - 1)
                break;

            while (roadSize >= 1 && xPos < GameController.instance.numTilesX - 1)
            {
                for (int yPos = 1; yPos < GameController.instance.numTilesY - 1; yPos++)
                {
                    if (tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = false;
                    }
                }
                xPos++;
                roadSize--;
            }
        }
        for (int i = 0, yPos = 1; i < GameController.instance.numStreets / 2; i++)
        {
            var roadSize = UnityEngine.Random.Range(1, 4);
            yPos += UnityEngine.Random.Range(0, 2 * (GameController.instance.numTilesX / (GameController.instance.numStreets / 2)));
            if (yPos >= GameController.instance.numTilesX - 1)
                break;

            while (roadSize >= 1 && yPos < GameController.instance.numTilesY - 1)
            {
                for (var xPos = 1; xPos < GameController.instance.numTilesX - 1; xPos++)
                {
                    if (tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = false;
                    }
                }
                yPos++;
                roadSize--;
            }
        }

        // Road border boundary
        for (int y = 0; y < GameController.instance.numTilesY; y++)
        {
            tileExists[y, 0] = true;
            tileExists[y, GameController.instance.numTilesX - 1] = true;
        }

        // Road border boundary
        for (int x = 1; x < GameController.instance.numTilesX - 1; x++)
        {
            tileExists[0, x] = true;
            tileExists[GameController.instance.numTilesY - 1, 0] = true;
        }

        // Fill in buildings
        for (var y = 0; y < GameController.instance.numTilesY; y++)
        {
            for (var x = 0; x < GameController.instance.numTilesX; x++)
            {
                if (tileExists[y, x])
                {
                    tileUnitKinds.Add(TileUnitKinds.BuildingTile);
                    tileUnitPositions.Add(new int3(x, 1, y));
                    tileUnitHealth.Add(0);
                    tileUnitDamage.Add(0);
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

            do
            {
                xPos = UnityEngine.Random.Range(1, GameController.instance.numTilesX - 1);
                yPos = UnityEngine.Random.Range(1, GameController.instance.numTilesY - 1);
            } while (tileExists[yPos, xPos]);

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
            } while (tileExists[yPos, xPos]);

            tileExists[yPos, xPos] = true;
            tileUnitKinds.Add(TileUnitKinds.ZombieUnit);
            tileUnitPositions.Add(new int3(xPos, 1, yPos));
            tileUnitHealth.Add(GameController.instance.zombieStartingHealth);
            tileUnitDamage.Add(GameController.instance.zombieDamage);
        }

        // Spawn Tiles and Units
        var tileUnitPositionsNativeArray = new NativeArray<int3>(tileUnitPositions.ToArray(), Allocator.TempJob);
        var tileUnitKindsNativeArray = new NativeArray<TileUnitKinds>(tileUnitKinds.ToArray(), Allocator.TempJob);
        var tileUnitHealthNativeArray = new NativeArray<int>(tileUnitHealth.ToArray(), Allocator.TempJob);
        var tileUnitDamageNativeArray = new NativeArray<int>(tileUnitDamage.ToArray(), Allocator.TempJob);
        var CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        var humanTurnDelay = GameController.instance.humanTurnDelay;
        var zombieTurnDelay = GameController.instance.zombieTurnDelay;

        var spawnJob = Entities
            .WithReadOnly(tileUnitPositionsNativeArray)
            .WithReadOnly(tileUnitKindsNativeArray)
            .WithReadOnly(tileUnitHealthNativeArray)
            .WithReadOnly(tileUnitDamageNativeArray)
            .WithBurst()
            .ForEach((Entity entity, int entityInQueryIndex, in TileUnitSpawner_Data tileUnitSpawner) =>
                {
                    for (int i = 0; i < tileUnitPositionsNativeArray.Length; i++)
                    {
                        Entity instance;
                        switch (tileUnitKindsNativeArray[i])
                        {
                            case TileUnitKinds.BuildingTile:
                                instance = CommandBuffer.Instantiate(entityInQueryIndex, tileUnitSpawner.BuildingTile_Prefab);
                                CommandBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = tileUnitPositionsNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new GridPosition { Value = new int3(tileUnitPositionsNativeArray[i]) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new StaticCollidable());
                                break;
                            case TileUnitKinds.RoadTile:
                                instance = CommandBuffer.Instantiate(entityInQueryIndex, tileUnitSpawner.RoadTile_Prefab);
                                CommandBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = new float3(tileUnitPositionsNativeArray[i].x / 2.0f, 0.5f, tileUnitPositionsNativeArray[i].z / 2.0f) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Scale { Value = tileUnitPositionsNativeArray[i].x / 10.0f - 0.1f });
                                break;
                            case TileUnitKinds.HumanUnit:
                                var turnsUntilActive = i % humanTurnDelay + 1;
                                instance = CommandBuffer.Instantiate(entityInQueryIndex, tileUnitSpawner.HumanUnit_Prefab);
                                CommandBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = tileUnitPositionsNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new GridPosition { Value = new int3(tileUnitPositionsNativeArray[i]) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new NextGridPosition { Value = new int3(tileUnitPositionsNativeArray[i]) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Health { Value = tileUnitHealthNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Damage { Value = tileUnitDamageNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new TurnsUntilActive { Value = turnsUntilActive });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Human());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new DynamicCollidable());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new FollowTarget());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new LineOfSight());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new CharacterColor { Value = new float4(0.0f, 1.0f, 0.0f, turnsUntilActive == 1 ? 1.0f : 0.85f) });
                                break;
                            case TileUnitKinds.ZombieUnit:
                                turnsUntilActive = i % zombieTurnDelay + 1;
                                instance = CommandBuffer.Instantiate(entityInQueryIndex, tileUnitSpawner.ZombieUnit_Prefab);
                                CommandBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = tileUnitPositionsNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new GridPosition { Value = new int3(tileUnitPositionsNativeArray[i]) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new NextGridPosition { Value = new int3(tileUnitPositionsNativeArray[i]) });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Health { Value = tileUnitHealthNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Damage { Value = tileUnitDamageNativeArray[i] });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new TurnsUntilActive { Value = turnsUntilActive });
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new Zombie());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new DynamicCollidable());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new MoveTowardsTarget());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new MoveEscapeTarget());
                                CommandBuffer.AddComponent(entityInQueryIndex, instance, new CharacterColor { Value = new float4(1.0f, 0.0f, 0.0f, turnsUntilActive == 1 ? 1.0f : 0.85f) });
                                break;
                        }
                    }

                    CommandBuffer.DestroyEntity(entityInQueryIndex, entity);
                })
            .WithDisposeOnCompletion(tileUnitPositionsNativeArray)
            .WithDisposeOnCompletion(tileUnitKindsNativeArray)
            .WithDisposeOnCompletion(tileUnitHealthNativeArray)
            .WithDisposeOnCompletion(tileUnitDamageNativeArray)
            .ScheduleParallel(Dependency);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnJob);

        Dependency = spawnJob;
    }
}
