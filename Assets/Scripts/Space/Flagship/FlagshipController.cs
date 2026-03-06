using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(SpaceshipHealthComponent))]
public class FlagshipController : MonoBehaviour
{
    public enum ObjectiveMode
    {
        DestroyShieldsToBoard,
        DestroyShip,
        Protect
    }

    public enum BattleState
    {
        Shielded,
        Vulnerable,
        Boardable,
        Destroyed
    }

    public event Action<BattleState> StateChanged;
    public event Action<FlagshipShieldNode> ShieldNodeDestroyed;

    [Header("Objective")]
    [SerializeField] private ObjectiveMode objectiveMode = ObjectiveMode.DestroyShieldsToBoard;
    [SerializeField] private string boardingSceneName = "";
    [SerializeField] private bool boardWhenShieldsFail = true;

    [Header("Shield Nodes")]
    [SerializeField] private bool autoCollectShieldNodes = true;
    [SerializeField] private List<FlagshipShieldNode> shieldNodes = new List<FlagshipShieldNode>();

    private SpaceshipHealthComponent health;
    private TeamAgent teamAgent;
    private BattleState currentState;

    public SpaceshipHealthComponent Health => health;
    public TeamAgent TeamAgent => teamAgent;
    public ObjectiveMode CurrentObjectiveMode => objectiveMode;
    public BattleState CurrentState => currentState;
    public string BoardingSceneName => boardingSceneName;
    public bool IsBoardable => currentState == BattleState.Boardable;
    public int RemainingShieldNodes
    {
        get
        {
            int alive = 0;
            for (int i = 0; i < shieldNodes.Count; i++)
            {
                if (shieldNodes[i] != null && !shieldNodes[i].IsDestroyed)
                    alive++;
            }

            return alive;
        }
    }

    private void Awake()
    {
        health = GetComponent<SpaceshipHealthComponent>();
        teamAgent = GetComponent<TeamAgent>();

        if (autoCollectShieldNodes)
        {
            shieldNodes.Clear();
            GetComponentsInChildren(true, shieldNodes);
        }

        for (int i = 0; i < shieldNodes.Count; i++)
        {
            if (shieldNodes[i] != null)
                shieldNodes[i].Bind(this);
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
            health.ShieldChanged += OnShieldChanged;
        }

        RefreshState();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.ShieldChanged -= OnShieldChanged;
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        RefreshState();
    }

    private void OnShieldChanged(float current, float max)
    {
        RefreshState();
    }

    public void NotifyShieldNodeDestroyed(FlagshipShieldNode node)
    {
        ShieldNodeDestroyed?.Invoke(node);
        RefreshState();
    }

    private void RefreshState()
    {
        BattleState nextState;

        if (health == null || health.Health <= 0)
        {
            nextState = BattleState.Destroyed;
        }
        else if (health.HasShields)
        {
            nextState = BattleState.Shielded;
        }
        else if (objectiveMode == ObjectiveMode.DestroyShieldsToBoard && boardWhenShieldsFail)
        {
            nextState = BattleState.Boardable;
        }
        else
        {
            nextState = BattleState.Vulnerable;
        }

        if (nextState == currentState) return;
        currentState = nextState;
        StateChanged?.Invoke(currentState);
    }
}
