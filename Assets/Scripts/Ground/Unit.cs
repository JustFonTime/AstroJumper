// Unit is the base class for players and enemies.
using UnityEngine;

public class Unit : MonoBehaviour
{
    // private GroundEventManager groundEventManager;
    [SerializeField] private string name = "Unit";
    [SerializeField] private int health = 100;
    [SerializeField] private int damage = 10;

    void Start()
    {
        // groundEventManager = FindFirstObjectByType<GroundEventManager>();
        // if (groundEventManager == null)
        // {
        //     Debug.LogError("Unit: No GroundEventManager found in the scene.");
        // }

        // groundEventManager.OnUnitDamaged += OnUnitDamaged;
        // groundEventManager.OnUnitDeath += OnUnitDeath;
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        // Eventually add death animation, sound, etc. For now just destroy the game object.
        Destroy(gameObject);
    }

    public void BeginAttack(GameObject hitBoxPrefab)
    {
        GenerateHitBox(hitBoxPrefab);
    }

    private void GenerateHitBox(GameObject hitBoxPrefab)
    {
        GameObject hitBox = Instantiate(hitBoxPrefab, transform.position, Quaternion.identity);
        hitBox.transform.parent = transform; // make the hitbox a child of the unit so it moves with the unit

        HitBox hitBoxInfo = hitBox.GetComponent<HitBox>();
        if (hitBoxInfo.GetIsMelee())
        {
            GroundMovement groundMovement = GetComponent<GroundMovement>();
            Vector3 offsetDirection = groundMovement.isFacingRight ? Vector3.right : Vector3.left; // flip the offset based on facing direction
            Vector3 offset = new Vector3(hitBoxInfo.GetOffset().x * offsetDirection.x, hitBoxInfo.GetOffset().y, hitBoxInfo.GetOffset().z);
            hitBox.transform.position = transform.position + offset;
        }
    }
}
