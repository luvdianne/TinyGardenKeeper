using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Type the exact name of your main menu scene here.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    // Call this method from the UI Button's OnClick event
    public void LoadMainMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}