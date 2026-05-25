using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraSequence : MonoBehaviour
{
    public Transform targetCycle;
    
    [Header("Sequence Settings")]
    public float cycleSpeed = 5f; // 200 meters in 40 seconds = 5 m/s
    public float timeBetweenPopups = 7.5f; // Spaced out across 40 seconds

    // Track bounds: 200m from z=-100 to z=+100
    private float trackStartZ = -100f;
    private float trackEndZ   =  100f;

    private float elapsedTime   = 0f;
    private float distanceTravelled = 0f;
    private bool  isMoving      = false;
    
    private GameObject canvasObj;
    private Text popupText;
    private GameObject panelObj;

    // UI Panel for the points list
    private GameObject pointsContainer;

    // 3D HUD elements (attached left and right of cycle, low on the road)
    private TextMesh hudDistText3D;
    private TextMesh hudTimeText3D;

    // Camera states
    private bool isCinematicIntro = true;
    private Vector3 currentCamPos = new Vector3(0, 200f, 0);
    private Quaternion currentCamRot = Quaternion.Euler(90f, 0, 0);

    private string[] speedPoints = {
        "1) Speed is distance per unit time",
        "2) Speed = Distance / Time",
        "3) Scalar quantity (magnitude only)",
        "4) Always positive",
        "5) SI unit: m/s"
    };

    private string[] velocityPoints = {
        "1) Velocity is displacement per unit time",
        "2) Velocity = Displacement / Time",
        "3) Vector quantity (magnitude + direction)",
        "4) Can be positive, negative, or zero",
        "5) SI unit: m/s"
    };

    void Start()
    {
        // timeBetweenPopups can remain locally defined or overridden
        timeBetweenPopups = 7.5f;

        // Auto-find bicycle
        if (targetCycle == null)
        {
            foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
            {
                if (obj.name.Contains("Meshy") && obj.transform.parent == null)
                {
                    targetCycle = obj.transform;
                    break;
                }
            }
        }

        SetupUI();
        Setup3DHUD();
        
        // Sequence is now started EXTERNALLY by ManifestReceiver.
        // If ManifestReceiver is not in the scene, run standalone!
        ManifestReceiver receiver = FindAnyObjectByType<ManifestReceiver>();
        if (receiver == null)
        {
            Debug.Log("[CameraSequence] Auto-attaching ManifestReceiver to the scene!");
            receiver = gameObject.AddComponent<ManifestReceiver>();
        }
    }

    public void BeginSequenceFromManifest()
    {
        StartCoroutine(RunSequence());
    }

    void LateUpdate()
    {
        if (targetCycle != null)
        {
            if (isCinematicIntro)
            {
                transform.position = currentCamPos;
                transform.rotation = currentCamRot;
            }
            else
            {
                // World-space chase camera: 2m up, 3m behind (much closer)
                Vector3 targetPos = targetCycle.position + new Vector3(0, 2f, -3.5f);
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
                Vector3 lookTarget = targetCycle.position + new Vector3(0, 1.5f, 2f);
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.LookRotation(lookTarget - transform.position),
                    Time.deltaTime * 5f);
            }

            // Update 3D HUD floating on the road next to the cycle
            if (isMoving)
            {
                elapsedTime += Time.deltaTime;
                distanceTravelled = Mathf.Clamp(targetCycle.position.z - trackStartZ, 0f, 200f);

                if (hudDistText3D != null)
                {
                    hudDistText3D.text = $"DISTANCE\n{distanceTravelled:F1} m";
                    hudDistText3D.transform.position = targetCycle.position + new Vector3(-2f, 0.2f, 0f);
                    // Rotate to face camera
                    hudDistText3D.transform.rotation = Quaternion.LookRotation(hudDistText3D.transform.position - transform.position);
                }

                if (hudTimeText3D != null)
                {
                    hudTimeText3D.text = $"TIME\n{elapsedTime:F1} s";
                    hudTimeText3D.transform.position = targetCycle.position + new Vector3(2f, 0.2f, 0f);
                    // Rotate to face camera
                    hudTimeText3D.transform.rotation = Quaternion.LookRotation(hudTimeText3D.transform.position - transform.position);
                }
            }
        }
    }

    // ─── UI Setup ───────────────────────────────────────────────────────────

    void SetupUI()
    {
        canvasObj = new GameObject("PopupCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        panelObj = new GameObject("PopupPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0.15f, 0.95f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f); // Cover complete screen for final popup
        panelRect.pivot     = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("PopupText");
        textObj.transform.SetParent(panelObj.transform, false);
        popupText = textObj.AddComponent<Text>();
        popupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        popupText.fontSize = 28;
        popupText.color = Color.white;
        popupText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = popupText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40, 40);
        textRect.offsetMax = new Vector2(-40, -40);

        panelObj.SetActive(false);

        // Container for mid-travel points, placed in the top-center (above the cyclist)
        pointsContainer = new GameObject("PointsContainer");
        pointsContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform pcRect = pointsContainer.AddComponent<RectTransform>();
        pcRect.anchorMin = new Vector2(0.1f, 0.5f);
        pcRect.anchorMax = new Vector2(0.9f, 0.9f);
        pcRect.offsetMin = Vector2.zero;
        pcRect.offsetMax = Vector2.zero;
    }

    void Setup3DHUD()
    {
        hudDistText3D = Create3DTextTemplate("DistanceHUD", Color.white);
        hudTimeText3D = Create3DTextTemplate("TimeHUD", Color.white);
        
        hudDistText3D.gameObject.SetActive(false);
        hudTimeText3D.gameObject.SetActive(false);
    }

    TextMesh Create3DTextTemplate(string name, Color col)
    {
        GameObject textObj = new GameObject(name);
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.characterSize = 0.015f; // Extremely reduced size so it perfectly fits the road
        tm.fontSize = 80;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = col;
        tm.fontStyle = FontStyle.Bold;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = tm.font.material;
        return tm;
    }

    // ─── Sequence ────────────────────────────────────────────────────────────

    IEnumerator RunSequence()
    {
        if (targetCycle == null)
        {
            Debug.LogError("[CameraSequence] Critical Error: 'targetCycle' is null! The bicycle (Meshy) was not found in the scene.");
            yield break;
        }

        // ── INTRO: TOP DOWN CAMERA ──
        isCinematicIntro = true;
        Vector3 topPos = new Vector3(0, 200f, 0); 
        Quaternion topRot = Quaternion.Euler(90f, 0, 0);
        currentCamPos = topPos;
        currentCamRot = topRot;

        // Optimization: Render full depth for the 200m high camera
        if (Camera.main != null) Camera.main.farClipPlane = 350f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.5f, 0.7f, 0.9f); // Sky blue
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 350f;

        // Hold Top Down for 2 seconds (reduced from 3)
        yield return new WaitForSeconds(2f);

        // Reset the bike to the starting line BEFORE swooping the camera down, 
        // to prevent the camera from jerking to a new position later.
        ResetBikeToStart(rightSide: true);

        // Swoop down to chase position over 2 seconds (reduced from 3)
        float swoopDuration = 2.0f;
        float elapsed = 0f;
        while (elapsed < swoopDuration)
        {
            float t = elapsed / swoopDuration;
            t = t * t * (3f - 2f * t);

            Vector3 chasePos = targetCycle.position + new Vector3(0, 2f, -3.5f);
            Vector3 lookTarget = targetCycle.position + new Vector3(0, 1.5f, 2f);
            Quaternion chaseRot = Quaternion.LookRotation(lookTarget - chasePos);

            currentCamPos = Vector3.Lerp(topPos, chasePos, t);
            currentCamRot = Quaternion.Lerp(topRot, chaseRot, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        GameObject groundText = GameObject.Find("GeneratedText");
        if (groundText != null) groundText.SetActive(false);

        isCinematicIntro = false;

        // Optimization: Reduce far clip plane drastically so it ONLY renders what is right in front of the camera!
        if (Camera.main != null) Camera.main.farClipPlane = 80f;
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 80f;
        
        // Show giant SPEED title popup before traveling
        yield return StartCoroutine(ShowTitlePopup("SPEED", new Color(1f, 0.9f, 0f)));

        // ── PHASE 1: SPEED (Right Side) ──
        elapsedTime = 0f; distanceTravelled = 0f;
        
        hudDistText3D.gameObject.SetActive(true);
        hudTimeText3D.gameObject.SetActive(true);
        isMoving = true;

        // Start popping up UI points while travelling
        StartCoroutine(SpawnUIPoints(speedPoints));

        // Drive 200m
        while (targetCycle.position.z < trackEndZ)
        {
            targetCycle.position += Vector3.forward * cycleSpeed * Time.deltaTime;
            yield return null;
        }
        isMoving = false;
        targetCycle.position = new Vector3(targetCycle.position.x, targetCycle.position.y, trackEndZ);

        yield return new WaitForSeconds(2f);
        ClearUIPoints();

        // ── PHASE 2: VELOCITY (Left Side) ──
        ResetBikeToStart(rightSide: false);
        
        // Show giant VELOCITY title popup before traveling
        yield return StartCoroutine(ShowTitlePopup("VELOCITY", new Color(0f, 1f, 1f)));

        elapsedTime = 0f; distanceTravelled = 0f;
        hudDistText3D.text = "DISTANCE\n0.0 m";
        hudTimeText3D.text = "TIME\n0.0 s";

        hudDistText3D.gameObject.SetActive(true);
        hudTimeText3D.gameObject.SetActive(true);
        isMoving = true;

        StartCoroutine(SpawnUIPoints(velocityPoints));

        // Drive 200m
        while (targetCycle.position.z < trackEndZ)
        {
            targetCycle.position += Vector3.forward * cycleSpeed * Time.deltaTime;
            yield return null;
        }
        isMoving = false;
        targetCycle.position = new Vector3(targetCycle.position.x, targetCycle.position.y, trackEndZ);

        hudDistText3D.gameObject.SetActive(false);
        hudTimeText3D.gameObject.SetActive(false);

        yield return new WaitForSeconds(3f);
        ClearUIPoints();

        // ── FINAL COMPARISON ──
        // Put bicycle exactly on the divider
        targetCycle.position = new Vector3(0f, targetCycle.position.y, trackEndZ);
        ShowFinalComparison();
    }

    IEnumerator SpawnUIPoints(string[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            yield return new WaitForSeconds(timeBetweenPopups);
            CreateUIPoint(points[i], i);
        }
    }

    void CreateUIPoint(string text, int index)
    {
        GameObject txtObj = new GameObject("UIPoint_" + index);
        txtObj.transform.SetParent(pointsContainer.transform, false);
        
        Text t = txtObj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = 65; // Extremely huge text size for screen scaling
        t.color = Color.black;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        // Outline to ensure it's completely visible against bright backgrounds
        Outline outline = txtObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform r = txtObj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1);
        r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.sizeDelta = new Vector2(0, 100); // Increased height to hold the larger font
        // Stack them down from the top with increased spacing
        r.anchoredPosition = new Vector2(0, -20 - (index * 110));

        // Smooth pop up scale
        StartCoroutine(SmoothPopUpUI(r));
    }

    IEnumerator SmoothPopUpUI(RectTransform r)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        r.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            float scale = Mathf.Lerp(0f, 1f, elapsed / duration);
            r.localScale = new Vector3(scale, scale, scale);
            elapsed += Time.deltaTime;
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    IEnumerator ShowTitlePopup(string title, Color col)
    {
        GameObject titleObj = new GameObject("TitlePopup");
        titleObj.transform.SetParent(canvasObj.transform, false);
        
        Text t = titleObj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = title;
        t.fontSize = 200;
        t.color = col;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = titleObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -5f);

        RectTransform r = titleObj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;

        // Smooth pop up scale
        float duration = 0.3f;
        float elapsed = 0f;
        r.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            float scale = Mathf.Lerp(0f, 1f, elapsed / duration);
            r.localScale = new Vector3(scale, scale, scale);
            elapsed += Time.deltaTime;
            yield return null;
        }
        r.localScale = Vector3.one;

        // Display for 2.5 seconds
        yield return new WaitForSeconds(2.5f);

        Destroy(titleObj);
    }

    void ClearUIPoints()
    {
        foreach (Transform child in pointsContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }

    void ResetBikeToStart(bool rightSide)
    {
        if (targetCycle != null)
        {
            float xPos = rightSide ? 5f : -5f; 
            targetCycle.position = new Vector3(xPos, targetCycle.position.y, trackStartZ);
        }
    }

    void ShowFinalComparison()
    {
        popupText.fontSize = 32;
        popupText.text =
            "<b>SPEED</b>                                                                      <b>VELOCITY</b>\n" +
            "──────────────────────────────────────────────────────────────────────────\n" +
            "1) The distance travelled by a moving body     1) The distance travelled by a moving body in\n" +
            "     per unit time is called its speed.                    particular direction per unit time is called its velocity.\n\n" +
            "2) It is a scalar quantity.                                       2) It is a vector quantity.\n\n" +
            "3) It cannot be zero.                                             3) It can be zero.\n\n" +
            "4) The speed is always positive.                         4) The velocity can be both positive and negative.\n\n" +
            "5) Speed = Distance / Time taken                      5) Velocity = Displacement / Time interval";

        panelObj.SetActive(true);
    }
}
