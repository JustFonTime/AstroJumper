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
    private EnemySpaceshipSpawner legacyEnemySpawner;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<SpaceshipHealthComponent>();
            playerMovement = player.GetComponent<SpaceshipMovement>();
        }

        fleetSpawner = FleetSpawner.Instance;
        if (fleetSpawner != null)
        {
            fleetSpawner.OnWaveChanged += SetWave;
            fleetSpawner.OnAliveEnemiesChanged += SetAliveEnemies;
            fleetSpawner.AllWavesCompleted += HandleAllWavesCompleted;

            SetWave(fleetSpawner.CurrentWave);
            SetAliveEnemies(fleetSpawner.AliveTrackedEnemies);
            return;
        }

        legacyEnemySpawner = EnemySpaceshipSpawner.Instance;
        if (legacyEnemySpawner != null)
        {
            legacyEnemySpawner.OnWaveChanged += SetWave;
            legacyEnemySpawner.OnAliveEnemiesChanged += SetAliveEnemies;
            legacyEnemySpawner.AllWavesCompleted += HandleAllWavesCompleted;

            SetWave(legacyEnemySpawner.CurrentWave);
            SetAliveEnemies(legacyEnemySpawner.AliveEnemies);
        }
    }

    private void OnDestroy()
    {
        if (fleetSpawner != null)
        {
            fleetSpawner.OnWaveChanged -= SetWave;
            fleetSpawner.OnAliveEnemiesChanged -= SetAliveEnemies;
            fleetSpawner.AllWavesCompleted -= HandleAllWavesCompleted;
            fleetSpawner = null;
        }

        if (legacyEnemySpawner != null)
        {
            legacyEnemySpawner.OnWaveChanged -= SetWave;
            legacyEnemySpawner.OnAliveEnemiesChanged -= SetAliveEnemies;
            legacyEnemySpawner.AllWavesCompleted -= HandleAllWavesCompleted;
            legacyEnemySpawner = null;
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

    public void SetWave(int wave)
    {
        waveText.text = "Wave: " + wave.ToString();
    }

    public void SetAliveEnemies(int aliveEnemies)
    {
        aliveEnemiesText.text = "Enemies Left: " + aliveEnemies.ToString();
    }

    private void HandleAllWavesCompleted()
    {
        waveText.text = "All Waves Completed!";
        aliveEnemiesText.text = "";
    }
}
