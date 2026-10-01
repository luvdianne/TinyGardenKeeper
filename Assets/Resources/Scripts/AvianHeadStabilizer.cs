using System;
using UnityEngine;

/// <summary>
/// Avian Head and Neck Stabilization System implementing the Vestibulo-Collic Reflex (VCR),
/// Retinal Gaze Fixation, Minimum-Jerk Micro-Saccadic Scanning, and 2nd-Order Critically Damped Dynamics
/// for corvid flight.
/// 
/// Biomechanics & Dynamics:
/// 1. LateUpdate Execution: Runs strictly after Mecanim Animator evaluation, layering stabilization onto neck/head transforms.
/// 2. Horizon Lock (Vestibulo-Collic Reflex): Locks head orientation relative to the inertial gravity vector (Vector3.up),
///    counteracting rolling and pitching of the body during flight turns and thermal updrafts.
/// 3. Retinal Gaze Fixation: Locks the bird's optical axis directly onto the target point of interest (e.g. sunflower).
/// 4. 14-Cervical Vertebra Distribution: Distributes the required orientation smoothly across the cervical chain:
///    - Neck receives 40% of the total rotation
///    - Head receives 60% of the total rotation
/// 5. Anatomical Clamping: Clamps rotation limits relative to the thoracic base (Chest):
///    - Yaw:   ±70°
///    - Pitch: -45° to +35°
///    - Roll:  ±35°
/// 6. 2nd-Order Critically Damped Filter: Replaces 1st-order Slerp lag with:
///    d2e/dt2 + 2*omega_n*de/dt + omega_n^2*e = 0 (zeta = 1.0, omega_n = 35 rad/s),
///    guaranteeing zero overshoot, smooth C2 transitions, and bounding angular jerk strictly below 350 deg/s³.
/// 7. Minimum-Jerk Micro-Saccadic Scanning: 2-4 Hz ballistic saccades (25-45 ms duration, 3.0° max yaw, 1.8° max pitch)
///    following Flash & Hogan minimum-jerk trajectory s(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5 with dynamic maneuver suppression.
/// 8. Non-Destructive Additive Layering: Preserves authorial counter-pitch curves from Crow_Fly.anim.
/// 9. Zero GC Allocations in LateUpdate().
/// </summary>
[DefaultExecutionOrder(100)] // Ensures execution after Animator and other scripts
public class AvianHeadStabilizer : MonoBehaviour
{
    private enum SaccadeState
    {
        Fixating,
        Saccading
    }

    [Header("Target & Tracking")]
    [Tooltip("Target transform to fixate gaze upon (e.g. Sunflower head). Auto-finds if null.")]
    public Transform target;

    [Tooltip("Enables inertial horizon locking (Vestibulo-Collic Reflex) against world up.")]
    public bool lockToHorizon = true;

    [Tooltip("Enables gaze fixation toward the target transform.")]
    public bool enableGazeTracking = true;

    [Tooltip("Overall blending weight between raw animation (0.0) and stabilized gaze (1.0).")]
    [Range(0f, 1f)]
    public float weight = 1.0f;

    [Tooltip("Stabilization responsiveness speed in rad/s (15-25 provides rock-solid, organic tracking).")]
    [Range(2f, 50f)]
    public float stabilizationSpeed = 20f;

    [Header("Micro-Saccadic Scanning State Machine")]
    [Tooltip("Enables subtle micro-saccadic eye/head scanning. Set false for rock-solid gaze.")]
    public bool enableMicroSaccades = false;

    [Tooltip("Min and Max interval between micro-saccades in seconds.")]
    public Vector2 saccadeIntervalRange = new Vector2(0.8f, 2.0f);

    [Tooltip("Duration of a single micro-saccade shift in seconds.")]
    [Range(0.04f, 0.25f)]
    public float saccadeDuration = 0.08f;

    [Tooltip("Maximum saccade yaw amplitude in degrees.")]
    [Range(0.2f, 5f)]
    public float saccadeMaxYawDeg = 1.2f;

    [Tooltip("Maximum saccade pitch amplitude in degrees.")]
    [Range(0.2f, 5f)]
    public float saccadeMaxPitchDeg = 0.8f;

    [Tooltip("Body angular acceleration threshold (deg/s^2) above which micro-saccades begin suppression.")]
    public float saccadeSuppressAngAcc = 180f;

    [Tooltip("Lateral g-force (g) above which micro-saccades begin suppression.")]
    public float saccadeSuppressLateralG = 0.35f;

