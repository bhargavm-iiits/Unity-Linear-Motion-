using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>
/// Drives animation playback for both Legacy and Humanoid/Generic rigs using the Unity Playables API.
/// This bypasses the need for an Animator Controller asset on disk and ensures that skinned meshes
/// (with humanoid or generic rigs) deform and animate hands and legs correctly in all Unity versions.
/// </summary>
public class ForcePlayAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    public AnimationClip clip;
    
    [Range(0.1f, 5f)]
    [Tooltip("Multiplier for the animation playback speed to match the character's movement speed.")]
    public float animationSpeed = 1.8f; // Sped up to match standard athletic sprint

    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;
    private bool isGraphValid = false;

    void Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
            Debug.LogWarning($"[ForcePlayAnimation] Animator was missing on '{gameObject.name}' at runtime. Programmatically added.");
        }

        if (animator != null && clip != null)
        {
            // Ensure animator is enabled
            animator.enabled = true;
            animator.applyRootMotion = false;

            // Force the clip to loop
            clip.wrapMode = WrapMode.Loop;

            // Create a PlayableGraph for running the animation
            graph = PlayableGraph.Create("ForcePlayAnimationGraph_" + gameObject.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // Create an AnimationPlayableOutput that targets our Animator
            var playableOutput = AnimationPlayableOutput.Create(graph, "AnimationPlayableOutput", animator);

            // Create an AnimationClipPlayable for our specific animation clip
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            
            // Set the animation playback speed
            clipPlayable.SetSpeed(animationSpeed);
            
            // Enable foot IK so the running posture looks natural
            clipPlayable.SetApplyFootIK(true);

            // Connect the Playable to the Output
            playableOutput.SetSourcePlayable(clipPlayable);

            // Start playing the graph
            graph.Play();
            isGraphValid = true;

            Debug.Log($"[ForcePlayAnimation] PlayableGraph initialized. Clip: '{clip.name}' on '{gameObject.name}' with Speed: {animationSpeed}.");
        }
        else
        {
            Debug.LogError($"[ForcePlayAnimation] Failed to initialize. Animator exists: {animator != null}, Clip exists: {clip != null}");
        }
    }

    void Update()
    {
        if (isGraphValid && graph.IsValid() && clip != null)
        {
            // Update speed dynamically in case it is changed in inspector during play mode
            if (clipPlayable.IsValid())
            {
                clipPlayable.SetSpeed(animationSpeed);
            }

            // Guarantee loop fallback regardless of FBX import settings
            double currentTime = clipPlayable.GetTime();
            if (currentTime >= clip.length)
            {
                clipPlayable.SetTime(0.0);
            }
        }
    }

    void OnDestroy()
    {
        // Properly destroy and clean up the PlayableGraph to avoid memory leaks in the editor and build
        if (isGraphValid && graph.IsValid())
        {
            graph.Destroy();
        }
    }

    void OnDisable()
    {
        // Stop playing if disabled
        if (isGraphValid && graph.IsValid())
        {
            graph.Stop();
        }
    }

    void OnEnable()
    {
        // Resume playing if enabled
        if (isGraphValid && graph.IsValid())
        {
            graph.Play();
        }
    }
}
