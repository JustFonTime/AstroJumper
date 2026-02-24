using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set;}

    public string SceneToLoad;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        DontDestroyOnLoad(this);
    }

    public void LoadNextScene()
    {
        SceneManager.LoadScene("LoadingScreen");
    }

    public void LoadNextScene(string nextScene)
    {
        SceneToLoad = nextScene;
        SceneManager.LoadScene("LoadingScreen");
    }
}