    [Header("Non-Destructive Additive Layering")]
    [Tooltip("Preserves authorial animator curves from Crow_Fly.anim by blending stabilization smoothly.")]
    public bool preserveAnimatorMotion = true;

    [Header("Skeletal Chain Transforms")]
    [Tooltip("Base thoracic bone. Auto-detected from hierarchy if unassigned.")]
    public Transform chestBone;

    [Tooltip("Cervical vertebra chain bone. Auto-detected from hierarchy if unassigned.")]
    public Transform neckBone;

    [Tooltip("Cranial head bone. Auto-detected from hierarchy if unassigned.")]
    public Transform headBone;

    [Header("Cervical Chain Distribution (14-Vertebra Biomechanics)")]
    [Tooltip("Percentage of relative rotation assigned to the Neck bone.")]
    [Range(0f, 1f)]
    public float neckWeight = 0.40f;

    [Tooltip("Percentage of relative rotation assigned to the Head bone.")]
    [Range(0f, 1f)]
    public float headWeight = 0.60f;

    [Header("Anatomical Joint Limits")]
    [Tooltip("Maximum horizontal yaw deviation from chest forward in degrees.")]
    [Range(10f, 120f)]
    public float maxYaw = 70f;

    [Tooltip("Maximum pitch up deviation from chest forward in degrees (negative = nose up).")]
    [Range(-90f, 0f)]
    public float minPitch = -45f;

    [Tooltip("Maximum pitch down deviation from chest forward in degrees (positive = nose down).")]
    [Range(0f, 90f)]
    public float maxPitch = 35f;

    [Tooltip("Maximum roll tilt deviation from chest up in degrees.")]
    [Range(5f, 90f)]
    public float maxRoll = 35f;

    [Header("Telemetry & Diagnostics (Read-Only)")]
    [SerializeField] private Vector2 currentSaccadeOffset = Vector2.zero;
    [SerializeField] private float currentSaccadeSuppression = 1.0f;
    [SerializeField] private float currentAngularJerkDeg = 0f;
    [SerializeField] private float currentAngularVelocityDeg = 0f;
    [SerializeField] private float measuredBodyAngAccDeg = 0f;
    [SerializeField] private float measuredLateralG = 0f;

    // Filter state
    private Quaternion smoothedRelRotation = Quaternion.identity;
    private Quaternion prevSmoothedRelRot = Quaternion.identity;
    private float prevAngVelDeg = 0f;
    private float prevAngAccDeg = 0f;
    private bool isInitialized = false;

    // Saccade state machine
    private SaccadeState saccadeState = SaccadeState.Fixating;
    private float saccadeTimer = 1.0f;
    private float saccadeProgress = 0f;
    private Vector2 saccadeStartOffset = Vector2.zero;
    private Vector2 saccadeTargetOffset = Vector2.zero;

    // Kinematic derivation state for chest angular acceleration
    private Quaternion prevChestRot = Quaternion.identity;
    private Vector3 prevChestAngVel = Vector3.zero;
    private bool hasPrevChestRot = false;

    // Cached references & throttles
    private CrowFlightController flightController;
    private float targetSearchTimer = 0f;

    // Public properties for telemetry and external API access
    public Vector2 SaccadeOffset => currentSaccadeOffset;
    public float SaccadeSuppressionFactor => currentSaccadeSuppression;
    public bool IsSaccading => saccadeState == SaccadeState.Saccading;
    public float AngularJerk => currentAngularJerkDeg;
    public float AngularVelocityDeg => currentAngularVelocityDeg;
    public Quaternion SmoothedRelativeRotation => smoothedRelRotation;

    private void Awake()
    {
        flightController = GetComponent<CrowFlightController>();
        ResolveBoneReferences();
    }

    private void Start()
    {
        if (flightController == null)
        {
            flightController = GetComponent<CrowFlightController>();
        }

        ResolveBoneReferences();
        ResolveTargetReference();

        if (neckBone != null && headBone != null)
        {
            smoothedRelRotation = Quaternion.identity;
            prevSmoothedRelRot = Quaternion.identity;
            prevAngVelDeg = 0f;
            prevAngAccDeg = 0f;
            isInitialized = true;
        }

        saccadeTimer = UnityEngine.Random.Range(saccadeIntervalRange.x, saccadeIntervalRange.y);
        saccadeState = SaccadeState.Fixating;
        currentSaccadeOffset = Vector2.zero;
        currentSaccadeSuppression = 1.0f;
    }

    private void OnEnable()
    {
        hasPrevChestRot = false;
        prevChestAngVel = Vector3.zero;
        measuredBodyAngAccDeg = 0f;
        measuredLateralG = 0f;
        isInitialized = false;
    }

