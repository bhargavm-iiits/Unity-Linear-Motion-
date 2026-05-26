using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.5f, -6.5f);
    public float smoothSpeed = 5f;

    // Cinematic properties
    private bool isCinematic = true;
    private float cinematicDuration = 4f;
    private float cinematicTimer = 0f;
    
    private Vector3 startPos = new Vector3(0, 150f, 0);

    // Title Card Transitions
    private GameObject introTextObj;
    private TextMesh introTextMesh;
    private Vector3 initialTextScale;
    private float textFadeTimer = 0f;
    private float textFadeDuration = 1.2f; // Smooth fade-out duration
    private bool isTextFading = false;

    void Start()
    {
        // Force the camera's culling mask, clear flags, and far clip plane to render EVERYTHING!
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.cullingMask = ~0; // Render everything!
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 1000f; // High far clip plane to guarantee visibility
        }

        // Dynamically recover target transform if it was lost or cleared in the editor
        if (target == null)
        {
            TrackRunner runnerObj = FindAnyObjectByType<TrackRunner>();
            if (runnerObj != null)
            {
                target = runnerObj.transform;
            }
        }

        if (target != null)
        {
            // Initial top-down position
            transform.position = startPos;
            transform.rotation = Quaternion.Euler(90f, 0, 0);
            
            // Stop athlete from running during intro
            TrackRunner runner = target.GetComponent<TrackRunner>();
            if (runner != null)
            {
                runner.isRunning = false;
            }

            // Cache and store intro text configurations for smooth fade
            introTextObj = GameObject.Find("IntroText");
            if (introTextObj != null)
            {
                introTextMesh = introTextObj.GetComponent<TextMesh>();
                initialTextScale = introTextObj.transform.localScale;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredFollowPos = target.position + target.TransformDirection(offset);
        Vector3 desiredLookPos = target.position + Vector3.up * 2f;

        if (isCinematic)
        {
            // Cinematic glide down
            cinematicTimer += Time.deltaTime;
            float t = cinematicTimer / cinematicDuration;
            
            // Ease-in-out smoothing
            t = Mathf.SmoothStep(0, 1, t);

            transform.position = Vector3.Lerp(startPos, desiredFollowPos, t);
            
            Quaternion startRot = Quaternion.Euler(90f, 0, 0);
            Vector3 lookDir = desiredLookPos - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion endRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            }

            // Finish cinematic
            if (t >= 1f)
            {
                isCinematic = false;
                isTextFading = true; // Trigger elegant title card fade-out
                textFadeTimer = 0f;

                TrackRunner runner = target.GetComponent<TrackRunner>();
                if (runner != null)
                {
                    runner.isRunning = true;
                }
            }
        }
        else
        {
            // Standard Follow - Smoothly lerp camera position
            transform.position = Vector3.Lerp(transform.position, desiredFollowPos, smoothSpeed * Time.deltaTime);
            
            // Standard Follow - Smoothly slerp camera rotation (eliminates robotic LookAt snapping during turns!)
            Vector3 lookDir = desiredLookPos - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
            }
        }

        // Handle smooth title card shrink and fade-out
        if (isTextFading && introTextObj != null)
        {
            textFadeTimer += Time.deltaTime;
            float textT = textFadeTimer / textFadeDuration;
            
            if (textT >= 1f)
            {
                introTextObj.SetActive(false);
                isTextFading = false;
            }
            else
            {
                // Smoothly scale down and fade color alpha
                float smoothT = Mathf.SmoothStep(0f, 1f, textT);
                introTextObj.transform.localScale = Vector3.Lerp(initialTextScale, Vector3.zero, smoothT);
                
                if (introTextMesh != null)
                {
                    Color c = introTextMesh.color;
                    c.a = Mathf.Lerp(1f, 0f, smoothT);
                    introTextMesh.color = c;
                }
            }
        }
    }
}
