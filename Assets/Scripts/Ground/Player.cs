using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : Unit
{
    [SerializeField] private GameObject hitBoxPrefab; 
    [SerializeField] private InputActionAsset actionsAsset; //this is jsut to test, will move to GroundMovement when it is updated
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string attackActionName = "Attack";
    private InputAction attackAction;
    
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
        // spawn hitbox, bad way of doing it will update later
        print("Player: Attack action performed.");
        Vector3 hitBoxPosition = transform.position + new Vector3(1f, 0f, 0f);
        GameObject hitBox = Instantiate(hitBoxPrefab, hitBoxPosition, Quaternion.identity);
        hitBox.transform.parent = transform; 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
