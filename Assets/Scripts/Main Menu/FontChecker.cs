using UnityEngine;
using TMPro;

public class FontChecker : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Check what fonts you have
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var font in fonts)
        {
            Debug.Log($"Font: {font.name} - Has characters: {font.characterTable.Count}");
        }
    }
}
