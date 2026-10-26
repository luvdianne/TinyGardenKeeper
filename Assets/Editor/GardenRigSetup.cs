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
            gardenRig.transform.rotation = Quaternion.identity;
            gardenRig.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(gardenRig, "Create GardenRig");

            // 4. Pot_AR
            GameObject potObj = SetupPotAR(gardenRig);

            // 5. Seed
            GameObject seedObj = SetupSeed(gardenRig);

            // 6. Sprout (Dancing Sprout with sapling color textures)
            GameObject sproutObj = SetupSprout(gardenRig);

            // 7. Watering Can
            GameObject wateringCanObj = SetupWateringCan(gardenRig);

            // 8. Companions (Butterfly, Bird, Crow)
            GameObject companionsObj = SetupCompanions(gardenRig, potObj);

            // 9. UI Canvas (Top status & progress bar, bottom action buttons)
            GameObject canvasObj = SetupARCanvas();

            // 10. Wire GardenGrowthManager
            var growthManager = gardenRig.AddComponent<GardenGrowthManager>();
            WireGrowthManager(growthManager, seedObj, sproutObj, wateringCanObj, companionsObj, canvasObj);

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
            AssetDatabase.Refresh();
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

        [MenuItem("TinyGarden/Preview/Show Sprout")]
        public static void PreviewSprout()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(false);
            if (can != null) can.SetActive(false);
            if (sprout != null)
            {
                sprout.SetActive(true);
                sprout.transform.localScale = Vector3.one * 0.35f;
                sprout.transform.localPosition = new Vector3(-0.18f, 0.211f, 0f);
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.28f, 24f, -15f, 1.2f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Watering Can")]
        public static void PreviewWateringCan()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var seed = rig.transform.Find("Seed")?.gameObject;
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(true);
            if (sprout != null) sprout.SetActive(false);
            if (can != null)
            {
                can.SetActive(true);
                can.transform.localPosition = new Vector3(0.50f, -0.06f, 0f);
                can.transform.localScale = Vector3.one * 0.40f;
            }

            FocusSceneView(rig.transform.position + Vector3.up * 0.28f, 24f, -15f, 1.2f);
            CaptureViewDirect();
        }

        [MenuItem("TinyGarden/Preview/Show Butterfly")]
        public static void PreviewButterfly()
        {
            var rig = GameObject.Find("GardenRig");
            if (rig == null) return;
            var butterfly = rig.transform.Find("Companions/Butterfly")?.gameObject;
            if (butterfly != null) butterfly.SetActive(true);

            FocusSceneView(rig.transform.position + Vector3.up * 0.30f, 20f, -20f, 1.5f);
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
            var sprout = rig.transform.Find("Sprout")?.gameObject;
            var can = rig.transform.Find("WateringCan")?.gameObject;
            if (seed != null) seed.SetActive(true);
            if (sprout != null) sprout.SetActive(false);
            if (can != null) can.SetActive(false);
            var companions = rig.transform.Find("Companions");
            if (companions != null)
            {
                foreach (Transform child in companions) child.gameObject.SetActive(false);
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
            string[] namesToSweep = { "GardenRig", "Pot_AR", "Bird", "Crow", "Butterfly", "AR_Canvas", "ActivePlacementReticle" };
            foreach (var name in namesToSweep)
            {
                GameObject existing = GameObject.Find(name);
                while (existing != null)
                {
                    Object.DestroyImmediate(existing);
                    existing = GameObject.Find(name);
                }
            }
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
                sproutObj.transform.localPosition = new Vector3(-0.18f, 0.211f, 0f);
                sproutObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                sproutObj.transform.localScale = Vector3.one * 0.35f;

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
                }

                // Attach SproutDanceButton for celebratory growth & dancing
                var danceBtn = sproutObj.AddComponent<SproutDanceButton>();
                danceBtn.sprout = sproutObj.transform;
                danceBtn.startingSize = 0.15f;
                danceBtn.growDuration = 2.5f;
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
                // Hovered over the pot rim tilted into soil
                canObj.transform.localPosition = new Vector3(0.50f, -0.06f, 0f);
                canObj.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
                canObj.transform.localScale = Vector3.one * 0.40f;

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

                // Add ARWateringController
                var wateringCtrl = canObj.AddComponent<ARWateringController>();
                var so = new SerializedObject(wateringCtrl);
                so.FindProperty("wateringCanRoot").objectReferenceValue = canObj;
                so.FindProperty("wateringAnimator").objectReferenceValue = anim;
                so.FindProperty("waterAudioSource").objectReferenceValue = audioSrc;
                so.ApplyModifiedProperties();

                // Watering can starts hidden until WATER button is clicked
                canObj.SetActive(false);
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

            // 1. Butterfly
            GameObject butterflyObj = null;
            var butterflyBlend = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Butterfly/Butterfly.blend");
            if (butterflyBlend != null)
            {
                butterflyObj = (GameObject)Object.Instantiate(butterflyBlend, companionsRoot.transform);
                butterflyObj.name = "Butterfly";
                // Centered on pot soil
                butterflyObj.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                butterflyObj.transform.localRotation = Quaternion.identity;
                butterflyObj.transform.localScale = Vector3.one * 0.045f;

                // Ensure Animator with ButterflyController
                var anim = butterflyObj.GetComponent<Animator>();
                if (anim == null) anim = butterflyObj.AddComponent<Animator>();
                var butterflyController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/GameSceneAssets/ButterflyController.controller");
                if (butterflyController != null) anim.runtimeAnimatorController = butterflyController;

                // Remove ButterflyTransition if attached so visitManager controls stay duration
                var transition = butterflyObj.GetComponent<ButterflyTransition>();
                if (transition != null) Object.DestroyImmediate(transition);

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
                var fillImg = fillArea.GetComponent<Image>();
                if (fillImg != null) fillImg.color = new Color(0.30f, 0.85f, 0.40f, 1f);
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
            bottomRect.anchorMin = new Vector2(0.05f, 0.04f);
            bottomRect.anchorMax = new Vector2(0.95f, 0.12f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            // Water Button
            GameObject waterBtnObj = DefaultControls.CreateButton(uiResources);
            waterBtnObj.name = "WaterButton";
            waterBtnObj.transform.SetParent(bottomPanel.transform, false);
            var waterBtnRect = waterBtnObj.GetComponent<RectTransform>();
            waterBtnRect.anchorMin = new Vector2(0f, 0f);
            waterBtnRect.anchorMax = new Vector2(0.48f, 1f);
            waterBtnRect.offsetMin = Vector2.zero;
            waterBtnRect.offsetMax = Vector2.zero;
            var waterBtnImg = waterBtnObj.GetComponent<Image>();
            waterBtnImg.color = new Color(0.20f, 0.55f, 0.90f, 0.95f);
            var waterBtnText = waterBtnObj.GetComponentInChildren<Text>();
            if (waterBtnText != null)
            {
                waterBtnText.text = "WATER";
                waterBtnText.font = defaultFont;
                waterBtnText.fontSize = 32;
                waterBtnText.fontStyle = FontStyle.Bold;
                waterBtnText.color = Color.white;
            }

            // Nurture / Dance Button
            GameObject danceBtnObj = DefaultControls.CreateButton(uiResources);
            danceBtnObj.name = "PlantDanceButton";
            danceBtnObj.transform.SetParent(bottomPanel.transform, false);
            var danceBtnRect = danceBtnObj.GetComponent<RectTransform>();
            danceBtnRect.anchorMin = new Vector2(0.52f, 0f);
            danceBtnRect.anchorMax = new Vector2(1f, 1f);
            danceBtnRect.offsetMin = Vector2.zero;
            danceBtnRect.offsetMax = Vector2.zero;
            var danceBtnImg = danceBtnObj.GetComponent<Image>();
            danceBtnImg.color = new Color(0.95f, 0.60f, 0.15f, 0.95f);
            var danceBtnText = danceBtnObj.GetComponentInChildren<Text>();
            if (danceBtnText != null)
            {
                danceBtnText.text = "NURTURE (+25%)";
                danceBtnText.font = defaultFont;
                danceBtnText.fontSize = 28;
                danceBtnText.fontStyle = FontStyle.Bold;
                danceBtnText.color = Color.white;
            }

            return canvasObj;
        }

        private static void WireGrowthManager(GardenGrowthManager growthManager, GameObject seedObj, GameObject sproutObj, GameObject wateringCanObj, GameObject companionsObj, GameObject canvasObj)
        {
            var so = new SerializedObject(growthManager);
            so.FindProperty("seedObject").objectReferenceValue = seedObj;
            so.FindProperty("sproutObject").objectReferenceValue = sproutObj;

            if (sproutObj != null)
            {
                var danceBtn = sproutObj.GetComponent<SproutDanceButton>();
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

                    var canSO = new SerializedObject(waterCtrl);
                    canSO.FindProperty("waterButton").objectReferenceValue = waterBtn;
                    canSO.FindProperty("buttonLabel").objectReferenceValue = waterBtnText;
                    canSO.ApplyModifiedProperties();

                    waterBtn.onClick.AddListener(waterCtrl.ToggleWatering);
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
            var uiDanceBtn = canvasObj.transform.Find("BottomControls/PlantDanceButton")?.GetComponent<Button>();

            so.FindProperty("growthProgressBar").objectReferenceValue = slider;
            so.FindProperty("growthPercentageText").objectReferenceValue = percentText;
            so.FindProperty("statusBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("plantDanceButton").objectReferenceValue = uiDanceBtn;

            if (uiDanceBtn != null)
            {
                uiDanceBtn.onClick.AddListener(growthManager.OnPlantDanceButtonClicked);
            }

            // Growth rates
            so.FindProperty("waterRate").floatValue = 0.15f;
            so.FindProperty("butterflyBoostRate").floatValue = 0.04f;
            so.FindProperty("birdBoostRate").floatValue = 0.05f;
            so.FindProperty("crowWeakenRate").floatValue = 0.04f;

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
