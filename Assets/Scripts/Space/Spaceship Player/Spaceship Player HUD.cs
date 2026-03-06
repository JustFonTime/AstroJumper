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

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<SpaceshipHealthComponent>();
            playerMovement = player.GetComponent<SpaceshipMovement>();
        }

        if (EnemySpaceshipSpawner.Instance != null)
        {
            EnemySpaceshipSpawner.Instance.OnWaveChanged += SetWave;
            EnemySpaceshipSpawner.Instance.OnAliveEnemiesChanged += SetAliveEnemies;
            EnemySpaceshipSpawner.Instance.AllWavesCompleted += HandleAllWavesCompleted;
        }
    }

    private void OnDestroy()
    {
        if (EnemySpaceshipSpawner.Instance != null)
        {
            EnemySpaceshipSpawner.Instance.OnWaveChanged -= SetWave;
            EnemySpaceshipSpawner.Instance.OnAliveEnemiesChanged -= SetAliveEnemies;
            EnemySpaceshipSpawner.Instance.AllWavesCompleted -= HandleAllWavesCompleted;
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
