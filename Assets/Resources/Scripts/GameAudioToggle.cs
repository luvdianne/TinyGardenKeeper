using UnityEngine;

public class GameAudioToggle : MonoBehaviour
{
    [Header("Audio UI Buttons")]
    public GameObject audioOnButton;
    public GameObject audioOffButton;

    private bool isMuted = false;

    private void Start()
    {
        // Check current global volume to set initial button states
        isMuted = AudioListener.volume == 0f;
        UpdateButtonVisuals();
    }

    public void ToggleAudio()
    {
        isMuted = !isMuted;
        
        // Mutes or unmutes ALL audio in the scene
        AudioListener.volume = isMuted ? 0f : 1f;
        
        UpdateButtonVisuals();
    }

    private void UpdateButtonVisuals()
    {
        if (audioOnButton != null) audioOnButton.SetActive(!isMuted);
        if (audioOffButton != null) audioOffButton.SetActive(isMuted);
    }
}