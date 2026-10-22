using System;
using UnityEngine;

/// <summary>
/// Ultra-Smooth Flight Dynamics and Aerodynamic Stabilization Controller for Corvids.
/// 
/// Core Flight Dynamics Upgrades:
/// 1. Analytical Closed-Form Kinematics: Closed-form position, velocity, and acceleration
///    for Orbit and Lemniscate Figure-8 hover modes, eliminating numerical differentiation jitter.
/// 2. Macro Atmospheric Thermals: Conflicting 1.5 Hz programmatic vertical bobbing eliminated.
///    Replaced with low-frequency macro atmospheric updrafts (f = 0.15 Hz, A = 0.08 m).
/// 3. True Coordinated Aerodynamic Banking:
///    phi = -atan2(a_lateral, 9.81) * bankAuthority.
///    Eliminates the 90° phase inversion in Figure-8 hover (wings level at crossing, peak roll at lobes).
/// 4. Flight Path Pitch Stabilization:
///    Pitch is driven purely by trajectory climb angle + static AoA trim (+2.5°), eliminating the violent ±45.7° dolphin wave.
/// 5. Dynamic Wing Flap Speed Modulation:
///    Flap frequency scales with turning load factor n = sqrt(1 + (a_lat/g)^2) and vertical climb rate.
/// 6. Corvid Cadence State Machine:
///    Flapping bursts (1.5 - 2.5s) alternating with soaring glides (2.0 - 4.5s), driving Mecanim parameters:
///    IsGliding, TurnRate, FlapSpeed, VerticalSpeed, Speed, BankAngle.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CrowFlightController : MonoBehaviour
{
    public enum FlightPattern
    {
        Orbit,
        Hover
    }

    public enum CadenceMode
    {
        Flapping,
        Gliding
    }

    [Header("Target & Mode")]
    [Tooltip("Target transform to orbit or hover above (e.g. Sunflower). Auto-finds sunflower if null.")]
    public Transform target;

    [Tooltip("Flight movement pattern (Circular Orbit or Lemniscate Figure-8 Hover).")]
    public FlightPattern pattern = FlightPattern.Orbit;

    [Header("Orbit Dynamics")]
    [Tooltip("Horizontal radius around the target in meters.")]
    [Range(0.6f, 4.0f)]
    public float orbitRadius = 1.25f;

    [Tooltip("Orbit speed in degrees per second.")]
    [Range(10f, 120f)]
    public float orbitSpeed = 40f;

    [Tooltip("Orbit direction (clockwise vs counter-clockwise).")]
    public bool clockwise = true;

    [Tooltip("Starting orbit angle in degrees.")]
    [Range(0f, 360f)]
    public float startOrbitAngle = 0f;

    [Header("Hover Dynamics (Lemniscate Figure-8)")]
    [Tooltip("Horizontal width (lateral span) of the figure-8 hover in meters.")]
    [Range(0.2f, 3.0f)]
    public float hoverWidth = 0.8f;

    [Tooltip("Forward/backward depth of the figure-8 hover in meters.")]
    [Range(0.2f, 3.0f)]
    public float hoverDepth = 0.5f;

    [Tooltip("Oscillation frequency of the figure-8 hover cycle in Hz.")]
    [Range(0.1f, 2.0f)]
    public float hoverFrequency = 0.35f;

    [Header("Altitude & Atmospheric Updrafts")]
    [Tooltip("Mean vertical height above the target in meters.")]
    [Range(0.4f, 3.0f)]
    public float heightAboveTarget = 0.65f;

    [Tooltip("Low-frequency macro atmospheric thermal updraft amplitude in meters (default = 0.08m).")]
    [Range(0.0f, 0.3f)]
    public float verticalBobAmplitude = 0.08f;

    [Tooltip("Low-frequency macro atmospheric thermal updraft frequency in Hz (f = 0.15 Hz).")]
    [Range(0.05f, 0.5f)]
    public float verticalBobFrequency = 0.15f;

    [Header("Aerodynamic Stroke Plane & Heave Alignment")]
    [Tooltip("Forward-inclined stroke plane anterior sweep angle on downstroke in degrees (+13.5 deg).")]
    public const float AnteriorSweepDeg = 13.5f;

    [Tooltip("Stroke plane retraction angle on upstroke in degrees (-11.0 deg).")]
    public const float RetractionDeg = -11.0f;

    [Tooltip("Nominal cruising flapping duration in seconds (T = 0.48s, 2.083 Hz).")]
    public const float CruisingStrokePeriod = 0.480f;

    [Tooltip("Asymmetric downstroke fraction of the flapping cycle (0.00s - 0.192s = 40%).")]
    public const float DownstrokeFraction = 0.40f;

    [Tooltip("Procedural aerodynamic body heave amplitude during flapping in meters.")]
    [Range(0.005f, 0.06f)]
    public float flapHeaveAmplitude = 0.024f;

    [Header("Aerodynamics & Coordinated Banking")]
    [Tooltip("Nominal reference bank angle limit in degrees.")]
    [Range(5f, 45f)]
    public float bankAngle = 16f;

    [Tooltip("Authority multiplier on coordinated bank angle.")]
    [Range(0.2f, 3.0f)]
    public float bankAuthority = 1.0f;

    [Tooltip("Hard physiological clamp on body bank angle in degrees.")]
    [Range(15f, 50f)]
    public float maxBankAngle = 35f;

    [Tooltip("Static aerodynamic Angle-of-Attack (AoA) pitch trim in degrees.")]
    [Range(-5f, 10f)]
    public float aoaTrim = 2.5f;

    [Tooltip("Smoothing speed for body orientation slerp.")]
    [Range(2f, 25f)]
    public float rotationSmoothing = 8f;

    [Header("Flight Cadence State Machine (Flap-Glide)")]
    [Tooltip("Enables natural corvid intermittent flap-bounding cadence.")]
    public bool enableCadence = true;

    [Tooltip("Min and Max duration of active flapping bursts in seconds.")]
    public Vector2 flapDurationRange = new Vector2(1.5f, 2.5f);

    [Tooltip("Min and Max duration of soaring glide phases in seconds.")]
    public Vector2 glideDurationRange = new Vector2(2.0f, 4.5f);

    [Tooltip("Base wing flap speed multiplier.")]
    [Range(0.5f, 2.0f)]
    public float baseFlapSpeed = 1.0f;

    [Tooltip("Flap speed boost factor per m/s of vertical climb rate.")]
    [Range(0.0f, 1.5f)]
    public float climbFlapMultiplier = 0.6f;

    [Tooltip("Normalizes TurnRate sent to Animator to [-1, 1] matching the 2D BlendTree.")]
    public bool normalizeAnimatorTurnRate = true;

    [Header("Micro-Animation: Aeroelastic Flutter")]
    [Tooltip("Enables dynamic aeroelastic flutter on wing feathers under aerodynamic pressure.")]
    public bool enableAeroelasticFlutter = true;

    [Tooltip("Base flutter deflection amplitude in degrees.")]
    [Range(0f, 10f)]
    public float baseFlutterAmplitude = 2.2f;

    [Tooltip("Flutter oscillation frequency in Hz.")]
    [Range(1f, 30f)]
    public float flutterFrequency = 18f;

    [Header("Micro-Animation: Turbulence & Gusts")]
    [Tooltip("Enables stochastic atmospheric turbulence and micro-gust perturbations.")]
    public bool enableMicroGusts = true;

    [Tooltip("Maximum turbulence wobble deflection in degrees.")]
    [Range(0f, 10f)]
    public float maxGustWobbleDeg = 1.8f;

    [Tooltip("Turbulence Perlin noise frequency in Hz.")]
    [Range(0.5f, 10f)]
    public float gustFrequency = 3.2f;

    [Header("Micro-Animation: Respiration & Exertion")]
    [Tooltip("Enables respiratory chest expansion and sternum pitch oscillation.")]
    public bool enablePectoralBreathing = true;

    [Tooltip("Resting respiration frequency in Hz.")]
    [Range(0.2f, 2f)]
    public float restingBreathFrequency = 0.95f;

    [Tooltip("Peak respiration frequency under maximum flight exertion in Hz.")]
    [Range(1f, 4f)]
    public float peakBreathFrequency = 2.20f;

    [Tooltip("Sternum pitch deflection amplitude in degrees.")]
    [Range(0.1f, 3f)]
    public float breathPitchAmplitude = 0.9f;

    [Header("Micro-Animation: Tail Aerodynamic Trim")]
    [Tooltip("Enables dynamic elevator, rudder, and cant trimming on the tail bone.")]
    public bool enableTailTrim = true;

    [Tooltip("Sensitivity of tail elevator pitch trim to flight path pitch / climb rate.")]
    [Range(0f, 2f)]
    public float tailElevatorSensitivity = 0.6f;

    [Tooltip("Sensitivity of tail rudder yaw trim to turning and bank angle.")]
    [Range(0f, 2f)]
    public float tailRudderSensitivity = 0.5f;

    [Tooltip("Multiplier for tail cant (roll) angle during bank maneuvers.")]
    [Range(0f, 2f)]
    public float tailCantMultiplier = 0.35f;

    [Header("Micro-Animation: Leg Drag Trim")]
    [Tooltip("Enables asymmetric leg drag droop as aerodynamic rudder during sharp turns.")]
    public bool enableLegDragTrim = true;

    [Tooltip("Maximum leg droop angle in degrees during sharp turns.")]
    [Range(0f, 45f)]
    public float dragDroopAngle = 13.0f;

    [Tooltip("Inverts pitch rotation direction for procedural leg droop (used on rigs with tucked hindlimb rest poses like crow).")]
    public bool invertLegDroopPitch = false;

    [Header("Procedural Bone References")]
    [Tooltip("Chest / sternum bone for breathing expansion and pitch oscillation.")]
    public Transform chestBone;

    [Tooltip("Tail bone for elevator, rudder, cant trim, and lateral fanning.")]
    public Transform tailBone;

    [Tooltip("Left wing primary flight feathers bone.")]
    public Transform wingL_Feathers;

    [Tooltip("Right wing primary flight feathers bone.")]
    public Transform wingR_Feathers;

    [Tooltip("Left wing secondary flight feathers bone.")]
    public Transform wingL_Secondary;

    [Tooltip("Right wing secondary flight feathers bone.")]
    public Transform wingR_Secondary;

    [Tooltip("Left leg bone for asymmetric drag trim.")]
    public Transform legL_Bone;

    [Tooltip("Right leg bone for asymmetric drag trim.")]
    public Transform legR_Bone;

    [Tooltip("Left foot / talon bone for aerodynamic drag flare.")]
    public Transform footL_Bone;

    [Tooltip("Right foot / talon bone for aerodynamic drag flare.")]
    public Transform footR_Bone;

    [Tooltip("Multiplier for talon flare pitch angle relative to leg droop during sharp turns.")]
    [Range(0.5f, 2.5f)]
    public float footDragFlareMultiplier = 1.35f;

    // Runtime Kinematic State (Public Read-Only)
    [Header("Telemetry (Read-Only)")]
    [SerializeField] private Vector3 currentVelocity;
    [SerializeField] private Vector3 currentAcceleration;
    [SerializeField] private float currentLateralAcceleration;
    [SerializeField] private float currentLoadFactor = 1.0f;
    [SerializeField] private float currentBankAngle;
    [SerializeField] private float currentFlightPitch;
    [SerializeField] private float currentFlapSpeed = 1.0f;
    [SerializeField] private bool isGliding = false;
    [SerializeField] private float flightExertion = 0.0f;
    [SerializeField] private float currentFlutterAmplitude = 0.0f;
    [SerializeField] private float currentBreathFrequency = 0.95f;
    [SerializeField] private float currentTurbulenceWobble = 0.0f;
    [SerializeField] private float currentTurbulenceIntensity = 0.0f;
    [SerializeField] private float currentStrokePhase = 0.0f;
    [SerializeField] private float currentStrokePlaneAngle = AnteriorSweepDeg;
    [SerializeField] private float currentFlapHeave = 0.0f;

    // Phase angle parameters
    private float currentOrbitAngleRad = 0f;
    private float hoverPhaseRad = 0f;
    private float thermalPhaseRad = 0f;
    private float flutterPhaseRad = 0f;
    private float breathPhaseRad = 0f;
    private float currentLegLDroop = 0f;
    private float currentLegRDroop = 0f;
    private float flapHeaveWeight = 1.0f;

    // Cadence state machine
    private CadenceMode cadenceMode = CadenceMode.Flapping;
    private float cadenceTimer = 2.0f;

    // Animator binding
    private Animator animator;
    private int idIsGliding;
    private int idTurnRate;
    private int idFlapSpeed;
    private int idVerticalSpeed;
    private int idSpeed;
    private int idBankAngle;
    private int idMicroFlutterWeight;
    private int idFlutterSpeed;
    private int idBreathWeight;
    private int idTurbulenceIntensity;
    private int idTalonTrimWeight;
    private int idStateFlap;
    private int idStateGlide;

    private bool hasIsGliding = false;
    private bool hasTurnRate = false;
    private bool hasFlapSpeed = false;
    private bool hasVerticalSpeed = false;
    private bool hasSpeed = false;
    private bool hasBankAngle = false;
    private bool hasMicroFlutterWeight = false;
    private bool hasFlutterSpeed = false;
    private bool hasBreathWeight = false;
    private bool hasTurbulenceIntensity = false;
    private bool hasTalonTrimWeight = false;

    // Properties for external API access
    public Vector3 Velocity => currentVelocity;
    public Vector3 Acceleration => currentAcceleration;
    public float LateralAcceleration => currentLateralAcceleration;
    public float LoadFactor => currentLoadFactor;
    public float CurrentBankAngle => currentBankAngle;
    public float CurrentFlightPitch => currentFlightPitch;
    public float CurrentFlapSpeed => currentFlapSpeed;
    public bool IsGliding => isGliding;
    public float StrokePhase => currentStrokePhase;
    public float StrokePlaneAngle => currentStrokePlaneAngle;
    public float FlapHeave => currentFlapHeave;

    public float Airspeed => currentVelocity.magnitude;
    public float DynamicPressureIndex => Mathf.Pow(Airspeed / 0.87f, 2f);
    public float TurnRateRad
    {
        get
        {
            float horizSpeed = Mathf.Sqrt(currentVelocity.x * currentVelocity.x + currentVelocity.z * currentVelocity.z);
            return horizSpeed > 1e-4f ? (currentLateralAcceleration / horizSpeed) : 0f;
        }
    }
    public float ClimbRate => currentVelocity.y;
    public float VerticalAcceleration => currentAcceleration.y;
    public float FlightExertion => flightExertion;
    public float FlutterAmplitude => currentFlutterAmplitude;
    public float BreathFrequency => currentBreathFrequency;
    public float TurbulenceWobble => currentTurbulenceWobble;
    public float TurbulenceIntensity => currentTurbulenceIntensity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        CacheAnimatorParameters();
        AutoResolveBones();
    }

    private void Start()
    {
        // Auto-resolve procedural bone references if null
        AutoResolveBones();

        // Initialize phase angles
        currentOrbitAngleRad = startOrbitAngle * Mathf.Deg2Rad;
        hoverPhaseRad = 0f;
        thermalPhaseRad = 0f;
        flutterPhaseRad = 0f;
        breathPhaseRad = 0f;
        currentStrokePhase = 0f;
        currentStrokePlaneAngle = AnteriorSweepDeg;
        flapHeaveWeight = 1.0f;

        currentBreathFrequency = restingBreathFrequency;
        flightExertion = 0f;

        // Auto-find sunflower target if null
        ResolveTargetReference();

        // Initialize cadence timer
        cadenceMode = CadenceMode.Flapping;
        cadenceTimer = UnityEngine.Random.Range(flapDurationRange.x, flapDurationRange.y);
        isGliding = false;

        // Evaluate and set initial kinematic pose
        EvaluateKinematics(0f, out Vector3 initialPos, out Vector3 initialVel, out Vector3 initialAcc);
        transform.position = initialPos;
        currentVelocity = initialVel;
        currentAcceleration = initialAcc;

        Vector3 horizVel = new Vector3(initialVel.x, 0f, initialVel.z);
        if (horizVel.sqrMagnitude > 1e-4f)
        {
            transform.rotation = Quaternion.LookRotation(horizVel.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Auto-resolves procedural bone transform references from the skeleton hierarchy.
    /// </summary>
    [ContextMenu("Auto-Resolve Bone References")]
    public void AutoResolveBones()
    {
        // 1. Direct path lookup from root/body standard hierarchy
        if (chestBone == null) chestBone = transform.Find("Root/Body/Chest");
        if (tailBone == null) tailBone = transform.Find("Root/Body/Tail");
        if (wingL_Feathers == null) wingL_Feathers = transform.Find("Root/Body/Chest/Wing_L_Upper/Wing_L_Forearm/Wing_L_Hand/Wing_L_Feathers");
        if (wingR_Feathers == null) wingR_Feathers = transform.Find("Root/Body/Chest/Wing_R_Upper/Wing_R_Forearm/Wing_R_Hand/Wing_R_Feathers");
        if (wingL_Secondary == null) wingL_Secondary = transform.Find("Root/Body/Chest/Wing_L_Upper/Wing_L_Secondary");
        if (wingR_Secondary == null) wingR_Secondary = transform.Find("Root/Body/Chest/Wing_R_Upper/Wing_R_Secondary");
        if (legL_Bone == null) legL_Bone = transform.Find("Root/Body/Leg_L");
        if (legR_Bone == null) legR_Bone = transform.Find("Root/Body/Leg_R");
        if (footL_Bone == null) footL_Bone = transform.Find("Root/Body/Leg_L/Foot_L");
        if (footR_Bone == null) footR_Bone = transform.Find("Root/Body/Leg_R/Foot_R");

        // 2. Recursive fallback if any bone was not found via standard direct path
        if (chestBone == null || tailBone == null || wingL_Feathers == null || wingR_Feathers == null ||
            wingL_Secondary == null || wingR_Secondary == null || legL_Bone == null || legR_Bone == null ||
            footL_Bone == null || footR_Bone == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                Transform t = allChildren[i];
                string boneName = t.name;

                if (chestBone == null && boneName == "Chest") chestBone = t;
                else if (tailBone == null && boneName == "Tail") tailBone = t;
                else if (wingL_Feathers == null && (boneName == "Wing_L_Feathers" || boneName.Contains("Wing_L_Feather"))) wingL_Feathers = t;
                else if (wingR_Feathers == null && (boneName == "Wing_R_Feathers" || boneName.Contains("Wing_R_Feather"))) wingR_Feathers = t;
                else if (wingL_Secondary == null && (boneName == "Wing_L_Secondary" || boneName.Contains("Wing_L_Sec"))) wingL_Secondary = t;
                else if (wingR_Secondary == null && (boneName == "Wing_R_Secondary" || boneName.Contains("Wing_R_Sec"))) wingR_Secondary = t;
                else if (legL_Bone == null && (boneName == "Leg_L" || boneName.Contains("Leg_L"))) legL_Bone = t;
                else if (legR_Bone == null && (boneName == "Leg_R" || boneName.Contains("Leg_R"))) legR_Bone = t;
                else if (footL_Bone == null && (boneName == "Foot_L" || boneName.Contains("Foot_L"))) footL_Bone = t;
                else if (footR_Bone == null && (boneName == "Foot_R" || boneName.Contains("Foot_R"))) footR_Bone = t;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (chestBone == null || tailBone == null || wingL_Feathers == null || wingR_Feathers == null ||
            wingL_Secondary == null || wingR_Secondary == null || legL_Bone == null || legR_Bone == null ||
            footL_Bone == null || footR_Bone == null)
        {
            AutoResolveBones();
        }
    }
#endif

    /// <summary>
    /// Caches Animator parameter hashes and checks existence to prevent console warnings.
    /// </summary>
    private void CacheAnimatorParameters()
    {
        if (animator == null) return;

        idIsGliding = Animator.StringToHash("IsGliding");
        idTurnRate = Animator.StringToHash("TurnRate");
        idFlapSpeed = Animator.StringToHash("FlapSpeed");
        idVerticalSpeed = Animator.StringToHash("VerticalSpeed");
        idSpeed = Animator.StringToHash("Speed");
        idBankAngle = Animator.StringToHash("BankAngle");
        idMicroFlutterWeight = Animator.StringToHash("MicroFlutterWeight");
        idFlutterSpeed = Animator.StringToHash("FlutterSpeed");
        idBreathWeight = Animator.StringToHash("BreathWeight");
        idTurbulenceIntensity = Animator.StringToHash("TurbulenceIntensity");
        idTalonTrimWeight = Animator.StringToHash("TalonTrimWeight");
        idStateFlap = Animator.StringToHash("Flap_State");
        idStateGlide = Animator.StringToHash("Glide_State");

        foreach (var param in animator.parameters)
        {
            if (param.nameHash == idIsGliding && param.type == AnimatorControllerParameterType.Bool) hasIsGliding = true;
            else if (param.nameHash == idTurnRate && param.type == AnimatorControllerParameterType.Float) hasTurnRate = true;
            else if (param.nameHash == idFlapSpeed && param.type == AnimatorControllerParameterType.Float) hasFlapSpeed = true;
            else if (param.nameHash == idVerticalSpeed && param.type == AnimatorControllerParameterType.Float) hasVerticalSpeed = true;
            else if (param.nameHash == idSpeed && param.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            else if (param.nameHash == idBankAngle && param.type == AnimatorControllerParameterType.Float) hasBankAngle = true;
            else if (param.nameHash == idMicroFlutterWeight && param.type == AnimatorControllerParameterType.Float) hasMicroFlutterWeight = true;
            else if (param.nameHash == idFlutterSpeed && param.type == AnimatorControllerParameterType.Float) hasFlutterSpeed = true;
            else if (param.nameHash == idBreathWeight && param.type == AnimatorControllerParameterType.Float) hasBreathWeight = true;
            else if (param.nameHash == idTurbulenceIntensity && param.type == AnimatorControllerParameterType.Float) hasTurbulenceIntensity = true;
            else if (param.nameHash == idTalonTrimWeight && param.type == AnimatorControllerParameterType.Float) hasTalonTrimWeight = true;
        }
    }

    /// <summary>
    /// Auto-finds Sunflower in the scene hierarchy.
    /// </summary>
    public void ResolveTargetReference()
    {
        if (target != null) return;

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

        SunflowerController sc = FindAnyObjectByType<SunflowerController>();
        if (sc != null)
        {
            target = sc.flowerHead != null ? sc.flowerHead : sc.transform;
            return;
        }

        GameObject sunObj = GameObject.Find("StylizedSunflower_Pot");
        if (sunObj != null)
        {
            Transform head = sunObj.transform.Find("Plant_Group/FlowerHead");
            target = head != null ? head : sunObj.transform;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (target == null)
        {
            ResolveTargetReference();
        }

        // Smoothly transition procedural heave weight based on cadence mode (gliding = 0, flapping = 1)
        flapHeaveWeight = Mathf.MoveTowards(flapHeaveWeight, isGliding ? 0f : 1f, dt * 5.0f);

        // 1. Advance Phase Angles
        AdvancePhase(dt);

        // Synchronize stroke phase with Mecanim Animator when playing Flap_State
        if (animator != null && !isGliding)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == idStateFlap)
            {
                float animStrokePhase = (stateInfo.normalizedTime % 1.0f + 1.0f) % 1.0f;
                currentStrokePhase = animStrokePhase;
            }
        }

        // 2. Evaluate Analytical Closed-Form Kinematics (Position, Velocity, Acceleration)
        EvaluateKinematics(0f, out Vector3 targetPos, out Vector3 velocity, out Vector3 acceleration);
        currentVelocity = velocity;
        currentAcceleration = acceleration;

        // Update Position
        transform.position = targetPos;

        // 3. Update Corvid Flight Cadence State Machine
        UpdateCadenceStateMachine(dt);

        // 4. Dynamic Wing Flap Speed Modulation
        UpdateFlapSpeedModulation(dt);

        // 5. Update Flight Exertion Accumulator & Respiration Dynamics
        UpdateExertionAndRespiration(dt);

        // 6. Compute Perlin Gradient Noise Micro-Wobble for Turbulence
        UpdateTurbulence(dt);

        // 7. Dynamic Aeroelastic Flutter Dynamics
        UpdateFlutterDynamics(dt);

        // 8. Coordinated Aerodynamic Orientation (Heading + Pitch + Roll Banking + Turbulence)
        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
        float horizSpeed = horizVel.magnitude;

        if (horizSpeed > 1e-4f)
        {
            Vector3 forwardHoriz = horizVel / horizSpeed;
            Vector3 rightHoriz = Vector3.Cross(Vector3.up, forwardHoriz);

            // Centripetal / Lateral Acceleration along local right axis
            float aRight = Vector3.Dot(acceleration, rightHoriz);
            currentLateralAcceleration = Mathf.Abs(aRight);

            // Coordinated Bank Angle: phi = -atan2(a_lateral_right, g) * bankAuthority
            // Accelerating right (aRight > 0) rolls right wing down (negative roll).
            // Accelerating left (aRight < 0) rolls left wing down (positive roll).
            float idealBankDeg = -Mathf.Atan2(aRight, 9.81f) * Mathf.Rad2Deg * bankAuthority;
            currentBankAngle = Mathf.Clamp(idealBankDeg, -maxBankAngle, maxBankAngle);

            // Pitch: based purely on trajectory climb angle + static AoA trim (+2.5 deg)
            float climbAngleDeg = Mathf.Atan2(velocity.y, horizSpeed) * Mathf.Rad2Deg;
            currentFlightPitch = climbAngleDeg + aoaTrim;

            // Compose Heading, Pitch, and Roll Quaternions
            Quaternion headingRot = Quaternion.LookRotation(forwardHoriz, Vector3.up);
            Quaternion pitchRot = Quaternion.AngleAxis(-currentFlightPitch, Vector3.right);
            Quaternion rollRot = Quaternion.AngleAxis(currentBankAngle, Vector3.forward);

            Quaternion desiredOrientation = headingRot * pitchRot * rollRot;

            // Micro-Gust Turbulence wobble on body orientation
            if (enableMicroGusts && Mathf.Abs(currentTurbulenceWobble) > 1e-4f)
            {
                Quaternion gustWobble = Quaternion.Euler(currentTurbulenceWobble * 0.35f, 0f, currentTurbulenceWobble);
                desiredOrientation = desiredOrientation * gustWobble;
            }

            // Slerp orientation smoothly
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredOrientation, dt * rotationSmoothing);
        }

        // 9. Leg Drag Trim Dynamics
        UpdateLegDragTrim(dt);

        // 10. Feed Mecanim Animator Parameters
        UpdateAnimatorParameters(horizSpeed);
    }

    /// <summary>
    /// Evaluates after Mecanim Animator execution to layer dynamic procedural bone micro-offsets:
    /// 1. Aeroelastic flutter onto primary & secondary wing feathers.
    /// 2. Tail elevator, rudder, and cant trim, plus lateral rectrices fanning.
    /// 3. Pectoral breathing (sternum pitch + thoracic scaling) onto chest bone.
    /// 4. Asymmetric foot drag trim on inside leg during sharp turns (|dpsi/dt| > 35 deg/s).
    /// Guarantees zero GC allocations per frame.
    /// </summary>
    private void LateUpdate()
    {
        // 1. Dynamic Aeroelastic Flutter onto wing feathers and secondaries
        if (enableAeroelasticFlutter && currentFlutterAmplitude > 1e-4f)
        {
            ApplyAeroelasticFlutter();
        }

        // 2. Dynamic Elevator, Rudder, and Cant trim onto tailBone, plus lateral fanning
        if (tailBone != null && enableTailTrim)
        {
            ApplyTailTrimAndFanning();
        }

        // 3. Pectoral breathing (sternum pitch + thoracic scale) onto chestBone
        if (chestBone != null && enablePectoralBreathing)
        {
            ApplyPectoralBreathing();
        }

        // 4. Asymmetric foot drag trim on legL_Bone / legR_Bone when turning sharply
        if (enableLegDragTrim)
        {
            ApplyLegDragTrim();
        }
    }

    /// <summary>
    /// Computes flight exertion accumulator flightExertion (dE/dt > 0 when flapping, dE/dt < 0 when gliding)
    /// and advances dynamic pectoral respiration frequency.
    /// </summary>
    private void UpdateExertionAndRespiration(float dt)
    {
        if (isGliding)
        {
            // Gliding recovery: steady exponential decay towards resting state (dE/dt < 0)
            flightExertion = Mathf.Max(0f, flightExertion - 0.22f * dt);
        }
        else
        {
            // Flapping buildup: scales with flap speed and aerodynamic load factor (dE/dt > 0)
            float exertionRate = 0.35f * (currentFlapSpeed / Mathf.Max(0.1f, baseFlapSpeed)) * currentLoadFactor;
            flightExertion = Mathf.Min(1.0f, flightExertion + exertionRate * dt);
        }

        // Respiration frequency scales dynamically between resting and peak rates
        currentBreathFrequency = Mathf.Lerp(restingBreathFrequency, peakBreathFrequency, flightExertion);

        // Advance respiratory phase angle monotonically
        breathPhaseRad += currentBreathFrequency * Mathf.PI * 2f * dt;
        breathPhaseRad = Mathf.Repeat(breathPhaseRad, Mathf.PI * 2f);
    }

    /// <summary>
    /// Computes Perlin gradient noise micro-wobble and turbulence intensity for atmospheric micro-gusts.
    /// </summary>
    private void UpdateTurbulence(float dt)
    {
        if (!enableMicroGusts)
        {
            currentTurbulenceWobble = 0f;
            currentTurbulenceIntensity = 0f;
            return;
        }

        float tNoise = Time.time * gustFrequency;
        float rawNoise1 = (Mathf.PerlinNoise(tNoise, 19.83f) - 0.5f) * 2f;
        float rawNoise2 = (Mathf.PerlinNoise(tNoise * 0.7f, 62.45f) - 0.5f) * 2f;

        float speedNorm = Mathf.Clamp01(Airspeed / 0.87f);
        currentTurbulenceWobble = rawNoise1 * maxGustWobbleDeg * speedNorm;
        currentTurbulenceIntensity = Mathf.Clamp01((Mathf.Abs(rawNoise1) * 0.6f + Mathf.Abs(rawNoise2) * 0.4f) * speedNorm);
    }

    /// <summary>
    /// Computes aeroelastic flutter amplitude based on dynamic pressure index and turning load factor.
    /// Smoothly filters amplitude to eliminate numerical step jitter and strictly bounds to QA limits (<= 3.2 deg).
    /// </summary>
    private void UpdateFlutterDynamics(float dt)
    {
        if (!enableAeroelasticFlutter)
        {
            currentFlutterAmplitude = 0f;
            return;
        }

        // Dynamic pressure index q = (v / 0.87)^2, load factor n
        float targetAmplitude = baseFlutterAmplitude * DynamicPressureIndex * currentLoadFactor;
        targetAmplitude = Mathf.Clamp(targetAmplitude, 0f, 3.2f);

        // Smoothly filter amplitude to eliminate numerical step jitter
        currentFlutterAmplitude = Mathf.Lerp(currentFlutterAmplitude, targetAmplitude, dt * 8.0f);

        // Advance flutter phase angle monotonically
        flutterPhaseRad += flutterFrequency * Mathf.PI * 2f * dt;
        flutterPhaseRad = Mathf.Repeat(flutterPhaseRad, Mathf.PI * 2f);
    }

    /// <summary>
    /// Evaluates target leg droop for asymmetric foot drag trim when turning sharply (|dpsi/dt| > 35 deg/s).
    /// </summary>
    private void UpdateLegDragTrim(float dt)
    {
        if (!enableLegDragTrim)
        {
            currentLegLDroop = Mathf.Lerp(currentLegLDroop, 0f, dt * 8.0f);
            currentLegRDroop = Mathf.Lerp(currentLegRDroop, 0f, dt * 8.0f);
            return;
        }

        // Turn rate in degrees per second
        float turnRateDegSec = TurnRateRad * Mathf.Rad2Deg;
        float absTurnRate = Mathf.Abs(turnRateDegSec);

        float targetL = 0f;
        float targetR = 0f;

        // Trigger drag trim when turn rate exceeds 35 deg/s
        if (absTurnRate > 35.0f)
        {
            float droopFactor = Mathf.Clamp01((absTurnRate - 35.0f) / 25.0f);
            float droopAngle = droopFactor * dragDroopAngle;

            // Inside leg droops to create aerodynamic yaw braking into the turn
            if (currentBankAngle > 0f)
            {
                targetL = droopAngle;
            }
            else if (currentBankAngle < 0f)
            {
                targetR = droopAngle;
            }
        }

        currentLegLDroop = Mathf.Lerp(currentLegLDroop, targetL, dt * 6.0f);
        currentLegRDroop = Mathf.Lerp(currentLegRDroop, targetR, dt * 6.0f);
    }

    /// <summary>
    /// Layers dynamic aeroelastic flutter oscillations onto primary and secondary flight feathers.
    /// Calibrated to cleanly layer on top of the -20 deg BDC rebound and apex flick without creating
    /// joint pops or numerical jitter.
    /// Bounded within strict QA amplitude limits (<= 3.5 deg).
    /// Zero GC allocations per frame.
    /// </summary>
    private void ApplyAeroelasticFlutter()
    {
        // Compute stroke phase flutter modulation envelope:
        // Smoothly attenuates high-frequency flutter during crisp BDC rebound (u = 0.40)
        // and apex flick (u = 0.00 / 1.00) transients to prevent joint pops and angular jerk spikes.
        float flutterEnvelope = 1.0f;
        if (!isGliding)
        {
            // BDC turnaround window (u = 0.40 +/- 0.06): smooth Hermite easing
            float distBDC = Mathf.Abs(currentStrokePhase - DownstrokeFraction);
            if (distBDC < 0.06f)
            {
                float tBDC = distBDC / 0.06f;
                float smoothBDC = tBDC * tBDC * (3f - 2f * tBDC);
                flutterEnvelope *= Mathf.Lerp(0.32f, 1.0f, smoothBDC);
            }

            // Apex turnaround window (u = 0.00 / 1.00 +/- 0.05): smooth Hermite easing
            float distApex = Mathf.Min(currentStrokePhase, 1.0f - currentStrokePhase);
            if (distApex < 0.05f)
            {
                float tApex = distApex / 0.05f;
                float smoothApex = tApex * tApex * (3f - 2f * tApex);
                flutterEnvelope *= Mathf.Lerp(0.35f, 1.0f, smoothApex);
            }
        }

        float effAmplitude = currentFlutterAmplitude * flutterEnvelope;
        if (effAmplitude < 1e-4f) return;

        float sinFlutter = Mathf.Sin(flutterPhaseRad);
        float cosFlutter = Mathf.Cos(flutterPhaseRad);

        // Primary feathers: pitch oscillation + spanwise roll torsion
        // Strictly clamped within +/-3.2 deg to conform to QA limits and prevent pops
        float tipPitch = Mathf.Clamp(sinFlutter * effAmplitude, -3.2f, 3.2f);
        float tipRoll  = Mathf.Clamp(cosFlutter * (effAmplitude * 0.40f), -2.4f, 2.4f);

        Quaternion flutterRotL = Quaternion.Euler(tipPitch, 0f, tipRoll);
        Quaternion flutterRotR = Quaternion.Euler(tipPitch, 0f, -tipRoll);

        if (wingL_Feathers != null)
        {
            wingL_Feathers.localRotation *= flutterRotL;
        }
        if (wingR_Feathers != null)
        {
            wingR_Feathers.localRotation *= flutterRotR;
        }

        // Secondary feathers: phase lag (~0.7 rad) and reduced amplitude
        float secPhase = flutterPhaseRad - 0.70f;
        float secPitch = Mathf.Clamp(Mathf.Sin(secPhase) * (effAmplitude * 0.55f), -2.2f, 2.2f);
        float secRoll  = Mathf.Clamp(Mathf.Cos(secPhase) * (effAmplitude * 0.20f), -1.6f, 1.6f);

        Quaternion secRotL = Quaternion.Euler(secPitch, 0f, secRoll);
        Quaternion secRotR = Quaternion.Euler(secPitch, 0f, -secRoll);

        if (wingL_Secondary != null)
        {
            wingL_Secondary.localRotation *= secRotL;
        }
        if (wingR_Secondary != null)
        {
            wingR_Secondary.localRotation *= secRotR;
        }
    }

    /// <summary>
    /// Layers dynamic elevator, rudder, and cant trim onto the tail bone, plus lateral fanning.
    /// During flapping, couples tail elevator and rudder trim to the stroke phase and load factor.
    /// Scales lateral tail fanning (tailBone.localScale.x) additively/multiplicatively with base clip fanning,
    /// clamped strictly to [1.0f, 1.45f] to prevent clipping or distorted scaling.
    /// Zero GC allocations per frame.
    /// </summary>
    private void ApplyTailTrimAndFanning()
    {
        // 1. Elevator Trim: counteracts flight path climb angle + static AoA trim
        float baseElevatorPitch = -Mathf.Clamp(currentFlightPitch * tailElevatorSensitivity, -20f, 20f);

        // Couple elevator trim to stroke phase and load factor during flapping
        float strokeElevatorTrim = 0f;
        if (!isGliding && flapHeaveWeight > 0.01f)
        {
            if (currentStrokePhase < DownstrokeFraction)
            {
                // Downstroke downwash moment counter-trim (peaks at mid-downstroke, scales with load factor)
                float tau = currentStrokePhase / DownstrokeFraction;
                strokeElevatorTrim = Mathf.Sin(tau * Mathf.PI) * 7.5f * currentLoadFactor;
            }
            else
            {
                // Upstroke recovery depression
                float tau = (currentStrokePhase - DownstrokeFraction) / (1.0f - DownstrokeFraction);
                strokeElevatorTrim = -Mathf.Sin(tau * Mathf.PI) * 3.2f;
            }
            strokeElevatorTrim *= flapHeaveWeight;
        }
        float elevatorPitch = Mathf.Clamp(baseElevatorPitch + strokeElevatorTrim, -22f, 22f);

        // 2. Rudder Trim: counters adverse yaw and coordinates turn
        float turnRateDegSec = TurnRateRad * Mathf.Rad2Deg;
        float baseRudderYaw = (-currentBankAngle * 0.7f + turnRateDegSec * 0.3f) * tailRudderSensitivity;

        // Couple rudder trim to stroke phase and load factor during flapping turns
        float strokeRudderTrim = 0f;
        if (!isGliding && Mathf.Abs(turnRateDegSec) > 3.0f && flapHeaveWeight > 0.01f)
        {
            // Asymmetric wing drag during banked downstroke produces cyclic yaw moments
            float strokeYawOscillation = Mathf.Sin(currentStrokePhase * Mathf.PI * 2f);
            strokeRudderTrim = strokeYawOscillation * 2.0f * (currentLoadFactor - 0.5f) * Mathf.Sign(turnRateDegSec) * flapHeaveWeight;
        }
        float rudderYaw = Mathf.Clamp(baseRudderYaw * currentLoadFactor + strokeRudderTrim, -25f, 25f);

        // 3. Cant Trim: rolls tail into bank for aerodynamic stability
        float cantRoll = Mathf.Clamp(currentBankAngle * tailCantMultiplier * currentLoadFactor, -16f, 16f);

        // Layer rotation additively onto animated local rotation
        tailBone.localRotation *= Quaternion.Euler(elevatorPitch, rudderYaw, cantRoll);

        // 4. Lateral Tail Fanning:
        // Works additively/multiplicatively with the base clip's +25% fanning without clipping or distorted scaling.
        // Clamps maximum x-scale strictly to 1.45f.
        Vector3 curScale = tailBone.localScale;
        float baseScaleX = curScale.x > 0.1f ? curScale.x : 1.0f;

        float turnIntensity = Mathf.Clamp01(Mathf.Abs(turnRateDegSec) / 45f);
        float loadIntensity = Mathf.Clamp01(currentLoadFactor - 1.0f);
        float proceduralFanningFactor = 0.28f * Mathf.Clamp01(turnIntensity * 0.65f + loadIntensity * 0.35f);

        // Scale multiplicatively with animated base scale and clamp to 1.45f max
        float combinedScaleX = baseScaleX * (1.0f + proceduralFanningFactor);
        float clampedScaleX = Mathf.Clamp(combinedScaleX, 1.0f, 1.45f);

        tailBone.localScale = new Vector3(clampedScaleX, curScale.y, curScale.z);
    }

    /// <summary>
    /// Layers pectoral breathing oscillations (sternum pitch + thoracic scaling) onto the chest bone.
    /// Frequency scales dynamically with flight exertion accumulator.
    /// Zero GC allocations per frame.
    /// </summary>
    private void ApplyPectoralBreathing()
    {
        float breathCycle = Mathf.Sin(breathPhaseRad);

        // Sternum pitch oscillation
        float sternumPitch = breathCycle * breathPitchAmplitude;
        chestBone.localRotation *= Quaternion.Euler(sternumPitch, 0f, 0f);

        // Thoracic lateral and vertical volumetric expansion
        float exertionMultiplier = Mathf.Lerp(0.5f, 1.2f, flightExertion);
        float scaleExpansion = breathCycle * 0.030f * exertionMultiplier;
        chestBone.localScale = new Vector3(1f + scaleExpansion, 1f + scaleExpansion * 0.8f, 1f + scaleExpansion * 0.5f);
    }

    /// <summary>
    /// Layers asymmetric foot drag trim on legs when turning sharply (|dpsi/dt| > 35 deg/s).
    /// Inside leg droops into the slipstream to act as an aerodynamic drag pivot.
    /// Zero GC allocations per frame.
    /// </summary>
    private void ApplyLegDragTrim()
    {
        float sign = invertLegDroopPitch ? -1.0f : 1.0f;
        if (legL_Bone != null && currentLegLDroop > 0.01f)
        {
            legL_Bone.localRotation *= Quaternion.Euler(currentLegLDroop * sign, 0f, 0f);
            if (footL_Bone != null)
            {
                footL_Bone.localRotation *= Quaternion.Euler(currentLegLDroop * footDragFlareMultiplier * sign, 0f, 0f);
            }
        }
        if (legR_Bone != null && currentLegRDroop > 0.01f)
        {
            legR_Bone.localRotation *= Quaternion.Euler(currentLegRDroop * sign, 0f, 0f);
            if (footR_Bone != null)
            {
                footR_Bone.localRotation *= Quaternion.Euler(currentLegRDroop * footDragFlareMultiplier * sign, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// Increments phase angles monotonically to maintain infinite numerical precision and smooth runtime parameter tuning.
    /// </summary>
    private void AdvancePhase(float dt)
    {
        if (pattern == FlightPattern.Orbit)
        {
            float direction = clockwise ? -1f : 1f;
            float orbitRadPerSec = orbitSpeed * Mathf.Deg2Rad;
            currentOrbitAngleRad += direction * orbitRadPerSec * dt;
            currentOrbitAngleRad = Mathf.Repeat(currentOrbitAngleRad, Mathf.PI * 2f);
        }
        else
        {
            float hoverRadPerSec = hoverFrequency * Mathf.PI * 2f;
            hoverPhaseRad += hoverRadPerSec * dt;
            hoverPhaseRad = Mathf.Repeat(hoverPhaseRad, Mathf.PI * 2f);
        }

        // Advance atmospheric thermal draft phase
        float thermalRadPerSec = verticalBobFrequency * Mathf.PI * 2f;
        thermalPhaseRad += thermalRadPerSec * dt;
        thermalPhaseRad = Mathf.Repeat(thermalPhaseRad, Mathf.PI * 2f);

        // Advance flapping stroke phase (T = 0.48s cruising cadence)
        if (!isGliding)
        {
            float flapFreq = (1.0f / CruisingStrokePeriod) * currentFlapSpeed;
            currentStrokePhase += flapFreq * dt;
            currentStrokePhase = Mathf.Repeat(currentStrokePhase, 1.0f);
        }
    }

    /// <summary>
    /// Computes aerodynamic heave displacement, vertical velocity, and acceleration coordinated with
    /// the forward-inclined stroke plane (+13.5 deg anterior sweep on downstroke, -11.0 deg retraction on upstroke).
    /// Downstroke (0 <= u < 0.40, 40%): drives upward with anterior sweep.
    /// Upstroke (0.40 <= u < 1.00, 60%): gently settles with retraction.
    /// Guarantees C2 seam continuity and zero GC allocations per frame.
    /// </summary>
    public void ComputeAerodynamicStrokeHeave(float lookaheadDt, out float yHeave, out float vyHeave, out float ayHeave)
    {
        if (flapHeaveWeight <= 1e-4f)
        {
            yHeave = 0f;
            vyHeave = 0f;
            ayHeave = 0f;
            currentStrokePlaneAngle = 0f;
            currentFlapHeave = 0f;
            return;
        }

        float f_flap = (1.0f / CruisingStrokePeriod) * currentFlapSpeed;
        float phase = currentStrokePhase + f_flap * lookaheadDt;
        phase = Mathf.Repeat(phase, 1.0f);

        if (phase < DownstrokeFraction)
        {
            // --- DOWNSTROKE (0.00 to 0.40, 40% of cycle) ---
            // Forward-inclined stroke plane (+13.5 deg anterior sweep)
            currentStrokePlaneAngle = AnteriorSweepDeg;
            float strokeCos = Mathf.Cos(AnteriorSweepDeg * Mathf.Deg2Rad);

            // Normalized downstroke progress tau in [0, 1]
            float tau = phase / DownstrokeFraction;
            float cosTau = Mathf.Cos(tau * Mathf.PI);
            float sinTau = Mathf.Sin(tau * Mathf.PI);

            // Displacement drives upward from -flapHeaveAmplitude to +flapHeaveAmplitude
            yHeave = -flapHeaveAmplitude * cosTau * flapHeaveWeight;

            // Velocity: dy/dt = A * PI * (dtau/dt) * sin(tau * PI) * cos(anteriorSweep)
            float dtau_dt = (1.0f / DownstrokeFraction) * f_flap;
            vyHeave = flapHeaveAmplitude * Mathf.PI * dtau_dt * sinTau * strokeCos * flapHeaveWeight;

            // Acceleration: d2y/dt2 = A * PI^2 * (dtau/dt)^2 * cos(tau * PI) * cos(anteriorSweep)
            ayHeave = flapHeaveAmplitude * (Mathf.PI * Mathf.PI) * (dtau_dt * dtau_dt) * cosTau * strokeCos * flapHeaveWeight;
        }
        else
        {
            // --- UPSTROKE (0.40 to 1.00, 60% of cycle) ---
            // Retracted stroke plane (-11.0 deg retraction)
            currentStrokePlaneAngle = RetractionDeg;
            float strokeCos = Mathf.Cos(RetractionDeg * Mathf.Deg2Rad);

            // Normalized upstroke progress tau in [0, 1]
            float tau = (phase - DownstrokeFraction) / (1.0f - DownstrokeFraction);
            float cosTau = Mathf.Cos(tau * Mathf.PI);
            float sinTau = Mathf.Sin(tau * Mathf.PI);

            // Displacement settles downward from +flapHeaveAmplitude to -flapHeaveAmplitude
            yHeave = flapHeaveAmplitude * cosTau * flapHeaveWeight;

            // Velocity: dy/dt = -A * PI * (dtau/dt) * sin(tau * PI) * cos(retraction)
            float dtau_dt = (1.0f / (1.0f - DownstrokeFraction)) * f_flap;
            vyHeave = -flapHeaveAmplitude * Mathf.PI * dtau_dt * sinTau * strokeCos * flapHeaveWeight;

            // Acceleration: d2y/dt2 = -A * PI^2 * (dtau/dt)^2 * cos(tau * PI) * cos(retraction)
            ayHeave = -flapHeaveAmplitude * (Mathf.PI * Mathf.PI) * (dtau_dt * dtau_dt) * cosTau * strokeCos * flapHeaveWeight;
        }

        currentFlapHeave = yHeave;
    }

    /// <summary>
    /// Computes exact analytical closed-form kinematics (Position, Velocity, Acceleration)
    /// without finite-difference approximation noise.
    /// </summary>
    public void EvaluateKinematics(float lookaheadDt, out Vector3 position, out Vector3 velocity, out Vector3 acceleration)
    {
        Vector3 center = target != null ? target.position : Vector3.zero;

        // 1. Vertical Atmospheric Thermal Updraft Kinematics (f = 0.15 Hz, A = 0.08 m)
        float omegaTherm = verticalBobFrequency * Mathf.PI * 2f;
        float phaseTherm = thermalPhaseRad + omegaTherm * lookaheadDt;

        float yThermal = verticalBobAmplitude * Mathf.Sin(phaseTherm);
        float vyThermal = omegaTherm * verticalBobAmplitude * Mathf.Cos(phaseTherm);
        float ayThermal = -(omegaTherm * omegaTherm) * verticalBobAmplitude * Mathf.Sin(phaseTherm);

        // 2. Procedural Aerodynamic Stroke Heave Alignment (Coordinated with stroke plane +13.5° / -11.0°)
        ComputeAerodynamicStrokeHeave(lookaheadDt, out float yFlap, out float vyFlap, out float ayFlap);

        float posY = center.y + heightAboveTarget + yThermal + yFlap;
        float velY = vyThermal + vyFlap;
        float accY = ayThermal + ayFlap;

        // 2. Horizontal Kinematics
        if (pattern == FlightPattern.Orbit)
        {
            float omegaOrbit = orbitSpeed * Mathf.Deg2Rad;
            float direction = clockwise ? -1f : 1f;
            float phaseOrbit = currentOrbitAngleRad + direction * omegaOrbit * lookaheadDt;

            float cosTheta = Mathf.Cos(phaseOrbit);
            float sinTheta = Mathf.Sin(phaseOrbit);

            float posX = center.x + orbitRadius * cosTheta;
            float posZ = center.z + orbitRadius * sinTheta;

            float velX = -direction * omegaOrbit * orbitRadius * sinTheta;
            float velZ = direction * omegaOrbit * orbitRadius * cosTheta;

            float accX = -(omegaOrbit * omegaOrbit) * orbitRadius * cosTheta;
            float accZ = -(omegaOrbit * omegaOrbit) * orbitRadius * sinTheta;

            position = new Vector3(posX, posY, posZ);
            velocity = new Vector3(velX, velY, velZ);
            acceleration = new Vector3(accX, accY, accZ);
        }
        else
        {
            // Lemniscate of Gerono (Figure-8 Hover):
            // x(t) = a * sin(t)
            // z(t) = b * sin(2t)
            // where a = hoverWidth, b = hoverDepth * 0.5
            float omegaHover = hoverFrequency * Mathf.PI * 2f;
            float phaseHover = hoverPhaseRad + omegaHover * lookaheadDt;

            float a = hoverWidth;
            float b = hoverDepth * 0.5f;

            float sinTheta = Mathf.Sin(phaseHover);
            float cosTheta = Mathf.Cos(phaseHover);
            float sin2Theta = Mathf.Sin(2f * phaseHover);
            float cos2Theta = Mathf.Cos(2f * phaseHover);

            float posX = center.x + a * sinTheta;
            float posZ = center.z + b * sin2Theta;

            float velX = a * omegaHover * cosTheta;
            float velZ = 2f * b * omegaHover * cos2Theta;

            // Zero acceleration at crossing (sin(0)=0, sin(2*0)=0) -> zero roll banking at crossing!
            // Maximum acceleration at lobe crests -> peak roll banking at lobes!
            float accX = -a * (omegaHover * omegaHover) * sinTheta;
            float accZ = -4f * b * (omegaHover * omegaHover) * sin2Theta;

            position = new Vector3(posX, posY, posZ);
            velocity = new Vector3(velX, velY, velZ);
            acceleration = new Vector3(accX, accY, accZ);
        }
    }

    /// <summary>
    /// Corvid flight cadence state machine alternating between flapping bursts and soaring glides.
    /// </summary>
    private void UpdateCadenceStateMachine(float dt)
    {
        if (!enableCadence)
        {
            cadenceMode = CadenceMode.Flapping;
            isGliding = false;
            return;
        }

        cadenceTimer -= dt;
        if (cadenceTimer <= 0f)
        {
            if (cadenceMode == CadenceMode.Flapping)
            {
                cadenceMode = CadenceMode.Gliding;
                cadenceTimer = UnityEngine.Random.Range(glideDurationRange.x, glideDurationRange.y);
                isGliding = true;
            }
            else
            {
                cadenceMode = CadenceMode.Flapping;
                cadenceTimer = UnityEngine.Random.Range(flapDurationRange.x, flapDurationRange.y);
                isGliding = false;
            }
        }
    }

    /// <summary>
    /// Dynamic wing flap speed modulation based on turning load factor n = sqrt(1 + (a_lat/g)^2)
    /// and climb rate.
    /// </summary>
    private void UpdateFlapSpeedModulation(float dt)
    {
        // Aerodynamic load factor: n = sqrt(1 + (a_lat / g)^2)
        float latRatio = currentLateralAcceleration / 9.81f;
        currentLoadFactor = Mathf.Sqrt(1f + latRatio * latRatio);

        // Climb rate boost factor: birds flap faster when climbing against gravity
        float climbBoost = 1.0f + climbFlapMultiplier * Mathf.Max(0f, currentVelocity.y);

        // Target Flap Speed: base * sqrt(n) * climbBoost
        float targetFlap = baseFlapSpeed * Mathf.Sqrt(currentLoadFactor) * climbBoost;
        targetFlap = Mathf.Clamp(targetFlap, 0.60f, 2.20f);

        // Smoothly approach target flap speed
        currentFlapSpeed = Mathf.Lerp(currentFlapSpeed, targetFlap, dt * 4.0f);
    }

    /// <summary>
    /// Feeds telemetry into Mecanim Animator parameters.
    /// </summary>
    private void UpdateAnimatorParameters(float horizSpeed)
    {
        if (animator == null) return;

        // 1. IsGliding (Bool)
        if (hasIsGliding)
        {
            animator.SetBool(idIsGliding, isGliding);
        }

        // 2. TurnRate (Float)
        // In 2D BlendTree: -1 = Bank Left, +1 = Bank Right, 0 = Level Soar
        // CurrentBankAngle is negative when banking right (right wing down), positive when banking left.
        if (hasTurnRate)
        {
            float normalizedTurn = Mathf.Clamp(-currentBankAngle / maxBankAngle, -1f, 1f);
            float turnRateValue = normalizeAnimatorTurnRate ? normalizedTurn : (-currentBankAngle);
            animator.SetFloat(idTurnRate, turnRateValue);
        }

        // 3. FlapSpeed (Float)
        if (hasFlapSpeed)
        {
            animator.SetFloat(idFlapSpeed, isGliding ? 1.0f : currentFlapSpeed);
            animator.speed = 1.0f; // Handled internally by Animator state speed multiplier
        }
        else
        {
            // Fallback for controllers without FlapSpeed parameter multiplier
            animator.speed = isGliding ? 1.0f : currentFlapSpeed;
        }

        // 4. VerticalSpeed (Float)
        // In 2D BlendTree: +1 = Climb, -1 = Dive, 0 = Level
        if (hasVerticalSpeed)
        {
            float normalizedVert = Mathf.Clamp(currentVelocity.y / 0.5f, -1f, 1f);
            animator.SetFloat(idVerticalSpeed, normalizedVert);
        }

        // 5. Speed (Float)
        if (hasSpeed)
        {
            animator.SetFloat(idSpeed, currentVelocity.magnitude);
        }

        // 6. BankAngle (Float)
        if (hasBankAngle)
        {
            animator.SetFloat(idBankAngle, currentBankAngle);
        }

        // 7. MicroFlutterWeight (Float)
        if (hasMicroFlutterWeight)
        {
            float microFlutterWeight = Mathf.Clamp01(DynamicPressureIndex * currentLoadFactor);
            animator.SetFloat(idMicroFlutterWeight, microFlutterWeight);
        }

        // 8. FlutterSpeed (Float)
        if (hasFlutterSpeed)
        {
            float flutterSpeed = Mathf.Clamp(Airspeed / 0.87f, 0.6f, 2.0f);
            animator.SetFloat(idFlutterSpeed, flutterSpeed);
        }

        // 9. BreathWeight (Float)
        if (hasBreathWeight)
        {
            float breathWeight = Mathf.Lerp(0.40f, 0.95f, flightExertion);
            animator.SetFloat(idBreathWeight, breathWeight);
        }

        // 10. TurbulenceIntensity (Float)
        if (hasTurbulenceIntensity)
        {
            animator.SetFloat(idTurbulenceIntensity, currentTurbulenceIntensity);
        }

        // 11. TalonTrimWeight (Float)
        if (hasTalonTrimWeight)
        {
            float talonTrimWeight = Mathf.Clamp01(DynamicPressureIndex);
            animator.SetFloat(idTalonTrimWeight, talonTrimWeight);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = target != null ? target.position : transform.position;
        center.y += heightAboveTarget;

        // 1. Draw Trajectory Path
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.65f);
        if (pattern == FlightPattern.Orbit)
        {
            int segments = 64;
            Vector3 prev = center + new Vector3(orbitRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 cur = center + new Vector3(Mathf.Cos(a) * orbitRadius, 0f, Mathf.Sin(a) * orbitRadius);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
        else
        {
            int segments = 80;
            Vector3 prev = center;
            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 cur = center + new Vector3(Mathf.Sin(t) * hoverWidth, 0f, Mathf.Sin(t * 2f) * (hoverDepth * 0.5f));
                if (i > 0) Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }

        // 2. Draw Kinematic Vectors at Crow Position
        if (Application.isPlaying)
        {
            // Velocity Vector (Cyan)
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, currentVelocity);

            // Lateral Acceleration Vector (Yellow)
            Vector3 horizVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            if (horizVel.sqrMagnitude > 1e-4f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, horizVel.normalized);
                float aRight = Vector3.Dot(currentAcceleration, right);
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, right * aRight * 0.2f);
            }
        }
    }
#endif
}
