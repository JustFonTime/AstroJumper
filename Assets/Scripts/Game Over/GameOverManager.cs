using UnityEngine;
<<<<<<< Updated upstream

public class GameOverManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
=======
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
        // Load the last played level
        string lastLevel = PlayerPrefs.GetString("LastLevel", "Level1");
        SceneManager.LoadScene(lastLevel);
    }

    // Called by Level Select button
    public void GoToLevelSelect()
    {
        SceneManager.LoadScene(levelSelectScene);
    }

    // Called by Main Menu button
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}
>>>>>>> Stashed changes
