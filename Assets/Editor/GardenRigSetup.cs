using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TinyGardenKeeper.AR;

namespace TinyGardenKeeper.EditorTools
{
    public static class GardenRigSetup
    {
        [MenuItem("TinyGarden/Setup AR Garden Rig")]
        public static void BuildGardenRigScene()
        {
            const string scenePath = "Assets/Scenes/AR_PotPlacementTest.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 0. Set Game View to iPhone 15 Portrait
            GameViewSizeHelper.SetToiPhone15();

            // 1. Ensure Room & XR Origin exist
            EnsureRoomAndXROrigin(scene);

            // 2. Clean up any existing GardenRig or loose props (Idempotent Rule 3)
            CleanExistingGardenObjects();

            // 3. Create Root GardenRig
            Vector3 rigOrigin = new Vector3(0f, 0f, 1.2f);
            GameObject gardenRig = new GameObject("GardenRig");
            gardenRig.transform.position = rigOrigin;
            gardenRig.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            gardenRig.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(gardenRig, "Create GardenRig");

            // 4. Pot_AR
            GameObject potObj = SetupPotAR(gardenRig);

            // 5. Seed (Stage 0: 0% - 30%)
            GameObject seedObj = SetupSeed(gardenRig);

            // 6. Sprout Growth Stages:
            // Stage 1 (30% - 65%)
            GameObject sproutStage1Obj = SetupSproutStage1(gardenRig);

            // Stage 2 (65% - 100%)
            GameObject sproutStage2Obj = SetupSproutStage2(gardenRig);

            // Stage 3 (Final dancing sprout at 100%)
            GameObject sproutFinalObj = SetupSprout(gardenRig);

            // 7. Watering Can
            GameObject wateringCanObj = SetupWateringCan(gardenRig);

            // 8. Companions (Butterfly, Bird, Crow)
            GameObject companionsObj = SetupCompanions(gardenRig, potObj);

            // 9. UI Canvas (Top status & progress bar, bottom action buttons)
            GameObject canvasObj = SetupARCanvas();

            // 10. Wire GardenGrowthManager
            var growthManager = gardenRig.AddComponent<GardenGrowthManager>();
            WireGrowthManager(growthManager, seedObj, sproutStage1Obj, sproutStage2Obj, sproutFinalObj, wateringCanObj, companionsObj, canvasObj);

            // 11. Wire ARPotPlacement on XR Origin to track GardenRig
            SetupPotPlacement(gardenRig);

            // 12. Directional Light
            SetupDirectionalLight();

            // 13. Focus SceneView
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(rigOrigin + Vector3.up * 0.12f, Quaternion.Euler(38f, -25f, 0f), 1.1f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[GardenRigSetup] AR Garden Rig, Companions, Growth Manager, and UI successfully built and saved!</color>");
        }

        [MenuItem("TinyGarden/Capture View")]
        public static void CaptureViewDirect()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(1920, 1080, 24);
            var prevRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            cam.targetTexture = prevRT;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes("Assets/Capture_Preview.png", bytes);
            AssetDatabase.ImportAsset("Assets/Capture_Preview.png", ImportAssetOptions.ForceUpdate);
            Debug.Log("<color=cyan>[GardenRigSetup] Captured fresh Camera view to Assets/Capture_Preview.png</color>");
        }

        public static void FocusSceneView(Vector3 targetPos, float pitch = 28f, float yaw = -15f, float size = 1.2f)
        {
            var sv = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : (SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null);
            if (sv != null)
            {
                sv.LookAt(targetPos, Quaternion.Euler(pitch, yaw, 0f), size);
                sv.Repaint();
            }
        }

        [MenuItem("TinyGarden/Preview/Show Stage 0 (Seed)")]
        public static void PreviewStage0Seed()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(true);
            if (s1 != null) s1.SetActive(false);
            if (s2 != null) s2.SetActive(false);
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.15f, 32f, -15f, 0.9f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Stage 1 Sprout")]
        public static void PreviewStage1Sprout()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(false);
            if (s1 != null)
            {
                s1.SetActive(true);
                s1.transform.localPosition = new Vector3(0f, 0.211f, 0f);
                s1.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                s1.transform.localScale = Vector3.one * 18f;
            }
            if (s2 != null) s2.SetActive(false);
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.25f, 24f, -15f, 1.1f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Stage 2 Sprout")]
        public static void PreviewStage2Sprout()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(false);
            if (s1 != null) s1.SetActive(false);
            if (s2 != null)
            {
                s2.SetActive(true);
                s2.transform.localPosition = new Vector3(0f, 0.211f, 0f);
                s2.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                s2.transform.localScale = Vector3.one * 26f;
            }
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.28f, 24f, -15f, 1.2f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Final Sprout")]
        public static void PreviewSprout()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(false);
            if (s1 != null) s1.SetActive(false);
            if (s2 != null) s2.SetActive(false);
            if (sprout != null)
            {
                sprout.SetActive(true);
                sprout.transform.localScale = Vector3.one * 0.65f;
                sprout.transform.localPosition = new Vector3(-0.334f, 0.175f, 0f);
            }
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.35f, 24f, -15f, 1.4f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Watering Can")]
        public static void PreviewWateringCan()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(true);
            if (s1 != null) s1.SetActive(false);
            if (s2 != null) s2.SetActive(false);
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(true);
                can.transform.localPosition = new Vector3(1.60f, -0.66f, 0.83f);
                can.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
                can.transform.localScale = Vector3.one * 1.12f;

