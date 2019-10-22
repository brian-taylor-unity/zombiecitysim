using Unity.Entities;

public struct TileSpawner_Data : IComponentData
{
    public Entity BuildingTile_Prefab;
    public Entity RoadTile_Prefab;
}
