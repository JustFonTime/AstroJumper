using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class LoadingManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text loadingText; // Reference to the TextMeshProUGUI component for displaying loading text

    [Header("Loading Settings")]
    public string sceneToLoad = "Level Selector 2"; // Name of the scene to load

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        // Start loading the scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);
        asyncLoad.allowSceneActivation = false; // Prevent the scene from activating immediately when loading is complete

        float minLoadingTime = 2f; // Minimum time to show the loading screen
        float elapsedTime = 0f;

        // While the scene is still loading
        while (!asyncLoad.isDone)
        {
            elapsedTime += Time.deltaTime;
            if (asyncLoad.progress >= 0.9f && elapsedTime >= minLoadingTime) // Check if the scene has finished loading (progress is 0.9 when loading is complete)
            {
                asyncLoad.allowSceneActivation = true; // Allow the scene to activate
            }
            yield return null;
        } 
    }
}