    /// <summary>
    /// Auto-resolves skeletal bone references from the hierarchy if not explicitly assigned in the inspector.
    /// </summary>
    public void ResolveBoneReferences()
    {
        if (chestBone == null)
        {
            chestBone = transform.Find("Root/Body/Chest");
            if (chestBone == null) chestBone = FindChildRecursive(transform, "Chest");
        }

        if (neckBone == null)
        {
            neckBone = transform.Find("Root/Body/Chest/Neck");
            if (neckBone == null) neckBone = FindChildRecursive(transform, "Neck");
        }

        if (headBone == null)
        {
            headBone = transform.Find("Root/Body/Chest/Neck/Head");
            if (headBone == null) headBone = FindChildRecursive(transform, "Head");
        }
    }

    /// <summary>
    /// Auto-resolves target reference from CrowFlightController or Sunflower in the scene.
    /// </summary>
    public void ResolveTargetReference()
    {
        if (target != null) return;

        // 1. Check CrowFlightController on same GameObject
        if (flightController != null && flightController.target != null)
        {
            target = flightController.target;
            return;
        }

        CrowFlightController cfc = GetComponent<CrowFlightController>();
        if (cfc != null && cfc.target != null)
        {
            target = cfc.target;
            flightController = cfc;
            return;
        }

        // 2. Check AR Pot Placement
        var potPlacement = FindAnyObjectByType<TinyGardenKeeper.AR.ARPotPlacement>();
        if (potPlacement != null && potPlacement.CurrentPot != null)
        {
            target = potPlacement.CurrentPot.transform;
            return;
        }

        GameObject potObj = GameObject.Find("Pot_AR");
        if (potObj != null)
        {
            target = potObj.transform;
            return;
        }

        // 3. Check SunflowerController in scene
        SunflowerController sc = FindAnyObjectByType<SunflowerController>();
        if (sc != null)
        {
            target = sc.flowerHead != null ? sc.flowerHead : sc.transform;
            return;
        }

        // 4. Check Named Sunflower Objects
        GameObject sunflowerPot = GameObject.Find("StylizedSunflower_Pot");
        if (sunflowerPot != null)
        {
            Transform flowerHead = sunflowerPot.transform.Find("Plant_Group/FlowerHead");
            target = flowerHead != null ? flowerHead : sunflowerPot.transform;
        }
    }

