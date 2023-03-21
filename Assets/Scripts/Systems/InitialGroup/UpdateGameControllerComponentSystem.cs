using System;
using Unity.Burst;
using Unity.Entities;

[Serializable]
public struct UpdateGameControllerComponent : IComponentData
{
}

[Serializable]
public struct GameControllerComponent : IComponentData
{
    public int numTilesX;
    public int numTilesY;
    public int numStreets;

    public int numHumans;
    public int humanStartingHealth;
    public int humanDamage;
    public int humanVisionDistance;
    public int humanTurnDelay;

    public int numZombies;
    public int zombieStartingHealth;
    public int zombieDamage;
    public int zombieVisionDistance;
    public int zombieHearingDistance;
    public int zombieTurnDelay;

    public int audibleDecayTime;
    public float turnDelayTime;
}

[UpdateInGroup(typeof(InitialGroup))]
public partial class UpdateGameControllerComponentSystem : SystemBase
{
    private EntityQuery _updateGameControllerComponentQuery;

    [BurstCompile]
    protected override void OnCreate()
    {
        World.EntityManager.CreateSingleton<GameControllerComponent>();
        _updateGameControllerComponentQuery = GetEntityQuery(ComponentType.ReadOnly<UpdateGameControllerComponent>());
        RequireForUpdate<UpdateGameControllerComponent>();
    }

    protected override void OnUpdate()
    {
        var gameControllerComponent = SystemAPI.GetSingletonRW<GameControllerComponent>();

        gameControllerComponent.ValueRW.numTilesX = GameController.Instance.numTilesX;
        gameControllerComponent.ValueRW.numTilesY = GameController.Instance.numTilesY;
        gameControllerComponent.ValueRW.numStreets = GameController.Instance.numStreets;

        gameControllerComponent.ValueRW.numHumans = GameController.Instance.numHumans;
        gameControllerComponent.ValueRW.humanStartingHealth = GameController.Instance.humanStartingHealth;
        gameControllerComponent.ValueRW.humanDamage = GameController.Instance.humanDamage;
        gameControllerComponent.ValueRW.humanVisionDistance = GameController.Instance.humanVisionDistance;
        gameControllerComponent.ValueRW.humanTurnDelay = GameController.Instance.humanTurnDelay;

        gameControllerComponent.ValueRW.numZombies = GameController.Instance.numZombies;
        gameControllerComponent.ValueRW.zombieStartingHealth = GameController.Instance.zombieStartingHealth;
        gameControllerComponent.ValueRW.zombieDamage = GameController.Instance.zombieDamage;
        gameControllerComponent.ValueRW.zombieVisionDistance = GameController.Instance.zombieVisionDistance;
        gameControllerComponent.ValueRW.zombieHearingDistance = GameController.Instance.zombieHearingDistance;
        gameControllerComponent.ValueRW.zombieTurnDelay = GameController.Instance.zombieTurnDelay;

        gameControllerComponent.ValueRW.audibleDecayTime = GameController.Instance.audibleDecayTime;
        gameControllerComponent.ValueRW.turnDelayTime = GameController.Instance.turnDelayTime;

        var ecb = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        ecb.DestroyEntity(_updateGameControllerComponentQuery);
    }
}