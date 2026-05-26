using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI; // Added for UI Connection Status Toast!
using UnityEngine.XR; // Added for VR inputs!
using NativeWebSocket; // Native websocket package

namespace NCERT.Introduction
{
    public class ManifestReceiver : MonoBehaviour
    {
    [Header("Server Configuration")]
    public bool offlineMode = false;
    [SerializeField] private string backendBaseUrl = "http://localhost:8088";
    [SerializeField] private string fallbackWsUrl = "ws://localhost:8088/ws/lesson";
    
    [Header("Student Session Info")]
    [SerializeField] private string studentId = "bf57df1b-2d87-46f0-9ec7-9fdd03adc871";
    [SerializeField] private string subjectCode = "physics";
    [SerializeField] private string topicCode = "motion";

    [Header("VR Simulation Controls")]
    public bool isPaused = false;

    private WebSocket websocket;
    private string webSocketUrl;
    private string sessionId;
    private bool hasConnected = false;
    private UnityWebRequest currentWebRequest; // Track active discovery web requests!

    public EducationalSeqManager seqManager;

    // Connection HUD Elements
    private GameObject connectionStatusPanel;
    private Text connectionStatusText;

    // VR button edge-detection states
    private bool wasPrimaryPressed = false;
    private bool wasTriggerPressed = false;

    [System.Serializable]
    public class WsInfoResponse
    {
        public string websocket_url;
        public string note;
    }

    [Serializable]
    public class BackendMessage
    {
        public string @event;
        public string session_id;
        public ManifestData manifest;
        public string message;
    }

    [Serializable]
    public class ManifestData
    {
        public string lesson_title;
        public float journey_distance;
        public float cycling_speed;
    }

    [System.Serializable]
    public class StartLessonEvent
    {
        public string @event = "start_lesson";
        public string student_id;
        public string topic_code;
        public string subject_code;
    }

    [System.Serializable]
    public class TelemetryBatchEvent
    {
        public string @event = "telemetry";
        public string session_id;
        public List<TelemetryItem> events = new List<TelemetryItem>();
    }

    [System.Serializable]
    public class TelemetryItem
    {
        public string type;
        public string timestamp;
        public string detail;
    }

    private void Start()
    {
        // Migrate default port 8000 to 8088 to avoid port collisions with unrelated projects!
        if (backendBaseUrl != null && backendBaseUrl.Contains(":8000"))
        {
            backendBaseUrl = backendBaseUrl.Replace(":8000", ":8088");
        }
        if (fallbackWsUrl != null && fallbackWsUrl.Contains(":8000"))
        {
            fallbackWsUrl = fallbackWsUrl.Replace(":8000", ":8088");
        }

        if (seqManager == null)
        {
            seqManager = FindAnyObjectByType<EducationalSeqManager>();
        }

        CreateConnectionStatusUI();

        if (offlineMode)
        {
            Debug.Log("[ManifestReceiver] OFFLINE MODE - Starting lesson immediately.");
            SetStatusText("Offline Mode: Starting simulation...", new Color(1f, 0.5f, 0f));
            HideConnectionUIWithDelay(2.5f);
            if (seqManager != null)
            {
                if (seqManager.runner != null)
                {
                    seqManager.runner.speed = 7.5f; // Force NCERT athletic speed
                }
                seqManager.BeginSequenceFromManifest();
            }
            return;
        }

        // Start connection timeout timer immediately at Start to cover both discovery and connection phases!
        StartCoroutine(ConnectionTimeoutCoroutine(4.0f));

        // Start the connection sequence
        StartCoroutine(DiscoverAndConnect());
    }

