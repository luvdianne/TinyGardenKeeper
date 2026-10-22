using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TinyGardenKeeper.AR;

namespace TinyGardenKeeper.EditorTools
{
    public static class ARSceneSetupHelper
    {
        [MenuItem("TinyGarden/Setup AR Pot Scene")]
        public static void SetupScene()
        {
            const string scenePath = "Assets/Scenes/AR_PotPlacementTest.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 0. Configure iPhone 15 Resolution & Portrait Orientation
            GameViewSizeHelper.SetToiPhone15();

            // 1. RealisticRoom Prefab
            GameObject roomObj = GameObject.Find("RealisticRoom");
            if (roomObj == null)
            {
                var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RealisticRoom.prefab");
                if (roomPrefab != null)
                {
                    roomObj = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, scene);
                    roomObj.name = "RealisticRoom";
                    roomObj.transform.position = Vector3.zero;
                    roomObj.transform.rotation = Quaternion.identity;
                    Undo.RegisterCreatedObjectUndo(roomObj, "Instantiate RealisticRoom");
                }
                else
                {
                    Debug.LogError("[ARSceneSetup] Could not load Assets/Prefabs/RealisticRoom.prefab!");
                }
            }

            if (roomObj != null && roomObj.GetComponent<ARTestEnvironment>() == null)
            {
                roomObj.AddComponent<ARTestEnvironment>();
            }

            // 2. Pre-placed Pot_AR sitting flush on the floor
            Vector3 floorPos = new Vector3(0f, 0f, 1.2f);
            GameObject potObj = GameObject.Find("Pot_AR");
            if (potObj == null)
            {
                var potPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Pot_AR.prefab");
                if (potPrefab != null)
                {
                    potObj = (GameObject)PrefabUtility.InstantiatePrefab(potPrefab, scene);
                    potObj.name = "Pot_AR";
                    potObj.transform.position = floorPos;
                    potObj.transform.rotation = Quaternion.identity;
                    potObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    Undo.RegisterCreatedObjectUndo(potObj, "Instantiate Pot_AR");
                }
                else
                {
                    Debug.LogError("[ARSceneSetup] Could not load Assets/Resources/Prefabs/Pot_AR.prefab!");
                }
            }
            else
            {
                potObj.transform.position = floorPos;
                potObj.transform.rotation = Quaternion.identity;
                potObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                EditorUtility.SetDirty(potObj.transform);
            }

            // 3. XR Origin & Camera Framing (Frames pot on the floor)
            GameObject xrOriginObj = GameObject.Find("XR Origin");
            if (xrOriginObj != null)
            {
                xrOriginObj.transform.position = Vector3.zero;
                xrOriginObj.transform.rotation = Quaternion.identity;

                // Add AREditorCameraController for WASD + Right-Click movement in Game View
                if (xrOriginObj.GetComponent<AREditorCameraController>() == null)
                {
                    xrOriginObj.AddComponent<AREditorCameraController>();
                }

                EditorUtility.SetDirty(xrOriginObj.transform);
                EditorUtility.SetDirty(xrOriginObj);
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.localPosition = Vector3.zero;
                mainCam.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                mainCam.farClipPlane = 100f;
                mainCam.nearClipPlane = 0.1f;
                mainCam.cullingMask = ~0;
                EditorUtility.SetDirty(mainCam);
                EditorUtility.SetDirty(mainCam.transform);
            }

            // Frame SceneView directly onto the Pot on the floor
            if (SceneView.lastActiveSceneView != null && potObj != null)
            {
                SceneView.lastActiveSceneView.LookAt(floorPos + Vector3.up * 0.25f, Quaternion.Euler(22f, -20f, 0f), 2.2f);
            }

            // 4. Configure ARPotPlacement on XR Origin
            var placement = Object.FindAnyObjectByType<ARPotPlacement>();
            if (placement != null)
            {
                var so = new SerializedObject(placement);
                var potPrefabProp = so.FindProperty("potPrefab");
                var reticlePrefabProp = so.FindProperty("placementReticlePrefab");
                var initialPotProp = so.FindProperty("initialPot");
                var allowMultipleProp = so.FindProperty("allowMultiplePots");
                var faceCamProp = so.FindProperty("faceCameraOnSpawn");

                var showPlanesProp = so.FindProperty("showDetectedPlanes");

                var potPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Pot_AR.prefab");
                var reticlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/AR_PlacementReticle.prefab");

                if (potPrefabProp != null && potPrefab != null) potPrefabProp.objectReferenceValue = potPrefab;
                if (reticlePrefabProp != null && reticlePrefab != null) reticlePrefabProp.objectReferenceValue = reticlePrefab;
                if (initialPotProp != null && potObj != null) initialPotProp.objectReferenceValue = potObj;
                if (allowMultipleProp != null) allowMultipleProp.boolValue = false;
                if (faceCamProp != null) faceCamProp.boolValue = true;
                if (showPlanesProp != null) showPlanesProp.boolValue = false;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(placement);
            }

