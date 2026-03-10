using UnityEngine;
using UnityEngine.SceneManagement;

public class GamePauseTrigger : MonoBehaviour
{
    [Header("Settings")]
    public string pauseSceneName = "PauseMenu";

    private bool isPaused = false;

    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
            SceneManager.LoadScene("PauseMenu", LoadSceneMode.Additive);
            Debug.Log("Game Paused");
        }
    }

    public void ResumeGame()
    {
        if (isPaused)
        {
            SceneManager.UnloadSceneAsync(pauseSceneName);
            Time.timeScale = 1f; 
            isPaused = false;
            Debug.Log("Game Resumed");
        }
    }
}
