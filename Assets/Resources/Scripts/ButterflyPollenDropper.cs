using UnityEngine;

/// <summary>
/// Drops glowing clusters of multiple tiny golden pollen spores from the butterfly as it orbits over the pot.
/// The pollen grains hit the soil collision plane and stay resting directly on top of the soil bed.
/// When the butterfly departs or is gone, the resting pollen smoothly fades out and disappears.
/// </summary>
public class ButterflyPollenDropper : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager component on SoilPollenFX handling cluster drops and departure fade-out.")]
    public SoilPollenManager soilPollenManager;

    [Tooltip("Transform of the butterfly body from which pollen drops.")]
    public Transform butterflyBody;

    [Tooltip("GardenRig root transform used as the custom simulation space.")]
    public Transform gardenRig;

    [Tooltip("Transition component to check if butterfly is currently hovering or departing.")]
    public ButterflyTransition butterflyTransition;

    [Header("Drop Settings")]
    [Tooltip("Time in seconds between pollen cluster drops.")]
    public float dropInterval = 0.20f;

    [Tooltip("Minimum number of pollens dropped per cluster.")]
    public int minClusterCount = 3;

    [Tooltip("Maximum number of pollens dropped per cluster.")]
    public int maxClusterCount = 6;

    [Tooltip("Minimum size of really small pollen grain (0.8mm).")]
    public float minPollenSize = 0.0008f;

    [Tooltip("Maximum size of really small pollen grain (1.6mm).")]
    public float maxPollenSize = 0.0016f;

    [Tooltip("Downward velocity of falling pollen in m/s.")]
    public float downwardVelocity = -0.08f;

    [Tooltip("Gentle inward pull factor towards pot center to keep all pollen neatly on the soil.")]
    public float inwardPull = 0.15f;

    [Tooltip("Duration in seconds for the pollen to smoothly fade out and disappear when the butterfly is gone.")]
    public float fadeDuration = 2.2f;

    private float timer = 0f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        timer = 0f;
    }

    private void OnDisable()
    {
        // When butterfly is gone, fade out and disappear
        TriggerDepartureFade();
    }

    public void ResolveReferences()
    {
        if (butterflyTransition == null)
        {
            butterflyTransition = GetComponent<ButterflyTransition>();
        }

        if (butterflyBody == null)
        {
            butterflyBody = transform.Find("butterfly body");
        }

        if (gardenRig == null)
        {
            Transform curr = transform;
            while (curr != null)
            {
                if (curr.name == "GardenRig")
                {
                    gardenRig = curr;
                    break;
                }
                curr = curr.parent;
            }
        }

        if (soilPollenManager == null && gardenRig != null)
        {
            var fxTrans = gardenRig.Find("SoilPollenFX");
            if (fxTrans != null)
            {
                soilPollenManager = fxTrans.GetComponent<SoilPollenManager>();
                if (soilPollenManager == null)
                {
                    soilPollenManager = fxTrans.gameObject.AddComponent<SoilPollenManager>();
                }
            }
        }
    }

    private void Update()
    {
        if (soilPollenManager == null || butterflyBody == null || gardenRig == null)
        {
            ResolveReferences();
            if (soilPollenManager == null || butterflyBody == null || gardenRig == null) return;
        }

        // Do not drop pollen if currently scaling up (entrance) or departing (scaling down / climbing)
        if (butterflyTransition != null && (butterflyTransition.IsDeparting || !butterflyTransition.IsHovering))
        {
            return;
        }

        // Fallback check: if scale is too small, don't drop pollen
        if (transform.localScale.x < 0.005f) return;

        timer += Time.deltaTime;
        while (timer >= dropInterval)
        {
            timer -= dropInterval;
            DropPollenCluster();
        }
    }

    /// <summary>
    /// Emits a cluster of multiple tiny pollen grains at the butterfly body's position.
    /// </summary>
    public void DropPollenCluster()
    {
        if (soilPollenManager == null || butterflyBody == null || gardenRig == null) return;

        Vector3 localPos = gardenRig.InverseTransformPoint(butterflyBody.position);
        int count = Random.Range(minClusterCount, maxClusterCount + 1);

        soilPollenManager.DropPollenCluster(localPos, count, minPollenSize, maxPollenSize, downwardVelocity, inwardPull);
    }

    /// <summary>
    /// Triggers smooth fade out and disappearance of resting pollen on the soil.
    /// </summary>
    public void TriggerDepartureFade()
    {
        if (soilPollenManager != null)
        {
            soilPollenManager.FadeOutAndClear(fadeDuration);
        }
    }

    /// <summary>
    /// Helper for Editor Preview to simulate multiple clusters of tiny pollen grains resting on the soil.
    /// </summary>
    public void SimulatePollenLandingPreview(int clusterCount = 12, int perCluster = 4)
    {
        ResolveReferences();
        if (soilPollenManager != null)
        {
            soilPollenManager.SimulatePreviewClusters(clusterCount, perCluster, minPollenSize, maxPollenSize);
        }
    }
}
