// This script will listen for interactions with units and trigger events
using UnityEngine;
using System;

public class GroundEventManager : MonoBehaviour
{
    public event Action OnUnitDamaged;
    public event Action OnUnitDeath;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UnitDamaged()
    {
        OnUnitDamaged?.Invoke();
    }
    
    public void UnitDeath()
    {
        OnUnitDeath?.Invoke();
    }
}
