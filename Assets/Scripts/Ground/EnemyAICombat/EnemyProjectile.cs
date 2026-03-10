using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private float knockbackVerticalForce = 2f;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    // Set by EnemyProjectilePool when fired
    private EnemyProjectilePool pool;
    private Rigidbody2D rb;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Projectiles shouldn't be affected by gravity
        rb.gravityScale = 0f;
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (Time.time - spawnTime >= maxLifetime)
            ReturnToPool();
    }

    /// by EnemyProjectilePool to fire the bullet.
    public void Fire(Vector2 velocity, EnemyProjectilePool ownerPool)
    {
        pool = ownerPool;
        rb.linearVelocity = velocity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to damage a Unit on whatever we hit
        Unit unit = other.GetComponent<Unit>();
        if (unit != null)
        {
            unit.TakeDamage(damage, knockbackForce, knockbackVerticalForce, transform.position);
        }

        // Return on any solid hit 
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        rb.linearVelocity = Vector2.zero;
        if (pool != null)
            pool.Return(gameObject);
        else
            gameObject.SetActive(false); 
        // fallback if pool ref is lost
    }
}