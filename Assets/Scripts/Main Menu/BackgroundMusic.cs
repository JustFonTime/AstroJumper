using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (FindObjectsOfType<BackgroundMusic>().Length > 1)
        {
            Destroy(gameObject);
        }
    }
}
