using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class EyeTrackingManager : MonoBehaviour
{
    private ManifestReceiver manifestReceiver;
    
    // In Unity XR Interaction Toolkit, you typically have an XRBaseInteractor for eye gaze
    // Or you can use specific eye tracking packages.
    
    void Start()
    {
        manifestReceiver = Object.FindFirstObjectByType<ManifestReceiver>();
    }

    // This method is intended to be called by an XR Simple Interactable's OnHoverEntered event 
    // when using an Eye Gaze Interactor (from XR Interaction Toolkit 2.3+).
    public void OnGazeFocusEntered(string objectName)
    {
        if (manifestReceiver != null)
        {
            manifestReceiver.SendTelemetryItem("eye_gaze_enter", $"Student started looking at: {objectName}");
            Debug.Log($"[EyeTrackingManager] Gaze entered: {objectName}");
        }
    }

    public void OnGazeFocusExited(string objectName)
    {
        if (manifestReceiver != null)
        {
            manifestReceiver.SendTelemetryItem("eye_gaze_exit", $"Student stopped looking at: {objectName}");
            Debug.Log($"[EyeTrackingManager] Gaze exited: {objectName}");
        }
    }
}
