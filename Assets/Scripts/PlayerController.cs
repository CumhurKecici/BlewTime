using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : BombermanUnit
{
    [SerializeField] private InputActionReference m_movementAction;
    [SerializeField] private InputActionReference m_useBombAction;

    void Start()
    {
        //Unit has to be initilaized because its derived from abtract class
        InitilaizeUnit();
        EnableInputActions();
    }

    void Update()
    {
        if (IsDead)
        {
            MainMenuController.Instance.ShowLoseScreen();
            return;
        }
        else if (GameManager.Instance.BombermanUnits.Count == 1 && GameManager.Instance.IsPlayMode)
        {
            MainMenuController.Instance.ShowWinScreen();
            return;
        }

        Movement();
        GatherPowerUpIfPossible();

        if (m_useBombAction.ToInputAction().WasPressedThisFrame())
            UseBomb();
    }

    private void EnableInputActions()
    {
        if (!m_movementAction.ToInputAction().enabled)
            m_movementAction.ToInputAction().Enable();
        if (!m_useBombAction.ToInputAction().enabled)
            m_useBombAction.ToInputAction().Enable();
    }

    public override void Movement()
    {
        if (!GameManager.Instance.IsPlayMode)
        {
            Agent.isStopped = true;
            return;
        }
        else
            Agent.isStopped = false;

        if (Agent.hasPath)
            return;

        FindNode();
        if (GetRoad())
            ExecuteNode();
    }

    public override void FindNode()
    {
        var direction = m_movementAction.ToInputAction().ReadValue<Vector2>();

        var DirectionList = new List<Vector3>();

        if (direction.sqrMagnitude == 0)
            return;

        if (direction.x != 0) DirectionList.Add(direction.x > 0 ? Vector3.right : Vector3.left);
        if (direction.y != 0) DirectionList.Add(direction.y > 0 ? Vector3.forward : Vector3.back);

        CurrentNode.LimitMoveDirections(DirectionList.ToArray());
        base.FindNode();
    }

    public override bool GetRoad()
    {
        CurrentNode.PrepareRoads();

        if (CurrentNode.Roads.Count == 0)
        {
            NextNode = null;
            return false;
        }

        PathNode _nextNode = new PathNode();

        if (CurrentNode.Roads.Count > 1)
        {
            _nextNode.Position = CurrentNode.Roads.Dequeue();
            if (_nextNode.Position - CurrentNode.Position == CurrentNode.Direction)
                _nextNode.Position = CurrentNode.Roads.Dequeue();
        }
        else
            _nextNode.Position = CurrentNode.Roads.Dequeue();

        _nextNode.Direction = _nextNode.Position - CurrentNode.Position;
        _nextNode.PrevNode = CurrentNode;
        CurrentNode.Roads.Clear();

        NextNode = _nextNode;

        return true;
    }
}
