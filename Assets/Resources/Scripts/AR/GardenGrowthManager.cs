using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinyGardenKeeper.AR
{
    public enum GrowthStage
    {
        Seed = 0,
        SproutStage1 = 1,
        SproutStage2 = 2,
        FinalSprout = 3
    }

    /// <summary>
    /// Master gameplay manager for AR plant growth:
    /// 1. Starts with Seed in pot soil (0% - 30%).
    /// 2. Grows into Sprout Stage 1 (30% - 65%).
    /// 3. Grows into Sprout Stage 2 (65% - 100%).
    /// 4. Water, Butterfly, and Bird accelerate growth.
    /// 5. Crow weakens growth (player can tap crow to shoo it away).
    /// 6. UI Progress Bar displays real-time 0-100% progress and status alerts.
    /// 7. At 100%, transforms into the Final Sprout, which executes its celebratory dance.
    /// </summary>
    public class GardenGrowthManager : MonoBehaviour
    {
        [Header("Plant Stages")]
        [Tooltip("Stage 0: Starting seed (0% - 30%).")]
        [SerializeField] private GameObject seedObject;

        [Tooltip("Stage 1: Early sprout (30% - 65%).")]
        [SerializeField] private GameObject sproutStage1Object;

        [Tooltip("Stage 2: Maturing sprout (65% - 100%).")]
        [SerializeField] private GameObject sproutStage2Object;

        [Tooltip("Stage 3: Fully-grown dancing sprout (100%).")]
        [SerializeField] private GameObject sproutFinalObject;

        [Tooltip("Procedural growth and dance controller on Final Sprout.")]
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
        private GrowthStage currentStage = GrowthStage.Seed;
        private bool isFullyGrown = false;

        public float CurrentProgress => currentProgress;
        public GrowthStage CurrentStage => currentStage;
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
            SetStage(GrowthStage.Seed);
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
            UpdateGrowthStages();
            UpdateUI(status);
        }

        private void UpdateGrowthStages()
        {
            GrowthStage targetStage;
            if (currentProgress >= 1f)
            {
                targetStage = GrowthStage.FinalSprout;
            }
            else if (currentProgress >= 0.65f)
            {
                targetStage = GrowthStage.SproutStage2;
            }
            else if (currentProgress >= 0.30f)
            {
                targetStage = GrowthStage.SproutStage1;
            }
            else
            {
                targetStage = GrowthStage.Seed;
            }

            if (targetStage != currentStage)
            {
                SetStage(targetStage);
            }
        }

        private void SetStage(GrowthStage stage)
        {
            currentStage = stage;

            if (seedObject != null)
                seedObject.SetActive(stage == GrowthStage.Seed);

            if (sproutStage1Object != null)
                sproutStage1Object.SetActive(stage == GrowthStage.SproutStage1);

            if (sproutStage2Object != null)
                sproutStage2Object.SetActive(stage == GrowthStage.SproutStage2);

            if (sproutFinalObject != null)
                sproutFinalObject.SetActive(stage == GrowthStage.FinalSprout);

            if (stage == GrowthStage.FinalSprout && !isFullyGrown)
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
                // Instant cheat/nurture button for testing (+25%)
                currentProgress = Mathf.Clamp01(currentProgress + 0.25f);
                UpdateGrowthStages();
                UpdateUI();
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
                string stageBadge = isFullyGrown ? "Full Bloom! Sprout Dancing!" :
                                   currentStage == GrowthStage.SproutStage2 ? "Maturing Sprout (Stage 2)" :
                                   currentStage == GrowthStage.SproutStage1 ? "Young Sprout (Stage 1)" :
                                   "Seed in Soil";

                statusBadgeText.text = statusOverride != null ? $"{stageBadge} - {statusOverride}" : stageBadge;
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
