using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpaceshipHealthComponent : MonoBehaviour, ISpaceDamagable
{
    [SerializeField] private bool isPlayer;
    [SerializeField] private PlayerUpgradeState playerUpgradeState;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    private Collider2D _collider2D;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float maxShileds = 100;
    [SerializeField] private float currentShields = 100;
    [SerializeField] private bool canRechargeShield = true;
    [SerializeField] private float rechargeShieldDelay = 2f;
    [SerializeField] private float rechargeShieldRatePerHalfSecond = 5f;

    //For the player hud
    public int Health
    {
        get { return currentHealth; }
    }

    public int MaxHealth
    {
        get { return maxHealth; }
    }


    void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
    }

    void Start()
    {
        if (isPlayer)
        {
            maxShileds += playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxShields);
            maxHealth += (int)(playerUpgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxHealth));
        }
        else
        {
            maxHealth = shipProfile.maxHealth;
        }

        currentHealth = maxHealth;
        currentShields = maxShileds;
    }


    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (isPlayer)
        {
            ShieldDamage(damage);
        }
        else
        {
            RawDamage(damage);
        }
    }

    private void ShieldDamage(int damage)
    {
        //furst check if we have shields
        if (currentShields > 0)
        {
            currentShields -= damage;
            currentShields = Mathf.Clamp(currentShields, 0, MaxHealth);
        }
        else
        {
            RawDamage(damage);
        }

        //Stop it cause we took damage and needa reset the timer 
        StopCoroutine(ShieldRechargeCheck());

        //But then start the countdown from the top
        StartCoroutine(ShieldRechargeCheck());
    }

    private void RawDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
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
        while (canRechargeShield)
        {
            currentShields += rechargeShieldRatePerHalfSecond;
            currentShields = Mathf.Clamp(currentShields, 0, maxShileds);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void Die()
    {
        var pooled = this.GetComponent<PooledEnemy>();
        if (pooled != null) pooled.Despawn();
        else Destroy(this);
    }
}