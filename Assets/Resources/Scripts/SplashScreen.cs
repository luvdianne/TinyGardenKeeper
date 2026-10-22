using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [Header("Loading Time")]
    public float loadingTime = 5f;

    void Start()
    {
        StartCoroutine(LoadMainMenu());
    }

    IEnumerator LoadMainMenu()
    {
        yield return new WaitForSeconds(loadingTime);

        SceneManager.LoadScene("MainMenu");
    }
}