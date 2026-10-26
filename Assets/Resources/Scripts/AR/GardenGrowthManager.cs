using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Master gameplay manager for AR plant growth:
    /// 1. Starts with Seed in pot soil.
    /// 2. Water, Butterfly, and Bird accelerate growth.
    /// 3. Crow weakens growth (player can tap crow to shoo it away).
    /// 4. UI Progress Bar displays real-time 0-100% progress and status alerts.
    /// 5. At 100%, Seed transforms into the Sprout, which executes its celebratory dance.
    /// </summary>
    public class GardenGrowthManager : MonoBehaviour
    {
        [Header("Plant Stages")]
        [Tooltip("The starting seed GameObject in the pot.")]
        [SerializeField] private GameObject seedObject;

        [Tooltip("The fully-grown sprout GameObject.")]
        [SerializeField] private GameObject sproutObject;

        [Tooltip("Procedural growth and dance controller on Sprout.")]
        [SerializeField] private SproutDanceButton sproutDanceButton;

        [Header("Systems")]
        [SerializeField] private ARWateringController wateringController;
        [SerializeField] private CompanionVisitManager companionManager;

        [Header("Growth Rates (% per second)")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float waterRate = 0.15f;

        [Range(0.01f, 0.2f)]
        [SerializeField] private float butterflyBoostRate = 0.04f;

        [Range(0.01f, 0.2f)]
        [SerializeField] private float birdBoostRate = 0.05f;

        [Range(0.01f, 0.2f)]
        [SerializeField] private float crowWeakenRate = 0.04f;

        [Header("UI References")]
        [SerializeField] private Slider growthProgressBar;
        [SerializeField] private Text growthPercentageText;
        [SerializeField] private Text statusBadgeText;
        [SerializeField] private Button plantDanceButton;

        [Header("State")]
        [Range(0f, 1f)]
        [SerializeField] private float currentProgress = 0f;
        private bool isFullyGrown = false;

        public float CurrentProgress => currentProgress;
        public bool IsFullyGrown => isFullyGrown;

        private void Start()
        {
            InitializeGarden();

            if (plantDanceButton != null)
            {
                plantDanceButton.onClick.AddListener(OnPlantDanceButtonClicked);
            }
        }

        public void InitializeGarden()
        {
            currentProgress = 0f;
            isFullyGrown = false;

            if (seedObject != null)
                seedObject.SetActive(true);

            if (sproutObject != null)
            {
                sproutObject.SetActive(false);
            }

            UpdateUI();
        }

        private void Update()
        {
            if (!isFullyGrown)
            {
                ApplyGrowthDynamics();
            }

            HandleTapInput();
        }

        private void ApplyGrowthDynamics()
        {
            float delta = 0f;
            string status = "Resting in Soil...";

            // 1. Water growth
            if (wateringController != null && wateringController.IsWatering)
            {
                delta += waterRate * Time.deltaTime;
                status = "Watering (+15%/s)";
            }

            // 2. Companion effects
            if (companionManager != null)
            {
                switch (companionManager.ActiveCompanion)
                {
                    case CompanionType.Butterfly:
                        delta += butterflyBoostRate * Time.deltaTime;
                        status = "Butterfly Boost (+4%/s)";
                        break;
                    case CompanionType.Bird:
                        delta += birdBoostRate * Time.deltaTime;
                        status = "Bird Song Boost (+5%/s)";
                        break;
                    case CompanionType.Crow:
                        delta -= crowWeakenRate * Time.deltaTime;
                        status = "Crow Alert (-4%/s)! Tap Crow to Shoo!";
                        break;
                }
            }

            currentProgress = Mathf.Clamp01(currentProgress + delta);
            UpdateUI(status);

            if (currentProgress >= 1f && !isFullyGrown)
            {
                OnGrowthCompleted();
            }
        }

        private void OnGrowthCompleted()
        {
            isFullyGrown = true;

            // Stop watering if active
            if (wateringController != null && wateringController.IsWatering)
            {
                wateringController.SetWatering(false);
            }

            // Hide seed, reveal sprout
            if (seedObject != null)
                seedObject.SetActive(false);

            if (sproutObject != null)
            {
                sproutObject.SetActive(true);
            }

            // Trigger celebration dance
            if (sproutDanceButton != null)
            {
                sproutDanceButton.GrowSprout();
            }

            UpdateUI("Full Bloom! Sprout Dancing!");
        }

        public void OnPlantDanceButtonClicked()
        {
            if (isFullyGrown)
            {
                // Replay or toggle dance
                if (sproutDanceButton != null)
                {
                    sproutDanceButton.GrowSprout();
                }
            }
            else
            {
                // Instant cheat/nurture button for testing
                currentProgress = Mathf.Clamp01(currentProgress + 0.25f);
                if (currentProgress >= 1f)
                {
                    OnGrowthCompleted();
                }
            }
        }

        private void UpdateUI(string statusOverride = null)
        {
            if (growthProgressBar != null)
            {
                growthProgressBar.value = currentProgress;
            }

            if (growthPercentageText != null)
            {
                growthPercentageText.text = $"Growth: {(currentProgress * 100f):F0}%";
            }

            if (statusBadgeText != null)
            {
                statusBadgeText.text = statusOverride ?? (isFullyGrown ? "Full Bloom! Sprout Dancing!" : "Plant Seed & Water");
            }

            if (plantDanceButton != null)
            {
                Text btnText = plantDanceButton.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = isFullyGrown ? "DANCE" : "NURTURE (+25%)";
                }
            }
        }

        private void HandleTapInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // Check if tapped on Crow
                    CrowTapTarget crowTarget = hit.collider.GetComponentInParent<CrowTapTarget>();
                    if (crowTarget != null)
                    {
                        crowTarget.Shoo();
                        return;
                    }

                    // Check if tapped on Pot / Soil to toggle watering
                    if (hit.collider.CompareTag("Untagged") && (hit.collider.name.ToLower().Contains("pot") || hit.collider.name.ToLower().Contains("soil")))
                    {
                        if (wateringController != null && !isFullyGrown)
                        {
                            wateringController.ToggleWatering();
                        }
                    }
                }
            }
        }
    }
}
