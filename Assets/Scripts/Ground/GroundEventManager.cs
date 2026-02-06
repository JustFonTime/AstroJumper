// This script will listen for interactions with units and trigger events
using UnityEngine;
using System;
using UnityEngine.UIElements;

public class GroundEventManager : MonoBehaviour
{
    void OnEnable()
    {
        Unit.onDamaged += UnitDamaged;
        Unit.onDeath += UnitDeath;
    }

    void OnDisable()
    {
        Unit.onDamaged -= UnitDamaged;
        Unit.onDeath -= UnitDeath;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UnitDamaged(Unit unit)
    {
        print(unit.UnitName + " was damaged! Remaining health: " + unit.Health);
    }
    
    public void UnitDeath(Unit unit)
    {
        
    }
}
