using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[RequiresEntityConversion]
public class UnitSpawner_Authoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject ZombieUnit_Prefab;

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(ZombieUnit_Prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var unitSpawnerData = new UnitSpawner_Data
        {
            ZombieUnit_Prefab = conversionSystem.GetPrimaryEntity(ZombieUnit_Prefab),
        };
        dstManager.AddComponentData(entity, unitSpawnerData);
    }
}
