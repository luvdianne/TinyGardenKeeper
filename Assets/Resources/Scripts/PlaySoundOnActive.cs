using UnityEngine;

public class PlaySoundOnActive : MonoBehaviour
{
    private AudioSource audioSource;

    private void Awake()
    {
        // Cache the AudioSource on this object
        audioSource = GetComponent<AudioSource>();
    }

    // OnEnable is automatically called by Unity whenever this GameObject switches from inactive to active
    private void OnEnable()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}