using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpaceshipPlayerHUD : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider boostSlider;
    [SerializeField] private Slider shieldSlider;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI aliveEnemiesText;

    private GameObject player;
    private SpaceshipHealthComponent playerHealth;
    private SpaceshipMovement playerMovement;

    private FleetSpawner fleetSpawner;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<SpaceshipHealthComponent>();
            playerMovement = player.GetComponent<SpaceshipMovement>();
        }

        if (waveText != null)
            waveText.text = "Flagship Battle";

        fleetSpawner = FleetSpawner.Instance;
        if (fleetSpawner != null)
        {
            fleetSpawner.OnAliveEnemiesChanged += SetAliveEnemies;
            SetAliveEnemies(fleetSpawner.AliveTrackedEnemies);
        }
    }

    private void OnDestroy()
    {
        if (fleetSpawner != null)
        {
            fleetSpawner.OnAliveEnemiesChanged -= SetAliveEnemies;
            fleetSpawner = null;
        }
    }

    private void Update()
    {
        if (playerHealth != null && playerMovement != null)
        {
            SetHealth();
            SetBoost();
            SetShield();
        }
    }

    public void SetHealth()
    {
        float health = playerHealth.Health;
        float maxHealth = Mathf.Max(1f, playerHealth.MaxHealth);
        healthSlider.value = health / maxHealth;
    }

    public void SetBoost()
    {
        float boost = playerMovement.CurrentBoost;
        float maxBoost = Mathf.Max(1f, playerMovement.MaxBoost);
        boostSlider.value = boost / maxBoost;
    }

    public void SetShield()
    {
        float shield = playerHealth.Shield;
        float maxShield = Mathf.Max(1f, playerHealth.MaxShield);
        shieldSlider.value = shield / maxShield;
    }

    public void SetAliveEnemies(int aliveEnemies)
    {
        if (aliveEnemiesText != null)
            aliveEnemiesText.text = "Enemies Left: " + aliveEnemies.ToString();
    }
}
