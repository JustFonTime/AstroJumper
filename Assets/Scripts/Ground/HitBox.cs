// this script holds info for hitboxes, kinda named it wrong, should be named as attackInfo
// WARNING: Not supposed to used as the visible object, meant to attach to other sprite object
// WARNING: One of the objects which is interacting has to have a rigidbody2D for OnTriggerEnter2D to work
// I might just add a rigidbody2D to the hitbox if more problems appear.
using Unity.VisualScripting;
using UnityEngine;
using System;

public class HitBox : MonoBehaviour
{
    [Header("HitBox Settings")]
    [SerializeField] private string hitBoxName = "BaseHitBox";
    [SerializeField] private int damage = 10;
    [SerializeField] private bool isPermanent = false; // for hitboxes you attach to the enemy itself
    [SerializeField] private float duration = 1f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private bool isMelee = true; 
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private LayerMask targetLayer; // which layer the hitbox should interact with (player, enemy, etc.)
    [SerializeField] private LayerMask ignoreLayer; // which layer the hitbox should ignore 
    [SerializeField] private Vector3 offset = new Vector3(1f, 0f, 0f); // offset to tell where the hitbox should be based on the parent object
    [SerializeField] private Sprite sprite; 
    private Collider2D hitBoxCollider;
    [SerializeField] private float currentHitboxActiveDurration = 0f; // how long has the hitbox out
    [SerializeField] private bool displayHitbox = false;
    
    public static event Action<int> onDurationOver;
    public ProjectilePool projectilePool;
    public int attackListIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hitBoxCollider = GetComponent<Collider2D>();
        if (hitBoxCollider == null)
        {
            Debug.LogError("HitBox: No Collider2D found on the GameObject.");
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (!displayHitbox)
        {
            spriteRenderer.sprite = null;
        }
        else
        {
            spriteRenderer.sprite = sprite;    
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        // Update the time the hitbox has been out for
        currentHitboxActiveDurration += Time.deltaTime;
        if (currentHitboxActiveDurration > duration && !isPermanent)
        {
            DestroyAttack();
        }
        if(!isMelee)
        {
            
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        GameObject otherObject = other.gameObject;
        print("hitbox: Hit " + other.name);
        if ((targetLayer.value & (1 << otherObject.layer)) == 0 || (ignoreLayer.value & (1 << otherObject.layer)) != 0) // Checks if objects layer is in the layer mask, found from https://discussions.unity.com/t/checking-if-a-layer-is-in-a-layer-mask/860331
        {
            print("hit wrong layer, ignoring");
            return;
        }
        Unit unit = other.GetComponent<Unit>();
        if (unit != null)
        {
            unit.TakeDamage(damage);
            if(!isMelee)
            {
                DestroyAttack();

            }
        }
    }

    public bool GetIsMelee()
    {
        return isMelee;
    }

    public Vector3 GetOffset()
    {
        return offset;
    }

    public Sprite GetSprite()
    {
        return sprite;
    }

    private void DestroyAttack()
    {
        onDurationOver?.Invoke(attackListIndex);
        if(!isMelee)
        {
            projectilePool.ReturnProjectile(transform.parent.gameObject);
            resetDuration();
            return;
        }
        Destroy(transform.parent.gameObject); 
        Destroy(gameObject);
    }

    public void setPool(ProjectilePool pool)
    {
        projectilePool = pool;
    }

    private void resetDuration()
    {
        currentHitboxActiveDurration = 0f;
    }
    
    public float GetProjectileSpeed()
    {
        return projectileSpeed;
    }
}
