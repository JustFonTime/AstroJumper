using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FlagshipShieldNode : MonoBehaviour, ISpaceDamagable
{
    public event Action<FlagshipShieldNode> Destroyed;

    [Header("Node Health")]
    [SerializeField] private int maxHealth = 40;
    [SerializeField] private int currentHealth = 40;

    [Header("Flagship Shield Effect")]
    [SerializeField] private float shieldDamageOnDestroy = 40f;
    [SerializeField] private float shieldPercentDamageOnDestroy = 0.1f;
    [SerializeField] private GameObject destroyedVisual;
    [SerializeField] private GameObject activeVisual;

    private FlagshipController flagship;

    public bool IsDestroyed => currentHealth <= 0;
    public FlagshipController Flagship => flagship;

    private void OnEnable()
    {
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        SetVisualState(true);
    }

    public void Bind(FlagshipController owner)
    {
        flagship = owner;
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
