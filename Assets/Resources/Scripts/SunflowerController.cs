using UnityEngine;

public class SunflowerController : MonoBehaviour
{
    [Header("Hierarchy References")]
    public Transform flowerHead;

    [Header("Spring Physics")]
    public float stiffness = 150f;
    public float damping = 10f;
    private Vector3 currentVelocity;
    private Vector3 currentDisplacement;
    private Vector3 targetDisplacement = Vector3.zero;

    [Header("Heliotropism (Sun Tracking)")]
    public Transform sunLight;
    public float trackingSpeed = 2f;
    public float maxTiltAngle = 25f;

    [Header("Idle Breathing")]
    public float swaySpeed = 1.5f;
    public float swayAmount = 2f;

    private Quaternion initialRotation;

    void Start()
    {
        if (flowerHead != null)
        {
            initialRotation = flowerHead.localRotation;
        }

        if (sunLight == null)
        {
            Light dirLight = RenderSettings.sun;
            if (dirLight != null)
                sunLight = dirLight.transform;
            else
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach(var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l.transform;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (flowerHead == null) return;

        // Harmonic sway
        float swayX = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        float swayZ = Mathf.Cos(Time.time * swaySpeed * 0.8f) * swayAmount;
        Vector3 swayRotation = new Vector3(swayX, 0, swayZ);

        // Spring physics
        Vector3 springForce = (targetDisplacement - currentDisplacement) * stiffness;
        Vector3 dampingForce = currentVelocity * damping;
        Vector3 acceleration = springForce - dampingForce;
        currentVelocity += acceleration * Time.deltaTime;
        currentDisplacement += currentVelocity * Time.deltaTime;
        targetDisplacement = Vector3.Lerp(targetDisplacement, Vector3.zero, Time.deltaTime * 2f);

        // Sun tracking
        Quaternion sunRotation = Quaternion.identity;
        if (sunLight != null)
        {
            Vector3 sunDir = -sunLight.forward;
            Vector3 localSunDir = transform.InverseTransformDirection(sunDir);
            localSunDir.Normalize();
            
            // Calculate rotation towards sun relative to forward
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.forward, localSunDir);
            
            // Limit tilt angle
            float angle = Quaternion.Angle(Quaternion.identity, targetRot);
            if (angle > maxTiltAngle)
            {
                targetRot = Quaternion.Slerp(Quaternion.identity, targetRot, maxTiltAngle / angle);
            }
            sunRotation = targetRot;
        }

        Quaternion targetFinalRot = initialRotation * sunRotation * Quaternion.Euler(swayRotation + currentDisplacement);
        flowerHead.localRotation = Quaternion.Slerp(flowerHead.localRotation, targetFinalRot, Time.deltaTime * trackingSpeed);
    }

    public void TriggerBounce(Vector3 force)
    {
        currentVelocity += force;
    }

    void OnMouseDown()
    {
        TriggerBounce(new Vector3(Random.Range(-50f, 50f), 0, Random.Range(-50f, 50f)));
    }
}
