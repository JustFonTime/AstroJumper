using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage; // health bar fill image
    [SerializeField] private Text healthText; // don't have this working yet, but will be used to show current health as text if we want to add that later

    [Header("Settings")]
    [SerializeField] private int maxHealth = 100;

    private void OnEnable()
    {
        Player.onPlayerDamaged += OnPlayerDamaged;
    }

    private void OnDisable()
    {
        Player.onPlayerDamaged -= OnPlayerDamaged;
    }

    private void OnPlayerDamaged(Unit unit)
    {
        UpdateBar(unit.Health);
    }

    private void UpdateBar(int currentHealth)
    {
        float fillAmount = Mathf.Clamp01((float)currentHealth / maxHealth);
        fillImage.fillAmount = fillAmount;

        if (healthText != null)
            healthText.text = $"{currentHealth} / {maxHealth}";
    }
}