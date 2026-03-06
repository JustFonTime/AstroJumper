using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlagshipBoardingTrigger : MonoBehaviour
{
    [SerializeField] private FlagshipController flagship;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string overrideBoardingScene = "";
    [SerializeField] private bool requireBoardableState = true;
    [SerializeField] private bool oneShot = true;

    private bool used;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used && oneShot) return;
        if (!other.CompareTag(playerTag)) return;
        if (flagship == null) return;
        if (requireBoardableState && !flagship.IsBoardable) return;

        string sceneName = string.IsNullOrWhiteSpace(overrideBoardingScene)
            ? flagship.BoardingSceneName
            : overrideBoardingScene;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"{name} tried to board a flagship but no boarding scene was assigned.");
            return;
        }

        used = true;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadNextScene(sceneName);
        else
            Debug.LogWarning("SceneLoader.Instance was null when boarding trigger fired.");
    }
}
