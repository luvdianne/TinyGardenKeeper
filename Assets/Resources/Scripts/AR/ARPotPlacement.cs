using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Handles AR surface detection, placement reticle visualization, and pot spawning/repositioning.
    /// Adapted from the battle-tested AR Paper Toss placement system.
    /// Supports AR Trackables on mobile devices and direct / multi-PhysicsScene raycasting for Editor room geometry.
    /// </summary>
    public class ARPotPlacement : MonoBehaviour
    {
        [Header("Prefabs & Scene References")]
        [Tooltip("The Pot prefab to instantiate (e.g. Pot_AR)")]
        [SerializeField] private GameObject potPrefab;

        [Tooltip("Visual reticle ring indicating valid plane surfaces")]
        [SerializeField] private GameObject placementReticlePrefab;

        [Tooltip("Pre-placed Pot instance in the scene (optional). If assigned or found in scene, placement will reposition this pot.")]
        [SerializeField] private GameObject initialPot;

        [Header("AR References (Auto-detected if null)")]
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private Camera arCamera;

        [Header("Placement Options")]
        [Tooltip("If true, each tap places a new pot. If false, tapping repositions the single pot.")]
        [SerializeField] private bool allowMultiplePots = false;

        [Tooltip("Make the pot face the camera on spawn.")]
        [SerializeField] private bool faceCameraOnSpawn = true;

        [Tooltip("How fast the reticle glides to the hit position for smooth tracking.")]
        [SerializeField] private float reticleMoveSpeed = 25f;

        [Header("Plane Visualization")]
        [Tooltip("If true, AR plane polygons are visualized. If false, visual plane meshes are hidden so the scene remains clean.")]
        [SerializeField] private bool showDetectedPlanes = false;

        // Internal State
        private GameObject spawnedReticle;
        private GameObject currentPot;
        private Vector3 currentHitPosition;
        private Quaternion currentHitRotation = Quaternion.identity;
        private bool placementReady = false;

        private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        public GameObject CurrentPot => currentPot;
        public bool IsPlacementReady => placementReady;

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            if (arCamera != null && arCamera.farClipPlane < 100f)
                arCamera.farClipPlane = 100f;

            if (raycastManager == null)
                raycastManager = FindAnyObjectByType<ARRaycastManager>();

            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();

            if (!showDetectedPlanes && planeManager != null)
            {
                planeManager.planePrefab = null;
            }

            // If an initial pot was set or exists in the scene, track it as currentPot
            if (initialPot != null)
            {
                currentPot = initialPot;
            }
            else
            {
                var existing = GameObject.Find("Pot_AR");
                if (existing != null)
                {
                    currentPot = existing;
                }
            }

            if (placementReticlePrefab != null)
            {
                spawnedReticle = Instantiate(placementReticlePrefab);
                spawnedReticle.name = "ActivePlacementReticle";
                spawnedReticle.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
        {
            if (!showDetectedPlanes)
            {
                foreach (var plane in eventArgs.added)
                {
                    plane.gameObject.SetActive(false);
                }
                foreach (var plane in eventArgs.updated)
                {
                    plane.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            UpdateIndicator();
            HandlePlacementInput();
        }

        /// <summary>
        /// Raycasts to find a valid placement surface.
        /// Checks AR detected planes first, then queries loaded physics scenes (including in-scene room and XR Simulation geometry).
        /// </summary>
        private void UpdateIndicator()
        {
            if (arCamera == null)
                arCamera = Camera.main;
            if (arCamera == null)
                return;

            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
                {
                    screenPoint = mousePos;
                }
            }
#endif

            bool foundHit = false;

            // 1. Try AR Raycast (Filter for upward horizontal planes)
            if (raycastManager != null && raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated) && hits.Count > 0)
            {
                foreach (var hit in hits)
                {
                    if (planeManager != null)
                    {
                        ARPlane plane = planeManager.GetPlane(hit.trackableId);
                        if (plane != null && plane.alignment != PlaneAlignment.HorizontalUp)
                            continue;
                    }

                    Pose hitPose = hit.pose;
                    if (Vector3.Dot(hitPose.up, Vector3.up) > 0.6f)
                    {
                        currentHitPosition = hitPose.position;
                        currentHitRotation = hitPose.rotation;
                        foundHit = true;
                        break;
                    }
                }
            }

            // 2. Physics Raycast fallback across ALL loaded scenes and PhysicsScenes (for in-scene room geometry and XR Simulation)
            if (!foundHit)
            {
                Ray ray = arCamera.ScreenPointToRay(screenPoint);
                List<RaycastHit> allHits = new List<RaycastHit>();

                // Query default physics scene (hits room placed directly in the scene)
                var defaultHits = Physics.RaycastAll(ray, 100f, ~0);
                if (defaultHits != null && defaultHits.Length > 0)
                {
                    allHits.AddRange(defaultHits);
                }

                // Query all other loaded scenes' PhysicsScenes (e.g. XR Simulation additive scene)
                int sceneCount = SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var physicsScene = scene.GetPhysicsScene();
                    if (physicsScene.IsValid() && physicsScene != Physics.defaultPhysicsScene)
                    {
                        RaycastHit[] sceneHits = new RaycastHit[30];
                        int hitCount = physicsScene.Raycast(ray.origin, ray.direction, sceneHits, 100f, ~0);
                        for (int h = 0; h < hitCount; h++)
                        {
                            allHits.Add(sceneHits[h]);
                        }
                    }
                }

                // Sort by distance to find the closest valid surface
                allHits.Sort((a, b) => a.distance.CompareTo(b.distance));

                foreach (var physHit in allHits)
                {
                    // Ignore raycasting against the spawned pot itself or the reticle
                    if (currentPot != null && physHit.collider.transform.root == currentPot.transform.root)
                        continue;
                    if (spawnedReticle != null && physHit.collider.transform.root == spawnedReticle.transform.root)
                        continue;

                    // Only accept upward horizontal surfaces (floors, tables, shelves, desks)
                    if (Vector3.Dot(physHit.normal, Vector3.up) > 0.6f)
                    {
                        currentHitPosition = physHit.point;
                        currentHitRotation = Quaternion.FromToRotation(Vector3.up, physHit.normal);
                        foundHit = true;
                        break;
                    }
                }
            }

            if (foundHit)
            {
                if (spawnedReticle != null)
                {
                    if (!spawnedReticle.activeSelf)
                    {
                        spawnedReticle.transform.SetPositionAndRotation(currentHitPosition, currentHitRotation);
                        spawnedReticle.SetActive(true);
                    }
                    else
                    {
                        // Smoothly interpolate reticle position and rotation
                        spawnedReticle.transform.position = Vector3.Lerp(spawnedReticle.transform.position, currentHitPosition, Time.deltaTime * reticleMoveSpeed);
                        spawnedReticle.transform.rotation = Quaternion.Slerp(spawnedReticle.transform.rotation, currentHitRotation, Time.deltaTime * reticleMoveSpeed);
                    }
                }
                placementReady = true;
            }
            else
            {
                if (spawnedReticle != null && spawnedReticle.activeSelf)
                {
                    spawnedReticle.SetActive(false);
                }
                placementReady = false;
            }
        }

        /// <summary>
        /// Handles tap on mobile or left-click in Editor to place or reposition the pot.
        /// </summary>
        private void HandlePlacementInput()
        {
            if (!TryGetPointerDown(out Vector2 pointerPosition))
                return;

            // Ignore input over UI
            if (IsPointerOverUI(pointerPosition))
                return;

            if (placementReady)
            {
                PlacePotAt(currentHitPosition, currentHitRotation);
            }
            else
            {
                // Fallback: Place 1.2m in front of camera on ground/table height
                if (arCamera != null)
                {
                    Vector3 fallbackPos = arCamera.transform.position + arCamera.transform.forward * 1.2f;
                    fallbackPos.y = 0.45f; // tabletop height
                    PlacePotAt(fallbackPos, Quaternion.identity);
                }
            }
        }

        private void PlacePotAt(Vector3 position, Quaternion rotation)
        {
            if (potPrefab == null && currentPot == null)
            {
                Debug.LogWarning("[ARPotPlacement] Pot prefab is not assigned and no current pot exists!");
                return;
            }

            Quaternion targetRotation = rotation;
            if (faceCameraOnSpawn && arCamera != null)
            {
                Vector3 lookDirection = arCamera.transform.position - position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    targetRotation = Quaternion.LookRotation(lookDirection);
                }
            }

            if (currentPot == null || allowMultiplePots)
            {
                currentPot = Instantiate(potPrefab != null ? potPrefab : currentPot, position, targetRotation);
                currentPot.name = $"Pot_Instance_{System.DateTime.Now:HHmmss}";
                Debug.Log($"[ARPotPlacement] Placed Pot at {position}");
            }
            else
            {
                currentPot.transform.SetPositionAndRotation(position, targetRotation);
                currentPot.SetActive(true);
                Debug.Log($"[ARPotPlacement] Moved Pot to {position}");
            }
        }

        private bool TryGetPointerDown(out Vector2 position)
        {
            position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
            return false;
#else
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
            return false;
#endif
        }

        private bool IsPointerOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null)
                return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }
    }
}
