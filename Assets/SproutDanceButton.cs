using System.Collections;
using UnityEngine;

public class SproutDanceButton : MonoBehaviour
{
    [Header("Plant")]
    public Transform sprout;

    [Header("Growth")]
    [Range(0.01f, 1f)]
    public float startingSize = 1f;

    public float growDuration = 0f;
    public float finalScaleMultiplier = 1f;

    [Header("Dance")]
    public float danceDuration = 4f;
    public float swayAngle = 12f;
    public float swaySpeed = 2.5f;
    public float bounceHeight = 0.04f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private bool isPlaying = false;
    private Coroutine animationRoutine;

    private bool isInitialized = false;

    private void Awake()
    {
        EnsureInitialized();
    }

    void Start()
    {
        EnsureInitialized();
        ResetPlant();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        if (sprout == null) sprout = transform;
        if (sprout != null)
        {
            originalScale = sprout.localScale != Vector3.zero ? sprout.localScale : Vector3.one * 0.18f;
            originalPosition = sprout.localPosition;
            originalRotation = sprout.localRotation;
            isInitialized = true;
        }
    }

    // Connect the Water Plant button to this method.
    public void GrowSprout()
    {
        EnsureInitialized();
        if (sprout == null)
            return;

        if (isPlaying)
        {
            // Click 2, 4, 6... Stop and reset immediately.
            isPlaying = false;

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            ResetPlant();
        }
        else
        {
            // Click 1, 3, 5... Start the animation again.
            ResetPlant();

            isPlaying = true;
            animationRoutine = StartCoroutine(GrowAndDance());
        }
    }

    public void ResetPlant()
    {
        if (sprout == null)
            return;

        // Final sprout stays at full scale (scaleup animation removed)
        sprout.localScale = originalScale * finalScaleMultiplier;
        sprout.localPosition = originalPosition;
        sprout.localRotation = originalRotation;

        // Do not change the materials.
        // CreamWhite, StemPale, and LeafGreen stay as assigned in Unity.
    }

    IEnumerator GrowAndDance()
    {
        Vector3 fullScale = originalScale * finalScaleMultiplier;
        sprout.localScale = fullScale;
        sprout.localPosition = originalPosition;
        sprout.localRotation = originalRotation;

        // STEP 1: Scaleup animation removed - sprout starts dancing immediately at full size.

        // STEP 2: Dance.
        float elapsed = 0f;

        while (elapsed < danceDuration)
        {
            if (!isPlaying)
                yield break;

            elapsed += Time.deltaTime;

            float wave = Mathf.Sin(elapsed * swaySpeed);

            float bounce = Mathf.Abs(wave) * bounceHeight;
            float sway = wave * swayAngle;

            sprout.localPosition =
                originalPosition + Vector3.up * bounce;

            sprout.localRotation =
                originalRotation *
                Quaternion.Euler(0f, 0f, sway);

            yield return null;
        }

        // STEP 3: Stay at full scale until next click.
        sprout.localScale = fullScale;
        sprout.localPosition = originalPosition;
        sprout.localRotation = originalRotation;

        isPlaying = false;
        animationRoutine = null;
    }
}
