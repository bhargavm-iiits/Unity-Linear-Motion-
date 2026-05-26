using UnityEngine;
using UnityEngine.UI;

namespace NCERT.Chapter2.VR
{
    public class MidnightDefenderController : MonoBehaviour
    {
        [Header("Physics Settings")]
        public float initialVelocity = 0f;      // u (m/s)
        public float acceleration = 1.0f;        // a (m/s²)
        public float maxSpeed = 25f;            // Maximum speed limit (v_max)
        public float trackStart = -70f;          // Z start coordinate
        public float trackEnd = 130f;            // Z end coordinate

        [Header("Holographic Displays")]
        public TextMesh statsDisplay;            // Stats display component
        public TextMesh equationDisplay;         // Equations display component

        private float timeElapsed = 0f;          // t (s)
        private float currentSpeed = 0f;         // v (m/s)
        private float distanceTraveled = 0f;     // s (m)
        private Vector3 startPosition;
        private bool isRunning = false;

        // HUD Pop-up elements floating above the car
        private GameObject carHUDText;
        private TextMesh carHUDTextComponent;
        private GameObject milestoneHUD;
        private TextMesh milestoneHUDComponent;
        private float hudProgress = 1f;
        private int lastMilestoneTriggered = -1;

        private GameObject statsHUD;
        private GameObject equationHUD;

        // Stopping event variables
        private enum SimState
        {
            InitialSlides,
            Accelerating,
            Braking,
            ShowingPPTSlide,
            ContinuingAcceleration,
            Finished
        }
        private SimState state = SimState.InitialSlides;
        private float stateTimer = 0f;
        private int currentSlideIndex = 0;

        private float phase1Time = 0f;
        private float phase2Time = 0f;
        private float speedAtBrake = 0f;
        private float zAtBrake = 30f; // Spotted at 100m from start (Z = 30m)
        private GameObject pptCanvasInstance;
        private Transform elephantTransform;

        private enum ElephantState
        {
            WaitingLeft,
            WalkingToRoad,
            StayingOnRoad,
            WalkingToRight,
            Cleared
        }
        private ElephantState elephantState = ElephantState.WaitingLeft;
        private float elephantTimer = 0f;

        void Start()
        {
            startPosition = transform.position;
            // Find and destroy the static overlay title canvas if it exists so it doesn't block introductory slides
            GameObject titleCanvas = GameObject.Find("AccelerationTitleCanvas");
            if (titleCanvas != null)
            {
                Destroy(titleCanvas);
            }
            CreateCarHUD();
            CreateHolographicHUDs();
            ResetSimulation();
        }

        void CreateCarHUD()
        {
            carHUDText = new GameObject("CarHUDText");
            // Do not parent to prevent inheriting the 200x vehicle scale
            carHUDText.transform.position = transform.position + new Vector3(0f, 1.4f, 0f);
            carHUDText.transform.rotation = Quaternion.identity;

            carHUDTextComponent = carHUDText.AddComponent<TextMesh>();
            carHUDTextComponent.fontSize = 55;
            carHUDTextComponent.characterSize = 0.016f;
            carHUDTextComponent.anchor = TextAnchor.LowerCenter; // Align bottom center so it hovers above the roof
            carHUDTextComponent.alignment = TextAlignment.Center;
            carHUDTextComponent.color = Color.white;
            carHUDTextComponent.fontStyle = FontStyle.Bold;

            // Create secondary milestone/notification HUD floating above the main HUD
            milestoneHUD = new GameObject("MilestoneHUD");
            milestoneHUD.transform.position = transform.position + new Vector3(0f, 2.6f, 0f);
            milestoneHUD.transform.rotation = Quaternion.identity;

            milestoneHUDComponent = milestoneHUD.AddComponent<TextMesh>();
            milestoneHUDComponent.fontSize = 85;
            milestoneHUDComponent.characterSize = 0.022f;
            milestoneHUDComponent.anchor = TextAnchor.MiddleCenter;
            milestoneHUDComponent.alignment = TextAlignment.Center;
            milestoneHUDComponent.color = new Color(0f, 1f, 0.9f, 1f); // Neon Electric Cyan!
            milestoneHUDComponent.fontStyle = FontStyle.Bold;

            TriggerHUDMessage("START! ⚡");
        }

