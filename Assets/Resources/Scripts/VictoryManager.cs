using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource backgroundMusic;
    [SerializeField] private AudioSource danceMusic;

    [Header("UI Elements")]
    [SerializeField] private CanvasGroup victoryCanvasGroup;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Dance Script Reference")]
    [SerializeField] private SproutDanceButton sproutDance;

    private void Start()
    {
        // Hide modal at start
        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }
    }

    // Call this method when the sprout reaches 100% growth / Final Stage
    public void TriggerVictorySequence()
    {
        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        // 1. Mute/Pause standard ambient background music
        if (backgroundMusic != null && backgroundMusic.isPlaying)
        {
            backgroundMusic.Pause();
        }

        // 2. Play special dance music
        if (danceMusic != null)
        {
            danceMusic.Play();
        }

        // 3. Trigger the full sprout dance animation
        if (sproutDance != null)
        {
            sproutDance.GrowSprout();
        }

        // Wait for dance animation to finish (e.g. 4 seconds) before opening victory screen
        float danceDelay = sproutDance != null ? sproutDance.danceDuration : 4f;
        yield return new WaitForSeconds(danceDelay);

        // 4. Fade in the dim background overlay and Victory Modal
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (victoryCanvasGroup != null)
            {
                victoryCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            }
            yield return null;
        }

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
        }
    }

    // =========================
    // BUTTON ACTIONS
    // =========================

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        // Reload current active scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        SceneManager.LoadScene("MainMenu");
    }
}