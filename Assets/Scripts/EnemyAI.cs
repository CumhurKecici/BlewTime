using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : BombermanUnit
{
    [SerializeField] private int idleStepCounter = 0;

    void Start()
    {
        //Unit has to be initilaized because its derived from abtract class
        InitilaizeUnit();
    }

    void LateUpdate()
    {
        if (IsDead)
        {
            GameManager.Instance.BombermanUnits.Remove(this);
            Destroy(gameObject);
            return;
        }

        Movement();                     //Calling Movement function
        GatherPowerUpIfPossible();      //Calling Gathering ability
    }

    //Definition of how unit is going to perform movement
    public override void Movement()
    {
        if (!GameManager.Instance.IsPlayMode)
        {
            Agent.isStopped = true;
            return;
        }
        else
            Agent.isStopped = false;

        //CheckPoint for enemy movement if enemy has path no need for further works
        if (Agent.hasPath)
            return;

        if (!CurrentNode.IsEndNode)
        {
            FindNode();
            idleStepCounter++;
            if (GetRoad())
            {
                if (IsDangerZone(NextNode.Position))
                {
                    if (!GetEscapeRoad())
                        KeepStationary();
                }
            }
            else
                CurrentNode = new PathNode(transform);

            ExecuteNode();
        }
        else
        {
            CurrentNode = new PathNode(transform);
            FindNode();
            if (GetRoad())
            {
                if (CanTakeCover())
                {
                    UseBomb();
                    idleStepCounter = 0;
                    ExecuteNode();
                }
                else
                    KeepStationary();
            }
            else
                CurrentNode = new PathNode(transform);
        }
    }

    //Path finder method
    public override void FindNode()
    {
        base.FindNode();
        if (CurrentNode.PrevNode != null)
            CurrentNode.OpenRoads.Remove(CurrentNode.PrevNode.Position);
    }

    //How to acquire road
    public override bool GetRoad()
    {
        CurrentNode.RandomizeRoads();
        CurrentNode.PrepareRoads();

        if (CurrentNode.Roads.Count == 0)
        {
            NextNode = null;
            return false;
        }

        PathNode _nextNode = new PathNode();
        _nextNode.Position = CurrentNode.Roads.Dequeue();
        _nextNode.Direction = _nextNode.Position - CurrentNode.Position;
        _nextNode.PrevNode = CurrentNode;

        if (_nextNode.IsPathEnded())
            _nextNode.IsEndNode = true;
        else
            _nextNode.IsEndNode = false;

        NextNode = _nextNode;

        return true;
    }

    //Checks given location is danger zone or not
    private bool IsDangerZone(Vector3 position)
    {
        var colliders = Physics.OverlapSphere(position, 0.3f).ToList();
        IDangerZone dangerZoneTag;
        var dangerZone = colliders.Where(x => x.gameObject.TryGetComponent(out dangerZoneTag)).ToList();//   x.gameObject.name.Contains(GameManager.Instance.DangerZoneTag)).ToList();

        if (dangerZone.Count() != 0)
            return true;

        return false;
    }

    //Checks given location is active danger zone or not
    private bool IsDangerZoneActive(Vector3 position)
    {
        var colliders = Physics.OverlapSphere(position, 0.3f).ToList();
        IDangerZone dangerZoneTag;
        var dangerZones = colliders.Where(x => x.gameObject.TryGetComponent(out dangerZoneTag)).ToList();//   x.gameObject.name.Contains(GameManager.Instance.DangerZoneTag)).ToList();

        foreach (var item in dangerZones)
        {
            var controller = item.GetComponentInParent<BombController>();
            if (controller.IsExploded)
                return true;
        }

        return false;
    }

    //Checks enemy unit can find escape road to get away from danger or not
    private bool GetEscapeRoad()
    {
        //If Current location is safe do nothing
        if (!IsDangerZone(CurrentNode.Position))
        {
            CurrentNode = new PathNode(transform);
            NextNode = null;
            return true;
        }

        List<PathNode> _availableRoads = new List<PathNode>();

        PathNode _startNode = new PathNode();
        _startNode = CurrentNode;
        _startNode.PrevNode = null;
        _startNode.Cost = 0;
        _startNode.IsDangerZone = true;
        _startNode.IsEndNode = false;

        _availableRoads.Add(CurrentNode);

        while (_availableRoads.Where(x => !x.IsEndNode && x.Cost < 11).Count() != 0)
        {
            List<PathNode> tmpList = new List<PathNode>();
            tmpList.AddRange(_availableRoads);

            foreach (var item in tmpList)
            {
                if (!item.IsEndNode)
                {
                    var result = SearchEscapeRoad(item, _availableRoads);
                    _availableRoads.AddRange(result);
                }
            }
        }

        if (_availableRoads.Where(x => !x.IsDangerZone).Count() == 0)
        {
            NextNode = null;
            return false;
        }
        else
        {
            var shortestRoad = _availableRoads.Where(x => !x.IsDangerZone).OrderBy(x => x.Cost).First();

            while (shortestRoad.PrevNode != null)
            {
                NextNode = shortestRoad;
                NextNode.IsEndNode = NextNode.IsPathEnded();
                shortestRoad = shortestRoad.PrevNode;
            }
            return true;
        }
    }

    //Checks enemy can get away from danger after placing a bomb or not
    private bool CanTakeCover()
    {
        List<PathNode> _availableRoads = new List<PathNode>();

        PathNode _startNode = new PathNode();
        _startNode = CurrentNode;
        _startNode.PrevNode = null;
        _startNode.Cost = 0;
        _startNode.IsDangerZone = true;
        _startNode.IsEndNode = false;

        _availableRoads.Add(CurrentNode);

        while (_availableRoads.Where(x => !x.IsEndNode && x.Cost < 11).Count() != 0)
        {
            List<PathNode> tmpList = new List<PathNode>();
            tmpList.AddRange(_availableRoads);

            foreach (var item in tmpList)
            {
                if (!item.IsEndNode)
                {
                    var result = SearchCoverNode(item, _availableRoads);
                    _availableRoads.AddRange(result);
                }
            }
        }

        if (_availableRoads.Where(x => !x.IsDangerZone).Count() == 0)
        {
            NextNode = null;
            return false;
        }
        else
        {
            var shortestRoad = _availableRoads.Where(x => !x.IsDangerZone).OrderBy(x => x.Cost).First();

            while (shortestRoad.PrevNode != null)
            {
                NextNode = shortestRoad;
                NextNode.IsEndNode = NextNode.IsPathEnded();
                shortestRoad = shortestRoad.PrevNode;
            }
            return true;
        }
    }

    //Sub method for searching escape road
    private List<PathNode> SearchEscapeRoad(PathNode node, List<PathNode> roadList)
    {
        List<PathNode> _availableRoads = new List<PathNode>();

        foreach (var item in PathNode.MainDirections)
        {
            if (node.PrevNode != null)
            {
                if (node.PrevNode.Position == node.Position + item)
                    continue;
            }

            if (node.Cost > 11)
                continue;

            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(node.Position, node.Position + item, NavMesh.AllAreas, path);
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                PathNode subNode = new PathNode();
                subNode.Position = node.Position + item;
                subNode.PrevNode = node;
                subNode.Cost += subNode.PrevNode.Cost;
                subNode.IsDangerZone = false;
                subNode.IsEndNode = subNode.IsPathEnded();


                if (IsDangerZoneActive(subNode.Position))
                    subNode.IsDangerZone = true;
                else if (!IsDangerZone(subNode.Position))
                {
                    subNode.IsDangerZone = false;
                    subNode.IsEndNode = true;
                }


                if (IsAreaBombFree(subNode.Position))
                    _availableRoads.Add(subNode);
            }
        }

        roadList.Remove(node);

        return _availableRoads;
    }

    //Sub method for searching cover location
    private List<PathNode> SearchCoverNode(PathNode node, List<PathNode> roadList)
    {
        List<PathNode> _availableRoads = new List<PathNode>();

        List<Vector3> simulatedDangerZones = new List<Vector3>();
        simulatedDangerZones = DangerZoneSimulation();

        foreach (var item in PathNode.MainDirections)
        {
            if (node.PrevNode != null)
            {
                if (node.PrevNode.Position == node.Position + item)
                    continue;
            }

            if (node.Cost > 11)
                continue;

            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(node.Position, node.Position + item, NavMesh.AllAreas, path);
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                PathNode subNode = new PathNode();
                subNode.Position = node.Position + item;
                subNode.PrevNode = node;
                subNode.Cost += subNode.PrevNode.Cost;
                subNode.IsDangerZone = false;
                subNode.IsEndNode = subNode.IsPathEnded();


                if (IsDangerZoneActive(subNode.Position))
                {
                    subNode.IsDangerZone = true;
                    subNode.IsEndNode = true;
                }
                else if (IsDangerZone(subNode.Position))
                {
                    subNode.IsDangerZone = true;
                    subNode.IsEndNode = true;
                }
                else if (simulatedDangerZones.Contains(subNode.Position))
                    subNode.IsDangerZone = true;


                if (IsAreaBombFree(subNode.Position))
                    _availableRoads.Add(subNode);
            }
        }

        roadList.Remove(node);

        return _availableRoads;
    }

    //Simulates placed bomb's danger zone used for enemy units for calculating when they place bomb
    private List<Vector3> DangerZoneSimulation()
    {
        List<(Vector3 Position, Vector3 Direction, int Index)> simulatedDangerZones = new List<(Vector3 Position, Vector3 Direction, int Index)>();
        Vector3 position = transform.position;
        position.y = 0.5f;

        //Directionals
        foreach (var item in PathNode.MainDirections)
        {
            for (int i = 1; i < ExplosionRange + 1; i++)
            {
                var offset = item * i;
                simulatedDangerZones.Add((position + offset, item, i));
            }
        }

        List<Vector3> simulationResults = new List<Vector3>();

        //Center
        simulationResults.Add(position);

        foreach (var direction in PathNode.MainDirections)
        {
            bool autoFalse = false;
            foreach (var item in simulatedDangerZones.Where(x => x.Direction == direction).OrderBy(x => x.Index))
            {
                if (autoFalse)
                    continue;

                var res = Physics.OverlapSphere(item.Position, 0.3f);
                IDestroyable destroyableTag;
                IUndestroyable undestroyableTag;
                if (res.Where(x => x.gameObject.TryGetComponent(out destroyableTag)).Count() != 0)//   x.gameObject.CompareTag("Destroyable")).Count() != 0)
                {
                    simulationResults.Add(item.Position);
                    autoFalse = true;
                }
                else if (res.Where(x => x.gameObject.TryGetComponent(out undestroyableTag)).Count() != 0)// x.gameObject.CompareTag("Undestroyable")).Count() != 0)
                {
                    autoFalse = true;
                }
                else
                    simulationResults.Add(item.Position);
            }
        }


        return simulationResults;
    }
}
