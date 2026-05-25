using UnityEngine;

// This script is a passive marker - the actual movement is controlled by CameraSequence.cs
// It keeps track of speed/velocity values for display purposes only.
public class SpeedAndVelocityDemo : MonoBehaviour
{
    [HideInInspector] public float speed = 0f;

    private Vector3 previousPosition;
    private Vector3 currentVelocity;

    void Start()
    {
        previousPosition = transform.position;
    }

    void Update()
    {
        // Track velocity for informational purposes (used by CameraSequence)
        currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
        speed = currentVelocity.magnitude;
        previousPosition = transform.position;
    }
}
