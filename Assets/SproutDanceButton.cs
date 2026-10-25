
using System.Collections;
using UnityEngine;

public class SproutDanceButton : MonoBehaviour
{
    [Header("Plant")]
    public Transform sprout;

    [Header("Growth")]
    [Range(0.01f, 1f)]
    public float startingSize = 0.08f;

    public float growDuration = 3f;
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

    void Start()
    {
        if (sprout == null)
        {
            Debug.LogError(
                "Sprout is not assigned. Drag the plant into the Sprout field."
            );

            enabled = false;
            return;
        }

        // Save the original plant transform.
        originalScale = sprout.localScale;
        originalPosition = sprout.localPosition;
        originalRotation = sprout.localRotation;

        // Start small, without changing material colors.
        ResetPlant();
    }

    // Connect the Water Plant button to this method.
    public void GrowSprout()
    {
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

        sprout.localScale = originalScale * startingSize;
        sprout.localPosition = originalPosition;
        sprout.localRotation = originalRotation;

        // Do not change the materials.
        // CreamWhite, StemPale, and LeafGreen stay as assigned in Unity.
    }

    IEnumerator GrowAndDance()
    {
        Vector3 smallScale = originalScale * startingSize;
        Vector3 bigScale = originalScale * finalScaleMultiplier;

        float elapsed = 0f;

        // STEP 1: Grow.
        while (elapsed < growDuration)
        {
            if (!isPlaying)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / growDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            sprout.localScale = Vector3.Lerp(
                smallScale,
                bigScale,
                smoothT
            );

            sprout.localPosition = originalPosition;
            sprout.localRotation = originalRotation;

            yield return null;
        }

        sprout.localScale = bigScale;

        // STEP 2: Dance.
        elapsed = 0f;

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

        // STEP 3: Stay grown until the next click.
        sprout.localScale = bigScale;
        sprout.localPosition = originalPosition;
        sprout.localRotation = originalRotation;

        isPlaying = false;
        animationRoutine = null;
    }
}
