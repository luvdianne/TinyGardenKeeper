using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the visual lifecycle of pollen grains on the pot soil.
/// 1. Drops clusters of multiple tiny pollen grains.
/// 2. Handles smooth fade-out and disappearance when the butterfly companion departs.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class SoilPollenManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Particle system simulating the falling and resting pollen.")]
    public ParticleSystem pollenPS;

    [Tooltip("GardenRig root transform used as the custom simulation space.")]
    public Transform gardenRig;

    private Coroutine fadeRoutine;
    private bool isFading = false;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (pollenPS == null) pollenPS = GetComponent<ParticleSystem>();
        if (gardenRig == null)
        {
            Transform curr = transform;
            while (curr != null)
            {
                if (curr.name == "GardenRig") { gardenRig = curr; break; }
                curr = curr.parent;
            }
        }
    }

    /// <summary>
    /// Drops a cluster of multiple tiny pollen grains around the local origin in GardenRig space.
    /// </summary>
    public void DropPollenCluster(Vector3 localOrigin, int count, float minSize, float maxSize, float downwardVel, float inwardPullFactor)
    {
        ResolveReferences();
        if (pollenPS == null) return;

        // If currently fading out, cancel the fade routine because fresh pollen is arriving
        if (isFading && fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            isFading = false;
        }

        if (!pollenPS.isPlaying) pollenPS.Play();

        for (int i = 0; i < count; i++)
        {
            // Slight spatial jitter around the butterfly body for an organic cluster cloud
            Vector3 jitter = new Vector3(
                Random.Range(-0.007f, 0.007f),
                Random.Range(-0.004f, 0.004f),
                Random.Range(-0.007f, 0.007f)
            );
            Vector3 pos = localOrigin + jitter;

            // Inward velocity towards pot center (0, y, 0) + downward drift
            float vx = -pos.x * inwardPullFactor + Random.Range(-0.008f, 0.008f);
            float vz = -pos.z * inwardPullFactor + Random.Range(-0.008f, 0.008f);
            Vector3 vel = new Vector3(vx, downwardVel + Random.Range(-0.012f, 0.012f), vz);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = Random.Range(minSize, maxSize),
                startLifetime = 120.0f, // stays alive as long as butterfly is visiting
                startColor = new Color32(255, 215, 60, 255),
                applyShapeToPosition = false
            };

            pollenPS.Emit(emitParams, 1);
        }
    }

    /// <summary>
    /// Smoothly fades out and scales down all resting pollen grains, then clears them.
    /// Triggered when the butterfly companion is gone.
    /// </summary>
    public void FadeOutAndClear(float fadeDuration = 2.0f)
    {
        ResolveReferences();
        if (pollenPS == null || pollenPS.particleCount == 0) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        isFading = true;

        int particleCount = pollenPS.particleCount;
        if (particleCount == 0)
        {
            isFading = false;
            fadeRoutine = null;
            yield break;
        }

        ParticleSystem.Particle[] parts = new ParticleSystem.Particle[particleCount];
        int count = pollenPS.GetParticles(parts);

        float[] initialSizes = new float[count];
        byte[] initialAlphas = new byte[count];

        for (int i = 0; i < count; i++)
        {
            initialSizes[i] = parts[i].startSize;
            initialAlphas[i] = parts[i].startColor.a;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothFactor = 1f - Mathf.SmoothStep(0f, 1f, t);

            count = pollenPS.GetParticles(parts);
            for (int i = 0; i < count; i++)
            {
                float baseSize = i < initialSizes.Length ? initialSizes[i] : parts[i].startSize;
                byte baseAlpha = i < initialAlphas.Length ? initialAlphas[i] : parts[i].startColor.a;

                parts[i].startSize = baseSize * smoothFactor;
                Color32 c = parts[i].startColor;
                c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(baseAlpha * smoothFactor), 0, 255);
                parts[i].startColor = c;
            }
            pollenPS.SetParticles(parts, count);

            yield return null;
        }

        pollenPS.Clear();
        isFading = false;
        fadeRoutine = null;
    }

    /// <summary>
    /// Immediately clears all pollen particles (e.g. for preview reset).
    /// </summary>
    public void ClearImmediate()
    {
        ResolveReferences();
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        isFading = false;
        if (pollenPS != null) pollenPS.Clear();
    }

    /// <summary>
    /// Helper for Editor Preview to simulate multiple clusters of tiny pollen grains settling on the soil.
    /// </summary>
    public void SimulatePreviewClusters(int clusterCount = 10, int perCluster = 4, float minSize = 0.0008f, float maxSize = 0.0016f)
    {
        ResolveReferences();
        if (pollenPS == null) return;

        ClearImmediate();
        pollenPS.Play();

        for (int c = 0; c < clusterCount; c++)
        {
            float angle = c * (Mathf.PI * 2f / clusterCount);
            float radius = Random.Range(0.045f, 0.065f);
            Vector3 origin = new Vector3(Mathf.Cos(angle) * radius, 0.30f, Mathf.Sin(angle) * radius);
            DropPollenCluster(origin, perCluster, minSize, maxSize, -0.08f, 0.15f);
        }

        // Advance simulation by 2.2 seconds so all dropped pollen grains settle on the soil plane
        pollenPS.Simulate(2.2f, true, false);
    }
}