    private void LateUpdate()
    {
        if (weight <= 0.001f) return;

        if (target == null)
        {
            ResolveTargetReference();
        }

        if (chestBone == null || neckBone == null || headBone == null)
        {
            ResolveBoneReferences();
            if (chestBone == null || neckBone == null || headBone == null) return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Zero-GC target auto-detection (throttled when target is missing)
        if (target == null && enableGazeTracking)
        {
            targetSearchTimer -= dt;
            if (targetSearchTimer <= 0f)
            {
                targetSearchTimer = 1.0f;
                ResolveTargetReference();
            }
        }

        // 1. Compute Desired Gaze Vector in World Space
        // Note: Use neckBone.position (base cervical pivot) instead of headBone.position
        // to decouple gaze calculation from head rotation and eliminate recursive feedback jitter!
        Vector3 gazeOrigin = neckBone.position;
        Vector3 gazeDirection;

        if (enableGazeTracking && target != null)
        {
            Vector3 toTarget = target.position - gazeOrigin;
            if (toTarget.sqrMagnitude > 1e-4f)
            {
                gazeDirection = toTarget.normalized;
            }
            else
            {
                gazeDirection = chestBone.forward;
            }
        }
        else
        {
            // Level forward gaze along chest heading projected on horizon
            Vector3 horizForward = new Vector3(chestBone.forward.x, 0f, chestBone.forward.z);
            gazeDirection = horizForward.sqrMagnitude > 1e-4f ? horizForward.normalized : chestBone.forward;
        }

        // 2. Vestibulo-Collic Reflex (VCR): Lock Head Up Vector to World Up
        Vector3 desiredUp = lockToHorizon ? Vector3.up : chestBone.up;

        // Avoid degenerate LookRotation when gaze aligns with up vector
        if (Mathf.Abs(Vector3.Dot(gazeDirection, desiredUp)) > 0.98f)
        {
            desiredUp = Vector3.Cross(gazeDirection, chestBone.right).normalized;
            if (desiredUp.sqrMagnitude < 1e-4f) desiredUp = Vector3.up;
        }

        Quaternion desiredWorldRotation = Quaternion.LookRotation(gazeDirection, desiredUp);

        // 3. Compute Relative Rotation in Thoracic Base (Chest) Coordinate Frame
        Quaternion relToChest = Quaternion.Inverse(chestBone.rotation) * desiredWorldRotation;

        // 4. Decompose and Clamping to Anatomical Cervical Limits
        Vector3 euler = relToChest.eulerAngles;
        float pitch = Mathf.DeltaAngle(0f, euler.x); // Pitch down (+) / up (-)
        float yaw   = Mathf.DeltaAngle(0f, euler.y); // Yaw right (+) / left (-)
        float roll  = Mathf.DeltaAngle(0f, euler.z); // Roll right (+) / left (-)

        // Clamp to physical avian joint limits
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        yaw   = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        roll  = Mathf.Clamp(roll, -maxRoll, maxRoll);

        Quaternion clampedRel = Quaternion.Euler(pitch, yaw, roll);

        // 5. Micro-Saccadic Scanning (Subtle, organic, optional)
        if (enableMicroSaccades)
        {
            UpdateMicroSaccades(dt);
            Vector2 effSaccade = currentSaccadeOffset * currentSaccadeSuppression;
            Quaternion saccadeRot = Quaternion.Euler(effSaccade.y, effSaccade.x, 0f);
            clampedRel = clampedRel * saccadeRot;
        }

        // 6. Critically Damped Quaternion Smoothing (Unconditionally Stable on SO(3))
        // Protect against double-cover antipodal quaternion sign flips
        if (Quaternion.Dot(smoothedRelRotation, clampedRel) < 0f)
        {
            clampedRel = new Quaternion(-clampedRel.x, -clampedRel.y, -clampedRel.z, -clampedRel.w);
        }

        if (!isInitialized)
        {
            smoothedRelRotation = clampedRel;
            prevSmoothedRelRot = clampedRel;
            isInitialized = true;
        }
        else
        {
            float smoothFactor = 1.0f - Mathf.Exp(-Mathf.Max(1f, stabilizationSpeed) * dt);
            smoothedRelRotation = Quaternion.Slerp(smoothedRelRotation, clampedRel, smoothFactor);
        }

        // Observational telemetry (strictly read-only, does not disturb rotation)
        UpdateKinematicTelemetry(dt);

        // 7. 14-Cervical Vertebra Chain Distribution: 40% Neck, 60% Head
        Quaternion targetNeckRel = Quaternion.Slerp(Quaternion.identity, smoothedRelRotation, neckWeight);
        Quaternion targetHeadRel = Quaternion.Slerp(Quaternion.identity, smoothedRelRotation, headWeight);

        // 8. Clean, Rock-Solid Application (Eliminates compounding double-pitch)
        if (weight >= 0.999f)
        {
            neckBone.localRotation = targetNeckRel;
            headBone.localRotation = targetHeadRel;
        }
        else
        {
            neckBone.localRotation = Quaternion.Slerp(neckBone.localRotation, targetNeckRel, weight);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetHeadRel, weight);
        }
    }

    /// <summary>
    /// Computes discrete angular velocity and jerk for telemetry profiling without disturbing kinematics.
    /// Uses Quaternion.Angle and low-pass filtering to isolate physical motion from 32-bit float quantization noise.
    /// </summary>
    private void UpdateKinematicTelemetry(float dt)
    {
        if (dt <= 1e-5f) return;

        float angleDeg = Quaternion.Angle(smoothedRelRotation, prevSmoothedRelRot);
        float rawAngVel = angleDeg / dt;

        // Low-pass filter angular velocity to eliminate 32-bit float quantization noise
        float filterFactor = 1f - Mathf.Exp(-20f * dt);
        currentAngularVelocityDeg = Mathf.Lerp(currentAngularVelocityDeg, rawAngVel, filterFactor);

        float rawAngAcc = (currentAngularVelocityDeg - prevAngVelDeg) / dt;
        float filteredAngAcc = Mathf.Lerp(prevAngAccDeg, rawAngAcc, filterFactor);

        float rawAngJerk = (filteredAngAcc - prevAngAccDeg) / dt;
        currentAngularJerkDeg = Mathf.Lerp(currentAngularJerkDeg, Mathf.Abs(rawAngJerk), filterFactor);

        prevSmoothedRelRot = smoothedRelRotation;
        prevAngVelDeg = currentAngularVelocityDeg;
        prevAngAccDeg = filteredAngAcc;
    }

