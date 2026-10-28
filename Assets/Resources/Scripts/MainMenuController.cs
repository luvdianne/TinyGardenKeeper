using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Type the exact name of your main menu scene here.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    public void LoadMainMenu()
    {
        Time.timeScale = 1f; 
        
        // Ensure audio is unmuted before loading the Main Menu
        AudioListener.pause = false; 
        
        SceneManager.LoadScene(menuSceneName);
    }
}