using Unity.VisualScripting;
using UnityEngine;

public class UIEventManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        Player.onPlayerDamaged += UpdateHealthUI;
    }

    void OnDisable()
    {
        Player.onPlayerDamaged -= UpdateHealthUI;
    }

    private void UpdateHealthUI(Unit unit)
    {
        // Update the health UI based on the unit's current health
        print("Updating health UI for " + unit.UnitName + ". Current health: " + unit.Health);
    }
}