            // 5. Remove planePrefab from ARPlaneManager so no white polygonal sheets are rendered
            var planeManager = Object.FindAnyObjectByType<UnityEngine.XR.ARFoundation.ARPlaneManager>();
            if (planeManager != null)
            {
                planeManager.planePrefab = null;
                EditorUtility.SetDirty(planeManager);
            }

            // 6. Directional Light for pleasant ambient room lighting
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                var light = lightObj.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 1.0f;
                    light.shadows = LightShadows.Soft;
                    lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    EditorUtility.SetDirty(light);
                    EditorUtility.SetDirty(lightObj.transform);
                }
            }

            // 7. Bird & Crow Migration & Orbiting Setup
            AssetDatabase.ImportAsset("Assets/Resources/Models/Bird/Prefabs/Bird.prefab", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Resources/Models/Crow/Prefabs/Crow.prefab", ImportAssetOptions.ForceUpdate);

            var birdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Bird/Prefabs/Bird.prefab");
            var crowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Crow/Prefabs/Crow.prefab");

            GameObject birdObj = GameObject.Find("Bird");
            if (birdObj != null)
            {
                Object.DestroyImmediate(birdObj);
            }
            if (birdPrefab != null)
            {
                birdObj = (GameObject)PrefabUtility.InstantiatePrefab(birdPrefab, scene);
                birdObj.name = "Bird";
                Undo.RegisterCreatedObjectUndo(birdObj, "Instantiate Bird");
            }

            if (birdObj != null)
            {
                var birdTransformSO = new SerializedObject(birdObj.transform);
                var birdScaleProp = birdTransformSO.FindProperty("m_LocalScale");
                if (birdScaleProp != null) birdScaleProp.vector3Value = new Vector3(0.2f, 0.2f, 0.2f);
                birdTransformSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(birdObj.transform);

                var flight = birdObj.GetComponent<CrowFlightController>();
                if (flight != null)
                {
                    flight.target = potObj != null ? potObj.transform : null;
                    flight.pattern = CrowFlightController.FlightPattern.Orbit;
                    flight.orbitRadius = 0.55f;
                    flight.orbitSpeed = 48f;
                    flight.clockwise = true;
                    flight.startOrbitAngle = 45f;
                    flight.heightAboveTarget = 0.32f;
                    flight.verticalBobAmplitude = 0.025f;
                    flight.flapHeaveAmplitude = 0.008f;
                    flight.ResolveTargetReference();
                    EditorUtility.SetDirty(flight);
                }
                var head = birdObj.GetComponent<AvianHeadStabilizer>();
                if (head != null)
                {
                    head.target = potObj != null ? potObj.transform : null;
                    head.lockToHorizon = true;
                    head.enableGazeTracking = true;
                    head.ResolveTargetReference();
                    EditorUtility.SetDirty(head);
                }
                EditorUtility.SetDirty(birdObj);
            }

            GameObject crowObj = GameObject.Find("Crow");
            if (crowObj == null) crowObj = GameObject.Find("Crow2");
            if (crowObj != null)
            {
                Object.DestroyImmediate(crowObj);
            }
            if (crowPrefab != null)
            {
                crowObj = (GameObject)PrefabUtility.InstantiatePrefab(crowPrefab, scene);
                crowObj.name = "Crow";
                Undo.RegisterCreatedObjectUndo(crowObj, "Instantiate Crow");
            }

            if (crowObj != null)
            {
                var crowTransformSO = new SerializedObject(crowObj.transform);
                var crowScaleProp = crowTransformSO.FindProperty("m_LocalScale");
                if (crowScaleProp != null) crowScaleProp.vector3Value = new Vector3(0.25f, 0.25f, 0.25f);
                crowTransformSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(crowObj.transform);

                var flight = crowObj.GetComponent<CrowFlightController>();
                if (flight != null)
                {
                    flight.target = potObj != null ? potObj.transform : null;
                    flight.pattern = CrowFlightController.FlightPattern.Orbit;
                    flight.orbitRadius = 0.40f;
                    flight.orbitSpeed = 36f;
                    flight.clockwise = false;
                    flight.startOrbitAngle = 225f;
                    flight.heightAboveTarget = 0.42f;
                    flight.verticalBobAmplitude = 0.025f;
                    flight.flapHeaveAmplitude = 0.008f;
                    flight.ResolveTargetReference();
                    EditorUtility.SetDirty(flight);
                }
                var head = crowObj.GetComponent<AvianHeadStabilizer>();
                if (head != null)
                {
                    head.target = potObj != null ? potObj.transform : null;
                    head.lockToHorizon = true;
                    head.enableGazeTracking = true;
                    head.ResolveTargetReference();
                    EditorUtility.SetDirty(head);
                }
                EditorUtility.SetDirty(crowObj);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ARSceneSetup] AR Pot Placement Test Scene setup complete and saved successfully!");
        }
    }
}
