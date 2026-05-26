using UnityEngine;

/// <summary>
/// Moves the athlete along the oval track path.
/// Runs in LateUpdate so it always overrides Animator root motion
/// (Animator must have applyRootMotion = false for correct behaviour).
/// rotationOffsetY: set to 0 if the FBX faces +Z natively, 180 if it faces -Z.
/// </summary>
public class TrackRunner : MonoBehaviour
{
    [Header("Track Dimensions")]
    public float straightLength = 100f;
    public float radius = 36.5f; // Standard track radius
    public float speed = 5f;

    [Header("Visualization")]
    public LineRenderer displacementLine;

    [Header("Visual Settings")]
    [Tooltip("0 = FBX forward is +Z (faces motion direction). 180 = FBX forward is -Z (flip needed).")]
    public float rotationOffsetY = 0f; // Changed from 180 – set to 180 if athlete still moves backwards

    [Header("Transitions & Smoothing")]
    [Tooltip("How fast the athlete accelerates to full speed. Higher values mean quicker acceleration.")]
    public float accelerationRate = 4f; // Graceful push-off acceleration
    
    [Tooltip("How smoothly the athlete turns around corners. Lower values are smoother.")]
    public float turnSmoothness = 8f; // Organic slerp turning speed

    private float distanceTraveled = 0f;
    private Vector3 startPosition;
    private float trackPerimeter;
    private float currentSpeed = 0f; // Ramps up smoothly when running starts

    public bool isRunning = true;
    public bool snapToStartWhenStopped = true; // Control if stopped athlete snaps back to startPosition

    void Start()
    {
        trackPerimeter = 2f * straightLength + 2f * Mathf.PI * radius;
        startPosition = GetPositionOnTrack(0f);
        transform.position = startPosition;
        currentSpeed = 0f;

        // Set initial rotation facing forward along the track
        Vector3 nextPos = GetPositionOnTrack(0.1f);
        transform.rotation = Quaternion.LookRotation(nextPos - startPosition) * Quaternion.Euler(0, rotationOffsetY, 0);

        if (displacementLine == null)
        {
            displacementLine = gameObject.AddComponent<LineRenderer>();
            displacementLine.startWidth = 0.5f;
            displacementLine.endWidth = 0.5f;
            displacementLine.material = new Material(Shader.Find("Sprites/Default"));
            displacementLine.startColor = Color.red;
            displacementLine.endColor = Color.red;
        }

        // Hide displacement line as requested
        displacementLine.enabled = false;
    }

    void LateUpdate()
    {
        if (!isRunning)
        {
            if (snapToStartWhenStopped)
            {
                // Force athlete to face along the track direction and stay at startPosition during cinematic,
                // preventing the animation sampler from turning him towards the center!
                Vector3 nextPos = GetPositionOnTrack(0.1f);
                transform.rotation = Quaternion.LookRotation(nextPos - startPosition) * Quaternion.Euler(0, rotationOffsetY, 0);
                transform.position = startPosition;
            }
            currentSpeed = 0f; // Reset speed
            return;
        }

        // Smooth speed acceleration ramp-up
        currentSpeed = Mathf.MoveTowards(currentSpeed, speed, accelerationRate * Time.deltaTime);
        distanceTraveled += currentSpeed * Time.deltaTime;
        
        float currentPosInCycle = distanceTraveled % trackPerimeter;
        Vector3 newPos = GetPositionOnTrack(currentPosInCycle);
        
        // Look smoothly in direction of movement (using Quaternion.Slerp to prevent curve entry/exit snapping)
        if ((newPos - transform.position).sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(newPos - transform.position) * Quaternion.Euler(0, rotationOffsetY, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSmoothness * Time.deltaTime);
        }
        
        transform.position = newPos;

        // Update displacement line (kept updated in case of other components using it, though disabled/invisible)
        if (displacementLine != null)
        {
            displacementLine.SetPosition(0, startPosition);
            displacementLine.SetPosition(1, transform.position);
        }
    }

    Vector3 GetPositionOnTrack(float dist)
    {
        float l = straightLength;
        float r = radius;
        float piR = Mathf.PI * r;

        // Start at bottom-left of the bottom straight
        if (dist <= l)
        {
            // Bottom straight
            float t = dist / l;
            return new Vector3(Mathf.Lerp(-l / 2f, l / 2f, t), 0, -r);
        }
        dist -= l;

        if (dist <= piR)
        {
            // Right semicircle
            float t = dist / piR;
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            return new Vector3(l / 2f + r * Mathf.Cos(angle), 0, r * Mathf.Sin(angle));
        }
        dist -= piR;

        if (dist <= l)
        {
            // Top straight
            float t = dist / l;
            return new Vector3(Mathf.Lerp(l / 2f, -l / 2f, t), 0, r);
        }
        dist -= l;

        // Left semicircle
        float t2 = dist / piR;
        float angle2 = Mathf.Lerp(Mathf.PI / 2f, 3f * Mathf.PI / 2f, t2);
        return new Vector3(-l / 2f + r * Mathf.Cos(angle2), 0, r * Mathf.Sin(angle2));
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;

        float displacement = Vector3.Distance(startPosition, transform.position);

        GUI.Label(new Rect(20, 20, 400, 40), $"Distance (Scalar): {distanceTraveled:F2} m", style);
        GUI.Label(new Rect(20, 60, 400, 40), $"Displacement (Vector Mag): {displacement:F2} m", style);
    }
}
