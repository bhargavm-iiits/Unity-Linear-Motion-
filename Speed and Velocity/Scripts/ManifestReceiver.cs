using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket; // Native websocket package

public class ManifestReceiver : MonoBehaviour
{
    [Header("Server Configuration")]
    public bool offlineMode = false;
    [SerializeField] private string backendBaseUrl = "http://localhost:8000";
    [SerializeField] private string fallbackWsUrl = "ws://localhost:8000/ws/lesson";
    
    [Header("Student Session Info")]
    [SerializeField] private string studentId = "bf57df1b-2d87-46f0-9ec7-9fdd03adc871";
    [SerializeField] private string subjectCode = "physics";
    [SerializeField] private string topicCode = "motion";

    private WebSocket websocket;
    private string webSocketUrl;
    private string sessionId;

    public CameraSequence cameraSequence;

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
        if (cameraSequence == null)
        {
            cameraSequence = FindAnyObjectByType<CameraSequence>();
        }

        if (offlineMode)
        {
            Debug.Log("[ManifestReceiver] OFFLINE MODE - Starting lesson immediately.");
            if (cameraSequence != null)
            {
                cameraSequence.cycleSpeed = 5f; // Force exactly 5 m/s
                cameraSequence.BeginSequenceFromManifest();
            }
            return;
        }

        // Start the connection sequence
        StartCoroutine(DiscoverAndConnect());
    }

    private IEnumerator DiscoverAndConnect()
    {
        Debug.Log("[ManifestReceiver] Fetching WebSocket URL from ws-info...");
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get($"{backendBaseUrl}/ws-info"))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    WsInfoResponse info = JsonUtility.FromJson<WsInfoResponse>(webRequest.downloadHandler.text);
                    webSocketUrl = info.websocket_url;
                    
                    if (webSocketUrl.Contains("0.0.0.0"))
                    {
                        webSocketUrl = webSocketUrl.Replace("0.0.0.0", "localhost");
                    }
                    
                    Debug.Log($"[ManifestReceiver] Discovered WebSocket URL: {webSocketUrl}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ManifestReceiver] Failed to parse URL response: {e.Message}. Using fallback.");
                    webSocketUrl = fallbackWsUrl;
                }
            }
            else
            {
                Debug.LogWarning($"[ManifestReceiver] Server discovery failed: {webRequest.error}. Using fallback.");
                webSocketUrl = fallbackWsUrl;
            }
        }

        ConnectToWebSocket();
    }

    private async void ConnectToWebSocket()
    {
        websocket = new WebSocket(webSocketUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("[ManifestReceiver] WebSocket Connection Open!");
            SendStartLessonMessage();
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError($"[ManifestReceiver] WebSocket Error: {e}");
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("[ManifestReceiver] WebSocket Connection Closed.");
        };

        websocket.OnMessage += (bytes) =>
        {
            string messageText = System.Text.Encoding.UTF8.GetString(bytes);
            HandleIncomingMessage(messageText);
        };

        StartCoroutine(SendTelemetryLoop());
        await websocket.Connect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
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
                    break;
                case "manifest":
                    if (msg.manifest != null)
                    {
                        Debug.Log($"[ManifestReceiver] Manifest applied — distance={msg.manifest.journey_distance}, speed={msg.manifest.cycling_speed}");
                        
                        if (cameraSequence != null)
                        {
                            cameraSequence.cycleSpeed = msg.manifest.cycling_speed;
                            cameraSequence.BeginSequenceFromManifest();
                        }
                    }
                    break;
                case "error":
                    Debug.LogError($"[ManifestReceiver] Backend Error: {msg.message}");
                    break;
                case "done":
                    Debug.Log("[ManifestReceiver] Lesson setup stream complete.");
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

        batch.events.Add(new TelemetryItem
        {
            type = "gaze_focus",
            timestamp = DateTime.UtcNow.ToString("o"),
            detail = "Student is looking at primary learning asset"
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
}
