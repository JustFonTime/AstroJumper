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

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");

        EnemySpaceshipSpawner.Instance.OnWaveChanged += SetWave;
        EnemySpaceshipSpawner.Instance.OnAliveEnemiesChanged += SetAliveEnemies;
        EnemySpaceshipSpawner.Instance.AllWavesCompleted += () =>
        {
            waveText.text = "All Waves Completed!";
            aliveEnemiesText.text = "";
        };
    }


    private void Update()
    {
        if (player != null)
        {
            SetHealth();
            SetBoost();
            SetShield();
        }
    }

    public void SetHealth()
    {
        float health = player.GetComponent<SpaceshipHealthComponent>().Health;
        float maxHealth = player.GetComponent<SpaceshipHealthComponent>().MaxHealth;
        healthSlider.value = health / maxHealth;
    }

    public void SetBoost()
    {
        float boost = player.GetComponent<SpaceshipMovement>().CurrentBoost;
        float maxBoost = player.GetComponent<SpaceshipMovement>().MaxBoost;
        boostSlider.value = boost / maxBoost;
    }

    public void SetShield()
    {
        float shield = player.GetComponent<SpaceshipHealthComponent>().Shield;
        float maxShield = player.GetComponent<SpaceshipHealthComponent>().MaxShield;
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
}