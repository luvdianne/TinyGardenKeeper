using UnityEngine;

public class PauseMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void PauseGame()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);
            
        Time.timeScale = 0f; 
        
        // Globally pause all sounds in the scene
        AudioListener.pause = true; 
    }

    public void ResumeGame()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
            
        Time.timeScale = 1f; 
        
        // Globally resume all sounds
        AudioListener.pause = false; 
    }
}