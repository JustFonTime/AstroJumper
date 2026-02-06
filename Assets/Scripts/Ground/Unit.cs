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
        GameObject attackSprite = GenerateAttackSprite(hitBoxPrefab);
        GenerateHitBox(hitBoxPrefab, attackSprite);
    }

    private void GenerateHitBox(GameObject hitBoxPrefab, GameObject attackSprite)
    {
        GameObject hitBox = Instantiate(hitBoxPrefab, transform.position, Quaternion.identity);
        hitBox.transform.parent = attackSprite.transform; 

        HitBox hitBoxInfo = hitBox.GetComponent<HitBox>();
        
        hitBox.transform.position = attackSprite.transform.position;
        hitBox.transform.parent = attackSprite.transform;
    }

    private GameObject GenerateAttackSprite(GameObject hitBoxPrefab)
    {
        GameObject attackSprite = new GameObject("AttackSprite");

        HitBox hitBoxInfo = hitBoxPrefab.GetComponent<HitBox>();
        GroundMovement groundMovement = GetComponent<GroundMovement>();
        Vector3 offsetDirection = groundMovement.isFacingRight ? Vector3.right : Vector3.left;
        Vector3 offset = new Vector3(hitBoxInfo.GetOffset().x * offsetDirection.x, hitBoxInfo.GetOffset().y, hitBoxInfo.GetOffset().z);
        
        SpriteRenderer spriteRenderer = attackSprite.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = hitBoxInfo.GetSprite();
        
        attackSprite.transform.position = transform.position + offset;
        
        if(hitBoxInfo.GetIsMelee())
        {
        attackSprite.transform.parent = transform; 
            
        }
        else
        {
            Projectile projectile = attackSprite.AddComponent<Projectile>();
            projectile.SetDirection(GetComponent<GroundMovement>().isFacingRight ? 1 : -1);
            projectile.SetYValue(attackSprite.transform.position.y);
        }

        return attackSprite;
        //Instantiate(attackSprite, transform.position, Quaternion.identity);
    }
}
