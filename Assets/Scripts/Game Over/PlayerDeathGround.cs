using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeathGround : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private string gameOverScene = "GameOver";

    private void OnEnable()
    {
        Player.onPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        Player.onPlayerDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath(Unit unit)
    {
        SceneManager.LoadScene(gameOverScene);
    }
}
