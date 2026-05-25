using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupSceneTool
{
    [MenuItem("Tools/Setup Speed Velocity Scene")]
    public static void SetupScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Please exit Play Mode before running the Setup Tool!");
            return;
        }

        // Path to the scene you mentioned
        string scenePath = "Assets/speed/Speed and Velocity/Scene/speed Velocity.unity";
        
        // Open the Scene
        try
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Could not open the scene at " + scenePath + ". Ensure the path is correct. Error: " + e.Message);
            return;
        }

        // 1. Create or Find Environment Generator
        GameObject envObj = GameObject.Find("Hilly Environment");
        if (envObj == null)
        {
            envObj = new GameObject("Hilly Environment");
        }
        HillyEnvironmentGenerator generator = envObj.GetComponent<HillyEnvironmentGenerator>();
        if (generator == null)
        {
            generator = envObj.AddComponent<HillyEnvironmentGenerator>();
        }

        // 2. Add the Bicycle Mesh
        string fbxPath = "Assets/speed/Speed and Velocity/Scene/Assets/Meshy_AI__0523174242_generate.fbx";
        GameObject bicyclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        
        if (bicyclePrefab != null)
        {
            // Check if it already exists in the scene
            bool bikeExists = false;
            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
            {
                if (obj.name.Contains(bicyclePrefab.name) && obj.transform.parent == null)
                {
                    bikeExists = true;
                    // Ensure movement script is attached
                    if (obj.GetComponent<SpeedAndVelocityDemo>() == null)
                    {
                        obj.AddComponent<SpeedAndVelocityDemo>();
                    }
                    break;
                }
            }

            if (!bikeExists)
            {
                GameObject bikeInstance = (GameObject)PrefabUtility.InstantiatePrefab(bicyclePrefab);
                if (bikeInstance != null)
                {
                    if (bikeInstance.GetComponent<SpeedAndVelocityDemo>() == null)
                    {
                        bikeInstance.AddComponent<SpeedAndVelocityDemo>();
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Could not find the FBX at " + fbxPath + " - Did you rename or move it?");
        }

        // 3. Generate the Terrain
        if (generator != null)
        {
            generator.Generate();
        }

        // 4. Setup Camera Sequence
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            if (mainCam.GetComponent<CameraSequence>() == null)
            {
                mainCam.gameObject.AddComponent<CameraSequence>();
            }
        }

        // 5. Save the Scene
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        
        Debug.Log("<color=green>Scene Setup Complete!</color> The scene has been updated and saved automatically.");
    }
}
