using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class TileSpawner_Authoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject BuildingTile_Prefab;
    public GameObject RoadTile_Prefab;

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(BuildingTile_Prefab);
        referencedPrefabs.Add(RoadTile_Prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var tileSpawnerData = new TileSpawner_Data
        {
            BuildingTile_Prefab = conversionSystem.GetPrimaryEntity(BuildingTile_Prefab),
            RoadTile_Prefab = conversionSystem.GetPrimaryEntity(RoadTile_Prefab),
        };
        dstManager.AddComponentData(entity, tileSpawnerData);
    }
}
