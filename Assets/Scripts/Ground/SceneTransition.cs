using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered || !collision.CompareTag("Player"))
            return;

        hasTriggered = true;

        Inventory inventory = collision.GetComponent<Inventory>();
        LevelCompletionRewards.BankGroundScrap(inventory);

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadNextScene(sceneToLoad);
            return;
        }

        Debug.LogWarning("SceneLoader.Instance was null during ground level completion. Loading the next scene directly.");
        SceneManager.LoadScene(sceneToLoad);
    }
}