                // Sample tilt animation
                var all = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/WateringCan/WateringCanModel.fbx");
                AnimationClip clip = null;
                foreach (var a in all) if (a is AnimationClip c && c.name.Contains("ModelAction")) clip = c;
                if (clip != null) clip.SampleAnimation(can, 0.8f);

                // Simulate particles
                var ps = can.GetComponentInChildren<ParticleSystem>(true);
                var splash = rig.transform.Find("SoilSplashFX")?.GetComponent<ParticleSystem>();
                if (ps != null) { ps.Clear(); ps.Play(); ps.Simulate(0.14f, true, true); }
                if (splash != null) { splash.Clear(); splash.Play(); splash.Simulate(0.10f, true, true); }
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.28f, 22f, -18f, 1.1f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Butterfly")]
        public static void PreviewButterfly()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            PreviewResetState();
            var butterfly = rig.transform.Find("Companions/Butterfly")?.gameObject;
            if (butterfly != null)
            {
                butterfly.SetActive(true);
                butterfly.transform.localScale = Vector3.one * 0.0105f;

                // Sample flight animation at 2.0s so butterfly body is in flight position
                var blendPath = "Assets/Butterfly/Butterfly.blend";
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(blendPath);
                AnimationClip clip = null;
                foreach (var a in allAssets) if (a is AnimationClip c) { clip = c; break; }
                if (clip != null) clip.SampleAnimation(butterfly, 2.0f);

                // Simulate resting pollen grains directly on top of the soil
                var dropper = butterfly.GetComponent<ButterflyPollenDropper>();
                if (dropper != null)
                {
                    dropper.SimulatePollenLandingPreview(12, 4);
                }
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.28f, 20f, -20f, 1.2f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Bird")]
        public static void PreviewBird()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var bird = rig.transform.Find("Companions/Bird")?.gameObject;
            if (bird != null) bird.SetActive(true);

            FocusSceneView(rig.transform.position + Vector3.up * 0.35f, 20f, -20f, 1.8f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Crow")]
        public static void PreviewCrow()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var crow = rig.transform.Find("Companions/Crow")?.gameObject;
            if (crow != null) crow.SetActive(true);

            FocusSceneView(rig.transform.position + Vector3.up * 0.40f, 20f, -20f, 1.8f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Reset Initial State")]
        public static void PreviewResetState()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var s1 = rig.transform.Find("Sprout_Stage1")?.gameObject;
            var s2 = rig.transform.Find("Sprout_Stage2")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(true);
            if (s1 != null) s1.SetActive(false);
            if (s2 != null) s2.SetActive(false);
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                var model = can.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);
            }
            var companions = rig.transform.Find("Companions");
            if (companions != null)
            {
                foreach (Transform child in companions) child.gameObject.SetActive(false);
            }
            var soilManager = rig.transform.Find("SoilPollenFX")?.GetComponent<SoilPollenManager>();
            if (soilManager != null) soilManager.ClearImmediate();
            else
            {
                var pollenFX = rig.transform.Find("SoilPollenFX")?.GetComponent<ParticleSystem>();
                if (pollenFX != null) pollenFX.Clear();
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.12f, 38f, -25f, 1.1f);
            CaptureViewDirect();
        }

        private static void EnsureRoomAndXROrigin(UnityEngine.SceneManagement.Scene scene)
        {
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
                }
            }

            if (roomObj != null && roomObj.GetComponent<ARTestEnvironment>() == null)
            {
                roomObj.AddComponent<ARTestEnvironment>();
            }

