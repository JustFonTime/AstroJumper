using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : Unit
{
    [SerializeField] private GameObject hitBoxPrefab; 
    [SerializeField] private InputActionAsset actionsAsset; //this is jsut to test, will move to GroundMovement when it is updated
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string attackActionName = "Attack";
    private InputAction attackAction;
    public static event Action<Unit> onPlayerDeath;
    public static event Action<Unit> onPlayerDamaged;
    
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
    }
    private void OnEnable()
    {
        attackAction.Enable();

        attackAction.performed += OnAttack;
    }

    private void OnDisable()
    {
        attackAction.performed -= OnAttack;

        attackAction.Disable();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        BeginAttack(hitBoxPrefab);
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
        StartCoroutine(DamageEffect(spriteRenderer));
        onPlayerDamaged?.Invoke(this);
    }



    // Update is called once per frame
    void Update()
    {
        
    }


}
