using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : Unit
{
    
    [SerializeField] private InputActionAsset actionsAsset; //this is jsut to test, will move to GroundMovement when it is updated
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string attackActionName2 = "Attack2";
    private InputAction attackAction;
    private InputAction attackAction2;

    public static event Action<Unit> onPlayerDeath;
    public static event Action<Unit> onPlayerDamaged;
    private bool isAttacking2 = false;
    [SerializeField] private int projectileCount = 0; 
    [SerializeField] private int maxProjectile = 3;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        var map = actionsAsset.FindActionMap(actionMapName, true);
        attackAction = map.FindAction(attackActionName);
        if (attackAction == null)
        {
            Debug.LogError("Player: Attack action not found in the InputActionAsset.");
        }
        else        {
            Debug.Log("Player: Attack action found successfully.");
        }
        attackAction2 = map.FindAction(attackActionName2);
        if (attackAction2 == null)
        {
            Debug.LogError("Player: Attack2 action not found in the InputActionAsset.");
        }
        else
        {
            Debug.Log("Player: Attack2 action found successfully.");
        }
        hitBoxPrefab.GetComponent<HitBox>().attackListIndex = 1;
        hitBoxPrefab2.GetComponent<HitBox>().attackListIndex = 2;
    }
    private void OnEnable()
    {
        attackAction.Enable();
        attackAction.performed += OnAttack;

        attackAction2.Enable();
        attackAction2.performed += OnAttack2;

        HitBox.onDurationOver += OnHitBoxDurationOver;
    }

    private void OnDisable()
    {
        attackAction.performed -= OnAttack;
        attackAction.Disable();

        attackAction2.performed -= OnAttack2;
        attackAction2.Disable();

        HitBox.onDurationOver -= OnHitBoxDurationOver;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (isAttacking)
            return;

        // check for projectile attack
        if(!hitBoxPrefab.GetComponent<HitBox>().GetIsMelee() && projectileCount < maxProjectile)
        {
            projectileCount++;
            BeginAttack(hitBoxPrefab);
            return;
        }
        isAttacking = true;
    }

    private void OnAttack2(InputAction.CallbackContext context)
    {
        if (isAttacking2)
            return;

        // check for projectile attack
        if(!hitBoxPrefab2.GetComponent<HitBox>().GetIsMelee() && projectileCount < maxProjectile)
        {
            projectileCount++;
            BeginAttack(hitBoxPrefab2);
            return;
        }
        isAttacking2 = true;
    }

    public override void TakeDamage(int amount)
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
        onPlayerDamaged?.Invoke(this);
    }

    private void OnHitBoxDurationOver(int attackIndex)
    {
        if(attackIndex == 1)
        {
            isAttacking = false;
            if(!hitBoxPrefab.GetComponent<HitBox>().GetIsMelee())
            {
                projectileCount--;
            }
        }
        else if(attackIndex == 2)
        {
            isAttacking2 = false;
            if(!hitBoxPrefab2.GetComponent<HitBox>().GetIsMelee())
            {
                projectileCount--;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
}
