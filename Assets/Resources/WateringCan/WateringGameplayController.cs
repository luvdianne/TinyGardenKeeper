
using UnityEngine;

public class WateringGameplayController : MonoBehaviour
{
    [Header("Animation")]
    public Animator wateringAnimator;

    [Tooltip("Use the exact state name from the Animator window.")]
    public string wateringAnimationName = "Model|ModelAction";

    [Header("Sound")]
    public AudioSource wateringSound;

    private bool isWatering = false;

    public void UseWateringCan()
    {
        if (isWatering)
            return;

        if (wateringAnimator == null)
        {
            Debug.LogError("Assign the watering Animator!");
            return;
        }

        isWatering = true;

        wateringAnimator.Play(
            wateringAnimationName,
            0,
            0f
        );

        if (wateringSound != null)
        {
            wateringSound.Stop();
            wateringSound.Play();
        }

        CancelInvoke(nameof(ResetWatering));
        Invoke(nameof(ResetWatering), 1f);
    }

    private void ResetWatering()
    {
        isWatering = false;
    }
}
