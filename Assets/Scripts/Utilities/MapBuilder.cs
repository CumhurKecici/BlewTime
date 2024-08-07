using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.AsyncOperations;

public class MapBuilder : MonoBehaviour
{
    private static MapBuilder m_instance;
    public static MapBuilder Instance { get { return m_instance; } }

    #region Privates
    private GameObject m_undestroyablesContainer;
    private GameObject m_destroyablesContainer;
    private GameObject m_powerupsContainer;
    private Vector3[] m_safeLocations;

    private IList<IResourceLocation> m_destroyableAssets;
    private IList<IResourceLocation> m_undestroyableAssets;
    private IList<IResourceLocation> m_powerupAssets;
    private AsyncOperationHandle objectHandle;
    #endregion

    #region Private Variables - EditorOnly Visible
    [SerializeField] private Vector2Int m_mapSize = new Vector2Int(21, 20);
    [SerializeField] private Vector3 m_mapOrigin = new Vector3(0f, 0.5f, 0f);
    [Min(1)]
    [SerializeField] private int safeRange = 1;

    #endregion

    void Awake()
    {
        m_instance = this;
    }


    void Start()
    {
        CreateContainers();                 //Crates containers for game objects
        GatherObjectPools();                //Fills up object pools for map building
        GetSafeLocations();                 //Generates safe locations for player and ai enemies
        GenerateBoundaries();               //Generates obstacles for limiting game area - generated dynamicly for prototyping it will be premade on final release
        GenerateUndestroyableObjects();     //Generates inner solid obstacles - generated dynamicly for prototyping it will be premade on final release
        GenerateDestroyables();             //Generates interactable game objects
        GameManager.Instance.State = GameState.PlayMode;
    }

    private void CreateContainers()
    {
        //Creating gmaeObject containers
        m_undestroyablesContainer = new GameObject("Undestroyables");
        m_undestroyablesContainer.transform.SetParent(this.transform);

        m_destroyablesContainer = new GameObject("Destroyables");
        m_destroyablesContainer.transform.SetParent(this.transform);

        m_powerupsContainer = new GameObject("Power Ups");
        m_powerupsContainer.transform.SetParent(this.transform);
    }

    private void GatherObjectPools()
    {
        var handle = Addressables.LoadResourceLocationsAsync(new string[] { "Destroyable" }, Addressables.MergeMode.Union);
        m_destroyableAssets = handle.WaitForCompletion();
        handle.Release();

        handle = Addressables.LoadResourceLocationsAsync(new string[] { "Undestroyable" }, Addressables.MergeMode.Union);
        m_undestroyableAssets = handle.WaitForCompletion();
        handle.Release();

        handle = Addressables.LoadResourceLocationsAsync(new string[] { "Power Up" }, Addressables.MergeMode.Union);
        m_powerupAssets = handle.WaitForCompletion();
        handle.Release();
    }

    private void GetSafeLocations()
    {
        //Marks coords for player so they can have breathing room when game starts
        //Builder wont generate any gameobjects on those positions which cna block player or enemy ai movements

        BombermanUnit[] resultList = FindObjectsByType<BombermanUnit>(FindObjectsSortMode.None);
        GameManager.Instance.BombermanUnits = resultList.ToList();
        m_safeLocations = new Vector3[resultList.Length];
        for (int i = 0; i < resultList.Length; i++)
            m_safeLocations[i] = resultList[i].transform.position;
    }

    private void GenerateBoundaries()
    {
        //Left Boundary
        for (int i = 0; i < m_mapSize.y; i++)
            CreateEnviromentObject(EnviromentType.Indestructible, m_mapOrigin + new Vector3(-1, 0, -i));

        //Top boundary
        for (int i = -1; i < m_mapSize.x + 1; i++)
            CreateEnviromentObject(EnviromentType.Indestructible, m_mapOrigin + new Vector3(i, 0, +1));

        //Right boundary
        for (int i = 0; i < m_mapSize.y; i++)
            CreateEnviromentObject(EnviromentType.Indestructible, m_mapOrigin + new Vector3(m_mapSize.x, 0, -i));

        //Bottom boundary
        for (int i = -1; i < m_mapSize.x + 1; i++)
            CreateEnviromentObject(EnviromentType.Indestructible, m_mapOrigin + new Vector3(i, 0, -m_mapSize.y));
    }

    private void GenerateUndestroyableObjects()
    {
        for (int i = 0; i < m_mapSize.x; i++)
        {
            for (int j = 0; j < m_mapSize.y; j++)
            {
                //Creating for new location
                Vector3 offset = Vector3.right + Vector3.back;
                offset.x *= i;
                offset.z *= j;

                if (i % 2 == 1 && j % 2 == 1)
                {
                    int randomDestroyablePrefab = Random.Range(0, m_undestroyableAssets.Count);
                    CreateObject(m_undestroyableAssets[randomDestroyablePrefab], m_mapOrigin + offset, m_undestroyablesContainer.transform);
                }

            }
        }
    }

    private void GenerateDestroyables()
    {

        //Creating Random Destroyable Objects
        for (int i = 0; i < m_mapSize.x; i++)
        {
            for (int j = 0; j < m_mapSize.y; j++)
            {
                //Creating for new location
                Vector3 offset = Vector3.right + Vector3.back;
                offset.x *= i;
                offset.z *= j;

                //If location is a safe location leave empty
                if (IsSafeZone(m_mapOrigin + offset))
                    continue;
                else if (i % 2 != 0 && j % 2 != 0)
                    continue;

                MapBuilder.Instance.CreateEnviromentObject(EnviromentType.Destroyable, m_mapOrigin + offset);
            }
        }
    }

    public void CreateEnviromentObject(EnviromentType enviromentType, Vector3 position)
    {
        switch (enviromentType)
        {
            case EnviromentType.Indestructible:
                CreateObject(m_undestroyableAssets[0], position, m_undestroyablesContainer.transform);
                break;
            case EnviromentType.Destroyable:
                if (Random.Range(0, 100) > 50)
                {
                    int randomDestroyablePrefab = Random.Range(0, m_destroyableAssets.Count);
                    CreateObject(m_destroyableAssets[randomDestroyablePrefab], position, m_destroyablesContainer.transform);
                }
                break;
            case EnviromentType.PowerUp:
                if (Random.Range(0, 100) > 90)
                {
                    int randomUpgradeType = Random.Range(0, m_powerupAssets.Count);
                    CreateObject(m_powerupAssets[randomUpgradeType], position, m_powerupsContainer.transform);
                }
                break;
        }
    }

    private void CreateObject(IResourceLocation resourceLocation, Vector3 position, Transform parent)
    {
        objectHandle = Addressables.LoadAssetAsync<GameObject>(resourceLocation);
        var prefab = (GameObject)objectHandle.WaitForCompletion();
        Instantiate(prefab, position, Quaternion.identity, parent);
        //objectHandle.Release();
    }

    bool IsSafeZone(Vector3 position)
    {
        if (m_safeLocations.Contains(position))
            return true;

        float distance = float.PositiveInfinity;
        Vector3 closestSafeLocation = Vector3.positiveInfinity;

        foreach (var item in m_safeLocations)
        {
            if (Vector3.Distance(item, position) < distance)
            {
                distance = Vector3.Distance(item, position);
                closestSafeLocation = item;
            }
        }

        if (Vector3.Distance(closestSafeLocation, position) <= safeRange)
            return true;

        return false;
    }

}

public enum EnviromentType
{
    Destroyable,
    Indestructible,
    PowerUp
}