        void CreateHolographicHUDs()
        {
            // Side HUDs are deprecated and merged into the single central carHUDText above the car.
            statsHUD = null;
            equationHUD = null;
        }

        private Material CreateGlassMaterial()
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.04f, 0.12f, 0.18f, 0.55f); // Translucent deep cyan
            
            // Set rendering mode to Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.5f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            
            return mat;
        }

        private void TriggerHUDMessage(string message)
        {
            if (milestoneHUDComponent != null)
            {
                milestoneHUDComponent.text = message;
                hudProgress = 0f; // Reset animation to trigger the elastic pop!
            }
        }

        void Update()
        {
            if (isRunning)
            {
                // Position HUDs relative to the car (unparented to bypass the 200x car scale)
                if (carHUDText != null)
                {
                    carHUDText.transform.position = transform.position + new Vector3(0f, 1.4f, 0f);
                }
                if (milestoneHUD != null)
                {
                    milestoneHUD.transform.position = transform.position + new Vector3(0f, 2.6f, 0f);
                }

                // Smoothly orient the floating text and HUDs to always face the camera
                if (carHUDText != null && Camera.main != null)
                {
                    carHUDText.transform.rotation = Quaternion.LookRotation(carHUDText.transform.position - Camera.main.transform.position);
                }
                if (milestoneHUD != null && Camera.main != null)
                {
                    milestoneHUD.transform.rotation = Quaternion.LookRotation(milestoneHUD.transform.position - Camera.main.transform.position);
                }

                // Handle elephant movement (Coming from left to right, stays 3 seconds on road once car stops)
                if (elephantTransform != null)
                {
                    switch (elephantState)
                    {
                        case ElephantState.WaitingLeft:
                            elephantTransform.position = new Vector3(-8.0f, 2.0f, 75f);
                            elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                            elephantTransform.localScale = new Vector3(300f, 300f, 300f);
                            // Trigger when the car starts braking (which happens exactly when it spots the elephant at Z = 30m)
                            if (state == SimState.Braking)
                            {
                                elephantState = ElephantState.WalkingToRoad;
                            }
                            break;

                        case ElephantState.WalkingToRoad:
                            float currentX = elephantTransform.position.x;
                            float newX = Mathf.MoveTowards(currentX, 2.0f, Time.deltaTime * 2.0f); // Walk speed 2.0 units/sec
                            elephantTransform.position = new Vector3(newX, 2.0f, 75f);
                            elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                            elephantTransform.localScale = new Vector3(300f, 300f, 300f);
                            
                            if (Mathf.Abs(newX - 2.0f) < 0.01f)
                            {
                                elephantState = ElephantState.StayingOnRoad;
                                elephantTimer = 0f;
                            }
                            break;

                        case ElephantState.StayingOnRoad:
                            elephantTransform.position = new Vector3(2.0f, 2.0f, 75f);
                            elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                            elephantTransform.localScale = new Vector3(300f, 300f, 300f);

                            // Only count stay duration once the car stops!
                            if (state == SimState.ShowingPPTSlide)
                            {
                                // Force hide the HUDs completely during the elephant phase!
                                if (statsHUD != null && statsHUD.activeSelf) statsHUD.SetActive(false);
                                if (equationHUD != null && equationHUD.activeSelf) equationHUD.SetActive(false);
                                if (carHUDText != null && carHUDText.activeSelf) carHUDText.SetActive(false);
                                if (milestoneHUD != null && milestoneHUD.activeSelf) milestoneHUD.SetActive(false);

                                elephantTimer += Time.deltaTime;
                                if (elephantTimer >= 3.0f)
                                {
                                    elephantState = ElephantState.WalkingToRight;
                                }
                            }
                            break;

                        case ElephantState.WalkingToRight:
                            float currentX2 = elephantTransform.position.x;
                            float newX2 = Mathf.MoveTowards(currentX2, 8.0f, Time.deltaTime * 2.0f); // Speed 2.0 units/sec
                            elephantTransform.position = new Vector3(newX2, 2.0f, 75f);
                            elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                            elephantTransform.localScale = new Vector3(300f, 300f, 300f);

                            if (Mathf.Abs(newX2 - 8.0f) < 0.01f)
                            {
                                elephantState = ElephantState.Cleared;
                                
                                // Deactivate the elephant transform completely after crossing the road!
                                elephantTransform.gameObject.SetActive(false);

                                // Re-enable the holographic HUDs above the car!
                                if (carHUDText != null) carHUDText.SetActive(true);
                                if (milestoneHUD != null) milestoneHUD.SetActive(true);
                                TriggerHUDMessage("RESUMING! ⚡");

                                // Automatically resume constant acceleration!
                                state = SimState.ContinuingAcceleration;
                                stateTimer = 0f;
                            }
                            break;

                        case ElephantState.Cleared:
                            elephantTransform.position = new Vector3(8.0f, 2.0f, 75f);
                            elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                            elephantTransform.localScale = new Vector3(300f, 300f, 300f);
                            break;
                    }
                }

                switch (state)
                {


                    case SimState.InitialSlides:
                        currentSpeed = 0f;
                        // Hide HUDs during initial slide show so they don't show behind full-screen slides
                        if (statsHUD != null && statsHUD.activeSelf) statsHUD.SetActive(false);
                        if (equationHUD != null && equationHUD.activeSelf) equationHUD.SetActive(false);
                        if (carHUDText != null && carHUDText.activeSelf) carHUDText.SetActive(false);
                        
                        if (Input.GetKeyDown(KeyCode.Space))
                        {
                            AdvanceInitialSlide();
                        }
                        break;

                    case SimState.Accelerating:
                        timeElapsed += Time.deltaTime;
                        currentSpeed = initialVelocity + acceleration * timeElapsed;
                        distanceTraveled = initialVelocity * timeElapsed + 0.5f * acceleration * timeElapsed * timeElapsed;
                        
                        float newZ = startPosition.z + distanceTraveled;

                        // Spotted at Z = 30m (100m from start)
                        if (newZ >= zAtBrake)
                        {
                            state = SimState.Braking;
                            speedAtBrake = currentSpeed;
                            phase1Time = timeElapsed;
                            phase2Time = 0f;
                            TriggerHUDMessage("SPOT ELEPHANT! 🚨");
                        }
                        else
                        {
                            transform.position = new Vector3(startPosition.x, startPosition.y, newZ);
                        }
                        
                        CheckMilestones();
                        break;

                    case SimState.Braking:
                        {
                            phase2Time += Time.deltaTime;
                            timeElapsed = phase1Time + phase2Time;
                            
                            // Deceleration: a = -2.857 m/s²
                            float decel = -2.857f;
                            currentSpeed = speedAtBrake + decel * phase2Time;
                            if (currentSpeed < 0f) currentSpeed = 0f;

                            // Braking displacement
                            float brakeDisplacement = speedAtBrake * phase2Time + 0.5f * decel * phase2Time * phase2Time;
                            float brakeZ = zAtBrake + brakeDisplacement;

                            // Stop exactly 10m before the elephant (at Z = 65m)
                            if (brakeZ >= 65f || currentSpeed <= 0.01f)
                            {
                                brakeZ = 65f;
                                currentSpeed = 0f;
                                state = SimState.ShowingPPTSlide;
                                // Hide the HUDs during the elephant stopped phase
                                if (carHUDText != null) carHUDText.SetActive(false);
                                if (milestoneHUD != null) milestoneHUD.SetActive(false);
                            }

                            distanceTraveled = brakeZ - startPosition.z;
                            transform.position = new Vector3(startPosition.x, startPosition.y, brakeZ);
                        }
                        break;

                    case SimState.ShowingPPTSlide:
                        currentSpeed = 0f;
                        // Force hide the HUDs completely during the elephant phase!
                        if (carHUDText != null && carHUDText.activeSelf) carHUDText.SetActive(false);
                        if (milestoneHUD != null && milestoneHUD.activeSelf) milestoneHUD.SetActive(false);
                        break;

                    case SimState.ContinuingAcceleration:
                        {
                            stateTimer += Time.deltaTime;
                            timeElapsed = stateTimer;
                            
                            // Phase 3 Acceleration: a = 1.0 m/s² from rest starting at Z = 75f
                            currentSpeed = 0f + acceleration * stateTimer;
                            if (currentSpeed > maxSpeed) currentSpeed = maxSpeed;

                            float p3Displacement = 0.5f * acceleration * stateTimer * stateTimer;
                            float p3Z = 65f + p3Displacement;

                            // Travel to end of 200m track (Z = 130m)
                            if (p3Z >= 130f)
                            {
                                p3Z = 130f;
                                currentSpeed = 0f;
                                state = SimState.Finished;
                                TriggerHUDMessage("200m FINISH! 🏁");
                            }
                            else
                            {
                                CheckMilestones();
                            }

                            distanceTraveled = p3Z - startPosition.z;
                            transform.position = new Vector3(startPosition.x, startPosition.y, p3Z);
                        }
                        break;

                    case SimState.Finished:
                        currentSpeed = 0f;
                        if (Input.GetKeyDown(KeyCode.Space))
                        {
                            ResetSimulation();
                        }
                        break;
                }

                // Update text meshes on holographic billboards
                UpdateDisplayBillboards();
            }

            // Animate Car HUD scale pop
            if (hudProgress < 1f && carHUDText != null)
            {
                hudProgress += Time.deltaTime * 3.5f;
                if (hudProgress > 1f) hudProgress = 1f;
                
                float bounce = 1f + 0.25f * Mathf.Sin(hudProgress * Mathf.PI * 2.5f) * (1f - hudProgress);
                carHUDText.transform.localScale = Vector3.one * (hudProgress * bounce);
            }
        }

        private void CheckMilestones()
        {
            float currentDist = transform.position.z - startPosition.z;
            int currentMilestone = Mathf.FloorToInt(currentDist / 10f);
            
            if (currentMilestone > lastMilestoneTriggered && currentMilestone <= 20)
            {
                lastMilestoneTriggered = currentMilestone;
                int meters = currentMilestone * 10;
                TriggerHUDMessage($"{meters}m");
            }
        }

        private void ResumeSimulationAfterStop()
        {
            // 1. Destroy PPT Slide
            if (pptCanvasInstance != null)
            {
                Destroy(pptCanvasInstance);
                pptCanvasInstance = null;
            }

            // 2. Clear the Elephant off the road so the path is clear!
            if (elephantTransform != null)
            {
                // Transition elephant state to walk off to the right if it's not already walking/cleared
                if (elephantState != ElephantState.WalkingToRight && elephantState != ElephantState.Cleared)
                {
                    elephantState = ElephantState.WalkingToRight;
                }
            }

            // 3. Trigger HUD Resume Msg
            TriggerHUDMessage("RESUMING! ⚡");

            // 4. Transition State
            state = SimState.ContinuingAcceleration;
            stateTimer = 0f;
        }

        public void ResetSimulation()
        {
            timeElapsed = 0f;
            phase1Time = 0f;
            phase2Time = 0f;
            stateTimer = 0f;
            state = SimState.Accelerating;
            speedAtBrake = 0f;
            currentSpeed = initialVelocity;
            distanceTraveled = 0f;
            lastMilestoneTriggered = -1;
            transform.position = new Vector3(startPosition.x, startPosition.y, trackStart);
            transform.rotation = Quaternion.Euler(-90f, -180f, 90f);
            
            // Find elephant robustly (even if inactive)
            GameObject elephant = FindElephant();
            if (elephant != null)
            {
                elephantTransform = elephant.transform;
                // Ensure the elephant is active at start!
                elephant.SetActive(true);

                // Reset elephant to starting side of the road in the jungle (left side, X = -8m) at Y = 2m, Z = 75m
                elephantTransform.position = new Vector3(-8.0f, 2.0f, 75f);
                elephantTransform.rotation = Quaternion.Euler(-90f, 0f, -270f);
                elephantTransform.localScale = new Vector3(300f, 300f, 300f);
            }
            elephantState = ElephantState.WaitingLeft;
            elephantTimer = 0f;
            currentSlideIndex = 0;
            state = SimState.InitialSlides;

            // Hide HUDs initially during full-screen slides phase
            if (carHUDText != null) carHUDText.SetActive(false);
            if (milestoneHUD != null) milestoneHUD.SetActive(false);

            // Destroy any existing ScreenSpaceOverlay PPT pop-up
            if (pptCanvasInstance != null)
            {
                Destroy(pptCanvasInstance);
                pptCanvasInstance = null;
            }

            ShowImageSlide(0);
            isRunning = true;
        }

        private GameObject FindElephant()
        {
            // First try active
            GameObject go = GameObject.Find("Gentle_Giant_Elephant");
            if (go != null) return go;

            // If not found, search all GameObjects including inactive ones
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in all)
            {
                // Ensure it is a scene object and not a prefab template in the assets folder
#if UNITY_EDITOR
                if (obj.name == "Gentle_Giant_Elephant" && !UnityEditor.EditorUtility.IsPersistent(obj))
                {
                    return obj;
                }
#else
                if (obj.name == "Gentle_Giant_Elephant" && obj.scene.name != null)
                {
                    return obj;
                }
#endif
            }
            return null;
        }

        private void UpdateDisplayBillboards()
        {
            if (state == SimState.InitialSlides)
            {
                if (carHUDText != null && carHUDText.activeSelf) carHUDText.SetActive(false);
                return;
            }

            if (carHUDTextComponent == null) return;

            string stateName = "ACCELERATING";
            float currentAccel = acceleration;

            if (state == SimState.Braking)
            {
                stateName = "<color=#ff3333ff>EMERGENCY BRAKING</color>";
                currentAccel = -2.86f;
            }
            else if (state == SimState.ShowingPPTSlide)
            {
                stateName = "<color=#ffff00ff>HALTED</color>";
                currentAccel = 0f;
            }
            else if (state == SimState.ContinuingAcceleration)
            {
                stateName = "<color=#00ffffff>RESUMING ACCEL</color>";
                currentAccel = acceleration;
            }
            else if (state == SimState.Finished)
            {
                stateName = "<color=#00ff00ff>FINISHED RUN</color>";
                currentAccel = 0f;
            }

            // Build premium real-time equations and telemetry string
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            sb.AppendLine("<b><color=#00ffffff>REAL-TIME PHYSICS TELEMETRY</color></b>");
            sb.AppendLine("<color=#555555ff>--------------------------------------------------</color>");
            sb.AppendLine($"Simulation State : {stateName}");
            sb.AppendLine($"Time (t)         : <color=#00ffffff>{timeElapsed:F2} s</color>   |   Acceleration (a) : <color=#ffff00ff>{currentAccel:F2} m/s²</color>");
            sb.AppendLine($"Speed (v)        : <color=#00ff00ff>{currentSpeed:F2} m/s</color>   |   Displacement (s) : <color=#ff00ffff>{distanceTraveled:F2} m</color>");
            sb.AppendLine("<color=#555555ff>--------------------------------------------------</color>");
            sb.AppendLine("<b>ACCELERATION EQUATIONS</b>");

            if (state == SimState.Accelerating)
            {
                sb.AppendLine("<b>v = u + at</b>  =>  v = 0.00 + (1.00 * " + $"{timeElapsed:F2}) = <color=#00ff00ff>{currentSpeed:F2} m/s</color>");
                sb.AppendLine("<b>s = ut + ½at²</b>  =>  s = 0.00 + (0.5 * 1.00 * " + $"{timeElapsed*timeElapsed:F2}) = <color=#ff00ffff>{distanceTraveled:F2} m</color>");
            }
            else if (state == SimState.Braking || state == SimState.ShowingPPTSlide)
            {
                float brakeTime = phase2Time;
                sb.AppendLine("<b>v = u + at</b>  =>  v = " + $"{speedAtBrake:F2} + (-2.86 * {brakeTime:F2}) = <color=#ff3333ff>{currentSpeed:F2} m/s</color>");
                sb.AppendLine("<b>s = ut + ½at²</b>  =>  s = 100.00 + (" + $"{speedAtBrake:F2} * {brakeTime:F2}) + (0.5 * -2.86 * {brakeTime*brakeTime:F2}) = <color=#ff00ffff>{distanceTraveled:F2} m</color>");
            }
            else if (state == SimState.ContinuingAcceleration || state == SimState.Finished)
            {
                sb.AppendLine("<b>v = u + at</b>  =>  v = 0.00 + (1.00 * " + $"{timeElapsed:F2}) = <color=#00ff00ff>{currentSpeed:F2} m/s</color>");
                sb.AppendLine("<b>s = ut + ½at²</b>  =>  s = 135.00 + (0.5 * 1.00 * " + $"{timeElapsed*timeElapsed:F2}) = <color=#ff00ffff>{distanceTraveled:F2} m</color>");
            }

            carHUDTextComponent.text = sb.ToString();

            // Also keep legacy fields updated in case they are referenced elsewhere
            if (statsDisplay != null) statsDisplay.text = carHUDTextComponent.text;
            if (equationDisplay != null) equationDisplay.text = carHUDTextComponent.text;
        }

        private void AdvanceInitialSlide()
        {
            currentSlideIndex++;

            if (currentSlideIndex >= 3)
            {
                // Destroy slides canvas
                if (pptCanvasInstance != null)
                {
                    Destroy(pptCanvasInstance);
                    pptCanvasInstance = null;
                }

                // Start the car!
                state = SimState.Accelerating;
                timeElapsed = 0f;
                distanceTraveled = 0f;
                
                // Show floating HUDs
                if (statsHUD != null) statsHUD.SetActive(true);
                if (equationHUD != null) equationHUD.SetActive(true);
                if (carHUDText != null) carHUDText.SetActive(true);
                
                TriggerHUDMessage("START! ⚡");
            }
            else
            {
                ShowImageSlide(currentSlideIndex);
            }
        }

        private void ShowImageSlide(int slideIndex)
        {
            if (pptCanvasInstance != null)
            {
                Destroy(pptCanvasInstance);
                pptCanvasInstance = null;
            }

            // Create Canvas GameObject
            pptCanvasInstance = new GameObject("PhysicsPPTSlideCanvas");
            Canvas canvas = pptCanvasInstance.AddComponent<Canvas>();
            
            // Set to ScreenSpaceCamera so it renders perfectly in VR and standard view
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 0.5f; // Draw 0.5m in front of camera
            canvas.sortingOrder = 999;   // Keep on top of other scene elements

            CanvasScaler scaler = pptCanvasInstance.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 1. Create a full-screen solid black background panel
            GameObject bgObj = new GameObject("SlideBackground");
            bgObj.transform.SetParent(pptCanvasInstance.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 2. Create Slide Image GameObject
            GameObject imgObj = new GameObject("SlideImage");
            imgObj.transform.SetParent(pptCanvasInstance.transform, false);

            Image img = imgObj.AddComponent<Image>();

            // Determine image path
            string path = "";
            if (slideIndex == 0) path = "Assets/Acceleration/Assets/Acceleration.png";
            else if (slideIndex == 1) path = "Assets/Acceleration/Assets/types_Acceleration.png";
            else if (slideIndex == 2) path = "Assets/Acceleration/Assets/grapgs.png";

            Sprite slideSprite = null;
#if UNITY_EDITOR
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                slideSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.LogError($"[SlideLoader] Failed to load slide image at path: {path}");
            }
#endif
            if (slideSprite != null)
            {
                img.sprite = slideSprite;
            }

            // Center and Fit Slide Image within the screen at exactly 3/4th (75%) of the screen area, keeping aspect ratio
            RectTransform rect = imgObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.125f, 0.125f); // 12.5% margin from left and bottom
            rect.anchorMax = new Vector2(0.875f, 0.875f); // 12.5% margin from right and top (covers exactly 75% or 3/4th of the screen)
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            img.preserveAspect = true; // Center and fit within screen without distorting or going out of scope!
        }
    }
}
