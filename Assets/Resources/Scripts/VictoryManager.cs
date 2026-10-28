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

    public void TriggerVictorySequence()
    {
        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        // 1. Allow dance music to play even when all other audio is paused
        if (danceMusic != null)
        {
            danceMusic.ignoreListenerPause = true;
        }

        // 2. Pause ALL audio in the game
        AudioListener.pause = true;

        // 3. Play ONLY the dance music
        if (danceMusic != null)
        {
            danceMusic.Play();
        }

        // 4. Wait for the dance animation to finish
        float danceDelay = sproutDance != null ? sproutDance.danceDuration : 4f;
        yield return new WaitForSeconds(danceDelay);

        // 5. Fade in the victory modal
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