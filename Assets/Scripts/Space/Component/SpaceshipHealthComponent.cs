using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpaceshipHealthComponent : MonoBehaviour
{
    private Collider2D _collider2D;
    [SerializeField] private int health = 100;
    [SerializeField] private int maxHealth = 100;

    public int Health
    {
        get { return health; }
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
        health = maxHealth;
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Projectile"))
        {
            TakeDamage(10);
            Destroy(collision.gameObject);
        }
    }

    public void Heal(int amount)
    {
        health += amount;
        health = Mathf.Clamp(health, 0, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        health = Mathf.Clamp(health, 0, maxHealth);
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(this.gameObject);
    }
}