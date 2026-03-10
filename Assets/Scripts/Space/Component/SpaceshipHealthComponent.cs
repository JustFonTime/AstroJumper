using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(Collider2D))]
public class SpaceshipHealthComponent : MonoBehaviour, ISpaceDamagable
{
    public event Action<int, int> HealthChanged; // current health, max health
    public event Action<float, float> ShieldChanged; // current shields, max shields

    private Collider2D _collider2D;
    private Coroutine shieldRechargeDelayCoroutine;
    private Coroutine shieldRechargeCoroutine;
    private int baseMaxHealth;
    private float baseMaxShields;

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

    public int Health => currentHealth;
    public int MaxHealth => maxHealth;
    public int Shield => Mathf.RoundToInt(currentShields);
    public int MaxShield => Mathf.RoundToInt(maxShileds);
    public bool HasShields => currentShields > 0.01f;
    public float ShieldRatio => maxShileds <= 0.01f ? 0f : Mathf.Clamp01(currentShields / maxShileds);

    void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
        baseMaxHealth = maxHealth;
        baseMaxShields = maxShileds;
    }

    private void OnEnable()
    {
        StopShieldRechargeCoroutines();
        canRechargeShield = false;

        if (isPlayer)
        {
            float healthUpgrade = playerUpgradeState != null
                ? playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxHealth)
                : 0f;
            float shieldUpgrade = playerUpgradeState != null
                ? playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxShields)
                : 0f;

            maxHealth = baseMaxHealth + (int)healthUpgrade;
            maxShileds = baseMaxShields + shieldUpgrade;
            currentHealth = maxHealth;
            currentShields = maxShileds;
        }
        else if (shipProfile != null)
        {
            maxHealth = shipProfile.maxHealth;
            maxShileds = shipProfile.maxShields;
            currentHealth = maxHealth;
            currentShields = shipProfile.startingShields;
        }
        else
        {
            maxHealth = baseMaxHealth;
            maxShileds = baseMaxShields;
            currentHealth = maxHealth;
            currentShields = maxShileds;
        }

        UpdateShieldVfx();
        HealthChanged?.Invoke(currentHealth, maxHealth);
        ShieldChanged?.Invoke(currentShields, maxShileds);
    }

    private void OnDisable()
    {
        StopShieldRechargeCoroutines();
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

    public void DrainShields(float amount)
    {
        if (amount <= 0f || currentShields <= 0f) return;

        currentShields = Mathf.Clamp(currentShields - amount, 0f, maxShileds);
        UpdateShieldVfx();
        ShieldChanged?.Invoke(currentShields, maxShileds);
    }

    public void DrainShieldPercent(float percent)
    {
        if (percent <= 0f || maxShileds <= 0f) return;
        DrainShields(maxShileds * Mathf.Clamp01(percent));
    }

    private void ShieldDamage(int damage)
    {
        if (currentShields > 0)
        {
            currentShields -= damage;
            currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
            UpdateShieldVfx();
            ShieldChanged?.Invoke(currentShields, maxShileds);
        }
        else
        {
            RawDamage(damage);
        }

        bool canAutoRecharge = isPlayer || IsBuffedByShieldEnemy || (shipProfile != null && shipProfile.canSelfRechargeShields);
        if (canAutoRecharge)
        {
            if (shieldRechargeDelayCoroutine != null)
                StopCoroutine(shieldRechargeDelayCoroutine);

            if (shieldRechargeCoroutine != null)
            {
                StopCoroutine(shieldRechargeCoroutine);
                shieldRechargeCoroutine = null;
            }

            shieldRechargeDelayCoroutine = StartCoroutine(ShieldRechargeCheck());
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
        yield return new WaitForSeconds(rechargeShieldDelay);
        canRechargeShield = true;
        shieldRechargeCoroutine = StartCoroutine(RechargeShield());
        shieldRechargeDelayCoroutine = null;
    }

    IEnumerator RechargeShield()
    {
        UpdateShieldVfx();

        while (canRechargeShield && currentShields < maxShileds)
        {
            currentShields += rechargeShieldRatePerHalfSecond;
            currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
            ShieldChanged?.Invoke(currentShields, maxShileds);
            yield return new WaitForSeconds(0.5f);
        }

        UpdateShieldVfx();
        shieldRechargeCoroutine = null;
    }

    private void Die()
    {
        if (deathSfx != null)
            AudioSource.PlayClipAtPoint(deathSfx, transform.position, deathVolume);

        var pooledFleet = GetComponent<PooledFleetShip>();
        var pooled = GetComponent<PooledEnemy>();
        var pooledTeamate = GetComponent<PooledTeamate>();

        if (pooledFleet != null)
        {
            pooledFleet.Despawn();
        }
        else if (pooled != null)
        {
            pooled.Despawn();
        }
        else if (pooledTeamate != null)
        {
            pooledTeamate.Despawn();
        }
        else if (isPlayer)
        {
            SceneManager.LoadScene("GameOver");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void HealShields(float amount)
    {
        currentShields += amount;
        currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
        UpdateShieldVfx();
        ShieldChanged?.Invoke(currentShields, maxShileds);
    }

    private void StopShieldRechargeCoroutines()
    {
        if (shieldRechargeDelayCoroutine != null)
        {
            StopCoroutine(shieldRechargeDelayCoroutine);
            shieldRechargeDelayCoroutine = null;
        }

        if (shieldRechargeCoroutine != null)
        {
            StopCoroutine(shieldRechargeCoroutine);
            shieldRechargeCoroutine = null;
        }
    }

    private void UpdateShieldVfx()
    {
        if (shieldVFX == null) return;
        shieldVFX.SetActive(currentShields > 0f);
    }
}



