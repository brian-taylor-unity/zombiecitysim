using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[RequiresEntityConversion]
public class TileUnitSpawner_Authoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject BuildingTile_Prefab;
    public GameObject RoadTile_Prefab;
    public GameObject HumanUnit_Prefab;
    public GameObject ZombieUnit_Prefab;

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(BuildingTile_Prefab);
        referencedPrefabs.Add(RoadTile_Prefab);
        referencedPrefabs.Add(HumanUnit_Prefab);
        referencedPrefabs.Add(ZombieUnit_Prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var tileUnitSpawnerData = new TileUnitSpawner_Data
        {
            BuildingTile_Prefab = conversionSystem.GetPrimaryEntity(BuildingTile_Prefab),
            RoadTile_Prefab = conversionSystem.GetPrimaryEntity(RoadTile_Prefab),
            HumanUnit_Prefab = conversionSystem.GetPrimaryEntity(HumanUnit_Prefab),
            ZombieUnit_Prefab = conversionSystem.GetPrimaryEntity(ZombieUnit_Prefab)
        };
        dstManager.AddComponentData(entity, tileUnitSpawnerData);
    }
}
