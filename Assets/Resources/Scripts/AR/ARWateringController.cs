using UnityEngine;
using UnityEngine.UI;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Controls the on-demand AR Watering Can.
    /// Only visible while watering; plays pouring animation and bubbling audio over the pot, then hides.
    /// </summary>
    public class ARWateringController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Watering Can root GameObject (hidden by default).")]
        [SerializeField] private GameObject wateringCanRoot;

        [Tooltip("Animator on the watering can.")]
        [SerializeField] private Animator wateringAnimator;

        [Tooltip("Bubbling water audio source.")]
        [SerializeField] private AudioSource waterAudioSource;

        [Tooltip("Optional UI button toggling watering.")]
        [SerializeField] private Button waterButton;

        [Tooltip("Optional text element inside the water button.")]
        [SerializeField] private Text buttonLabel;

        [Header("State")]
        private bool isWatering = false;

        public bool IsWatering => isWatering;

        private void Awake()
        {
            if (wateringCanRoot != null)
                wateringCanRoot.SetActive(false);

            if (waterButton != null)
            {
                waterButton.onClick.AddListener(ToggleWatering);
            }

            if (buttonLabel == null && waterButton != null)
            {
                buttonLabel = waterButton.GetComponentInChildren<Text>();
            }

            UpdateButtonUI();
        }

        public void ToggleWatering()
        {
            SetWatering(!isWatering);
        }

        public void SetWatering(bool active)
        {
            isWatering = active;

            if (wateringCanRoot != null)
            {
                wateringCanRoot.SetActive(active);
            }

            if (wateringAnimator != null)
            {
                wateringAnimator.SetBool("IsWatering", active);
            }

            if (waterAudioSource != null)
            {
                if (active)
                {
                    waterAudioSource.loop = true;
                    if (!waterAudioSource.isPlaying)
                        waterAudioSource.Play();
                }
                else
                {
                    waterAudioSource.Stop();
                }
            }

            UpdateButtonUI();
        }

        private void UpdateButtonUI()
        {
            if (buttonLabel != null)
            {
                buttonLabel.text = isWatering ? "STOP" : "WATER";
            }
        }

        private void OnDisable()
        {
            if (isWatering)
            {
                SetWatering(false);
            }
        }
    }
}
