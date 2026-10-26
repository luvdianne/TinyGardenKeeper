using UnityEngine;
using UnityEngine.UI;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Controls the AR Watering Can with a 3.0-second hold-to-water mechanic
    /// and a 10-second cooldown. Spawns water droplets from the spout into the soil.
    /// Shows real-time cooldown countdown and hold progress on the UI button.
    /// </summary>
    public class ARWateringController : MonoBehaviour
    {
        [Header("Hold & Cooldown Config")]
        [Tooltip("Seconds required to hold the WATER button to complete a cycle.")]
        [SerializeField] private float holdDuration = 3.0f;

        [Tooltip("Seconds of cooldown after a completed watering cycle.")]
        [SerializeField] private float cooldownDuration = 10.0f;

        [Header("Scene References")]
        [Tooltip("The visual root or Model GameObject of the watering can (hidden by default).")]
        [SerializeField] private GameObject wateringCanRoot;

        [Tooltip("Animator on the watering can.")]
        [SerializeField] private Animator wateringAnimator;

        [Tooltip("Water pouring/bubbling audio source.")]
        [SerializeField] private AudioSource waterAudioSource;

        [Tooltip("Water droplet shower emitting from spout.")]
        [SerializeField] private ParticleSystem waterParticleSystem;

        [Tooltip("Soil splash effect where droplets hit.")]
        [SerializeField] private ParticleSystem soilSplashParticleSystem;

        [Header("UI References")]
        [SerializeField] private Button waterButton;
        [SerializeField] private Text buttonLabel;
        [SerializeField] private Image buttonFillImage;

        [Header("Runtime State")]
        [SerializeField] private bool isHolding = false;
        [SerializeField] private float holdTimer = 0f;
        [SerializeField] private float cooldownTimer = 0f;
        [SerializeField] private bool isWateringActive = false;

        public bool IsWatering => isWateringActive;
        public bool IsOnCooldown => cooldownTimer > 0f;
        public float CooldownRemaining => cooldownTimer;
        public float HoldProgress => isHolding ? Mathf.Clamp01(holdTimer / holdDuration) : 0f;

        public event System.Action OnWaterCycleCompleted;

        private void Awake()
        {
            if (buttonLabel == null && waterButton != null)
            {
                buttonLabel = waterButton.GetComponentInChildren<Text>();
            }

            SetCanVisible(false);
            ResetButtonUI();
        }

        private void Update()
        {
            // 1. Cooldown countdown
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (waterButton != null) waterButton.interactable = false;

                int secondsLeft = Mathf.CeilToInt(Mathf.Max(0f, cooldownTimer));
                if (buttonLabel != null)
                {
                    buttonLabel.text = $"COOLDOWN ({secondsLeft}s)";
                }

                if (buttonFillImage != null)
                {
                    buttonFillImage.fillAmount = Mathf.Clamp01(cooldownTimer / cooldownDuration);
                }

                if (cooldownTimer <= 0f)
                {
                    cooldownTimer = 0f;
                    ResetButtonUI();
                }
            }
            // 2. Hold-to-water progress
            else if (isHolding)
            {
                holdTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(holdTimer / holdDuration);

                if (buttonLabel != null)
                    buttonLabel.text = $"WATERING ({holdTimer:F1}s / {holdDuration:F0}s)";

                if (buttonFillImage != null)
                    buttonFillImage.fillAmount = progress;

                // Completed full 3 seconds!
                if (holdTimer >= holdDuration)
                {
                    CompleteWaterCycle();
                }
            }
        }

        public void OnPointerDown()
        {
            if (cooldownTimer > 0f) return;
            if (waterButton != null && !waterButton.interactable) return;

            isHolding = true;
            holdTimer = 0f;
            StartPouring();
        }

        public void OnPointerUp()
        {
            if (!isHolding) return;

            // Strict threshold: releasing early cancels and awards 0% (no cooldown)
            isHolding = false;
            holdTimer = 0f;
            StopPouring();
            ResetButtonUI();
        }

        public void OnPointerExit()
        {
            if (isHolding)
            {
                OnPointerUp();
            }
        }

        private void CompleteWaterCycle()
        {
            isHolding = false;
            holdTimer = 0f;
            StopPouring();

            // Trigger cooldown
            cooldownTimer = cooldownDuration;
            if (waterButton != null) waterButton.interactable = false;
            int secondsLeft = Mathf.CeilToInt(cooldownDuration);
            if (buttonLabel != null) buttonLabel.text = $"COOLDOWN ({secondsLeft}s)";
            if (buttonFillImage != null) buttonFillImage.fillAmount = 1f;

            // Notify listener (adds +3% growth)
            OnWaterCycleCompleted?.Invoke();
        }

        private void StartPouring()
        {
            isWateringActive = true;
            SetCanVisible(true);

            if (wateringAnimator != null)
            {
                wateringAnimator.SetBool("IsWatering", true);
            }

            if (waterAudioSource != null)
            {
                waterAudioSource.loop = true;
                if (!waterAudioSource.isPlaying)
                    waterAudioSource.Play();
            }

            if (waterParticleSystem != null)
            {
                waterParticleSystem.Play();
            }

            if (soilSplashParticleSystem != null)
            {
                soilSplashParticleSystem.Play();
            }
        }

        private void StopPouring()
        {
            isWateringActive = false;

            if (waterParticleSystem != null)
            {
                waterParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (soilSplashParticleSystem != null)
            {
                soilSplashParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (wateringAnimator != null)
            {
                wateringAnimator.SetBool("IsWatering", false);
            }

            if (waterAudioSource != null)
            {
                waterAudioSource.Stop();
            }

            SetCanVisible(false);
        }

        private void SetCanVisible(bool visible)
        {
            if (wateringCanRoot != null && wateringCanRoot != gameObject)
            {
                wateringCanRoot.SetActive(visible);
            }
            else
            {
                Transform model = transform.Find("Model");
                if (model != null)
                {
                    model.gameObject.SetActive(visible);
                }
                else
                {
                    foreach (var r in GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is not ParticleSystemRenderer)
                        {
                            r.enabled = visible;
                        }
                    }
                }
            }
        }

        public void CancelWateringImmediate()
        {
            isHolding = false;
            holdTimer = 0f;
            StopPouring();
            ResetButtonUI();
        }

        public void SetPlantFullyGrown()
        {
            CancelWateringImmediate();
            if (waterButton != null) waterButton.interactable = false;
            if (buttonLabel != null) buttonLabel.text = "FULL BLOOM! (TAP SPROUT TO DANCE)";
            if (buttonFillImage != null) buttonFillImage.fillAmount = 0f;
        }

        private void ResetButtonUI()
        {
            if (waterButton != null) waterButton.interactable = true;
            if (buttonLabel != null) buttonLabel.text = "HOLD TO WATER";
            if (buttonFillImage != null) buttonFillImage.fillAmount = 0f;
        }

        private void OnDisable()
        {
            if (isHolding || isWateringActive)
            {
                CancelWateringImmediate();
            }
        }
    }
}
