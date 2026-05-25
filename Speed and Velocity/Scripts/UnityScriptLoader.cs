using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UnityScriptLoader : MonoBehaviour
{
    [SerializeField] private string feedbackUrl = "http://localhost:8000/vr/script-feedback";
    
    // Call this if Unity compilation fails to send errors back to the self-healing API
    public void ReportCompilationError(string scriptName, string compilerErrorMessage)
    {
        StartCoroutine(SendErrorFeedback(scriptName, compilerErrorMessage));
    }
    
    private IEnumerator SendErrorFeedback(string scriptName, string errorMessage)
    {
        // Pydantic matching model
        string json = $"{{\"script_name\":\"{scriptName}\", \"error_message\":\"{errorMessage.Replace("\"", "\\\"")}\"}}";
        
        using (UnityWebRequest request = new UnityWebRequest(feedbackUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ScriptLoader] Error feedback sent. Patched script received: {request.downloadHandler.text}");
                // TODO: Rewrite the local script file with the patched C# code and trigger compilation again
            }
            else
            {
                Debug.LogError($"[ScriptLoader] Failed to send compilation error to backend: {request.error}");
            }
        }
    }
}
