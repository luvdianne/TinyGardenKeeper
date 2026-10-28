using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Type the exact name of your main menu scene here.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    public void LoadMainMenu()
    {
        // 1. Unfreeze time
        Time.timeScale = 1f; 
        
        // 2. Unpause the global audio listener (from the Pause menu)
        AudioListener.pause = false; 
        
        // 3. Reset the global volume back to 100% (from the Mute button)
        AudioListener.volume = 1f; 
        
        // 4. Load the Main Menu
        SceneManager.LoadScene(menuSceneName);
    }
}