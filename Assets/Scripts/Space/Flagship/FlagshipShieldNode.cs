using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(TeamAgent))]
public class FlagshipShieldNode : MonoBehaviour, ISpaceDamagable
{
    public event Action<FlagshipShieldNode> Destroyed;

    [Header("Node Health")] [SerializeField]
    private int maxHealth = 200;

    [SerializeField] private int currentHealth = 200;

    [Header("Flagship Shield Effect")] [SerializeField]
    private float shieldDamageOnDestroy = 40f;

    [SerializeField] private float shieldPercentDamageOnDestroy = 0.1f;
    [SerializeField] private GameObject destroyedVisual;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private bool disableColliderOnDestroy = true;

    private FlagshipController flagship;
    private Collider2D nodeCollider;
    private TeamAgent teamAgent;

    public bool IsDestroyed => currentHealth <= 0;
    public FlagshipController Flagship => flagship;

    private void Awake()
    {
        nodeCollider = GetComponent<Collider2D>();
        teamAgent = GetComponent<TeamAgent>();
    }

    private void OnEnable()
    {
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        if (nodeCollider != null)
            nodeCollider.enabled = true;
        if (teamAgent != null)
            teamAgent.enabled = true;
        SetVisualState(true);
    }

    public void Bind(FlagshipController owner)
    {
        flagship = owner;

        if (flagship != null && teamAgent != null && flagship.TeamAgent != null)
            teamAgent.SetTeam(flagship.TeamAgent.TeamId);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDestroyed) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (currentHealth <= 0)
            BreakNode();
    }

    private void BreakNode()
    {
        currentHealth = 0;
        SetVisualState(false);

        if (disableColliderOnDestroy && nodeCollider != null)
            nodeCollider.enabled = false;

        if (teamAgent != null)
            teamAgent.enabled = false;

        if (flagship != null && flagship.Health != null)
        {
            if (shieldDamageOnDestroy > 0f)
                flagship.Health.DrainShields(shieldDamageOnDestroy);

            if (shieldPercentDamageOnDestroy > 0f)
                flagship.Health.DrainShieldPercent(shieldPercentDamageOnDestroy);

            flagship.NotifyShieldNodeDestroyed(this);
        }

        Destroyed?.Invoke(this);
    }

    private void SetVisualState(bool active)
    {
        if (activeVisual != null)
            activeVisual.SetActive(active);

        if (destroyedVisual != null)
            destroyedVisual.SetActive(!active);
    }
}