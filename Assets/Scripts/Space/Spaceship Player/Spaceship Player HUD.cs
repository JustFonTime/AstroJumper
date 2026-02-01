using UnityEngine;
using UnityEngine.UI;

public class SpaceshipPlayerHUD : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider boostSlider;


    private GameObject player;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }


    private void Update()
    {
        if (player != null)
        {
            SetHealth();
            SetBoost();
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
}