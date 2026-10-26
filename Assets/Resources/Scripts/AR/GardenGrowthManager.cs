using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    /// 4. Growth occurs ONLY when watered (+3% per 3s hold) or when a Butterfly (+0.5%/s) / Bird (+0.55%/s) visits (capped at ~10% max per visit).
    /// 5. ZERO passive progression.
    /// 6. Crow weakens growth (-4%/s) until tapped and shooed away.
    /// 7. At 100%, transforms into the Final Sprout and celebrates. Tapping the sprout replays the dance.
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
        [Tooltip("Growth added per completed 3s watering cycle (+3%).")]
        [SerializeField] private float waterCycleProgressBonus = 0.03f;

        [Range(0.001f, 0.1f)]
        [Tooltip("Gentle boost rate per second for Butterfly visit (~10% total over visit).")]
        [SerializeField] private float butterflyBoostRate = 0.005f;

        [Range(0.001f, 0.1f)]
        [Tooltip("Gentle boost rate per second for Bird visit (~10% total over visit).")]
        [SerializeField] private float birdBoostRate = 0.0055f;

        [Range(0.001f, 0.1f)]
        [SerializeField] private float crowWeakenRate = 0.04f;

        [Tooltip("Maximum progress boost granted during a single companion visit (10%).")]
        [SerializeField] private float maxCompanionVisitBoost = 0.10f;

        [Header("UI References")]
        [SerializeField] private Slider growthProgressBar;
        [SerializeField] private Image progressBarFillImage;
        [SerializeField] private Text growthPercentageText;
        [SerializeField] private Text statusBadgeText;

        [Header("Progress Bar Colors")]
        [SerializeField] private Color normalFillColor = new Color(0.30f, 0.85f, 0.40f, 1f); // Vibrant Green
        [SerializeField] private Color weakenedFillColor = new Color(0.95f, 0.22f, 0.22f, 1f); // Warning Red
        [SerializeField] private float colorTransitionSpeed = 8f;

        [Header("State")]
        [Range(0f, 1f)]
        [SerializeField] private float currentProgress = 0f;
        private GrowthStage currentStage = GrowthStage.Seed;
        private bool isFullyGrown = false;
        private float currentVisitBoost = 0f;

        public float CurrentProgress => currentProgress;
        public GrowthStage CurrentStage => currentStage;
        public bool IsFullyGrown => isFullyGrown;

        private void OnEnable()
        {
            if (wateringController != null)
            {
                wateringController.OnWaterCycleCompleted += HandleWaterCycleCompleted;
            }
            if (companionManager != null)
            {
                companionManager.OnCompanionArrived += HandleCompanionArrived;
            }
        }

        private void OnDisable()
        {
            if (wateringController != null)
            {
                wateringController.OnWaterCycleCompleted -= HandleWaterCycleCompleted;
            }
            if (companionManager != null)
            {
                companionManager.OnCompanionArrived -= HandleCompanionArrived;
            }
        }

        private void HandleCompanionArrived(CompanionType type)
        {
            currentVisitBoost = 0f;
        }

        private void Start()
        {
            EnsureFillImageReference();
            InitializeGarden();
        }

        private void EnsureFillImageReference()
        {
            if (progressBarFillImage == null && growthProgressBar != null)
            {
                if (growthProgressBar.fillRect != null)
                {
                    progressBarFillImage = growthProgressBar.fillRect.GetComponent<Image>();
                }
                if (progressBarFillImage == null)
                {
                    progressBarFillImage = growthProgressBar.GetComponentInChildren<Image>();
                }
            }
        }

        public void InitializeGarden()
        {
            currentProgress = 0f;
            currentVisitBoost = 0f;
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

        /// <summary>
        /// Applies creature dynamics with strictly ZERO passive growth.
        /// Butterfly and Bird provide a gentle ~10% boost total during their visit.
        /// </summary>
        private void ApplyGrowthDynamics()
        {
            float delta = 0f;
            string status = "Resting in Soil...";

            // Creature effects (Bird / Butterfly / Crow)
            if (companionManager != null)
            {
                switch (companionManager.ActiveCompanion)
                {
                    case CompanionType.Butterfly:
                        float remainingBoostB = Mathf.Max(0f, maxCompanionVisitBoost - currentVisitBoost);
                        float allowedB = Mathf.Min(butterflyBoostRate * Time.deltaTime, remainingBoostB);
                        currentVisitBoost += allowedB;
                        delta += allowedB;
                        status = currentVisitBoost >= maxCompanionVisitBoost
                            ? "Butterfly Boost (+10% Max Reached)"
                            : $"Butterfly Boost (+{(butterflyBoostRate * 100f):0.#}%/s, 10% max)";
                        break;
                    case CompanionType.Bird:
                        float remainingBoostBird = Mathf.Max(0f, maxCompanionVisitBoost - currentVisitBoost);
                        float allowedBird = Mathf.Min(birdBoostRate * Time.deltaTime, remainingBoostBird);
                        currentVisitBoost += allowedBird;
                        delta += allowedBird;
                        status = currentVisitBoost >= maxCompanionVisitBoost
                            ? "Bird Song Boost (+10% Max Reached)"
                            : $"Bird Song Boost (+{(birdBoostRate * 100f):0.#}%/s, 10% max)";
                        break;
                    case CompanionType.Crow:
                        delta -= crowWeakenRate * Time.deltaTime;
                        status = "Crow Alert (-4%/s)! Tap Crow to Shoo!";
                        break;
                }
            }

            // Strictly zero passive progression: delta is only non-zero when companion is present
            if (delta != 0f)
            {
                currentProgress = Mathf.Clamp01(currentProgress + delta);
                UpdateGrowthStages();
            }

            UpdateUI(status);
        }

        /// <summary>
        /// Triggered when the player successfully holds the WATER button for 3 seconds.
        /// Awards exactly +3% (+0.03) progress.
        /// </summary>
        private void HandleWaterCycleCompleted()
        {
            if (isFullyGrown) return;

            currentProgress = Mathf.Clamp01(currentProgress + waterCycleProgressBonus);
            UpdateGrowthStages();
            UpdateUI("Watered (+3%)!");
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

            // Notify watering controller to lock out and show full bloom
            if (wateringController != null)
            {
                wateringController.SetPlantFullyGrown();
            }

            // Trigger celebration dance
            if (sproutDanceButton != null)
            {
                sproutDanceButton.GrowSprout();
            }

            UpdateUI("Full Bloom! Sprout Dancing! Tap Sprout to Dance!");
        }

        private void UpdateUI(string statusOverride = null)
        {
            EnsureFillImageReference();

            if (growthProgressBar != null)
            {
                growthProgressBar.value = currentProgress;
            }

            // Dynamically tint the progress bar fill red when weakened by the crow
            bool isWeakenedByCrow = companionManager != null && companionManager.ActiveCompanion == CompanionType.Crow;
            if (progressBarFillImage != null)
            {
                Color targetColor = isWeakenedByCrow ? weakenedFillColor : normalFillColor;
                if (Application.isPlaying)
                {
                    progressBarFillImage.color = Color.Lerp(progressBarFillImage.color, targetColor, Time.deltaTime * colorTransitionSpeed);
                }
                else
                {
                    progressBarFillImage.color = targetColor;
                }
            }

            if (growthPercentageText != null)
            {
                growthPercentageText.text = $"Growth: {(currentProgress * 100f):F0}%";
                if (isWeakenedByCrow)
                {
                    growthPercentageText.color = weakenedFillColor;
                }
                else
                {
                    growthPercentageText.color = normalFillColor;
                }
            }

            if (statusBadgeText != null)
            {
                string stageBadge = isFullyGrown ? "Full Bloom! Tap Sprout to Dance!" :
                                   currentStage == GrowthStage.SproutStage2 ? "Maturing Sprout (Stage 2)" :
                                   currentStage == GrowthStage.SproutStage1 ? "Young Sprout (Stage 1)" :
                                   "Seed in Soil";

                statusBadgeText.text = statusOverride != null ? $"{stageBadge} - {statusOverride}" : stageBadge;
            }
        }

        private void HandleTapInput()
        {
            Vector2 tapPos = Vector2.zero;
            bool hasTap = false;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                tapPos = Touchscreen.current.primaryTouch.position.ReadValue();
                hasTap = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapPos = Mouse.current.position.ReadValue();
                hasTap = true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                tapPos = Input.mousePosition;
                hasTap = true;
            }
#endif

            if (!hasTap) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(tapPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 1. Check if tapped on Crow to shoo
                CrowTapTarget crowTarget = hit.collider.GetComponentInParent<CrowTapTarget>();
                if (crowTarget != null)
                {
                    crowTarget.Shoo();
                    return;
                }

                // 2. Check if fully grown and tapped on Sprout to replay dance
                if (isFullyGrown)
                {
                    Transform hitTrans = hit.collider.transform;
                    if (hitTrans == sproutFinalObject?.transform || hitTrans.IsChildOf(sproutFinalObject?.transform))
                    {
                        if (sproutDanceButton != null)
                        {
                            sproutDanceButton.GrowSprout();
                            UpdateUI("Sprout Dancing!");
                        }
                        return;
                    }
                }
            }
        }
    }
}
