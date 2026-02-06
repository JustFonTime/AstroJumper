using UnityEngine;

public class HitBox : MonoBehaviour
{
    [Header("HitBox Settings")]
    [SerializeField] private string hitBoxName = "BaseHitBox";
    [SerializeField] private int damage = 10;
    [SerializeField] private bool isPermanent = false; // for hitboxes you attach to the enemy itself
    [SerializeField] private float duration = 1f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private bool isMelee = true; 
    [SerializeField] private LayerMask targetLayer; // which layer the hitbox should interact with (player, enemy, etc.)
    [SerializeField] private Vector3 offset = new Vector3(1f, 0f, 0f); // offset to tell where the hitbox should be based on the parent object
    private Collider2D hitBoxCollider;
    [SerializeField] private float currentHitboxActiveDurration = 0f; // how long has the hitbox out


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hitBoxCollider = GetComponent<Collider2D>();
        if (hitBoxCollider == null)
        {
            Debug.LogError("HitBox: No Collider2D found on the GameObject.");
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        // Update the time the hitbox has been out for
        currentHitboxActiveDurration += Time.deltaTime;
        if (currentHitboxActiveDurration > duration && !isPermanent)
        {
            Destroy(gameObject);
        }

        // Update the position of the hitbox based on the parent object and the offset
        
        //transform.position = transform.parent.position + offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        print("HitBox: Trigger entered by " + other.name);
        Unit unit = other.GetComponent<Unit>();
        if (unit != null)
        {
            unit.TakeDamage(damage);
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

}
