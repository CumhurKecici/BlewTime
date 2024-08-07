using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class BombermanUnit : MonoBehaviour
{
    #region Private Variables
    [SerializeField] private NavMeshAgent _agent;                   //NavMeshAgent for moving unit
    [Range(1, 10)]
    [SerializeField] private int _bombLimit = 1;                    //How much active bombs one unit can have
    [SerializeField] private int _activeBombs = 0;                  //Active bombs counter
    [Range(1, 10)]
    [SerializeField] private int _explosionRange = 1;               //How far explosion can reach - originated location not included
    [SerializeField] private GameObject _bombPrefab;                //Bomb game object for creating in runtime
    [SerializeField] private bool _isDead = false;                  //Represents unit is dead or not
    #endregion

    #region Properties           
    public NavMeshAgent Agent { get { return _agent; } }
    public int BombLimit { get { return _bombLimit; } }
    public int ActiveBombs { get { return _activeBombs; } }
    public int ExplosionRange { get { return _explosionRange; } }
    public GameObject BombPrefab { get { return _bombPrefab; } }
    public bool IsDead { get { return _isDead; } }
    public PathNode CurrentNode;
    public PathNode NextNode;
    #endregion

    //Sets required components
    public void InitilaizeUnit()
    {
        _agent = GetComponent<NavMeshAgent>();
        CurrentNode = new PathNode(transform);
        NextNode = null;
    }


    //Moves agent based on NextNode
    public void ExecuteNode()
    {
        if (NextNode != null)
        {
            Agent.SetDestination(NextNode.Position);
            CurrentNode = NextNode;
            NextNode = null;
        }
    }

    //Terminates path to avoid execution
    public void KeepStationary()
    {
        CurrentNode = new PathNode(transform);
        NextNode = null;
    }

    //Path finder base method looks for all available paths
    public virtual void FindNode()
    {
        CurrentNode.PrepareForNewCalculation();

        foreach (var item in CurrentNode.DirectionList)
        {
            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(CurrentNode.Position, CurrentNode.Position + item, NavMesh.AllAreas, path);
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                if (IsAreaBombFree(CurrentNode.Position + item))
                    CurrentNode.OpenRoads.Add(CurrentNode.Position + item);
            }
        }
    }

    //Adds bomb in scene
    public GameObject UseBomb()
    {
        //Checks unit reached its bomb limit or not
        if (_activeBombs == _bombLimit)
            return null;

        //Checks if location already has bomb or not. Given location can only have one bomb
        if (!IsAreaBombFree())
            return null;

        //Corrects position for aligned placement
        Vector3 pos = Vector3Int.RoundToInt(transform.position);
        pos.y = 0.5f;

        //Generates bomb
        GameObject bomb = Instantiate(_bombPrefab, pos, Quaternion.identity);
        //Sets bomb owner 
        bomb.GetComponent<BombController>().Owner = this;

        //Increase active bombs
        _activeBombs++;

        return bomb;
    }

    //Method for checking current location have bomb or not
    public bool IsAreaBombFree()
    {
        Vector3 bombPosition = Vector3Int.RoundToInt(transform.position);
        bombPosition.y = 0.5f;

        var checkResult = Physics.OverlapSphere(bombPosition, 0.3f);
        if (checkResult.Where(x => CompareTag(x.gameObject, typeof(IBomb)) == true).Count() == 0) //  x.gameObject.CompareTag("Bomb")).Count() == 0)
            return true;

        return false;
    }

    //Method for checking given location have bomb or not
    public bool IsAreaBombFree(Vector3 bombPosition)
    {
        var checkResult = Physics.OverlapSphere(bombPosition, 0.3f);
        if (checkResult.Where(x => CompareTag(x.gameObject, typeof(IBomb)) == true).Count() == 0) // x.gameObject.CompareTag("Bomb")).Count() == 0) 
                                                                                                  //if (checkResult.Where(x => x.gameObject.CompareTag("Bomb")).Count() == 0)
            return true;

        return false;
    }

    //Method for calling after bomb explode so unit can place a bomb againg
    public void LoadBomb()
    {
        if (_activeBombs - 1 >= 0)
            _activeBombs--;
    }

    //Method for checking power ups to collect if there is any
    public void GatherPowerUpIfPossible()
    {
        //Position correction for accurate check
        Vector3 pos = transform.position;
        pos.y = 0.5f;

        //Gather colliders in given position
        Collider[] result = Physics.OverlapBox(pos, new Vector3(0.1f, 0.1f, 0.1f));
        IPowerUp powerUpTag;
        if (result.Any(x => x.gameObject.TryGetComponent(out powerUpTag)))//x.gameObject.CompareTag("Powerup")))
        {
            //Get powerup game object
            GameObject powerupObject = result.Where(x => x.gameObject.TryGetComponent(out powerUpTag)).First().gameObject; //x.gameObject.CompareTag("Powerup")).First().gameObject;
            //Get powerup type
            var powerupType = powerupObject.GetComponent<PowerupController>().powerupType;
            //Destroy game object which contains powerup
            Destroy(powerupObject);
            //Apply powerup benefits
            ApplyPowerUp(powerupType);
        }
    }

    //Method for applying powerup benefits
    public void ApplyPowerUp(PowerupType powerupType)
    {
        //Does works based on powerup type
        switch (powerupType)
        {
            case PowerupType.ExtraBomb:
                if (_bombLimit + 1 <= 10)
                    _bombLimit++;
                break;
            case PowerupType.ExtraRange:
                if (_explosionRange + 1 <= 10)
                    _explosionRange++;
                break;
        }
    }

    //Method for calling when unit is dead
    public void Dead()
    {
        _isDead = true;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false;
    }

    bool CompareTag(GameObject gameObject, Type tag)
    {
        Component result;
        return gameObject.TryGetComponent(tag, out result);
    }

    #region Abstract Methods
    public abstract void Movement();        //Abstract method for defining how unit moves
    public abstract bool GetRoad();         //Abstract method for defining huw unit acquires next location to move
    #endregion

    //Node class for any path calculation
    public class PathNode
    {
        public Vector3 Position = Vector3.zero;
        public PathNode PrevNode = null;
        public Vector3 Direction = Vector3.zero;
        public int Cost = 1;
        public bool IsEndNode = false;
        public bool IsDangerZone = false;
        public bool IsRoadsReady = false;

        public List<Vector3> OpenRoads = new List<Vector3>();
        public Queue<Vector3> Roads = new Queue<Vector3>();

        public Vector3[] DirectionList = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public static Vector3[] MainDirections = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public PathNode() { }

        public PathNode(Transform transform)
        {
            Position = Vector3Int.RoundToInt(transform.position);
            Position.y = 0.5f;
        }

        public void LimitMoveDirections(Vector3[] directionList) => DirectionList = directionList;

        public void PrepareForNewCalculation()
        {
            OpenRoads.Clear();
            Roads.Clear();
            IsRoadsReady = false;
        }

        public void PrepareRoads()
        {
            if (!IsRoadsReady)
            {
                IsRoadsReady = true;
                for (int i = 0; i < OpenRoads.Count; i++)
                    Roads.Enqueue(OpenRoads[i]);
            }
        }

        public void RandomizeRoads() => OpenRoads = OpenRoads.OrderBy(x => UnityEngine.Random.Range(0, 100)).ToList();

        public bool IsPathEnded()
        {
            List<Vector3> tmpRoads = new List<Vector3>();

            foreach (var item in DirectionList)
            {
                NavMeshPath path = new NavMeshPath();
                NavMesh.CalculatePath(Position, Position + item, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete)
                    tmpRoads.Add(Position + item);
            }

            tmpRoads.Remove(PrevNode.Position);

            if (tmpRoads.Count == 0)
                return true;

            return false;
        }

    }


}
