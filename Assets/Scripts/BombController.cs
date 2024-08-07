using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;

public class BombController : MonoBehaviour, IBomb
{
    [SerializeField] private BombermanUnit _owner;                  //Bomb's owner's BombermanUnit component
    [SerializeField] private float _time = 3f;                      //Bomb's timer for explosion
    [SerializeField] private GameObject explosionPrefab;            //Explosion Effect

    private bool _canDamage = false;
    private bool _isExploded = false;
    private int _explosionRange;                                    //Bomb's explosion range
    private List<(GameObject Zone, Vector3 Direction, int Index)> _dangerZones;
    private GameObject _bombBody;
    private Vector3[] _directionList;

    public BombermanUnit Owner                                      //Property for setting owner
    {
        get { return _owner; }
        set                                                         //When owner is setted also set it's game object and explosion range
        {
            _owner = value;                                         //Sets BombermanUnit
            _explosionRange = _owner.ExplosionRange + 1;            //Sets explosion range
        }
    }

    public List<(GameObject Zone, Vector3 Direction, int Index)> DangerZones { get { return _dangerZones; } }
    public bool IsExploded { get { return _canDamage; } }

    void Start()
    {
        _dangerZones = new List<(GameObject Zone, Vector3 Direction, int Index)>();
        _bombBody = transform.Find("Body").gameObject;
        _directionList = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        CreateDangerZones();

        //Invoke("Explode", _time);                                 //Starts bomb's timer for explosion
        StartCoroutine(Explode());
    }

    void Update()
    {
        if (_isExploded)
        {
            var fxList = GetComponentsInChildren<VisualEffect>();
            foreach (var fxitem in fxList)
                fxitem.pause = !GameManager.Instance.IsPlayMode;
        }

        UpdateDangerZoneState();

        /*if (_canDamage)
        {
            foreach (var item in GameManager.Instance.BombermanUnits)
            {
                Vector3 unitPos = Vector3Int.RoundToInt(item.transform.position);
                unitPos.y = 0.5f;

                if (_dangerZones.Where(x => x.Zone.transform.position == unitPos && x.Zone.activeSelf == true).Count() != 0)
                {
                    //EditorApplication.isPaused = true;
                    item.Dead();
                }
            }
        }*/
    }

    void CreateDangerZones()
    {
        Vector3 position = transform.position;
        position.y = 0.5f;

        GameObject centerDangerZone = new GameObject("DangerZone_C", new Type[] { typeof(BoxCollider), typeof(DangerZoneTag) });
        centerDangerZone.transform.position = position;
        centerDangerZone.transform.parent = transform;
        _dangerZones.Add((centerDangerZone, Vector3.zero, 0));

        string[] indicator = new string[] { "F", "R", "B", "L" };
        int counter = 0;
        foreach (var item in _directionList)
        {
            for (int i = 1; i < _explosionRange; i++)
            {
                var offset = item * i;

                GameObject dangerZone = new GameObject("DangerZone_" + indicator[counter], new Type[] { typeof(BoxCollider), typeof(DangerZoneTag) });
                dangerZone.transform.position = position + offset;
                dangerZone.transform.parent = transform;
                _dangerZones.Add((dangerZone, item, i));
            }
            counter++;
        }
    }

    void UpdateDangerZoneState()
    {
        if (_isExploded)
            return;

        foreach (var direction in _directionList)
        {
            bool autoFalse = false;
            foreach (var item in _dangerZones.Where(x => x.Direction == direction).OrderBy(x => x.Index))
            {
                if (autoFalse)
                {
                    item.Zone.SetActive(false);
                    continue;
                }

                var res = Physics.OverlapSphere(item.Zone.transform.position, 0.3f);
                if (res.Where(x => CompareTag(x.gameObject, typeof(IDestroyable))).Count() != 0)//  x.gameObject.CompareTag("Destroyable")).Count() != 0)
                {
                    item.Zone.SetActive(true);
                    autoFalse = true;
                }
                else if (res.Where(x => CompareTag(x.gameObject, typeof(IUndestroyable))).Count() != 0)// x.gameObject.CompareTag("Undestroyable")).Count() != 0)
                {
                    item.Zone.SetActive(false);
                    autoFalse = true;
                }
                else
                    item.Zone.SetActive(true);
            }
        }
    }

    bool CompareTag(GameObject gameObject, Type tag)
    {
        Component result;
        return gameObject.TryGetComponent(tag, out result);
    }

    IEnumerator Explode()
    {
        float timer = 0;
        float duration = _time;
        while (duration >= (timer % 60))
        {
            if (GameManager.Instance.IsPlayMode)
                timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject.GetComponent<BoxCollider>());
        Destroy(_bombBody);
        _isExploded = true;
        _canDamage = true;

        //Creates explosion effect in dangerZones
        foreach (var item in _dangerZones)
        {
            if (!item.Zone.activeSelf)
                continue;

            Instantiate(explosionPrefab, item.Zone.transform.position, Quaternion.identity, transform);

            var res = Physics.OverlapSphere(item.Zone.transform.position, 0.3f);
            if (res.Where(x => CompareTag(x.gameObject, typeof(IDestroyable))).Count() != 0)// x.gameObject.CompareTag("Destroyable")).Count() != 0)
            {
                var obj = res.Where(x => CompareTag(x.gameObject, typeof(IDestroyable))).First().GetComponent<DestroyableController>();
                obj.Blow();
            }
        }

        if (_canDamage)
        {
            foreach (var item in GameManager.Instance.BombermanUnits)
            {
                Vector3 unitPos = Vector3Int.RoundToInt(item.transform.position);
                unitPos.y = 0.5f;

                if (_dangerZones.Where(x => x.Zone.transform.position == unitPos && x.Zone.activeSelf == true).Count() != 0)
                {
                    //EditorApplication.isPaused = true;
                    item.Dead();
                }
            }
        }

        _owner.LoadBomb();
        StartCoroutine(RemoveDangerZone());
    }

    IEnumerator RemoveDangerZone()
    {
        /*float timer = 0;
        float duration = 1.5f;
        while (duration >= (timer % 60))
        {
            if (GameManager.Instance.IsPlayMode)
                timer += Time.deltaTime;
            yield return null;
        }
*/
        _canDamage = false;
        foreach (var item in _dangerZones)
            Destroy(item.Zone);

        StartCoroutine(RemoveEffect());
        yield return null;
    }

    IEnumerator RemoveEffect()
    {
        float timer = 0;
        float duration = 1.5f;
        duration = 3.0f;
        while (duration >= (timer % 60))
        {
            if (GameManager.Instance.IsPlayMode)
                timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

}
