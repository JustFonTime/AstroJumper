using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System;

public class Menu_Manager : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public Slider volumeSlider;
    public TMP_Text volumeText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowMainMenu();

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
        AudioSource bgMusic = GameObject.FindObjectOfType<AudioSource>();
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
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Level Selector 2");
    }

    public void ShowOptionsMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
        creditsPanel.SetActive(false);
    }

    public void ShowCreditsMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }
}
