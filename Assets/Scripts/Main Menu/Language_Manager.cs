using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using Unity.VisualScripting;

public class Language_Manager : MonoBehaviour
{
    [Header("Language Buttons")]
    public Button englishButton;
    public Button koreanButton;
    public Button chineseButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (englishButton != null)
        {
            englishButton.onClick.AddListener(() => SetLanguage("en"));
        }
        if (koreanButton != null)
        {
            koreanButton.onClick.AddListener(() => SetLanguage("ko"));
        }
        if (chineseButton != null)
        {
            chineseButton.onClick.AddListener(() => SetLanguage("zh-Hans"));
        }
    }

    void SetLanguage(string localeCode)
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;

            PlayerPrefs.SetString("SelectedLanguage", localeCode);
            Debug.Log("Language set to: " + localeCode);
        }
        else
        {
            Debug.LogWarning("Locale not found: " + localeCode);
        }
    }
}
