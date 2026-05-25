using UnityEngine;
using System.Collections.Generic;

public class HillyEnvironmentGenerator : MonoBehaviour
{
    [Header("Environment Dimensions")]
    public float terrainWidth = 200f;
    public float terrainLength = 200f;
    public float maxHillHeight = 0f; // Flattens the terrain!
    public float roadWidth = 20f;
    
    [Header("Trees")]
    public GameObject treePrefab;
    public int numberOfTrees = 50;
    public float treeScaleMin = 0.8f;
    public float treeScaleMax = 2.5f;

    [Header("Bicycle Settings")]
    public string bicycleModelName = "Meshy";

    private List<GameObject> generatedObjects = new List<GameObject>();

    [ContextMenu("Generate Environment Now")]
    public void Generate()
    {
        ClearOldEnvironment();
        GenerateTerrain();
        GenerateRoad();
        GenerateRoadMarkings();
        GenerateTrees();
        GenerateGroundText();
        PlaceBicycle();
    }

    void Start()
    {
        if (Application.isPlaying && GameObject.Find("GeneratedTerrain") == null)
        {
            Generate();
        }
    }

    void ClearOldEnvironment()
    {
        foreach(var obj in generatedObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        generatedObjects.Clear();
        
        GameObject oldTerrain = GameObject.Find("GeneratedTerrain");
        if (oldTerrain) DestroyImmediate(oldTerrain);
        GameObject oldRoad = GameObject.Find("GeneratedRoad");
        if (oldRoad) DestroyImmediate(oldRoad);
        GameObject oldMarkings = GameObject.Find("GeneratedRoadMarkings");
        if (oldMarkings) DestroyImmediate(oldMarkings);
        GameObject oldTrees = GameObject.Find("GeneratedTrees");
        if (oldTrees) DestroyImmediate(oldTrees);
        GameObject oldText = GameObject.Find("GeneratedText");
        if (oldText) DestroyImmediate(oldText);
        GameObject oldSigns = GameObject.Find("GeneratedHighwaySigns");
        if (oldSigns) DestroyImmediate(oldSigns);
    }

    void GenerateTerrain()
    {
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = 513; 
        terrainData.size = new Vector3(terrainWidth, maxHillHeight, terrainLength);

        // Fill with 0 for completely flat
        float[,] heights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];
        terrainData.SetHeights(0, 0, heights);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/speed/Speed and Velocity/Scene/Generated"))
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/speed/Speed and Velocity/Scene"))
                {
                    UnityEditor.AssetDatabase.CreateFolder("Assets/speed/Speed and Velocity", "Scene");
                }
                UnityEditor.AssetDatabase.CreateFolder("Assets/speed/Speed and Velocity/Scene", "Generated");
            }
            string assetPath = "Assets/speed/Speed and Velocity/Scene/Generated/GeneratedTerrainData.asset";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null)
            {
                UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            }
            UnityEditor.AssetDatabase.CreateAsset(terrainData, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "GeneratedTerrain";
        terrainGO.transform.position = new Vector3(-terrainWidth / 2f, 0, -terrainLength / 2f);

        Material greenMat = new Material(Shader.Find("Standard"));
        greenMat.color = new Color(0.2f, 0.45f, 0.2f);
        terrainGO.GetComponent<Terrain>().materialTemplate = greenMat;

        generatedObjects.Add(terrainGO);
    }

    void GenerateRoad()
    {
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = "GeneratedRoad";
        road.transform.localScale = new Vector3(roadWidth / 10f, 1f, terrainLength / 10f);
        road.transform.position = new Vector3(0, 0.1f, 0); 
        
        Renderer renderer = road.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material roadMat = new Material(Shader.Find("Standard"));
            roadMat.color = new Color(0.15f, 0.15f, 0.15f); 
            renderer.material = roadMat;
        }

        generatedObjects.Add(road);
    }

    void GenerateRoadMarkings()
    {
        GameObject markingsParent = new GameObject("GeneratedRoadMarkings");
        generatedObjects.Add(markingsParent);

        Material yellowMat = new Material(Shader.Find("Standard"));
        yellowMat.color = new Color(0.9f, 0.8f, 0.1f);
        
        Material blackMat = new Material(Shader.Find("Standard"));
        blackMat.color = new Color(0.1f, 0.1f, 0.1f);
        
        Material whiteMat = new Material(Shader.Find("Standard"));
        whiteMat.color = Color.white;

        float segmentLength = 2f;
        bool isYellow = true;

        for (float z = -terrainLength / 2f; z < terrainLength / 2f; z += segmentLength)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.transform.parent = markingsParent.transform;
            segment.transform.localScale = new Vector3(0.6f, 0.4f, segmentLength);
            segment.transform.position = new Vector3(0, 0.3f, z + segmentLength / 2f); 
            
            segment.GetComponent<Renderer>().material = isYellow ? yellowMat : blackMat;
            isYellow = !isYellow;
        }

        float dashLength = 4f;
        float dashGap = 6f;
        float dashStep = dashLength + dashGap;

        float leftLaneX = -roadWidth / 4f;
        float rightLaneX = roadWidth / 4f;

        for (float z = -terrainLength / 2f; z < terrainLength / 2f; z += dashStep)
        {
            CreateDash(new Vector3(leftLaneX, 0.11f, z + dashLength / 2f), dashLength, whiteMat, markingsParent.transform);
            CreateDash(new Vector3(rightLaneX, 0.11f, z + dashLength / 2f), dashLength, whiteMat, markingsParent.transform);
        }
    }

    void CreateDash(Vector3 position, float length, Material mat, Transform parent)
    {
        GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dash.transform.parent = parent;
        dash.transform.localScale = new Vector3(0.2f, 0.02f, length); 
        dash.transform.position = position;
        dash.GetComponent<Renderer>().material = mat;
    }


    void GenerateTrees()
    {
        GameObject treeParent = new GameObject("GeneratedTrees");
        generatedObjects.Add(treeParent);

        for (int i = 0; i < numberOfTrees; i++)
        {
            float randomX;
            do
            {
                randomX = Random.Range(-terrainWidth / 2f, terrainWidth / 2f);
            } while (Mathf.Abs(randomX) < (roadWidth / 2f + 5f)); // Keep road clear

            float randomZ = Random.Range(-terrainLength / 2f, terrainLength / 2f);
            
            Vector3 position = new Vector3(randomX, 0f, randomZ); // terrain is flat so y=0
            CreateTree(position, treeParent.transform);
        }
    }

    void CreateTree(Vector3 position, Transform parent)
    {
        GameObject prefabToUse = treePrefab;
#if UNITY_EDITOR
        if (prefabToUse == null)
        {
            prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/speed/Speed and Velocity/Scene/Assets/Big Oak Tree FREE/Prefabs/OakBigTree01_pr.prefab");
        }
#endif

        if (prefabToUse != null)
        {
#if UNITY_EDITOR
            GameObject tree = null;
            if (Application.isPlaying) {
                tree = Instantiate(prefabToUse);
            } else {
                tree = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabToUse);
            }
            if (tree == null) tree = Instantiate(prefabToUse);
#else
            GameObject tree = Instantiate(prefabToUse);
#endif
            tree.transform.position = position;
            tree.transform.parent = parent;
            float randomScale = Random.Range(treeScaleMin, treeScaleMax);
            tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
        }
        else
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.position = position;
            tree.transform.parent = parent;
            float randomScale = Random.Range(treeScaleMin, treeScaleMax);
            tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.parent = tree.transform;
            trunk.transform.localPosition = new Vector3(0, 1f, 0); 
            trunk.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.transform.parent = tree.transform;
            leaves.transform.localPosition = new Vector3(0, 2.5f, 0);
            leaves.transform.localScale = new Vector3(1.5f, 3f, 1.5f);
        }
    }

    void GenerateGroundText()
    {
        GameObject textParent = new GameObject("GeneratedText");
        generatedObjects.Add(textParent);

        // SPEED Text (Left)
        CreateGroundText("SPEED", Color.black, new Vector3(-50f, 0.2f, 0f), textParent.transform);
        
        // VELOCITY Text (Right)
        CreateGroundText("VELOCITY", Color.black, new Vector3(50f, 0.2f, 0f), textParent.transform);
    }

    void CreateGroundText(string text, Color color, Vector3 position, Transform parent)
    {
        GameObject textObj = new GameObject(text + " Text");
        textObj.transform.parent = parent;
        textObj.transform.position = position;
        // Rotate to lie flat on the ground and run parallel to the road
        textObj.transform.rotation = Quaternion.Euler(90f, -90f, 0f);
        
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = 4f;
        tm.fontSize = 100;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold; // Make it BOLD and THICK
    }

    void PlaceBicycle()
    {
        GameObject bicycle = GameObject.Find(bicycleModelName);
        if (bicycle == null)
        {
            SpeedAndVelocityDemo demo = Object.FindAnyObjectByType<SpeedAndVelocityDemo>();
            if (demo != null) bicycle = demo.gameObject;
        }
        
        if (bicycle != null)
        {
            // Place bicycle at the START of the 100m track (z = -50)
            bicycle.transform.position = new Vector3(5f, 1f, -50f); 
            bicycle.transform.rotation = Quaternion.Euler(-90f, -90f, 90f);
            
            // The model was originally scaled at 100! Scaling to 2 made it the size of an ant.
            // If we want it "2x" as big, we use 200!
            bicycle.transform.localScale = new Vector3(200f, 200f, 200f);


            // Add attractive "school student" materials (Navy Blue, White, Red, Yellow)
            Color[] schoolColors = new Color[] {
                new Color(0.1f, 0.3f, 0.7f), // Bright Navy Blue
                new Color(0.9f, 0.9f, 0.9f), // Clean White
                new Color(0.8f, 0.1f, 0.1f), // Energetic Red
                new Color(1.0f, 0.8f, 0.1f)  // School Yellow
            };
            int colorIndex = 0;

            foreach (Renderer r in bicycle.GetComponentsInChildren<Renderer>())
            {
                Material[] newMats = new Material[r.sharedMaterials.Length];
                for(int i = 0; i < newMats.Length; i++) 
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = schoolColors[colorIndex % schoolColors.Length];
                    mat.SetFloat("_Glossiness", 0.6f); // Make it shiny and clean
                    newMats[i] = mat;
                    colorIndex++;
                }
                r.materials = newMats;
            }
        }
    }
}
