using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class Menu_Manager : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;

    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public Slider volumeSlider;
    public TMP_Text volumeText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        optionsPanel.SetActive(false);
    }

    public void ShowOptionsMenu()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }
}