    private IEnumerator DiscoverAndConnect()
    {
        Debug.Log("[ManifestReceiver] Fetching WebSocket URL from ws-info...");
        SetStatusText("Discovering backend server on port 8088...", new Color(1f, 0.75f, 0f));

        currentWebRequest = UnityWebRequest.Get($"{backendBaseUrl}/ws-info");
        currentWebRequest.timeout = 2; // Set 2 seconds maximum timeout to abort quickly when backend is offline!

        yield return currentWebRequest.SendWebRequest();
        
        if (currentWebRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                WsInfoResponse info = JsonUtility.FromJson<WsInfoResponse>(currentWebRequest.downloadHandler.text);
                webSocketUrl = info.websocket_url;
                
                if (webSocketUrl.Contains("0.0.0.0"))
                {
                    webSocketUrl = webSocketUrl.Replace("0.0.0.0", "localhost");
                }
                
                Debug.Log($"[ManifestReceiver] Discovered WebSocket URL: {webSocketUrl}");
                SetStatusText("Server found! Connecting to WebSocket...", new Color(0.2f, 0.8f, 1f));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ManifestReceiver] Failed to parse URL response: {e.Message}. Using fallback.");
                SetStatusText("Error parsing discovery response. Using fallback.", new Color(1f, 0.5f, 0f));
                webSocketUrl = fallbackWsUrl;
            }
        }
        else
        {
            Debug.LogWarning($"[ManifestReceiver] Server discovery failed: {currentWebRequest.error}. Using fallback.");
            SetStatusText($"Discovery failed ({currentWebRequest.error}). Using fallback.", new Color(1f, 0.5f, 0f));
            webSocketUrl = fallbackWsUrl;
        }

        currentWebRequest.Dispose();
        currentWebRequest = null;

