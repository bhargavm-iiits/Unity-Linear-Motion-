using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text.RegularExpressions;

/// <summary>
/// Controls the entire 3-Scene Educational Sequence for Physics Motion:
/// Scene 1: Camera glides down (0-4s). Pop-up slides appear directly above the athlete's head
///          with a 3-second gap ONLY after the camera settles near the runner.
/// Scene 2: Athlete Demonstration with real-time billboard text tracking. 
///          The athlete covers the COMPLETE track loop and returns exactly to the starting point.
///          Draws a Displacement Vector of 0.0m.
/// Scene 3: High-contrast comparison table covering the entire screen.
/// Incorporates exact handwritten notes points from the user.
/// </summary>
public class EducationalSeqManager : MonoBehaviour
{
    [Header("References")]
    public TrackRunner runner;
    public CameraFollow cameraFollow;
    public bool waitForManifest = true; // Wait for backend connection before starting race!
    
    [Header("UI Canvas Elements")]
    public Canvas educationCanvas;
    
    // Scene 2/1 billboard elements (Billboard above Athlete)
    private GameObject athleteBillboard;
    private TextMesh billboardText;
    private TextMesh billboardTextShadow; // 3D white drop-shadow for high-contrast backing
    private LineRenderer displacementVectorLine;
    private GameObject displacementLabelObj;
    
    // Scene 3 elements
    private GameObject scene3Panel;
    private GameObject cachedTrackObj;
    
    // Side metrics (Scene 2)
    private GameObject leftDistanceBillboard;
    private TextMesh leftDistanceText;
    private TextMesh leftDistanceTextShadow;
    
    private GameObject rightTimeBillboard;
    private TextMesh rightTimeText;
    private TextMesh rightTimeTextShadow;
    
    // Timing / state machine
    private int runPhase = 1; // 1 = Complete Loop, 2 = 100m Sprint
    private bool isDemoRunning = false;
    private bool isDemoFinished = false;
    
    // Athlete start/end tracking
    private Vector3 pointA;
    private Vector3 pointB;
    private float demoTime = 0f;
    private float targetDemoDistance = 0f; // Calculated dynamically at Start for one complete loop!

    private string[] scene1Slides = new string[]
    {
        // Distance Sequence (Slide 0 to 5) - "DISTANCE" is vibrant Blue!
        "<color=#0055ff><b>DISTANCE</b></color>",
        "Distance is the total path covered by an object during motion.",
        "Scalar quantity(only magnitude, no direction)",
        "Distance depends on the actual path travelled.",
        "Always positive",
        "Distance helps to measure speed",
        
        // Displacement Sequence (Slide 6 to 10) - "DISPLACEMENT" is vibrant Red!
        "<color=#ff2222><b>DISPLACEMENT</b></color>",
        "Displacement is the shortest straight-line distance between the initial position and final position of an object.",
        "Measures shortest path()",
        "Vector quantity(Both Magnitude + Direction)",
        "Can be positive, negative, or zero"
    };