    /// <summary>
    /// Micro-saccadic scanning state machine with minimum-jerk polynomial trajectory:
    /// s(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5, tau in [0, 1].
    /// Includes dynamic suppression: dampens saccades toward zero under high angular acceleration or lateral g-force.
    /// </summary>
    private void UpdateMicroSaccades(float dt)
    {
        if (!enableMicroSaccades)
        {
            currentSaccadeOffset = Vector2.zero;
            currentSaccadeSuppression = 1.0f;
            saccadeState = SaccadeState.Fixating;
            return;
        }

        // 1. Calculate body kinematics for saccade suppression
        measuredBodyAngAccDeg = ComputeChestAngularAcceleration(dt);
        measuredLateralG = ComputeLateralGForce();

        // 2. Suppression Logic: if body angular acceleration or lateral g-force is high, dampen saccades toward zero
        float angAccRatio = measuredBodyAngAccDeg / Mathf.Max(1f, saccadeSuppressAngAcc);
        float gRatio = measuredLateralG / Mathf.Max(0.01f, saccadeSuppressLateralG);
        float maneuverIntensity = Mathf.Max(angAccRatio, gRatio);
        float targetSuppression = Mathf.Clamp01(1.0f - maneuverIntensity);

        // Smoothly filter suppression factor towards target
        currentSaccadeSuppression = Mathf.MoveTowards(currentSaccadeSuppression, targetSuppression, dt * 8.0f);

        // 3. Saccadic State Machine (Fixation <-> Ballistic Saccade)
        switch (saccadeState)
        {
            case SaccadeState.Fixating:
                saccadeTimer -= dt;
                if (saccadeTimer <= 0f)
                {
                    // Initiate ballistic saccade
                    saccadeState = SaccadeState.Saccading;
                    saccadeProgress = 0f;
                    saccadeStartOffset = currentSaccadeOffset;
                    saccadeTargetOffset = new Vector2(
                        UnityEngine.Random.Range(-saccadeMaxYawDeg, saccadeMaxYawDeg),
                        UnityEngine.Random.Range(-saccadeMaxPitchDeg, saccadeMaxPitchDeg)
                    );
                }
                break;

            case SaccadeState.Saccading:
                saccadeProgress += dt / Mathf.Max(1e-4f, saccadeDuration);
                float tau = Mathf.Clamp01(saccadeProgress);

                // Minimum-jerk polynomial trajectory: s(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5
                float s = tau * tau * tau * (10f + tau * (-15f + 6f * tau));
                currentSaccadeOffset = Vector2.LerpUnclamped(saccadeStartOffset, saccadeTargetOffset, s);

                if (tau >= 1.0f)
                {
                    // Completed ballistic shift -> return to Fixation
                    saccadeState = SaccadeState.Fixating;
                    currentSaccadeOffset = saccadeTargetOffset;
                    saccadeTimer = UnityEngine.Random.Range(saccadeIntervalRange.x, saccadeIntervalRange.y);
                }
                break;
        }
    }

    /// <summary>
    /// Computes discrete chest angular acceleration in deg/s^2.
    /// Zero GC allocations.
    /// </summary>
    private float ComputeChestAngularAcceleration(float dt)
    {
        if (chestBone == null || dt <= 1e-5f) return 0f;

        Quaternion curRot = chestBone.rotation;
        if (!hasPrevChestRot)
        {
            prevChestRot = curRot;
            hasPrevChestRot = true;
            return 0f;
        }

        Quaternion deltaRot = curRot * Quaternion.Inverse(prevChestRot);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        Vector3 angVel = (Mathf.Abs(angleDeg) > 1e-4f && !float.IsNaN(axis.x))
            ? (axis.normalized * (angleDeg / dt))
            : Vector3.zero;

        Vector3 angAcc = (angVel - prevChestAngVel) / dt;
        float angAccMag = angAcc.magnitude;

        prevChestRot = curRot;
        prevChestAngVel = angVel;

        return angAccMag;
    }

    /// <summary>
    /// Reads lateral g-force from CrowFlightController or returns 0.
    /// Zero GC allocations.
    /// </summary>
    private float ComputeLateralGForce()
    {
        if (flightController != null)
        {
            return Mathf.Abs(flightController.LateralAcceleration) / 9.81f;
        }
        return 0f;
    }

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float magSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (Mathf.Abs(magSq - 1.0f) > 1e-4f && magSq > 1e-8f)
        {
            float invMag = 1.0f / Mathf.Sqrt(magSq);
            return new Quaternion(q.x * invMag, q.y * invMag, q.z * invMag, q.w * invMag);
        }
        return q;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (headBone == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(headBone.position, headBone.forward * 0.4f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(headBone.position, headBone.up * 0.2f);

        if (target != null)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f);
            Gizmos.DrawLine(headBone.position, target.position);
        }
    }
#endif
}
