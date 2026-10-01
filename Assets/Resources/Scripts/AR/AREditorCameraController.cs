using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Editor-only fly-camera controller for testing AR scenes in the Unity Game view.
    /// Allows free movement with WASD and mouse look with Right-Click (just like Unity Scene View navigation).
    /// Automatically disabled or removed in mobile device builds.
    /// </summary>
    public class AREditorCameraController : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Movement Settings")]
        [Tooltip("Normal walk speed in meters per second.")]
        [SerializeField] private float moveSpeed = 2.0f;

        [Tooltip("Sprint multiplier when holding Left Shift.")]
        [SerializeField] private float sprintMultiplier = 2.5f;

        [Tooltip("Mouse look sensitivity.")]
        [SerializeField] private float lookSensitivity = 0.15f;

        [Tooltip("If true, right-clicking toggles cursor lock so you don't have to hold it. Press Escape or Right-Click again to unlock.")]
        [SerializeField] private bool toggleRightClick = true;

        [Header("Camera Reference")]
        [SerializeField] private Transform cameraTransform;

        private float pitch = 0f;
        private float yaw = 0f;
        private bool isLooking = false;

        private void SetLookLocked(bool locked)
        {
            isLooking = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Start()
        {
            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraTransform = cam.transform;
                else
                    cameraTransform = transform;
            }

            Vector3 euler = cameraTransform.eulerAngles;
            pitch = euler.x;
            yaw = euler.y;

            // In Editor, disable TrackedPoseDriver so it doesn't fight mouse look & WASD flight in Game view
            var driver = GetComponentInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (driver != null)
            {
                driver.enabled = false;
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;

            // Right-click toggles cursor lock / free-look
            if (toggleRightClick)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    SetLookLocked(!isLooking);
                }
                else if (keyboard.escapeKey.wasPressedThisFrame && isLooking)
                {
                    SetLookLocked(false);
                }
            }
            else
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    SetLookLocked(true);
                }
                else if (mouse.rightButton.wasReleasedThisFrame)
                {
                    SetLookLocked(false);
                }
            }

            if (isLooking)
            {
                // Mouse Look (Yaw & Pitch)
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * lookSensitivity;
                pitch -= delta.y * lookSensitivity;
                pitch = Mathf.Clamp(pitch, -85f, 85f);

                cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // WASD Movement (active during Right-Click or whenever keys are pressed)
            Vector3 moveDir = Vector3.zero;
            if (keyboard.wKey.isPressed) moveDir += cameraTransform.forward;
            if (keyboard.sKey.isPressed) moveDir -= cameraTransform.forward;
            if (keyboard.dKey.isPressed) moveDir += cameraTransform.right;
            if (keyboard.aKey.isPressed) moveDir -= cameraTransform.right;
            if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed) moveDir += Vector3.up;
            if (keyboard.qKey.isPressed || keyboard.cKey.isPressed) moveDir -= Vector3.up;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1.0f);
                transform.position += moveDir.normalized * (speed * Time.deltaTime);
            }

            // Mouse scroll wheel forward/backward zoom
            if (mouse.scroll != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    transform.position += cameraTransform.forward * (Mathf.Sign(scroll) * 0.35f);
                }
            }
#endif
        }

        private void OnDisable()
        {
            SetLookLocked(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isLooking)
            {
                SetLookLocked(false);
            }
        }
#else
        private void Awake()
        {
            // Destroy immediately on mobile AR device builds so AR tracking handles camera
            Destroy(this);
        }
#endif
    }
}
