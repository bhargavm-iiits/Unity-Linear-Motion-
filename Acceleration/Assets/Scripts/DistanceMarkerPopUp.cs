using UnityEngine;

namespace NCERT.Chapter2.VR
{
    public class DistanceMarkerPopUp : MonoBehaviour
    {
        private Transform carTarget;
        private Vector3 targetScale;
        private bool popped = false;
        private float animationProgress = 0f;

        void Start()
        {
            targetScale = transform.localScale;
            transform.localScale = Vector3.zero; // Start hidden

            FindCar();
        }

        void FindCar()
        {
            GameObject car = GameObject.Find("Midnight_Defender_Vehicle");
            if (car != null)
            {
                carTarget = car.transform;
            }
        }

        void Update()
        {
            if (carTarget == null)
            {
                FindCar();
                return;
            }

            // Trigger pop-up when the car is within 25 meters of the marker
            if (!popped && carTarget.position.z >= transform.position.z - 25f)
            {
                popped = true;
            }

            if (popped && animationProgress < 1f)
            {
                animationProgress += Time.deltaTime * 2.5f; // Scale up in 0.4 seconds
                if (animationProgress > 1f) animationProgress = 1f;

                // Dynamic springy/elastic bounce scale formula:
                float bounce = 1f + 0.15f * Mathf.Sin(animationProgress * Mathf.PI * 2.5f) * (1f - animationProgress);
                transform.localScale = targetScale * (animationProgress * bounce);
            }
            else if (!popped)
            {
                transform.localScale = Vector3.zero;
            }
        }
    }
}
