using System.Collections;
using UnityEngine;

/// <summary>
/// Smooth entrance and graceful departure controller for the Butterfly companion.
/// 1. Flutters smoothly into the scene (scale-up entrance).
/// 2. Circles continuously directly above the pot, dropping pollen into the soil.
/// 3. Flutters gracefully upward and departs smoothly near the end of the visit (no sudden stops).
/// </summary>
public class ButterflyTransition : MonoBehaviour
{
    [Header("Transition Timings (Seconds)")]
    [Tooltip("Time to scale up from zero upon arriving.")]
    public float scaleUpDuration = 1.0f;

    [Tooltip("Total visit duration before departure (synced with CompanionVisitManager: 18s).")]
    public float stayDuration = 18.0f;

    [Tooltip("Time spent ascending and scaling down at the end of the visit.")]
    public float scaleDownDuration = 1.8f;

    [Header("Exit Motion")]
    [Tooltip("Vertical climb height during departure flutter.")]
    public float exitClimbHeight = 0.35f;

    /// <summary>
    /// True when the butterfly has finished entering and is actively circling / pollinating above the pot.
    /// </summary>
    public bool IsHovering { get; private set; }

    /// <summary>
    /// True when the butterfly is actively ascending and scaling down to depart.
    /// </summary>
    public bool IsDeparting { get; private set; }

    private Vector3 targetLocalScale;
    private Vector3 initialLocalPosition;
    private Coroutine transitionRoutine;
    private bool isInitialized = false;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        targetLocalScale = transform.localScale != Vector3.zero ? transform.localScale : Vector3.one * 0.0105f;
        initialLocalPosition = transform.localPosition;
        isInitialized = true;
    }

    public void SetTargetScale(Vector3 scale)
    {
        targetLocalScale = scale;
    }

    public void SetVisitDuration(float duration)
    {
        stayDuration = duration;
    }

    private void OnEnable()
    {
        EnsureInitialized();
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }
        transitionRoutine = StartCoroutine(VisitRoutine());
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        IsHovering = false;
        IsDeparting = false;
        if (isInitialized)
        {
            transform.localScale = targetLocalScale;
            transform.localPosition = initialLocalPosition;
        }
    }

    private IEnumerator VisitRoutine()
    {
        IsHovering = false;
        IsDeparting = false;

        // Start from origin and scale zero
        transform.localPosition = initialLocalPosition;
        transform.localScale = Vector3.zero;

        // 1. Entrance: Smooth flutter scale-up
        float elapsed = 0f;
        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleUpDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetLocalScale, smoothT);
            yield return null;
        }
        transform.localScale = targetLocalScale;
        IsHovering = true;

        // 2. Main Visit: Circle and drop pollen directly over the pot
        float hoverTime = Mathf.Max(0.5f, stayDuration - scaleUpDuration - scaleDownDuration);
        yield return new WaitForSeconds(hoverTime);

        // 3. Graceful Departure: Flutter up towards the sky and scale down smoothly (NO sudden stop!)
        IsHovering = false;
        IsDeparting = true;

        // Trigger smooth fade out of pollen on the soil as butterfly ascends and departs
        var dropper = GetComponent<ButterflyPollenDropper>();
        if (dropper != null)
        {
            dropper.TriggerDepartureFade();
        }

        elapsed = 0f;
        Vector3 startExitPos = transform.localPosition;
        Vector3 endExitPos = startExitPos + Vector3.up * exitClimbHeight;

        while (elapsed < scaleDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleDownDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(targetLocalScale, Vector3.zero, smoothT);
            transform.localPosition = Vector3.Lerp(startExitPos, endExitPos, smoothT);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        transform.localPosition = endExitPos;
        IsDeparting = false;
        transitionRoutine = null;
    }
}
