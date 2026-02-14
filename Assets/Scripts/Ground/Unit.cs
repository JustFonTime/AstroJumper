// Unit is the base class for players and enemies.
using UnityEngine;
using System;
using Unity.VisualScripting;
using System.Collections;

public class Unit : MonoBehaviour
{
    [Header("Unit Info")]
    [SerializeField] private string _unitName;
    public string UnitName
    {get; set;}
    [SerializeField] private int _health = 100;
    public int Health
    {
        get { return _health; }
        set { _health = value; }
    }
    [SerializeField] private int _damage = 10;
    public int Damage
    {
        get { return _damage; }
        set { _damage = value; }
    }
    public static event Action<Unit> onDeath;
    public static event Action<Unit> onDamaged;

    protected bool isDamageAnimation = false;
    protected bool isAttacking = false;

    void Start()
    {
        
    }

    public virtual void TakeDamage(int amount)
    {
        print("Taking damage");
        Health -= amount;
        if (Health <= 0)
        {
            Death();
        }
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if(!isDamageAnimation)
            StartCoroutine(DamageEffect(spriteRenderer));
        onDamaged?.Invoke(this);
    }

    public void Death()
    {
        // Eventually add death animation, sound, etc. For now just destroy the game object.
        onDeath?.Invoke(this);
        Destroy(gameObject);
    }

    
    #region Attacking
        
    public void BeginAttack(GameObject hitBoxPrefab)
    {
        CreateAttack(hitBoxPrefab);
        
    }

    private void CreateAttack(GameObject hitBoxPrefab)
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
            projectile.SetDirection(groundMovement.isFacingRight ? 1 : -1);
            projectile.SetYValue(attackSprite.transform.position.y);
        }

        return attackSprite;
    }

    protected IEnumerator DamageEffect(SpriteRenderer spriteRenderer)
    {
        isDamageAnimation = true;
        for (int i = 0; i < 2; i++)
        {
            Color baseColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.35f);
            spriteRenderer.color = baseColor;
            yield return new WaitForSeconds(0.35f);
        }
        isDamageAnimation = false;
    }
    #endregion

}