        ConnectToWebSocket();
    }

    private async void ConnectToWebSocket()
    {
        SetStatusText($"Connecting to WebSocket: {webSocketUrl}", new Color(0.2f, 0.8f, 1f));
        websocket = new WebSocket(webSocketUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("[ManifestReceiver] WebSocket Connection Open!");
            hasConnected = true;
            SetStatusText("WebSocket Connection Open! Requesting manifest...", new Color(0f, 1f, 0.2f)); // vibrant green
            SendStartLessonMessage();
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError($"[ManifestReceiver] WebSocket Error: {e}");
            SetStatusText($"WebSocket Error: {e}", new Color(1f, 0.1f, 0.1f)); // Red
            TriggerOfflineFallback();
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("[ManifestReceiver] WebSocket Connection Closed.");
            SetStatusText("WebSocket Connection Closed.", new Color(1f, 0.5f, 0f));
            TriggerOfflineFallback();
        };

        websocket.OnMessage += (bytes) =>
        {
            string messageText = System.Text.Encoding.UTF8.GetString(bytes);
            HandleIncomingMessage(messageText);
        };

        StartCoroutine(SendTelemetryLoop());
        
        try
        {
            await websocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ManifestReceiver] websocket.Connect() threw exception: {ex.Message}. Falling back.");
            SetStatusText($"WebSocket connection failed. Falling back...", new Color(1f, 0.5f, 0f));
            TriggerOfflineFallback();
        }
    }

    private IEnumerator ConnectionTimeoutCoroutine(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (!hasConnected)
        {
            Debug.LogWarning("[ManifestReceiver] Connection timeout! Gracefully falling back to local offline mode.");
            TriggerOfflineFallback();
        }
    }

    private async void TriggerOfflineFallback()
    {
        if (hasConnected) return; // Already connected, no fallback needed
        
        SetStatusText("Connection Timeout! Activating Offline Fallback...", new Color(1f, 0.4f, 0.1f)); // Orange
        HideConnectionUIWithDelay(3.5f);

        // Safely abort and dispose the active WebRequest if it's still hanging
        if (currentWebRequest != null)
        {
            try
            {
                currentWebRequest.Abort();
                currentWebRequest.Dispose();
            }
            catch {}
            currentWebRequest = null;
        }

        // Clean up websocket if it's open/connecting to prevent secondary triggers
        if (websocket != null)
        {
            try
            {
                await websocket.Close();
            }
            catch {}
            websocket = null;
        }

        Debug.Log("[ManifestReceiver] Offline Fallback: Starting simulation in offline mode.");
        if (seqManager != null)
        {
            // Disable waitForManifest so simulation starts immediately
            seqManager.waitForManifest = false;
            
            if (seqManager.runner != null)
            {
                seqManager.runner.speed = 7.5f; // Force NCERT athletic speed
            }
            seqManager.BeginSequenceFromManifest();
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif

        // Keyboard inputs for Play/Pause debugging (Space or P)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePlayPause();
        }

        // VR Controller button edge-detection (Primary Button A/X or Trigger)
        bool primaryPressed = GetVRButtonPressed(CommonUsages.primaryButton);
        bool triggerPressed = GetVRButtonPressed(CommonUsages.triggerButton);

        if ((primaryPressed && !wasPrimaryPressed) || (triggerPressed && !wasTriggerPressed))
        {
            TogglePlayPause();
        }

        wasPrimaryPressed = primaryPressed;
        wasTriggerPressed = triggerPressed;
    }

    bool GetVRButtonPressed(InputFeatureUsage<bool> usage)
    {
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        
        bool leftPressed = false;
        bool rightPressed = false;
        
        if (leftHand.isValid) leftHand.TryGetFeatureValue(usage, out leftPressed);
        if (rightHand.isValid) rightHand.TryGetFeatureValue(usage, out rightPressed);
        
        return leftPressed || rightPressed;
    }

    public void TogglePlayPause()
    {
        isPaused = !isPaused;
        Debug.Log($"[ManifestReceiver] VR Simulation Play/Pause Toggled! Current state: Paused={isPaused}");
        
        if (seqManager != null && seqManager.runner != null)
        {
            // Stop/Resume runner movement
            seqManager.runner.isRunning = !isPaused;

            // Stop/Resume runner animation
            ForcePlayAnimation anim = seqManager.runner.GetComponent<ForcePlayAnimation>();
            if (anim != null)
            {
                anim.animationSpeed = isPaused ? 0f : 1.6f;
            }
        }

        SendTelemetryItem("vr_simulation_control", isPaused ? "Student PAUSED the simulation" : "Student RESUMED the simulation");
    }

    private async void SendStartLessonMessage()
    {
        if (websocket.State == WebSocketState.Open)
        {
            StartLessonEvent startEvent = new StartLessonEvent
            {
                student_id = this.studentId,
                topic_code = this.topicCode,
                subject_code = this.subjectCode
            };
            string json = JsonUtility.ToJson(startEvent);
            Debug.Log($"[ManifestReceiver] Sending Start Lesson Request: {json}");
            await websocket.SendText(json);
        }
    }

    private void HandleIncomingMessage(string json)
    {
        try
        {
            BackendMessage msg = JsonUtility.FromJson<BackendMessage>(json);
            if (msg == null) return;
            
            Debug.Log($"[ManifestReceiver] Processing event type: '{msg.@event}'");

            switch (msg.@event)
            {
                case "scene_preload":
                    sessionId = msg.session_id;
                    Debug.Log($"[ManifestReceiver] Preloading scene requested by server.");
                    SetStatusText("Backend connection secured. Preloading session...", new Color(0.2f, 0.8f, 1f));
                    break;
                case "manifest":
                    if (msg.manifest != null)
                    {
                        Debug.Log($"[ManifestReceiver] Manifest applied — distance={msg.manifest.journey_distance}, speed={msg.manifest.cycling_speed}");
                        SetStatusText($"MANIFEST LOADED!\nDistance: {msg.manifest.journey_distance}m | Speed: {msg.manifest.cycling_speed}m/s", new Color(0f, 1f, 0.2f)); // Green
                        
                        if (seqManager != null)
                        {
                            if (seqManager.runner != null)
                            {
                                seqManager.runner.speed = msg.manifest.cycling_speed;
                            }
                            seqManager.BeginSequenceFromManifest();
                        }
                    }
                    break;
                case "error":
                    Debug.LogError($"[ManifestReceiver] Backend Error: {msg.message}");
                    SetStatusText($"Backend Error: {msg.message}", new Color(1f, 0.1f, 0.1f)); // Red
                    break;
                case "done":
                    Debug.Log("[ManifestReceiver] Lesson setup stream complete.");
                    SetStatusText("Setup Stream Complete! Running Lesson Simulation...", new Color(0f, 1f, 0.2f));
                    HideConnectionUIWithDelay(3.0f);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ManifestReceiver] Error processing JSON: {e.Message}\nRaw text: {json}");
        }
    }

    private IEnumerator SendTelemetryLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(2.0f);
            if (websocket != null && websocket.State == WebSocketState.Open && !string.IsNullOrEmpty(sessionId))
            {
                SendTelemetryBatch();
            }
        }
    }

    private async void SendTelemetryBatch()
    {
        TelemetryBatchEvent batch = new TelemetryBatchEvent
        {
            session_id = this.sessionId
        };

        // Gather real-time VR Head Tracking data from main camera
        Vector3 headPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        Quaternion headRot = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        batch.events.Add(new TelemetryItem
        {
            type = "head_tracking",
            timestamp = DateTime.UtcNow.ToString("o"),
            detail = $"Head Position: {headPos.ToString("F2")} | Rotation: {headRot.eulerAngles.ToString("F1")}"
        });

        // Gather real-time VR Eye/Gaze Tracking data using high-performance camera-forward raycast
        string gazeFocusObj = "None (Looking at open sky)";
        if (Camera.main != null)
        {
            Ray gazeRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            if (Physics.Raycast(gazeRay, out RaycastHit hit, 100f))
            {
                gazeFocusObj = hit.collider.gameObject.name;
            }
        }
        batch.events.Add(new TelemetryItem
        {
            type = "eye_gaze_focus",
            timestamp = DateTime.UtcNow.ToString("o"),
            detail = $"Student currently looking at: {gazeFocusObj}"
        });

        string json = JsonUtility.ToJson(batch);
        await websocket.SendText(json);
    }

    public async void SendTelemetryItem(string eventType, string detail)
    {
        if (websocket == null || websocket.State != WebSocketState.Open || string.IsNullOrEmpty(sessionId)) return;
        
        TelemetryBatchEvent batch = new TelemetryBatchEvent
        {
            session_id = this.sessionId
        };
        batch.events.Add(new TelemetryItem
        {
            type = eventType,
            timestamp = DateTime.UtcNow.ToString("o"),
            detail = detail
        });
        
        string json = JsonUtility.ToJson(batch);
        await websocket.SendText(json);
    }

    private async void OnDestroy()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    private void CreateConnectionStatusUI()
    {
        // 1. Find or create Screen Space Canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("ConnectionCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Create status panel in top-center of screen
        connectionStatusPanel = new GameObject("ConnectionStatusPanel");
        connectionStatusPanel.transform.SetParent(canvas.transform, false);
        
        RectTransform rt = connectionStatusPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.95f);
        rt.anchorMax = new Vector2(0.5f, 0.95f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector3(0f, -20f, 0f);
        rt.sizeDelta = new Vector2(700f, 85f);

        Image img = connectionStatusPanel.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f); // Sleek modern glassmorphism
        
        Outline outline = connectionStatusPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.7f, 0.8f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Create text inside status panel
        GameObject textObj = new GameObject("ConnectionStatusText");
        textObj.transform.SetParent(connectionStatusPanel.transform, false);
        
        RectTransform txtRt = textObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = new Vector2(-20f, -10f); // padding

        connectionStatusText = textObj.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Calibri", "Helvetica" }, 30);
        }
        connectionStatusText.font = font;
        connectionStatusText.fontSize = 22;
        connectionStatusText.fontStyle = FontStyle.Bold;
        connectionStatusText.alignment = TextAnchor.MiddleCenter;
        connectionStatusText.color = Color.white;
        connectionStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        connectionStatusText.verticalOverflow = VerticalWrapMode.Overflow;

        // Start with a generic discovery notice
        SetStatusText("Initializing backend connection discovery...", new Color(1f, 0.75f, 0f));
    }

    private void SetStatusText(string message, Color textColor)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
            connectionStatusText.color = textColor;
        }
    }

    private void HideConnectionUIWithDelay(float delay)
    {
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeOutConnectionUI(delay));
        }
    }

    private IEnumerator FadeOutConnectionUI(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (connectionStatusPanel != null && connectionStatusPanel.activeInHierarchy)
        {
            Image img = connectionStatusPanel.GetComponent<Image>();
            Text txt = connectionStatusText;
            float duration = 1.0f;
            float elapsed = 0f;
            Color panelStartColor = img.color;
            Color textStartColor = txt != null ? txt.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (img != null) img.color = new Color(panelStartColor.r, panelStartColor.g, panelStartColor.b, Mathf.Lerp(panelStartColor.a, 0f, t));
                if (txt != null) txt.color = new Color(textStartColor.r, textStartColor.g, textStartColor.b, Mathf.Lerp(textStartColor.a, 0f, t));
                yield return null;
            }
            if (connectionStatusPanel != null) connectionStatusPanel.SetActive(false);
        }
    }
}
}