            GameObject xrOriginObj = GameObject.Find("XR Origin");
            if (xrOriginObj != null)
            {
                if (xrOriginObj.GetComponent<AREditorCameraController>() == null)
                {
                    xrOriginObj.AddComponent<AREditorCameraController>();
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.localPosition = new Vector3(0f, 0.65f, -0.45f);
                mainCam.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                mainCam.farClipPlane = 100f;
                mainCam.nearClipPlane = 0.05f;
            }

            // Ensure EventSystem
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }
        }

        private static void CleanExistingGardenObjects()
        {
            string[] namesToSweep = { "GardenRig", "Pot_AR", "Bird", "Crow", "Butterfly", "AR_Canvas", "ActivePlacementReticle", "Sprout", "Sprout_Stage1", "Sprout_Stage2", "Seed", "WateringCan", "SoilSplashFX", "Companions", "SoilPollenFX" };
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                foreach (var name in namesToSweep)
                {
                    if (t.name == name)
                    {
                        Object.DestroyImmediate(t.gameObject);
                        break;
                    }
                }
            }
        }

        private static GameObject SetupSproutStage1(GameObject parent)
        {
            var stage1FBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/sprout/sprout_stage1.fbx");
            GameObject stage1Obj = null;
            if (stage1FBX != null)
            {
                stage1Obj = (GameObject)Object.Instantiate(stage1FBX, parent.transform);
                stage1Obj.name = "Sprout_Stage1";
                stage1Obj.transform.localPosition = new Vector3(0f, 0.211f, 0f);
                stage1Obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                stage1Obj.transform.localScale = Vector3.one * 18f;

                // Remove extraneous Blender Camera & Light if present
                var camChild = stage1Obj.transform.Find("Camera");
                if (camChild != null) Object.DestroyImmediate(camChild.gameObject);
                var lightChild = stage1Obj.transform.Find("Light");
                if (lightChild != null) Object.DestroyImmediate(lightChild.gameObject);

                // Assign PBR stage material with extracted 4K textures (blush cheeks, dot eyes, spiral stem)
                var stageMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/sprout/M_Sprout_Stage1.mat");
                if (stageMat != null)
                {
                    foreach (var rend in stage1Obj.GetComponentsInChildren<Renderer>(true))
                    {
                        rend.sharedMaterial = stageMat;
                    }
                }

                stage1Obj.SetActive(false);
            }
            return stage1Obj;
        }

        private static GameObject SetupSproutStage2(GameObject parent)
        {
            var stage2FBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/sprout/sprout_stage2.fbx");
            GameObject stage2Obj = null;
            if (stage2FBX != null)
            {
                stage2Obj = (GameObject)Object.Instantiate(stage2FBX, parent.transform);
                stage2Obj.name = "Sprout_Stage2";
                stage2Obj.transform.localPosition = new Vector3(0f, 0.211f, 0f);
                stage2Obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                stage2Obj.transform.localScale = Vector3.one * 26f;

                // Remove extraneous Blender Camera & Light if present
                var camChild = stage2Obj.transform.Find("Camera");
                if (camChild != null) Object.DestroyImmediate(camChild.gameObject);
                var lightChild = stage2Obj.transform.Find("Light");
                if (lightChild != null) Object.DestroyImmediate(lightChild.gameObject);

                // Assign PBR stage material with extracted 4K textures (blush cheeks, smiling eyes, leaves)
                var stageMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/sprout/M_Sprout_Stage2.mat");
                if (stageMat != null)
                {
                    foreach (var rend in stage2Obj.GetComponentsInChildren<Renderer>(true))
                    {
                        rend.sharedMaterial = stageMat;
                    }
                }

                stage2Obj.SetActive(false);
            }
            return stage2Obj;
        }

        private static GameObject SetupPotAR(GameObject parent)
        {
            var potPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Pot_AR.prefab");
            GameObject potObj = null;
            if (potPrefab != null)
            {
                potObj = (GameObject)PrefabUtility.InstantiatePrefab(potPrefab);
                potObj.name = "Pot_AR";
                potObj.transform.SetParent(parent.transform, false);
                potObj.transform.localPosition = Vector3.zero;
                potObj.transform.localRotation = Quaternion.identity;
                potObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

                // Add SoilCollisionPlane for dropped pollen collision
                var soilPlane = new GameObject("SoilCollisionPlane");
                soilPlane.transform.SetParent(potObj.transform, false);
                // In Pot_AR (local scale 0.1), local Y 2.03f corresponds to 0.203m in GardenRig space (top surface of soil)
                soilPlane.transform.localPosition = new Vector3(0f, 2.03f, 0f);
                soilPlane.transform.localRotation = Quaternion.identity;
            }
            return potObj;
        }

        private static GameObject SetupSeed(GameObject parent)
        {
            GameObject seedRoot = new GameObject("Seed");
            seedRoot.transform.SetParent(parent.transform, false);
            // Soil surface in Pot_AR (local y 2.11 * 0.1 = 0.211m)
            seedRoot.transform.localPosition = new Vector3(0f, 0.212f, 0f);
            seedRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 15f);
            seedRoot.transform.localScale = Vector3.one * 0.02f;

            // Load seed.blend model to extract the seed mesh and material
            var seedBlendPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/seed/seed.blend");
            if (seedBlendPrefab != null)
            {
                // Instantiate seed model children
                var instance = (GameObject)Object.Instantiate(seedBlendPrefab, seedRoot.transform);
                instance.name = "SeedModel";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Hide or remove 'seedholder' dish so only the cute seed sits in the soil
                var holder = instance.transform.Find("seedholder");
                if (holder != null)
                {
                    holder.gameObject.SetActive(false);
                }
            }
            else
            {
                // Fallback sphere if blend not loaded
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "SeedFallback";
                sphere.transform.SetParent(seedRoot.transform, false);
                sphere.transform.localScale = new Vector3(1f, 1.4f, 0.6f);
            }

            return seedRoot;
        }

        private static GameObject SetupSprout(GameObject parent)
        {
            var sproutFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/sprout/dancing sprout.fbx");
            GameObject sproutObj = null;
            if (sproutFBX != null)
            {
                sproutObj = (GameObject)Object.Instantiate(sproutFBX, parent.transform);
                sproutObj.name = "Sprout";
                sproutObj.transform.localPosition = new Vector3(-0.334f, 0.175f, 0f);
                sproutObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                sproutObj.transform.localScale = Vector3.one * 0.65f;

                // Remove extraneous Blender Camera & Light from imported FBX
                var camChild = sproutObj.transform.Find("Camera");
                if (camChild != null) Object.DestroyImmediate(camChild.gameObject);
                var lightChild = sproutObj.transform.Find("Light");
                if (lightChild != null) Object.DestroyImmediate(lightChild.gameObject);

                // Assign hand-painted 4K sapling color texture material to SkinnedMeshRenderer
                var creamWhite = AssetDatabase.LoadAssetAtPath<Material>("Assets/sprout/CreamWhite.mat");
                var smr = sproutObj.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && creamWhite != null)
                {
                    smr.sharedMaterial = creamWhite;
                    smr.gameObject.SetActive(true);

                    // Add CapsuleCollider for raycast tap detection to replay dance
                    var col = smr.gameObject.AddComponent<CapsuleCollider>();
                    col.center = smr.localBounds.center;
                    col.radius = 1.2f;
                    col.height = 3.5f;
                }

                // Attach SproutDanceButton for celebratory growth & dancing
                var danceBtn = sproutObj.AddComponent<SproutDanceButton>();
                danceBtn.sprout = sproutObj.transform;
                danceBtn.startingSize = 1f;
                danceBtn.growDuration = 0f;
                danceBtn.finalScaleMultiplier = 1f;
                danceBtn.danceDuration = 5f;
                danceBtn.swayAngle = 14f;
                danceBtn.swaySpeed = 3f;
                danceBtn.bounceHeight = 0.035f;

                // Sprout starts inactive until 100% growth
                sproutObj.SetActive(false);
            }
            return sproutObj;
        }

        private static GameObject SetupWateringCan(GameObject parent)
        {
            var canFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/WateringCan/WateringCanModel.fbx");
            GameObject canObj = null;
            if (canFBX != null)
            {
                canObj = (GameObject)Object.Instantiate(canFBX, parent.transform);
                canObj.name = "WateringCan";
                // Positioned on the right rim of the pot angled towards the soil (scaled appropriately to pot)
                canObj.transform.localPosition = new Vector3(1.60f, -0.66f, 0.83f);
                canObj.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
                canObj.transform.localScale = Vector3.one * 1.12f;

                // Disable/remove stray static mesh renderers on wateremitter/waterdrop
                var emitterTrans = canObj.transform.Find("Model/wateremitter");
                if (emitterTrans != null)
                {
                    var mr = emitterTrans.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    var mf = emitterTrans.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);
                }
                var dropTrans = canObj.transform.Find("waterdrop");
                if (dropTrans != null)
                {
                    Object.DestroyImmediate(dropTrans.gameObject);
                }

                // Add Animator
                var anim = canObj.GetComponent<Animator>();
                if (anim == null) anim = canObj.AddComponent<Animator>();
                var canController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/WateringCan/WateringCanController.controller");
                if (canController != null) anim.runtimeAnimatorController = canController;

                // Add AudioSource
                var audioSrc = canObj.GetComponent<AudioSource>();
                if (audioSrc == null) audioSrc = canObj.AddComponent<AudioSource>();
                var waterClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audios/Watering Sound Effect.mp3");
                if (waterClip != null)
                {
                    audioSrc.clip = waterClip;
                    audioSrc.playOnAwake = false;
                    audioSrc.loop = true;
                    audioSrc.spatialBlend = 1.0f;
                    audioSrc.minDistance = 0.5f;
                    audioSrc.maxDistance = 5.0f;
                }

                // Add Water Droplet ParticleSystem to spout (wateremitter)
                ParticleSystem waterPS = null;
                if (emitterTrans != null)
                {
                    waterPS = emitterTrans.gameObject.AddComponent<ParticleSystem>();
                    var main = waterPS.main;
                    main.playOnAwake = false;
                    main.loop = true;
                    main.duration = 1.0f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.16f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.85f, 1.15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.032f, 0.052f);
                    main.gravityModifier = 1.2f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.maxParticles = 100;

                    var emission = waterPS.emission;
                    emission.rateOverTime = 55f;

                    var shape = waterPS.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 6f;
                    shape.radius = 0.012f;

                    var colorLife = waterPS.colorOverLifetime;
                    colorLife.enabled = true;
                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(new Color(0.35f, 0.85f, 1.0f), 0f), new GradientColorKey(new Color(0.65f, 0.95f, 1.0f), 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.90f, 0.8f), new GradientAlphaKey(0.0f, 1f) }
                    );
                    colorLife.color = grad;

                    var sizeLife = waterPS.sizeOverLifetime;
                    sizeLife.enabled = true;
                    AnimationCurve sizeCurve = new AnimationCurve();
                    sizeCurve.AddKey(0f, 0.75f);
                    sizeCurve.AddKey(0.25f, 1.0f);
                    sizeCurve.AddKey(0.85f, 1.0f);
                    sizeCurve.AddKey(1f, 0.5f);
                    sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                    var dropletMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/WateringCan/M_WaterDroplet.mat");
                    var psRend = emitterTrans.GetComponent<ParticleSystemRenderer>();
                    if (dropletMat != null && psRend != null)
                    {
                        psRend.sharedMaterial = dropletMat;
                        psRend.renderMode = ParticleSystemRenderMode.Billboard;
                    }
                }

                // Add Soil Splash ParticleSystem right on pot soil (GardenRig space: 0.02, 0.218, 0.01)
                GameObject splashObj = new GameObject("SoilSplashFX");
                splashObj.transform.SetParent(parent.transform, false);
                splashObj.transform.localPosition = new Vector3(0.02f, 0.218f, 0.01f);
                var splashPS = splashObj.AddComponent<ParticleSystem>();
                var sMain = splashPS.main;
                sMain.playOnAwake = false;
                sMain.loop = true;
                sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
                sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.20f, 0.38f);
                sMain.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.030f);
                sMain.gravityModifier = 0.8f;
                sMain.simulationSpace = ParticleSystemSimulationSpace.World;
                sMain.maxParticles = 50;

                var sEmission = splashPS.emission;
                sEmission.rateOverTime = 35f;

                var sShape = splashPS.shape;
                sShape.shapeType = ParticleSystemShapeType.Circle;
                sShape.radius = 0.06f;
                sShape.rotation = new Vector3(-90f, 0f, 0f);

                var sDropletMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/WateringCan/M_WaterDroplet.mat");
                var sRend = splashObj.GetComponent<ParticleSystemRenderer>();
                if (sDropletMat != null && sRend != null)
                {
                    sRend.sharedMaterial = sDropletMat;
                    sRend.renderMode = ParticleSystemRenderMode.Billboard;
                }

                // Add ARWateringController
                var wateringCtrl = canObj.AddComponent<ARWateringController>();
                var so = new SerializedObject(wateringCtrl);
                var modelObj = canObj.transform.Find("Model");
                so.FindProperty("wateringCanRoot").objectReferenceValue = modelObj != null ? modelObj.gameObject : canObj;
                so.FindProperty("wateringAnimator").objectReferenceValue = anim;
                so.FindProperty("waterAudioSource").objectReferenceValue = audioSrc;
                so.FindProperty("waterParticleSystem").objectReferenceValue = waterPS;
                so.FindProperty("soilSplashParticleSystem").objectReferenceValue = splashPS;
                so.FindProperty("holdDuration").floatValue = 3.0f;
                so.FindProperty("cooldownDuration").floatValue = 10.0f;
                so.ApplyModifiedProperties();

                // Keep canObj active so ARWateringController can run its Update() loop and cooldown countdown.
                // The visual Model child is hidden initially.
                if (modelObj != null)
                {
                    modelObj.gameObject.SetActive(false);
                }
                canObj.SetActive(true);
            }
            return canObj;
        }

        private static GameObject SetupCompanions(GameObject parent, GameObject potObj)
        {
            GameObject companionsRoot = new GameObject("Companions");
            companionsRoot.transform.SetParent(parent.transform, false);
            companionsRoot.transform.localPosition = Vector3.zero;
            companionsRoot.transform.localRotation = Quaternion.identity;

            var visitManager = companionsRoot.AddComponent<CompanionVisitManager>();

            // Setup SoilPollenFX under GardenRig to accumulate falling pollen directly on top of the soil
            GameObject pollenFXObj = new GameObject("SoilPollenFX");
            pollenFXObj.transform.SetParent(parent.transform, false);
            pollenFXObj.transform.localPosition = Vector3.zero;
            pollenFXObj.transform.localRotation = Quaternion.identity;

            var pollenPS = pollenFXObj.AddComponent<ParticleSystem>();
            var soilManager = pollenFXObj.AddComponent<SoilPollenManager>();
            soilManager.pollenPS = pollenPS;
            soilManager.gardenRig = parent.transform;

            var main = pollenPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = parent.transform;
            main.gravityModifier = 0.16f;
            main.maxParticles = 800;
            main.loop = false;
            main.playOnAwake = true;

            var em = pollenPS.emission;
            em.enabled = false;

            var shape = pollenPS.shape;
            shape.enabled = false;

            var pollenCol = pollenPS.collision;
            pollenCol.enabled = true;
            pollenCol.type = ParticleSystemCollisionType.Planes;
            var soilPlane = potObj != null ? potObj.transform.Find("SoilCollisionPlane") : null;
            if (soilPlane != null)
            {
                pollenCol.SetPlane(0, soilPlane);
            }
            pollenCol.bounce = 0f;
            pollenCol.dampen = 1f;
            pollenCol.lifetimeLoss = 0f;
            pollenCol.minKillSpeed = 0f;
            pollenCol.radiusScale = 1f;

            // 1. Butterfly
            GameObject butterflyObj = null;
            const string blendPath = "Assets/Butterfly/Butterfly.blend";

            // Configure Mesh Renderer on SoilPollenFX using Icosphere and fade-capable M_Soil_Pollen.mat
            var psr = pollenFXObj.GetComponent<ParticleSystemRenderer>();
            var allBlendAssets = AssetDatabase.LoadAllAssetsAtPath(blendPath);
            UnityEngine.Mesh icoMesh = null;
            Material mat003 = null;
            foreach (var a in allBlendAssets)
            {
                if (a is UnityEngine.Mesh m && m.name == "Icosphere") icoMesh = m;
                if (a is Material mat && mat.name == "Material.003") mat003 = mat;
            }
            var pollenMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/M_Soil_Pollen.mat");
            psr.renderMode = ParticleSystemRenderMode.Mesh;
            if (icoMesh != null) psr.mesh = icoMesh;
            if (pollenMat != null) psr.sharedMaterial = pollenMat;
            else if (mat003 != null) psr.sharedMaterial = mat003;

            var butterflyBlend = AssetDatabase.LoadAssetAtPath<GameObject>(blendPath);
            if (butterflyBlend != null)
            {
                butterflyObj = (GameObject)Object.Instantiate(butterflyBlend, companionsRoot.transform);
                butterflyObj.name = "Butterfly";
                // Hovering directly above the pot center (Y = 0.30m)
                butterflyObj.transform.localPosition = new Vector3(0f, 0.30f, 0f);
                butterflyObj.transform.localRotation = Quaternion.identity;

                // Scale root so orbit radius (5.705 units) is ~0.06m (6cm), placing the flight and falling pollen directly over the pot soil!
                butterflyObj.transform.localScale = Vector3.one * 0.0105f;

                // Compensate butterfly body scale so the butterfly itself retains its visible size (~4.5cm wingspan)
                var bodyTrans = butterflyObj.transform.Find("butterfly body");
                if (bodyTrans != null)
                {
                    bodyTrans.localScale = new Vector3(0.7714f, 0.6000f, 0.7714f);

                    // Disable all 86 baked Blender Dupli particle objects so they don't plunge through the floor
                    for (int i = 0; i < bodyTrans.childCount; i++)
                    {
                        var child = bodyTrans.GetChild(i);
                        if (child.name.Contains("Dupli") || child.name.Contains("Icosphere"))
                        {
                            var mr = child.GetComponent<MeshRenderer>();
                            if (mr != null) mr.enabled = false;
                            child.gameObject.SetActive(false);
                        }
                    }
                }

                // Disable helper objects from Blender
                var rootIco = butterflyObj.transform.Find("Icosphere");
                if (rootIco != null) rootIco.gameObject.SetActive(false);
                var bezier = butterflyObj.transform.Find("BézierCircle");
                if (bezier != null) bezier.gameObject.SetActive(false);

                // Ensure Animator with ButterflyController
                var anim = butterflyObj.GetComponent<Animator>();
                if (anim == null) anim = butterflyObj.AddComponent<Animator>();
                var butterflyController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/GameSceneAssets/ButterflyController.controller");
                if (butterflyController != null) anim.runtimeAnimatorController = butterflyController;

                // Attach enhanced ButterflyTransition for smooth arrival & graceful departure
                var transition = butterflyObj.GetComponent<ButterflyTransition>();
                if (transition == null) transition = butterflyObj.AddComponent<ButterflyTransition>();
                transition.SetTargetScale(Vector3.one * 0.0105f);
                transition.SetVisitDuration(16f);

                // Attach ButterflyPollenDropper to drop clusters of multiple really small pollen grains that fade out on departure
                var dropper = butterflyObj.GetComponent<ButterflyPollenDropper>();
                if (dropper == null) dropper = butterflyObj.AddComponent<ButterflyPollenDropper>();
                dropper.soilPollenManager = soilManager;
                dropper.butterflyBody = bodyTrans;
                dropper.gardenRig = parent.transform;
                dropper.butterflyTransition = transition;
                dropper.dropInterval = 0.20f;
                dropper.minClusterCount = 3;
                dropper.maxClusterCount = 6;
                dropper.minPollenSize = 0.0008f;
                dropper.maxPollenSize = 0.0016f;
                dropper.downwardVelocity = -0.08f;
                dropper.inwardPull = 0.15f;
                dropper.fadeDuration = 2.2f;

                butterflyObj.SetActive(false);
            }

            // 2. Bird
            GameObject birdObj = null;
            var birdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Bird/Prefabs/Bird.prefab");
            if (birdPrefab != null)
            {
                birdObj = (GameObject)PrefabUtility.InstantiatePrefab(birdPrefab, companionsRoot.transform);
                birdObj.name = "Bird";
                birdObj.transform.localScale = Vector3.one * 0.20f;

                var flight = birdObj.GetComponent<CrowFlightController>();
                if (flight != null)
                {
                    flight.target = potObj != null ? potObj.transform : companionsRoot.transform;
                    flight.pattern = CrowFlightController.FlightPattern.Orbit;
                    flight.orbitRadius = 0.60f;
                    flight.orbitSpeed = 40f;
                    flight.clockwise = true;
                    flight.startOrbitAngle = 45f;
                    flight.heightAboveTarget = 0.35f;
                    flight.verticalBobAmplitude = 0.025f;
                    flight.flapHeaveAmplitude = 0.008f;
                }

                var audioSrc = birdObj.GetComponent<AudioSource>();
                if (audioSrc == null) audioSrc = birdObj.AddComponent<AudioSource>();
                var birdClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audios/bird sound.wav");
                if (birdClip != null)
                {
                    audioSrc.clip = birdClip;
                    audioSrc.playOnAwake = true;
                    audioSrc.loop = true;
                    audioSrc.spatialBlend = 1f;
                    audioSrc.volume = 0.85f;
                }

                birdObj.SetActive(false);
            }

            // 3. Crow
            GameObject crowObj = null;
            var crowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Crow/Prefabs/Crow.prefab");
            if (crowPrefab != null)
            {
                crowObj = (GameObject)PrefabUtility.InstantiatePrefab(crowPrefab, companionsRoot.transform);
                crowObj.name = "Crow";
                crowObj.transform.localScale = Vector3.one * 0.25f;

                var flight = crowObj.GetComponent<CrowFlightController>();
                if (flight != null)
                {
                    flight.target = potObj != null ? potObj.transform : companionsRoot.transform;
                    flight.pattern = CrowFlightController.FlightPattern.Orbit;
                    flight.orbitRadius = 0.45f;
                    flight.orbitSpeed = 32f;
                    flight.clockwise = false;
                    flight.startOrbitAngle = 225f;
                    flight.heightAboveTarget = 0.42f;
                    flight.verticalBobAmplitude = 0.025f;
                    flight.flapHeaveAmplitude = 0.008f;
                }

                var audioSrc = crowObj.GetComponent<AudioSource>();
                if (audioSrc == null) audioSrc = crowObj.AddComponent<AudioSource>();
                var crowClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audios/crow sound.wav");
                if (crowClip != null)
                {
                    audioSrc.clip = crowClip;
                    audioSrc.playOnAwake = true;
                    audioSrc.loop = true;
                    audioSrc.spatialBlend = 1f;
                    audioSrc.volume = 0.90f;
                }

                // Sphere collider for tap-to-shoo detection
                var col = crowObj.GetComponent<SphereCollider>();
                if (col == null) col = crowObj.AddComponent<SphereCollider>();
                col.radius = 0.45f;
                col.center = new Vector3(0f, 0.1f, 0f);
                col.isTrigger = false;

                // CrowTapTarget component
                var tapTarget = crowObj.GetComponent<CrowTapTarget>();
                if (tapTarget == null) tapTarget = crowObj.AddComponent<CrowTapTarget>();

                crowObj.SetActive(false);
            }

            // Wire into CompanionVisitManager
            var so = new SerializedObject(visitManager);
            so.FindProperty("butterflyCompanion").objectReferenceValue = butterflyObj;
            so.FindProperty("birdCompanion").objectReferenceValue = birdObj;
            so.FindProperty("crowCompanion").objectReferenceValue = crowObj;
            so.FindProperty("visitDuration").floatValue = 16f;
            so.FindProperty("intermissionDuration").floatValue = 4f;
            so.FindProperty("initialDelay").floatValue = 2f;
            so.ApplyModifiedProperties();

            return companionsRoot;
        }

        private static GameObject SetupARCanvas()
        {
            GameObject canvasObj = new GameObject("AR_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var uiResources = new DefaultControls.Resources();

            // Font setup
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- Top Status Card ---
            GameObject topPanel = new GameObject("TopCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            topPanel.transform.SetParent(canvasObj.transform, false);
            var topPanelRect = topPanel.GetComponent<RectTransform>();
            topPanelRect.anchorMin = new Vector2(0.05f, 0.88f);
            topPanelRect.anchorMax = new Vector2(0.95f, 0.97f);
            topPanelRect.offsetMin = Vector2.zero;
            topPanelRect.offsetMax = Vector2.zero;
            var topImg = topPanel.GetComponent<Image>();
            topImg.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);

            // Title Text
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleObj.transform.SetParent(topPanel.transform, false);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.60f);
            titleRect.anchorMax = new Vector2(0.50f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleObj.GetComponent<Text>();
            titleText.text = "TINY GARDEN KEEPER";
            titleText.font = defaultFont;
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            // Percentage Text
            GameObject percentObj = new GameObject("PercentText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            percentObj.transform.SetParent(topPanel.transform, false);
            var percentRect = percentObj.GetComponent<RectTransform>();
            percentRect.anchorMin = new Vector2(0.55f, 0.60f);
            percentRect.anchorMax = new Vector2(0.95f, 0.95f);
            percentRect.offsetMin = Vector2.zero;
            percentRect.offsetMax = Vector2.zero;
            var percentText = percentObj.GetComponent<Text>();
            percentText.text = "Growth: 0%";
            percentText.font = defaultFont;
            percentText.fontSize = 24;
            percentText.fontStyle = FontStyle.Bold;
            percentText.alignment = TextAnchor.MiddleRight;
            percentText.color = new Color(0.35f, 0.85f, 0.45f, 1f);

            // Growth Slider (Progress Bar)
            GameObject sliderObj = DefaultControls.CreateSlider(uiResources);
            sliderObj.name = "GrowthProgressBar";
            sliderObj.transform.SetParent(topPanel.transform, false);
            var sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.05f, 0.35f);
            sliderRect.anchorMax = new Vector2(0.95f, 0.55f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            var slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;

            // Custom slider colors
            var fillArea = sliderObj.transform.Find("Fill Area/Fill");
            if (fillArea != null)
            {
                var sliderFillImg = fillArea.GetComponent<Image>();
                if (sliderFillImg != null) sliderFillImg.color = new Color(0.30f, 0.85f, 0.40f, 1f);
            }
            var handleSlideArea = sliderObj.transform.Find("Handle Slide Area");
            if (handleSlideArea != null) handleSlideArea.gameObject.SetActive(false);

            // Status Badge Text
            GameObject badgeObj = new GameObject("StatusBadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            badgeObj.transform.SetParent(topPanel.transform, false);
            var badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.05f, 0.05f);
            badgeRect.anchorMax = new Vector2(0.95f, 0.30f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            var badgeText = badgeObj.GetComponent<Text>();
            badgeText.text = "Resting in Soil...";
            badgeText.font = defaultFont;
            badgeText.fontSize = 20;
            badgeText.fontStyle = FontStyle.Italic;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = new Color(0.85f, 0.90f, 0.95f, 1f);

            // --- Bottom Controls Panel ---
            GameObject bottomPanel = new GameObject("BottomControls", typeof(RectTransform));
            bottomPanel.transform.SetParent(canvasObj.transform, false);
            var bottomRect = bottomPanel.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.06f, 0.04f);
            bottomRect.anchorMax = new Vector2(0.94f, 0.12f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            // Water Button (Full width of bottom panel)
            GameObject waterBtnObj = DefaultControls.CreateButton(uiResources);
            waterBtnObj.name = "WaterButton";
            waterBtnObj.transform.SetParent(bottomPanel.transform, false);
            var waterBtnRect = waterBtnObj.GetComponent<RectTransform>();
            waterBtnRect.anchorMin = Vector2.zero;
            waterBtnRect.anchorMax = Vector2.one;
            waterBtnRect.offsetMin = Vector2.zero;
            waterBtnRect.offsetMax = Vector2.zero;
            var waterBtnImg = waterBtnObj.GetComponent<Image>();
            waterBtnImg.color = new Color(0.16f, 0.50f, 0.88f, 0.95f);

            // Progress Fill Image inside WaterButton (radial/horizontal fill)
            GameObject fillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObj.transform.SetParent(waterBtnObj.transform, false);
            fillObj.transform.SetAsFirstSibling();
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var btnFillImg = fillObj.GetComponent<Image>();
            btnFillImg.color = new Color(0.35f, 0.85f, 1.0f, 0.50f);
            btnFillImg.type = Image.Type.Filled;
            btnFillImg.fillMethod = Image.FillMethod.Horizontal;
            btnFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            btnFillImg.fillAmount = 0f;

            var waterBtnText = waterBtnObj.GetComponentInChildren<Text>();
            if (waterBtnText != null)
            {
                waterBtnText.text = "HOLD TO WATER";
                waterBtnText.font = defaultFont;
                waterBtnText.fontSize = 32;
                waterBtnText.fontStyle = FontStyle.Bold;
                waterBtnText.alignment = TextAnchor.MiddleCenter;
                waterBtnText.color = Color.white;
                waterBtnText.horizontalOverflow = HorizontalWrapMode.Overflow;
                waterBtnText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            // Attach WaterButtonHoldTrigger for 3-second hold detection
            waterBtnObj.AddComponent<WaterButtonHoldTrigger>();

            return canvasObj;
        }

        private static void WireGrowthManager(GardenGrowthManager growthManager, GameObject seedObj, GameObject sproutStage1Obj, GameObject sproutStage2Obj, GameObject sproutFinalObj, GameObject wateringCanObj, GameObject companionsObj, GameObject canvasObj)
        {
            var so = new SerializedObject(growthManager);
            so.FindProperty("seedObject").objectReferenceValue = seedObj;
            so.FindProperty("sproutStage1Object").objectReferenceValue = sproutStage1Obj;
            so.FindProperty("sproutStage2Object").objectReferenceValue = sproutStage2Obj;
            so.FindProperty("sproutFinalObject").objectReferenceValue = sproutFinalObj;

            if (sproutFinalObj != null)
            {
                var danceBtn = sproutFinalObj.GetComponent<SproutDanceButton>();
                so.FindProperty("sproutDanceButton").objectReferenceValue = danceBtn;
            }

            if (wateringCanObj != null)
            {
                var waterCtrl = wateringCanObj.GetComponent<ARWateringController>();
                so.FindProperty("wateringController").objectReferenceValue = waterCtrl;

                // Wire Water button into ARWateringController
                var waterBtnTransform = canvasObj.transform.Find("BottomControls/WaterButton");
                if (waterBtnTransform != null)
                {
                    var waterBtn = waterBtnTransform.GetComponent<Button>();
                    var waterBtnText = waterBtnTransform.GetComponentInChildren<Text>();
                    var fillImg = waterBtnTransform.Find("ProgressFill")?.GetComponent<Image>();
                    var holdTrigger = waterBtnTransform.GetComponent<WaterButtonHoldTrigger>();
                    if (holdTrigger != null)
                    {
                        holdTrigger.SetController(waterCtrl);
                    }

                    var canSO = new SerializedObject(waterCtrl);
                    canSO.FindProperty("waterButton").objectReferenceValue = waterBtn;
                    canSO.FindProperty("buttonLabel").objectReferenceValue = waterBtnText;
                    canSO.FindProperty("buttonFillImage").objectReferenceValue = fillImg;
                    canSO.ApplyModifiedProperties();
                }
            }

            if (companionsObj != null)
            {
                var compManager = companionsObj.GetComponent<CompanionVisitManager>();
                so.FindProperty("companionManager").objectReferenceValue = compManager;
            }

            // Wire UI elements
            var slider = canvasObj.transform.Find("TopCard/GrowthProgressBar")?.GetComponent<Slider>();
            var percentText = canvasObj.transform.Find("TopCard/PercentText")?.GetComponent<Text>();
            var badgeText = canvasObj.transform.Find("TopCard/StatusBadgeText")?.GetComponent<Text>();

            so.FindProperty("growthProgressBar").objectReferenceValue = slider;
            var barFillImg = slider != null && slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            so.FindProperty("progressBarFillImage").objectReferenceValue = barFillImg;
            so.FindProperty("growthPercentageText").objectReferenceValue = percentText;
            so.FindProperty("statusBadgeText").objectReferenceValue = badgeText;

            // Growth rates (Discrete +3% water, zero passive, creature visits ~10% gentle boost)
            so.FindProperty("waterCycleProgressBonus").floatValue = 0.03f;
            so.FindProperty("butterflyBoostRate").floatValue = 0.005f;
            so.FindProperty("birdBoostRate").floatValue = 0.0055f;
            so.FindProperty("crowWeakenRate").floatValue = 0.04f;
            so.FindProperty("maxCompanionVisitBoost").floatValue = 0.10f;

            so.ApplyModifiedProperties();
        }

        private static void SetupPotPlacement(GameObject gardenRig)
        {
            var placement = Object.FindAnyObjectByType<ARPotPlacement>();
            if (placement != null)
            {
                var so = new SerializedObject(placement);
                var initialPotProp = so.FindProperty("initialPot");
                if (initialPotProp != null)
                {
                    initialPotProp.objectReferenceValue = gardenRig;
                }
                var allowMultipleProp = so.FindProperty("allowMultiplePots");
                if (allowMultipleProp != null)
                {
                    allowMultipleProp.boolValue = false;
                }
                var showPlanesProp = so.FindProperty("showDetectedPlanes");
                if (showPlanesProp != null)
                {
                    showPlanesProp.boolValue = false;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(placement);
            }
        }

        private static void SetupDirectionalLight()
        {
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                var light = lightObj.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 1.1f;
                    light.shadows = LightShadows.Soft;
                    lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    EditorUtility.SetDirty(light);
                    EditorUtility.SetDirty(lightObj.transform);
                }
            }
        }
    }
}
