using Unity.Entities;

public struct TileUnitSpawner_Data : IComponentData
{
    public Entity BuildingTile_Prefab;
    public Entity RoadTile_Prefab;
    public Entity HumanUnit_Prefab;
    public Entity ZombieUnit_Prefab;
}
