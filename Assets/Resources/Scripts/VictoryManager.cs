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

    [Header("Blur Effect")]
    [SerializeField] private VictoryBackgroundBlur backgroundBlur;

    [Header("Dance Script Reference")]
    [SerializeField] private SproutDanceButton sproutDance;

    private bool victoryTriggered;

    private void Start()
    {
        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }
    }

    public void TriggerVictorySequence()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;
        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        // Pause every other source and keep only the dance music playing.
        if (danceMusic != null)
            danceMusic.ignoreListenerPause = true;

        AudioListener.pause = true;
        if (danceMusic != null)
            danceMusic.Play();

        // Keep the modal hidden while the sprout dances.
        float danceDelay = sproutDance != null ? sproutDance.danceDuration : 4f;
        yield return new WaitForSeconds(danceDelay);

        // Capture the game screen BEFORE the modal appears.
        // The background is a still frame, not a live blur.
        if (backgroundBlur != null)
            yield return StartCoroutine(backgroundBlur.CaptureAndBlur());

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (victoryCanvasGroup != null)
                victoryCanvasGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeDuration));
            yield return null;
        }

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
        }
    }

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
