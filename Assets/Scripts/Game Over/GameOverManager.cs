using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject gameOverPanel;

    [Header("Buttons")]
    public string levelSelectScene = "LevelSelector";
    public string mainMenuScene = "MainMenu";

    void Start()
    {
        // Make sure game over panel is visible
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        
        // Unpause the game if it was paused
        Time.timeScale = 1f;
    }

    // Called by Retry button
    public void RetryLevel()
    {
        SceneLoader.Instance.LoadNextScene("Tutorial Ground");
    }

    // Called by Level Select button
    public void GoToLevelSelect()
    {
        SceneLoader.Instance.LoadNextScene("Level Selector 2");
    }

    // Called by Main Menu button
    public void GoToMainMenu()
    {
        SceneLoader.Instance.LoadNextScene("Menus");
    }
}