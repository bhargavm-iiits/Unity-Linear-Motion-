using UnityEngine;
using UnityEngine.UI;

namespace NCERT.Chapter2.VR
{
    public class TitlePopUpFade : MonoBehaviour
    {
        private Text textComponent;
        private Outline outlineComponent;
        private float timeActive = 0f;

        void Start()
        {
            textComponent = GetComponent<Text>();
            outlineComponent = GetComponent<Outline>();

            // Start microscopic for an explosive pop-in punch
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            timeActive += Time.deltaTime;

            // 1. Springy pop-in scale (0.0s to 0.4s)
            if (timeActive < 0.4f)
            {
                float t = timeActive / 0.4f;
                float s = Mathf.Lerp(0f, 1f, t) + 0.15f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = new Vector3(s, s, s);
            }
            else if (timeActive < 1.8f)
            {
                // Solid holding phase
                transform.localScale = Vector3.one;
            }
            // 2. Smooth fade-out (1.8s to 2.8s)
            else if (timeActive >= 1.8f && timeActive < 2.8f)
            {
                float t = (timeActive - 1.8f) / 1.0f; // Progress from 0 to 1
                float alpha = 1f - t;

                if (textComponent != null)
                {
                    Color c = textComponent.color;
                    c.a = alpha;
                    textComponent.color = c;
                }

                if (outlineComponent != null)
                {
                    Color c = outlineComponent.effectColor;
                    c.a = alpha;
                    outlineComponent.effectColor = c;
                }
            }
            // 3. Auto-destroy Canvas to clean up the hierarchy
            else if (timeActive >= 3.0f)
            {
                Destroy(transform.parent.gameObject);
            }
        }
    }
}
