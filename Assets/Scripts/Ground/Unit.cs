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
}
