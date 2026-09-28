using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject howToPlayPanel;
    public GameObject creditsPanel;

    [Header("Audio")]
    public AudioSource backgroundMusic;
    public GameObject audioOnButton;
    public GameObject audioOffButton;

    // =========================
    // HOW TO PLAY
    // =========================

    public void OpenHowToPlay()
    {
        mainMenuPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    public void BackToMainMenu()
    {
        howToPlayPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // =========================
    // CREDITS
    // =========================

    public void OpenCredits()
    {
        mainMenuPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        creditsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // =========================
    // AUDIO
    // =========================

    public void ToggleAudio()
    {
        if (backgroundMusic.isPlaying)
        {
            // Stop music
            backgroundMusic.Pause();

            // Show OFF button
            audioOnButton.SetActive(false);
            audioOffButton.SetActive(true);
        }
        else
        {
            // Start music
            backgroundMusic.Play();

            // Show ON button
            audioOnButton.SetActive(true);
            audioOffButton.SetActive(false);
        }
    }

    // =========================
    // PLAY
    // =========================

    public void PlayGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    // =========================
    // EXIT
    // =========================

    public void ExitGame()
    {
        Application.Quit();
    }
}