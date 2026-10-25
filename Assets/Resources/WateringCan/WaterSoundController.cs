
using UnityEngine;
using UnityEngine.UI;

public class WaterSoundController : MonoBehaviour
{
    public AudioSource waterSound;
    public Animator wateringAnimator;
    public Button wateringButton;

    private bool isWatering = false;

    void Start()
    {
        if (wateringButton != null)
        {
            wateringButton.onClick.AddListener(ToggleWatering);
        }

        if (wateringAnimator != null)
        {
            wateringAnimator.SetBool("IsWatering", false);
        }

        if (waterSound != null)
        {
            waterSound.Stop();
            waterSound.loop = true;
        }
    }

    public void ToggleWatering()
    {
        isWatering = !isWatering;

        if (wateringAnimator != null)
        {
            wateringAnimator.SetBool("IsWatering", isWatering);
        }

        if (waterSound != null)
        {
            if (isWatering)
            {
                waterSound.loop = true;
                if (!waterSound.isPlaying)
                    waterSound.Play();
            }
            else
            {
                waterSound.Stop();
            }
        }

        if (wateringButton != null)
        {
            Text buttonText = wateringButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = isWatering ? "STOP" : "WATER";
        }
    }
}