    void Start()
    {
        // 1. Disable all other secondary cameras in the scene to guarantee only Main Camera renders!
        foreach (Camera otherCam in FindObjectsByType<Camera>())
        {
            if (otherCam != null && otherCam != Camera.main)
            {
                otherCam.gameObject.SetActive(false);
            }
        }

        // Calculate the exact target distance for one COMPLETE loop around the track
        if (runner != null)
        {
            float perimeter = 2f * runner.straightLength + 2f * Mathf.PI * runner.radius;
            targetDemoDistance = perimeter; // Athlete covers the COMPLETE track and returns exactly to Point A!
        }
        else
        {
            targetDemoDistance = 397.9f; // Fallback standard perimeter
        }

        // Initialize UI and Billboard elements
        InitializeUI();
        
        // Cache athlete starting position
        if (runner != null)
        {
            pointA = runner.transform.position; // Point A is start position (-50, 0, -31.5)
        }

        // Hide the track and runner initially during the 4-second camera glide to achieve zero-latency cinematic performance and render only text!
        // Search all root GameObjects robustly, finding "AthleteTrack" even if it starts as inactive!
        cachedTrackObj = null;
        foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObj != null && rootObj.name == "AthleteTrack")
            {
                cachedTrackObj = rootObj;
                break;
            }
        }
        
        if (cachedTrackObj != null) cachedTrackObj.SetActive(false);
        if (runner != null) runner.gameObject.SetActive(false);

        // Lock athlete movement and animation initially during the 4-second camera glide
        if (runner != null)
        {
            runner.isRunning = false;
            
            // Freeze the runner's animation loop initially during the glide!
            ForcePlayAnimation anim = runner.GetComponent<ForcePlayAnimation>();
            if (anim != null)
            {
                anim.animationSpeed = 0f; // Frozen in pose during the camera glide
            }
        }

        // Trigger camera cinematic glide down immediately
        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
        }

        // Start the sequence manager flow only if not waiting for backend manifest
        if (!waitForManifest)
        {
            BeginSequenceFromManifest();
        }
    }

    public void BeginSequenceFromManifest()
    {
        StartCoroutine(RunSequenceStateMachine());
    }

    void InitializeUI()
    {
        // 1. Create Screen Space Canvas for Scene 3 comparison table
        if (educationCanvas == null)
        {
            GameObject canvasObj = new GameObject("EducationCanvas");
            educationCanvas = canvasObj.AddComponent<Canvas>();
            educationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Load dynamic fallback font for comparison table
        Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (mainFont == null)
        {
            mainFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Calibri", "Helvetica" }, 40);
        }

        // 3. Create Athlete tracking billboard above runner's head
        if (runner != null)
        {
            athleteBillboard = new GameObject("RunnerTrackingBillboard");
            athleteBillboard.transform.SetParent(runner.transform, false);
            athleteBillboard.transform.localPosition = new Vector3(0f, 2.0f, 0f); // Placed lower to be perfectly visible in camera frustum!
            
            // Front Text Mesh (Pure Bold White Text / Rich Text Colors)
            GameObject frontTextObj = new GameObject("FrontText");
            frontTextObj.transform.SetParent(athleteBillboard.transform, false);
            frontTextObj.transform.localPosition = Vector3.zero;
            
            billboardText = frontTextObj.AddComponent<TextMesh>();
            billboardText.font = mainFont;
            frontTextObj.GetComponent<MeshRenderer>().sharedMaterial = mainFont.material;
            billboardText.fontSize = 120;
            billboardText.characterSize = 0.012f; // Reduced from 0.035f to 0.012f to prevent text from going out of the scene!
            billboardText.anchor = TextAnchor.LowerCenter;
            billboardText.alignment = TextAlignment.Center;
            billboardText.fontStyle = FontStyle.Bold;
            billboardText.color = Color.black; // Changed back to bold black as requested!

            athleteBillboard.SetActive(false);

            // Left side billboard (Distance measure - small text from start to end)
            leftDistanceBillboard = new GameObject("LeftDistanceBillboard");
            leftDistanceBillboard.transform.SetParent(runner.transform, false);
            leftDistanceBillboard.transform.localPosition = new Vector3(-2.2f, 1.2f, 0f);

            GameObject leftFront = new GameObject("LeftFront");
            leftFront.transform.SetParent(leftDistanceBillboard.transform, false);
            leftFront.transform.localPosition = Vector3.zero;
            leftDistanceText = leftFront.AddComponent<TextMesh>();
            leftDistanceText.font = mainFont;
            leftFront.GetComponent<MeshRenderer>().sharedMaterial = mainFont.material;
            leftDistanceText.fontSize = 100;
            leftDistanceText.characterSize = 0.025f; // Small text!
            leftDistanceText.anchor = TextAnchor.MiddleCenter;
            leftDistanceText.alignment = TextAlignment.Center;
            leftDistanceText.fontStyle = FontStyle.Bold;
            leftDistanceText.color = Color.white; // Pure white continuous distance calculation!

            // Right side billboard (Continuous time measure)
            rightTimeBillboard = new GameObject("RightTimeBillboard");
            rightTimeBillboard.transform.SetParent(runner.transform, false);
            rightTimeBillboard.transform.localPosition = new Vector3(2.2f, 1.2f, 0f);

            GameObject rightFront = new GameObject("RightFront");
            rightFront.transform.SetParent(rightTimeBillboard.transform, false);
            rightFront.transform.localPosition = Vector3.zero;
            rightTimeText = rightFront.AddComponent<TextMesh>();
            rightTimeText.font = mainFont;
            rightFront.GetComponent<MeshRenderer>().sharedMaterial = mainFont.material;
            rightTimeText.fontSize = 100;
            rightTimeText.characterSize = 0.025f; // Small text!
            rightTimeText.anchor = TextAnchor.MiddleCenter;
            rightTimeText.alignment = TextAlignment.Center;
            rightTimeText.fontStyle = FontStyle.Bold;
            rightTimeText.color = Color.white; // Pure white continuous time calculation!

            leftDistanceBillboard.SetActive(false);
            rightTimeBillboard.SetActive(false);
        }

        // 4. Create final Displacement Vector Line Renderer
        displacementVectorLine = gameObject.AddComponent<LineRenderer>();
        displacementVectorLine.startWidth = 0.8f;
        displacementVectorLine.endWidth = 0.8f;
        displacementVectorLine.positionCount = 0;
        displacementVectorLine.material = new Material(Shader.Find("Sprites/Default"));
        displacementVectorLine.startColor = new Color(0.9f, 0.1f, 0.1f); // Vibrant red vector line
        displacementVectorLine.endColor = new Color(0.9f, 0.1f, 0.1f);
        displacementVectorLine.enabled = false;

        // 5. Create Scene 3 Comparison Table UI
        CreateComparisonTableUI(mainFont);
    }

    string AutoWrapText(string text, int maxLineLength = 40)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // If it already has explicit newlines, don't wrap it
        if (text.Contains("\n")) return text;
        
        // Strip rich text color tags temporarily to calculate true string length for wrapping
        string tempText = Regex.Replace(text, @"<color=[^>]+>", "");
        tempText = tempText.Replace("</color>", "");
        if (tempText.Length <= maxLineLength) return text;

        string[] words = text.Split(' ');
        System.Text.StringBuilder wrappedText = new System.Text.StringBuilder();
        string currentLine = "";
        int currentLength = 0;

        foreach (string word in words)
        {
            // Calculate word length without color tags
            string cleanWord = Regex.Replace(word, @"<color=[^>]+>", "");
            cleanWord = cleanWord.Replace("</color>", "");

            if (currentLength + cleanWord.Length + 1 > maxLineLength)
            {
                if (wrappedText.Length > 0) wrappedText.Append("\n");
                wrappedText.Append(currentLine);
                currentLine = word;
                currentLength = cleanWord.Length;
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    currentLine += " ";
                    currentLength += 1;
                }
                currentLine += word;
                currentLength += cleanWord.Length;
            }
        }

        if (currentLine.Length > 0)
        {
            if (wrappedText.Length > 0) wrappedText.Append("\n");
            wrappedText.Append(currentLine);
        }

        return wrappedText.ToString();
    }

    string StripColorTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string clean = Regex.Replace(text, @"<color=[^>]+>", "");
        clean = clean.Replace("</color>", "");
        return clean;
    }

    void SetBillboardText(string newText)
    {
        string wrapped = AutoWrapText(newText, 55); // Wrapped at 55 characters so slides appear as complete, unbroken sentences!
        
        if (billboardText != null) 
        {
            billboardText.text = wrapped; // Shows rich colors (Blue/Red/Black)
        }
        
        if (billboardTextShadow != null) 
        {
            // Drop shadow must remain solid pitch black with NO color override tags for contrast!
            billboardTextShadow.text = StripColorTags(wrapped); 
        }
    }

    void SetLeftDistanceText(string text)
    {
        if (leftDistanceText != null) leftDistanceText.text = text;
        if (leftDistanceTextShadow != null) leftDistanceTextShadow.text = StripColorTags(text);
    }

    void SetRightTimeText(string text)
    {
        if (rightTimeText != null) rightTimeText.text = text;
        if (rightTimeTextShadow != null) rightTimeTextShadow.text = StripColorTags(text);
    }

    void CreateTextBackground(GameObject parent, Vector3 localPos, Vector3 scale)
    {
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (bg.GetComponent<Collider>() != null)
        {
            Destroy(bg.GetComponent<Collider>()); // No collision
        }
        bg.transform.SetParent(parent.transform, false);
        bg.transform.localPosition = localPos;
        bg.transform.localScale = scale;
        
        Renderer rend = bg.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(1f, 1f, 1f, 0.85f); // Beautiful semi-transparent white card background for bold black text!
    }

    IEnumerator RunSequenceStateMachine()
    {
        // ==========================================
        // SCENE 1: Camera Glide (4 Seconds Standstill)
        // ==========================================
        yield return new WaitForSeconds(4.0f); // Camera glides down near runner, screen remains clear

        // Activate the track and runner now that the camera has settled and the race is starting!
        if (cachedTrackObj != null) cachedTrackObj.SetActive(true);
        if (runner != null) runner.gameObject.SetActive(true);

        // ==========================================
        // SCENE 1 (Cont.): Start Running & Pop-up Slides
        // ==========================================
        if (runner != null)
        {
            runner.isRunning = true; // Athlete starts running now!
            
            // Enable the running animation cycle
            ForcePlayAnimation anim = runner.GetComponent<ForcePlayAnimation>();
            if (anim != null)
            {
                anim.animationSpeed = 1.6f; // Active sprinting!
            }
        }
        
        athleteBillboard.SetActive(true);

        // Enable left (Distance) and right (Time) metrics billboards immediately at the start of the race!
        if (leftDistanceBillboard != null) leftDistanceBillboard.SetActive(true);
        if (rightTimeBillboard != null) rightTimeBillboard.SetActive(true);
        isDemoRunning = true; // Start continuous tracking from starting point to end!

        // Sequence through DISTANCE (Slides 0 to 5) above running athlete with a 3.5-second gap!
        for (int i = 0; i < 6; i++)
        {
            SetBillboardText(scene1Slides[i]);
            yield return new WaitForSeconds(3.5f); // Gap of 3.5 seconds!
            SetBillboardText(""); // Clear
            yield return new WaitForSeconds(0.2f); // Short pause between popups
        }

        // Sequence through DISPLACEMENT (Slides 6 to 10) above running athlete with a 3.5-second gap!
        for (int i = 6; i < 11; i++)
        {
            SetBillboardText(scene1Slides[i]);
            yield return new WaitForSeconds(3.5f); // Gap of 3.5 seconds!
            SetBillboardText(""); // Clear
            yield return new WaitForSeconds(0.2f); // Short pause
        }

        // Formulas presentation above running athlete: Speed first, then Velocity
        SetBillboardText("Speed = Distance / Time");
        yield return new WaitForSeconds(3.5f); // Gap of 3.5 seconds!
        SetBillboardText("");
        yield return new WaitForSeconds(0.2f);

        SetBillboardText("Velocity = Displacement / Time");
        yield return new WaitForSeconds(3.5f); // Gap of 3.5 seconds!
        SetBillboardText("");
        yield return new WaitForSeconds(0.2f);

        // Hide main billboard after formulas finish
        if (athleteBillboard != null) athleteBillboard.SetActive(false);
    }

    void Update()
    {
        // 1. Maintain Billboard facing direction towards Main Camera
        if (athleteBillboard != null && athleteBillboard.activeInHierarchy && Camera.main != null)
        {
            athleteBillboard.transform.rotation = Quaternion.LookRotation(athleteBillboard.transform.position - Camera.main.transform.position);
        }
        if (leftDistanceBillboard != null && leftDistanceBillboard.activeInHierarchy && Camera.main != null)
        {
            leftDistanceBillboard.transform.rotation = Quaternion.LookRotation(leftDistanceBillboard.transform.position - Camera.main.transform.position);
        }
        if (rightTimeBillboard != null && rightTimeBillboard.activeInHierarchy && Camera.main != null)
        {
            rightTimeBillboard.transform.rotation = Quaternion.LookRotation(rightTimeBillboard.transform.position - Camera.main.transform.position);
        }

        // 2. Track athlete demo running distance & time in Scene 2
        if (isDemoRunning && !isDemoFinished && runner != null)
        {
            demoTime += Time.deltaTime;
            float distance = GetRunnerDistance();
            
            SetLeftDistanceText($"Distance\n{distance:F1}m");
            SetRightTimeText($"Time\n{demoTime:F1}s");

            if (runPhase == 1)
            {
                if (distance >= targetDemoDistance)
                {
                    StartCoroutine(ResetAndStartPhase2());
                }
            }
            else if (runPhase == 2)
            {
                if (distance >= 100f)
                {
                    Finish100mSprint();
                }
            }
        }
    }

    float GetRunnerDistance()
    {
        System.Reflection.FieldInfo distField = runner.GetType().GetField("distanceTraveled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (distField != null)
        {
            return (float)distField.GetValue(runner);
        }
        return demoTime * runner.speed;
    }

    IEnumerator ResetAndStartPhase2()
    {
        isDemoRunning = false; // Pause tracking
        
        if (runner != null)
        {
            runner.isRunning = false; // Stop athlete at start/finish line
            pointB = runner.transform.position;
        }

        SetLeftDistanceText($"Distance\n{GetRunnerDistance():F1}m");
        SetRightTimeText($"Time\n{demoTime:F1}s");

        // Briefly stop at starting point for 1.5 seconds to observe the completed loop (no displacement vector is drawn)
        yield return new WaitForSeconds(1.5f);

        // Reset meters to ZERO!
        SetRunnerDistance(0f);
        demoTime = 0f;
        SetLeftDistanceText("Distance\n0.0m");
        SetRightTimeText("Time\n0.0s");

        if (athleteBillboard != null)
        {
            athleteBillboard.SetActive(false);
        }
        displacementVectorLine.enabled = false;

        // Prevent the runner from snapping back to the start line when stopped at the 100m mark in Phase 2!
        if (runner != null)
        {
            runner.snapToStartWhenStopped = false;
        }

        // Start running again for Phase 2 (100m Sprint)!
        runPhase = 2;
        isDemoRunning = true;
        
        if (runner != null)
        {
            runner.isRunning = true;
            ForcePlayAnimation anim = runner.GetComponent<ForcePlayAnimation>();
            if (anim != null)
            {
                anim.animationSpeed = 1.6f;
            }
        }

        // Show "DISPLACEMENT EXAMPLE" pop-up in beautiful BROWN color above athlete's head at the start of Phase 2!
        if (athleteBillboard != null)
        {
            athleteBillboard.SetActive(true);
            SetBillboardText("<color=#8B4513><b>DISPLACEMENT EXAMPLE</b></color>");
            StartCoroutine(ClearBillboardAfterDelay(3.5f));
        }
    }

    IEnumerator ClearBillboardAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (runPhase == 2 && !isDemoFinished)
        {
            if (athleteBillboard != null)
            {
                athleteBillboard.SetActive(false);
            }
        }
    }

    void Finish100mSprint()
    {
        isDemoFinished = true;
        isDemoRunning = false;
        
        if (runner != null)
        {
            runner.isRunning = false; // Stop athlete at B (100m along the track!)
            pointB = runner.transform.position;
        }

        // Draw bold straight Displacement Vector Line from Start (Point A) to Finish (Point B - 100m away!)
        displacementVectorLine.enabled = true;
        displacementVectorLine.positionCount = 2;
        displacementVectorLine.SetPosition(0, pointA + Vector3.up * 0.2f);
        displacementVectorLine.SetPosition(1, pointB + Vector3.up * 0.2f);

        float displacementVal = Vector3.Distance(pointA, pointB); // ~100.0m
        
        SetLeftDistanceText($"Distance\n{GetRunnerDistance():F1}m");
        SetRightTimeText($"Time\n{demoTime:F1}s");

        // Show main billboard with final displacement summary above athlete's head in RED color!
        if (athleteBillboard != null)
        {
            athleteBillboard.SetActive(true);
            SetBillboardText("<color=#ff2222><b>Displacement = Final Point - Initial point\n                    = 100 - 0\n                    = 100 meter</b></color>");
        }

        // Wait 7 seconds for the user to observe, then trigger Scene 3 table
        StartCoroutine(TransitionToScene3());
    }

    void SetRunnerDistance(float value)
    {
        System.Reflection.FieldInfo distField = runner.GetType().GetField("distanceTraveled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (distField != null)
        {
            distField.SetValue(runner, value);
        }
    }

    IEnumerator TransitionToScene3()
    {
        yield return new WaitForSeconds(7.0f);
        
        // Hide athlete demo tracking billboards
        if (athleteBillboard != null) athleteBillboard.SetActive(false);
        if (leftDistanceBillboard != null) leftDistanceBillboard.SetActive(false);
        if (rightTimeBillboard != null) rightTimeBillboard.SetActive(false);
        displacementVectorLine.enabled = false;

        // Slide in / Fade in Scene 3 Comparison Table (Covers entire screen)
        scene3Panel.SetActive(true);
        Image s3Bg = scene3Panel.GetComponent<Image>();
        
        float fade = 0f;
        while (fade < 1f)
        {
            fade += Time.deltaTime * 1.5f;
            s3Bg.color = new Color(0f, 0f, 0f, fade); // Cover entire screen with black background
            yield return null;
        }
    }

    void CreateComparisonTableUI(Font tableFont)
    {
        scene3Panel = new GameObject("Scene3_ComparisonTable");
        scene3Panel.transform.SetParent(educationCanvas.transform, false);
        
        // Ensure comparison table covers the ENTIRE SCREEN
        RectTransform s3Rect = scene3Panel.AddComponent<RectTransform>();
        s3Rect.anchorMin = Vector2.zero;
        s3Rect.anchorMax = Vector2.one;
        s3Rect.sizeDelta = Vector2.zero; // Full stretch
        
        Image s3Bg = scene3Panel.AddComponent<Image>();
        s3Bg.color = new Color(0f, 0f, 0f, 1f); // BLACK background!

        // Frame structure / Table Title
        GameObject tableTitle = new GameObject("TableTitle");
        tableTitle.transform.SetParent(scene3Panel.transform, false);
        RectTransform titleRect = tableTitle.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.92f);
        titleRect.anchorMax = new Vector2(0.5f, 0.92f);
        titleRect.sizeDelta = new Vector2(1000f, 80f);
        
        Text titleText = tableTitle.AddComponent<Text>();
        titleText.font = tableFont;
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white; // White text for black background!
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "COMPARISON SUMMARY";

        // Create a central table container covering the screen beautifully
        GameObject tableContainer = new GameObject("TableGrid");
        tableContainer.transform.SetParent(scene3Panel.transform, false);
        RectTransform gridRect = tableContainer.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.05f, 0.05f); // 5% margin from left/bottom
        gridRect.anchorMax = new Vector2(0.95f, 0.85f); // 5% margin from right, 15% from top
        gridRect.sizeDelta = Vector2.zero; // Full stretch

        // Draw Table background & sharp straight borders (using sleek slate grey grid outlines)
        Image gridBg = tableContainer.AddComponent<Image>();
        gridBg.color = new Color(0.35f, 0.35f, 0.38f, 1f); // Slate-grey borders

        // Header Row (Row 0) - Restructured to 2 columns splitting 50/50!
        CreateCell(tableContainer, new Vector2(0f, 5f/6f), new Vector2(0.5f, 1f), "Distance", true, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 5f/6f), new Vector2(1f, 1f), "Displacement", true, tableFont);

        // 1. Definition Row (Row 1)
        CreateCell(tableContainer, new Vector2(0f, 4f/6f), new Vector2(0.5f, 5f/6f), "Definition:\nDistance is the total path covered by an object during motion.", false, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 4f/6f), new Vector2(1f, 5f/6f), "Definition:\nDisplacement is the shortest straight-line distance between the initial position and final position of an object.", false, tableFont);

        // 2. Type Row (Row 2)
        CreateCell(tableContainer, new Vector2(0f, 3f/6f), new Vector2(0.5f, 4f/6f), "Quantity Type:\nScalar quantity\n(only magnitude, no direction)", false, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 3f/6f), new Vector2(1f, 4f/6f), "Quantity Type:\nVector quantity\n(Both Magnitude + Direction)", false, tableFont);

        // 3. Path Relation Row (Row 3)
        CreateCell(tableContainer, new Vector2(0f, 2f/6f), new Vector2(0.5f, 3f/6f), "Path Relation:\nDistance depends on the actual path travelled.", false, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 2f/6f), new Vector2(1f, 3f/6f), "Path Relation:\nMeasures shortest path()", false, tableFont);

        // 4. Value Sign Row (Row 4)
        CreateCell(tableContainer, new Vector2(0f, 1f/6f), new Vector2(0.5f, 2f/6f), "Value Sign:\nAlways positive", false, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 1f/6f), new Vector2(1f, 2f/6f), "Value Sign:\nCan be positive, negative, or zero", false, tableFont);

        // 5. Motion Relation Row (Row 5)
        CreateCell(tableContainer, new Vector2(0f, 0f), new Vector2(0.5f, 1f/6f), "Motion Relation:\nDistance helps to measure speed\n(Speed = Distance / Time)", false, tableFont);
        CreateCell(tableContainer, new Vector2(0.5f, 0f), new Vector2(1f, 1f/6f), "Motion Relation:\nHelps to measure velocity\n(Velocity = Displacement / Time)", false, tableFont);

        scene3Panel.SetActive(false); // Hidden until Scene 3 triggers
    }

    void CreateCell(GameObject parent, Vector2 anchorMin, Vector2 anchorMax, string textValue, bool isHeader, Font cellFont)
    {
        GameObject cell = new GameObject("Cell_" + textValue.Replace("\n", "_").Replace(" ", "_"));
        cell.transform.SetParent(parent.transform, false);
        RectTransform rt = cell.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(2f, 2f); // 2px margin left/bottom
        rt.offsetMax = new Vector2(-2f, -2f); // 2px margin right/top
        
        Image img = cell.AddComponent<Image>();
        // Dark mode colors for high-contrast presentation on black screen background!
        img.color = isHeader ? new Color(0.18f, 0.2f, 0.24f, 1f) : new Color(0.08f, 0.08f, 0.1f, 1f); 

        GameObject cellTextObj = new GameObject("CellText");
        cellTextObj.transform.SetParent(cell.transform, false);
        RectTransform txtRt = cellTextObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = new Vector2(-40f, -20f); // Generous padding to prevent clipping and fit massive text!

        Text txt = cellTextObj.AddComponent<Text>();
        txt.font = cellFont;
        txt.fontSize = isHeader ? 48 : 38; // Highly increased text size!
        txt.fontStyle = isHeader ? FontStyle.Bold : FontStyle.Normal;
        txt.color = Color.white; // High-contrast White Text!
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = textValue;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
    }
}

/// <summary>
/// Simple component that forces a GameObject's rotation to face the main camera.
/// </summary>
public class BillboardLook : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
