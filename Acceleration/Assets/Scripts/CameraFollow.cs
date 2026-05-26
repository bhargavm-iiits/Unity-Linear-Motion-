using UnityEngine;

namespace NCERT.Chapter2.VR
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 1.6f, -5.5f);

        void LateUpdate()
        {
            if (target == null) return;

            // Instantly keep camera 12 meters behind the car, centered on the road
            transform.position = new Vector3(0f, target.position.y + offset.y, target.position.z + offset.z);
            
            // Beautiful downward tilt framing the car and the highway track perfectly
            transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        }
    }
}
