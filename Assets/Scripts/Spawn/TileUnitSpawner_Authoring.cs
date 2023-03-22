using Unity.Entities;
using UnityEngine;

public class TileUnitSpawner_Authoring : MonoBehaviour
{
    public GameObject BuildingTile_Prefab;
    public GameObject RoadTile_Prefab;
    public GameObject HumanUnit_Prefab;
    public GameObject ZombieUnit_Prefab;
}

public struct TileUnitSpawner_Data : IComponentData
{
    public Entity BuildingTile_Prefab;
    public Entity RoadTile_Prefab;
    public Entity HumanUnit_Prefab;
    public Entity ZombieUnit_Prefab;
}

public class TileUnitSpawner_Baker : Baker<TileUnitSpawner_Authoring>
{
    public override void Bake(TileUnitSpawner_Authoring authoring)
    {
        AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TileUnitSpawner_Data
        {
            BuildingTile_Prefab = GetEntity(authoring.BuildingTile_Prefab, TransformUsageFlags.WorldSpace),
            RoadTile_Prefab = GetEntity(authoring.RoadTile_Prefab, TransformUsageFlags.WorldSpace),
            HumanUnit_Prefab = GetEntity(authoring.HumanUnit_Prefab, TransformUsageFlags.Dynamic),
            ZombieUnit_Prefab = GetEntity(authoring.ZombieUnit_Prefab, TransformUsageFlags.Dynamic)
        });
    }
}
