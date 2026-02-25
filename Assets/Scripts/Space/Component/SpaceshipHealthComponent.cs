using System;
using System.Collections;
using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class SpaceshipHealthComponent : MonoBehaviour, ISpaceDamagable
{
    public event Action<int, int> HealthChanged; // current health, max health

    private Collider2D _collider2D;

    [SerializeField] private bool isPlayer;
    [Header("Refs")] [SerializeField] private PlayerUpgradeState playerUpgradeState;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    [SerializeField] private GameObject shieldVFX;
    [SerializeField] private AudioClip deathSfx;
    [SerializeField] private float deathVolume = 1f;

    [Tooltip(
        "These will all be overtin by player state or enemy ship profile, just visual indicators to see what the acutaly numbers are")]
    [Header("Health & Shields")]
    [SerializeField]
    private int currentHealth = 100;

    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float maxShileds = 100;
    [SerializeField] private float currentShields = 100;

    [Header("Shield Settings for Player and Self-Recharging Enemies")] [SerializeField]
    private bool canRechargeShield = true;

    [SerializeField] private float rechargeShieldDelay = 2f;
    [SerializeField] private float rechargeShieldRatePerHalfSecond = 5f;

    [Header("Effects")] public bool IsBuffedByShieldEnemy = false;

    //For the player hud
    public int Health
    {
        get { return currentHealth; }
    }

    public int MaxHealth
    {
        get { return maxHealth; }
    }

    public int Shield
    {
        get { return (int)currentShields; }
    }

    public int MaxShield
    {
        get { return (int)maxShileds; }
    }


    void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (isPlayer)
        {
            maxShileds += playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxShields);
            maxHealth += (int)(playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxHealth));
            currentHealth = maxHealth;
            currentShields = maxShileds;
        }
        else
        {
            maxHealth = shipProfile.maxHealth;
            maxShileds = shipProfile.maxShields;
            currentHealth = maxHealth;
            currentShields = shipProfile.startingShields;
            if (shieldVFX != null)
            {
                shieldVFX.SetActive(currentShields > 0);
            }
        }

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }


    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void TakeDamage(int damage)
    {
        ShieldDamage(damage);
    }

    private void ShieldDamage(int damage)
    {
        //furst check if we have shields
        if (currentShields > 0)
        {
            currentShields -= damage;
            currentShields = Mathf.Clamp(currentShields, 0, MaxHealth);
            if (currentShields <= 0 && shieldVFX != null)
            {
                shieldVFX.SetActive(false);
            }
            else if (currentShields > 0 && shieldVFX != null)
            {
                shieldVFX.SetActive(true);
            }
        }
        else
        {
            RawDamage(damage);
        }

        if ((isPlayer) || shipProfile.canSelfRechargeShields || IsBuffedByShieldEnemy)
        {
            //Stop it cause we took damage and needa reset the timer 
            StopCoroutine(ShieldRechargeCheck());

            //But then start the countdown from the top
            StartCoroutine(ShieldRechargeCheck());
        }
    }

    private void RawDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        HealthChanged?.Invoke(currentHealth, MaxHealth);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator ShieldRechargeCheck()
    {
        canRechargeShield = false;
        StopCoroutine(RechargeShield());
        yield return new WaitForSeconds(rechargeShieldDelay);
        canRechargeShield = true;
        StartCoroutine(RechargeShield());
    }

    IEnumerator RechargeShield()
    {
        if (shieldVFX != null)
            shieldVFX.SetActive(true);

        while (canRechargeShield)
        {
            currentShields += rechargeShieldRatePerHalfSecond;
            currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void Die()
    {
        if (deathSfx != null)
            AudioSource.PlayClipAtPoint(deathSfx, transform.position, deathVolume);

        var pooled = this.GetComponent<PooledEnemy>();
        if (pooled != null) pooled.Despawn();
        else Destroy(this.gameObject);
    }


    // This is for the shield giver to heal the enemy without worrying about messing with the shield recharge logic, since it only calls Heal() and doesn't directly modify currentHealth or currentShields, it won't interfere with the recharge timers or logic at all
    public void HealShields(float amount)
    {
        currentShields += amount;
        currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
        if (currentShields > 0 && shieldVFX != null)
        {
            shieldVFX.SetActive(true);
        }
    }
}