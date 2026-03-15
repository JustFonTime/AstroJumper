using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System;
using UnityEngine.Localization.Settings;

public class Menu_Manager : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;
    public GameObject keybindPanel;

    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public Slider volumeSlider;
    public TMP_Text volumeText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowMainMenu();

        if (PlayerPrefs.GetInt("OpenOptionsOnLoad", 0) == 1)
        {
            ShowOptionsMenu();
            PlayerPrefs.SetInt("OpenOptionsOnLoad", 0); // Clear the flag
        }

        string savedLanguage = PlayerPrefs.GetString("SelectedLanguage", "en");
        StartCoroutine(LoadSavedLanguage(savedLanguage));

        if (volumeSlider != null && volumeText != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
            volumeSlider.value = savedVolume;

            UpdateVolumeText(savedVolume);

            volumeSlider.onValueChanged.AddListener(SetMasterVolume);

            SetMasterVolume(savedVolume);
        }
    }

    public void SetMasterVolume(float sliderValue)
    {
        AudioSource bgMusic = FindFirstObjectByType<AudioSource>();
        if (bgMusic != null)
        {
            bgMusic.volume = sliderValue;
        }

        float volumedB = Mathf.Log10(sliderValue) * 20;
        audioMixer.SetFloat("MasterVolume", volumedB);

        UpdateVolumeText(sliderValue);

        PlayerPrefs.SetFloat("MasterVolume", sliderValue);
    }

    void UpdateVolumeText(float sliderValue)
    {
        if (volumeText != null)
        {
            int volumePercent = Mathf.RoundToInt(sliderValue * 100);
            volumeText.text = volumePercent.ToString() + "%";
        }
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        keybindPanel.SetActive(false);
    }

    public void StartGame()
    {
        SceneLoader.Instance.LoadNextScene("Tutorial Ground");
    }

    public void ShowOptionsMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
        creditsPanel.SetActive(false);
        keybindPanel.SetActive(false);
    }

    public void ShowCreditsMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(true);
        keybindPanel.SetActive(false);
    }

    public void ShowKeybindMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        keybindPanel.SetActive(true); 
    }

    System.Collections.IEnumerator LoadSavedLanguage(string savedLanguage)
    {
        yield return LocalizationSettings.InitializationOperation;

        var locale = LocalizationSettings.AvailableLocales.GetLocale(savedLanguage);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            Debug.Log("Loaded saved language: " + savedLanguage);
        }
        else
        {
            Debug.LogWarning("Saved language not found: " + savedLanguage);
        }
    }
}
