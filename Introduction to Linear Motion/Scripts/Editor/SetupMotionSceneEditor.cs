using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupMotionSceneEditor : EditorWindow
{
    [MenuItem("Tools/Setup Motion Scene")]
    public static void SetupScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Please exit Play Mode before running the Setup Motion Scene tool!");
            return;
        }

        string scenePath = "Assets/Introduction/Scene/Introduction.unity";
        
        // Open the scene
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        // Setup Skybox
        SetupSkybox();
        
        // Create Track
        CreateTrack();

        // Create Intro Text
        CreateIntroText();

        // Setup Athlete/Runner
        GameObject athlete = SetupAthlete();

        // Setup Camera
        SetupCamera(athlete);

        // Cleanup old Educational Sequence Manager if exists
        GameObject oldSeq = GameObject.Find("EducationalSequenceManager");
        if (oldSeq != null) DestroyImmediate(oldSeq);

        // Instantiate and configure new Educational Sequence Manager and hook up components
        GameObject seqManagerObj = new GameObject("EducationalSequenceManager");
        EducationalSeqManager seqManager = seqManagerObj.AddComponent<EducationalSeqManager>();
        
        // Add the ManifestReceiver to establish backend WebSocket/REST connection programmatically!
        NCERT.Introduction.ManifestReceiver receiver = seqManagerObj.AddComponent<NCERT.Introduction.ManifestReceiver>();
        receiver.seqManager = seqManager;
        receiver.offlineMode = false; // Enabled by default to connect to the Python backend!
        
        if (athlete != null)
        {
            seqManager.runner = athlete.GetComponent<TrackRunner>();
        }
        
        Camera cam = Camera.main;
        if (cam != null)
        {
            seqManager.cameraFollow = cam.GetComponent<CameraFollow>();
        }

        // Clean all missing scripts in the active scene to completely resolve the "referenced script is missing" warnings!
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            CleanMissingScripts(rootObj);
        }

        // Clean up all other unwanted GameObjects in the active scene to only render the stadium and athlete
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            if (rootObj == null) continue;
            if (rootObj.name == "Main Camera" || 
                rootObj.name == "Directional Light" || 
                rootObj.name == "EducationalSequenceManager" || 
                rootObj.name == "Athlete_Runner" || 
                rootObj.name == "AthleteTrack" || 
                rootObj.name == "IntroText")
            {
                continue;
            }
            DestroyImmediate(rootObj);
        }

        // Mark scene as dirty, save it programmatically to disk, and save asset database
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Motion in Straight Line Scene setup complete!");
    }

    private static void SetupSkybox()
    {
        string texPath = "Assets/Introduction/ASSETS/Skybox AI Asset Sample Pack/Partly Cloudy Open Sky/M3_Sky_Dome_equirectangular-jpg_clear_blue_sky_bright_1478788763_455173.jpg";
        Texture2D skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        
        if (skyTex != null)
        {
            string matPath = "Assets/Introduction/ASSETS/MotionSkybox.mat";
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (skyMat == null)
            {
                skyMat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(skyMat, matPath);
            }
            
            skyMat.SetTexture("_MainTex", skyTex);
            RenderSettings.skybox = skyMat;
            Debug.Log("Skybox setup successfully.");
        }
        else
        {
            Debug.LogWarning("Skybox texture not found at: " + texPath);
        }
    }

    private static void CreateTrack()
    {
        // Cleanup old track if exists
        GameObject oldTrack = GameObject.Find("AthleteTrack");
        if (oldTrack != null) DestroyImmediate(oldTrack);

        // Group root
        GameObject trackRoot = new GameObject("AthleteTrack");

        // Materials
        Material redMat = new Material(Shader.Find("Standard"));
        redMat.color = new Color(0.8f, 0.2f, 0.2f);
        redMat.SetFloat("_Glossiness", 0f); // Matte, not shiny
        redMat.SetFloat("_Metallic", 0f);
        
        Material greenMat = new Material(Shader.Find("Standard"));
        greenMat.color = new Color(0.2f, 0.6f, 0.2f);
        greenMat.SetFloat("_Glossiness", 0f);
        greenMat.SetFloat("_Metallic", 0f);

        Material darkGreenMat = new Material(Shader.Find("Standard"));
        darkGreenMat.color = new Color(0.15f, 0.5f, 0.15f);
        darkGreenMat.SetFloat("_Glossiness", 0f);
        darkGreenMat.SetFloat("_Metallic", 0f);

        // Dimensions
        float straightLength = 100f;
        float outerRadius = 36.5f;
        float innerRadius = 26.5f;

        // 1. Solid Red Track Base
        GameObject redCenter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        redCenter.name = "RedStraight";
        redCenter.transform.SetParent(trackRoot.transform);
        redCenter.transform.position = Vector3.zero;
        redCenter.transform.localScale = new Vector3(straightLength, 0.01f, outerRadius * 2f);
        redCenter.GetComponent<Renderer>().material = redMat;
        DestroyImmediate(redCenter.GetComponent<Collider>()); // Remove collider to prevent athlete sticking

        GameObject redLeft = CreateSmoothCylinder("RedLeftCurve", outerRadius, 0.01f, redMat, trackRoot.transform);
        redLeft.transform.position = new Vector3(-straightLength / 2f, 0.001f, 0f);

        GameObject redRight = CreateSmoothCylinder("RedRightCurve", outerRadius, 0.01f, redMat, trackRoot.transform);
        redRight.transform.position = new Vector3(straightLength / 2f, 0.001f, 0f);

        // 2. Green Inner Field
        GameObject greenCenter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        greenCenter.name = "GreenStraight";
        greenCenter.transform.SetParent(trackRoot.transform);
        greenCenter.transform.position = new Vector3(0, 0.02f, 0);
        greenCenter.transform.localScale = new Vector3(straightLength, 0.01f, innerRadius * 2f);
        greenCenter.GetComponent<Renderer>().material = greenMat;
        DestroyImmediate(greenCenter.GetComponent<Collider>()); // Remove collider

        GameObject greenLeft = CreateSmoothCylinder("GreenLeftCurve", innerRadius, 0.01f, greenMat, trackRoot.transform);
        greenLeft.transform.position = new Vector3(-straightLength / 2f, 0.021f, 0f);

        GameObject greenRight = CreateSmoothCylinder("GreenRightCurve", innerRadius, 0.01f, greenMat, trackRoot.transform);
        greenRight.transform.position = new Vector3(straightLength / 2f, 0.021f, 0f);

        // 3. Surrounding grass (No collider)
        GameObject grassBg = GameObject.CreatePrimitive(PrimitiveType.Plane);
        grassBg.name = "SurroundingGrass";
        grassBg.transform.SetParent(trackRoot.transform);
        grassBg.transform.position = new Vector3(0, -0.05f, 0);
        grassBg.transform.localScale = new Vector3(50f, 1f, 50f);
        grassBg.GetComponent<Renderer>().material = darkGreenMat;
        DestroyImmediate(grassBg.GetComponent<Collider>());

        CreateLines(trackRoot);
        CreateBleachers(trackRoot);
        CreateStadiumWalls(trackRoot);
    }

    private static void CreateIntroText()
    {
        // Cleanup old text if exists
        GameObject oldText = GameObject.Find("IntroText");
        if (oldText != null) DestroyImmediate(oldText);

        GameObject textObj = new GameObject("IntroText");
        textObj.transform.position = new Vector3(0, 0.05f, 0); // Flat on the field
        textObj.transform.rotation = Quaternion.Euler(90f, 0, 0); // Face straight up to the camera

        TextMesh tm = textObj.AddComponent<TextMesh>();
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Calibri", "Helvetica" }, 40);
        }
        tm.font = defaultFont;
        textObj.GetComponent<MeshRenderer>().sharedMaterial = defaultFont.material;

        tm.text = "Chapter - 2\nMotion in straight line";
        tm.fontSize = 200;
        tm.characterSize = 0.5f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.black;
    }

    private static void CreateLines(GameObject trackRoot)
    {
        Material whiteMat = new Material(Shader.Find("Sprites/Default"));
        whiteMat.color = Color.white;

        float straightLength = 100f;
        float innerRadius = 26.5f;
        int numLanes = 6;
        float laneWidth = 10f / numLanes;
        int segments = 60;

        for (int i = 0; i <= numLanes; i++)
        {
            float r = innerRadius + i * laneWidth;
            GameObject lineObj = new GameObject("LaneLine_" + i);
            lineObj.transform.SetParent(trackRoot.transform);
            lineObj.transform.position = new Vector3(0, 0.03f, 0);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = whiteMat;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.2f;
            lr.useWorldSpace = false;
            lr.loop = true;

            Vector3[] points = new Vector3[segments * 2 + 4];
            int ptIdx = 0;

            points[ptIdx++] = new Vector3(-straightLength / 2f, 0, -r);
            points[ptIdx++] = new Vector3(straightLength / 2f, 0, -r);

            for (int s = 1; s <= segments; s++)
            {
                float t = (float)s / segments;
                float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
                points[ptIdx++] = new Vector3(straightLength / 2f + r * Mathf.Cos(angle), 0, r * Mathf.Sin(angle));
            }

            points[ptIdx++] = new Vector3(straightLength / 2f, 0, r);
            points[ptIdx++] = new Vector3(-straightLength / 2f, 0, r);

            for (int s = 1; s < segments; s++)
            {
                float t = (float)s / segments;
                float angle = Mathf.Lerp(Mathf.PI / 2f, 3f * Mathf.PI / 2f, t);
                points[ptIdx++] = new Vector3(-straightLength / 2f + r * Mathf.Cos(angle), 0, r * Mathf.Sin(angle));
            }

            lr.positionCount = ptIdx;
            lr.SetPositions(points);
        }
    }

    private static void CreateBleachers(GameObject trackRoot)
    {
        string seatPath = "Assets/Introduction/ASSETS/Meshy_AI_Red_Plastic_Seat_0525123221_texture_fbx/Meshy_AI_Red_Plastic_Seat_0525123221_texture.fbx";
        GameObject seatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(seatPath);

        if (seatPrefab != null)
        {
            CleanMissingScripts(seatPrefab);
        }
        else
        {
            Debug.LogWarning("Seat FBX prefab not found at: " + seatPath + ". Falling back to primitive cubes.");
        }

        // Load textures for custom materials
        string diffusePath = "Assets/Introduction/ASSETS/Meshy_AI_Red_Plastic_Seat_0525123221_texture_fbx/Meshy_AI_Red_Plastic_Seat_0525123221_texture.png";
        Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);

        string normalPath = "Assets/Introduction/ASSETS/Meshy_AI_Red_Plastic_Seat_0525123221_texture_fbx/Meshy_AI_Red_Plastic_Seat_0525123221_texture_normal.png";
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        // Create premium Red seat material
        Material redSeatMat = new Material(Shader.Find("Standard"));
        redSeatMat.name = "PremiumRedPlasticSeat";
        redSeatMat.color = new Color(0.9f, 0.15f, 0.15f); // Rich vibrant red
        redSeatMat.SetFloat("_Glossiness", 0.6f);
        redSeatMat.SetFloat("_Metallic", 0.05f);
        if (diffuseTex != null)
        {
            redSeatMat.SetTexture("_MainTex", diffuseTex);
        }
        if (normalTex != null)
        {
            redSeatMat.SetTexture("_BumpMap", normalTex);
            redSeatMat.EnableKeyword("_NORMALMAP");
        }

        // Create premium Blue seat material
        Material blueSeatMat = new Material(Shader.Find("Standard"));
        blueSeatMat.name = "PremiumBluePlasticSeat";
        blueSeatMat.color = new Color(0.15f, 0.4f, 0.9f); // Sleek modern blue
        blueSeatMat.SetFloat("_Glossiness", 0.6f);
        blueSeatMat.SetFloat("_Metallic", 0.05f);
        if (normalTex != null)
        {
            blueSeatMat.SetTexture("_BumpMap", normalTex);
            blueSeatMat.EnableKeyword("_NORMALMAP");
        }

        float straightLength = 100f;
        float startRadius = 40f;
        int numTiers = 3; // Exactly 3 levels of chairs on the ground!
        float tierWidth = 1.5f;
        float tierHeight = 1.5f; // Step height of 1.5m so they form a beautiful, distinctly stepped stadium!
        float chairSpacing = 1.5f;

        GameObject bleacherRoot = new GameObject("Bleachers");
        bleacherRoot.transform.SetParent(trackRoot.transform);

        // Concrete material for the bleacher steps
        Material concreteStepMat = new Material(Shader.Find("Standard"));
        concreteStepMat.name = "ConcreteStepMaterial";
        concreteStepMat.color = new Color(0.45f, 0.47f, 0.5f); // Sleek modern light concrete
        concreteStepMat.SetFloat("_Glossiness", 0.05f);
        concreteStepMat.SetFloat("_Metallic", 0.05f);

        // Keep track of calculated auto-scale factor so we only compute it once
        float calculatedScaleFactor = -1f;

        for (int tier = 0; tier < numTiers; tier++)
        {
            float r = startRadius + tier * tierWidth;
            float y = tier * tierHeight + 4.0f; // Base elevation of Y=4.0m for all tiers, forming a majestic elevated stand!
            float chairY = y + 0.35f; // Offset to place the seat exactly on top of the concrete step!

            // Create concrete steps for the straight sections to support the chairs beautifully
            // Bottom straight concrete step
            GameObject stepBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepBottom.name = "ConcreteStep_Bottom_Tier" + tier;
            stepBottom.transform.SetParent(bleacherRoot.transform);
            stepBottom.transform.position = new Vector3(0f, y / 2f, -r);
            stepBottom.transform.localScale = new Vector3(straightLength, y, tierWidth);
            stepBottom.GetComponent<Renderer>().material = concreteStepMat;
            DestroyImmediate(stepBottom.GetComponent<Collider>());

            // Top straight concrete step
            GameObject stepTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stepTop.name = "ConcreteStep_Top_Tier" + tier;
            stepTop.transform.SetParent(bleacherRoot.transform);
            stepTop.transform.position = new Vector3(0f, y / 2f, r);
            stepTop.transform.localScale = new Vector3(straightLength, y, tierWidth);
            stepTop.GetComponent<Renderer>().material = concreteStepMat;
            DestroyImmediate(stepTop.GetComponent<Collider>());

            int chairsPerStraight = Mathf.FloorToInt(straightLength / chairSpacing);
            float actualSpacing = straightLength / chairsPerStraight;
            for (int i = 0; i <= chairsPerStraight; i++)
            {
                float x = -straightLength / 2f + i * actualSpacing;
                
                CreateChairInstance(seatPrefab, new Vector3(x, chairY, -r), new Vector3(x, chairY, 0), bleacherRoot.transform, ref calculatedScaleFactor, tier, redSeatMat, blueSeatMat);
                CreateChairInstance(seatPrefab, new Vector3(x, chairY, r), new Vector3(x, chairY, 0), bleacherRoot.transform, ref calculatedScaleFactor, tier, redSeatMat, blueSeatMat);
            }

            float circumference = Mathf.PI * r;
            int chairsPerCurve = Mathf.FloorToInt(circumference / chairSpacing);
            for (int i = 1; i < chairsPerCurve; i++)
            {
                float t = (float)i / chairsPerCurve;
                
                // Right semicircle - position
                float angleR = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
                Vector3 posR = new Vector3(straightLength / 2f + r * Mathf.Cos(angleR), y, r * Mathf.Sin(angleR));
                
                // Create concrete step block under right semicircle chair
                GameObject stepBlockR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stepBlockR.name = "ConcreteStep_R_Tier" + tier + "_" + i;
                stepBlockR.transform.SetParent(bleacherRoot.transform);
                stepBlockR.transform.position = new Vector3(posR.x, y / 2f, posR.z);
                stepBlockR.transform.localScale = new Vector3(chairSpacing * 1.05f, y, tierWidth * 1.02f);
                stepBlockR.transform.LookAt(new Vector3(straightLength / 2f, y / 2f, 0f));
                stepBlockR.GetComponent<Renderer>().material = concreteStepMat;
                DestroyImmediate(stepBlockR.GetComponent<Collider>());

                CreateChairInstance(seatPrefab, new Vector3(posR.x, chairY, posR.z), new Vector3(straightLength / 2f, chairY, 0f), bleacherRoot.transform, ref calculatedScaleFactor, tier, redSeatMat, blueSeatMat);

                // Left semicircle - position
                float angleL = Mathf.Lerp(Mathf.PI / 2f, 3f * Mathf.PI / 2f, t);
                Vector3 posL = new Vector3(-straightLength / 2f + r * Mathf.Cos(angleL), y, r * Mathf.Sin(angleL));

                // Create concrete step block under left semicircle chair
                GameObject stepBlockL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stepBlockL.name = "ConcreteStep_L_Tier" + tier + "_" + i;
                stepBlockL.transform.SetParent(bleacherRoot.transform);
                stepBlockL.transform.position = new Vector3(posL.x, y / 2f, posL.z);
                stepBlockL.transform.localScale = new Vector3(chairSpacing * 1.05f, y, tierWidth * 1.02f);
                stepBlockL.transform.LookAt(new Vector3(-straightLength / 2f, y / 2f, 0f));
                stepBlockL.GetComponent<Renderer>().material = concreteStepMat;
                DestroyImmediate(stepBlockL.GetComponent<Collider>());
                
                CreateChairInstance(seatPrefab, new Vector3(posL.x, chairY, posL.z), new Vector3(-straightLength / 2f, chairY, 0f), bleacherRoot.transform, ref calculatedScaleFactor, tier, redSeatMat, blueSeatMat);
            }
        }
    }

    private static void CreateStadiumWalls(GameObject trackRoot)
    {
        // Cleanup old walls if exist
        GameObject oldWalls = GameObject.Find("StadiumWalls");
        if (oldWalls != null) DestroyImmediate(oldWalls);

        // Material for walls - modern sleek concrete stadium enclosure
        Material wallMat = new Material(Shader.Find("Standard"));
        wallMat.color = new Color(0.35f, 0.38f, 0.42f);
        wallMat.SetFloat("_Glossiness", 0.1f);
        wallMat.SetFloat("_Metallic", 0.1f);

        float straightLength = 100f;
        float startRadius = 40f;
        int numTiers = 3; // Exactly 3 tiers!
        float tierWidth = 1.5f;
        float outerWallRadius = startRadius + numTiers * tierWidth + 0.5f; // Placed right behind the last seat row
        float wallHeight = 12.0f; // Majestic concrete stadium wall, rising tall behind the highest seats
        float wallThickness = 1.5f;

        GameObject wallRoot = new GameObject("StadiumWalls");
        wallRoot.transform.SetParent(trackRoot.transform);

        // 1. Straight Walls
        GameObject bottomWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomWall.name = "StraightWall_Bottom";
        bottomWall.transform.SetParent(wallRoot.transform);
        bottomWall.transform.position = new Vector3(0f, wallHeight / 2f, -outerWallRadius);
        bottomWall.transform.localScale = new Vector3(straightLength, wallHeight, wallThickness);
        bottomWall.GetComponent<Renderer>().material = wallMat;

        GameObject topWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topWall.name = "StraightWall_Top";
        topWall.transform.SetParent(wallRoot.transform);
        topWall.transform.position = new Vector3(0f, wallHeight / 2f, outerWallRadius);
        topWall.transform.localScale = new Vector3(straightLength, wallHeight, wallThickness);
        topWall.GetComponent<Renderer>().material = wallMat;

        // 2. Semicircle Wall Segments
        int segments = 45; // 45 segments ensures a beautifully smooth circular background wall
        float circumference = Mathf.PI * outerWallRadius;
        float segmentWidth = circumference / segments;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            // Right curve wall
            float angleR = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            Vector3 posR = new Vector3(straightLength / 2f + outerWallRadius * Mathf.Cos(angleR), wallHeight / 2f, outerWallRadius * Mathf.Sin(angleR));
            GameObject segmentR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segmentR.name = "RightWallSegment_" + i;
            segmentR.transform.SetParent(wallRoot.transform);
            segmentR.transform.position = posR;
            segmentR.transform.localScale = new Vector3(wallThickness, wallHeight, segmentWidth * 1.05f); // 1.05x width to prevent visual gaps
            segmentR.transform.LookAt(new Vector3(straightLength / 2f, wallHeight / 2f, 0f));
            segmentR.GetComponent<Renderer>().material = wallMat;

            // Left curve wall
            float angleL = Mathf.Lerp(Mathf.PI / 2f, 3f * Mathf.PI / 2f, t);
            Vector3 posL = new Vector3(-straightLength / 2f + outerWallRadius * Mathf.Cos(angleL), wallHeight / 2f, outerWallRadius * Mathf.Sin(angleL));
            GameObject segmentL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segmentL.name = "LeftWallSegment_" + i;
            segmentL.transform.SetParent(wallRoot.transform);
            segmentL.transform.position = posL;
            segmentL.transform.localScale = new Vector3(wallThickness, wallHeight, segmentWidth * 1.05f);
            segmentL.transform.LookAt(new Vector3(-straightLength / 2f, wallHeight / 2f, 0f));
            segmentL.GetComponent<Renderer>().material = wallMat;
        }
    }

    private static void CleanMissingScripts(GameObject go)
    {
        if (go == null) return;
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        foreach (Transform child in go.transform)
        {
            CleanMissingScripts(child.gameObject);
        }
    }

    private static Bounds GetLocalBounds(GameObject go)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
        if (filters != null && filters.Length > 0)
        {
            bounds = filters[0].sharedMesh.bounds;
            for (int i = 1; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null)
                {
                    bounds.Encapsulate(filters[i].sharedMesh.bounds);
                }
            }
        }
        else
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                bounds.center = go.transform.InverseTransformPoint(bounds.center);
                bounds.size = go.transform.InverseTransformVector(bounds.size);
            }
        }
        return bounds;
    }

    private static GameObject CreateChairInstance(GameObject seatPrefab, Vector3 pos, Vector3 lookAtTarget, Transform parent, ref float calculatedScaleFactor, int tier, Material redMat, Material blueMat)
    {
        GameObject chair;
        if (seatPrefab != null)
        {
            chair = (GameObject)PrefabUtility.InstantiatePrefab(seatPrefab);
            chair.transform.SetParent(parent);
            chair.transform.position = pos;
            
            // Orient face towards the center of the track curve with your exact relative euler offset
            Vector3 lookDir = lookAtTarget - pos;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                chair.transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(-90f, -180f, 180f);
            }
            
            // Clean up missing scripts from the newly instantiated seat
            CleanMissingScripts(chair);

            // Apply exact user-specified uniform scale factor
            calculatedScaleFactor = 75f;
            chair.transform.localScale = new Vector3(calculatedScaleFactor, calculatedScaleFactor, calculatedScaleFactor);

            // Assign Red or Blue custom premium materials
            Material applyMat = (tier % 2 == 0) ? redMat : blueMat;
            Renderer[] renderers = chair.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.material = applyMat;
                }
            }
        }
        else
        {
            // Fallback primitive cube in case asset is not present (or deleted)
            chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.transform.SetParent(parent);
            chair.transform.position = pos;
            chair.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            chair.transform.LookAt(lookAtTarget);
            
            chair.GetComponent<Renderer>().material = (tier % 2 == 0) ? redMat : blueMat;
        }
        return chair;
    }

    private static GameObject SetupAthlete()
    {
        // Deep cleanup of all old athletes and broken/missing prefabs in the scene
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go != null && !EditorUtility.IsPersistent(go))
            {
                if (go.name.Contains("Athlete_Runner") || go.name.Contains("Athlete_Bicycle"))
                {
                    DestroyImmediate(go);
                }
            }
        }

        string fbxPath = "Assets/Introduction/ASSETS/Meshy_AI_Blue_Kit_Number_7_biped/Meshy_AI_Blue_Kit_Number_7_biped_Animation_RunFast_withSkin.fbx";

        // Programmatically configure the FBX importer to generate a Generic Rig so that the character is properly rigged and animates!
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        bool hasAvatar = false;
        Object[] initialSubAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (initialSubAssets != null)
        {
            foreach (Object subAsset in initialSubAssets)
            {
                if (subAsset is Avatar av && av != null)
                {
                    hasAvatar = true;
                    break;
                }
            }
        }

        if (importer != null)
        {
            bool needsReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                needsReimport = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }
            if (!hasAvatar)
            {
                needsReimport = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                needsReimport = true;
            }
            if (needsReimport)
            {
                Debug.Log("[Rig Setup] Force-configuring FBX Rig to Generic and generating Avatar (synchronous reimport)...");
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }
        }

        GameObject athletePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

        if (athletePrefab != null)
        {
            CleanMissingScripts(athletePrefab);

            GameObject athlete = (GameObject)PrefabUtility.InstantiatePrefab(athletePrefab);
            athlete.name = "Athlete_Runner";
            PrefabUtility.UnpackPrefabInstance(athlete, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            
            // Strip any rigidbodies or colliders from the athlete to prevent physics glitches or sticking
            foreach (Collider c in athlete.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(c);
            }
            foreach (Rigidbody rb in athlete.GetComponentsInChildren<Rigidbody>(true))
            {
                DestroyImmediate(rb);
            }
            
            // Set athlete to start exactly on the track (bottom straight)
            athlete.transform.position = new Vector3(-50f, 0f, -31.5f);
            athlete.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Set athlete rotation exactly x=0, y=90, z=0!

            // Reverting back to standard scale
            athlete.transform.localScale = new Vector3(2f, 2f, 2f);

            // Setup Robust Animation (Bypasses Animator State Machine)
            ForcePlayAnimation forceAnim = athlete.AddComponent<ForcePlayAnimation>();
            
            // Search for a valid, non-empty running animation clip in the FBX model sub-assets
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            System.Collections.Generic.List<AnimationClip> clipsList = new System.Collections.Generic.List<AnimationClip>();
            if (subAssets != null)
            {
                foreach (Object subAsset in subAssets)
                {
                    if (subAsset is AnimationClip clip && clip != null && clip.length > 0.01f)
                    {
                        // Exclude Unity internal placeholder animations
                        if (!clip.name.StartsWith("__"))
                        {
                            clipsList.Add(clip);
                        }
                    }
                }
            }

            AnimationClip[] clips = clipsList.ToArray();
            AnimationClip runningClip = null;
            if (clips != null && clips.Length > 0)
            {
                foreach (AnimationClip c in clips)
                {
                    if (c != null)
                    {
                        string nameLower = c.name.ToLower();
                        if (nameLower.Contains("run") || nameLower.Contains("fast") || nameLower.Contains("clip"))
                        {
                            runningClip = c;
                            break;
                        }
                    }
                }
                
                // Fallback to the first non-empty clip
                if (runningClip == null)
                {
                    foreach (AnimationClip c in clips)
                    {
                        if (c != null)
                        {
                            runningClip = c;
                            break;
                        }
                    }
                }
                
                // Final fallback
                if (runningClip == null)
                {
                    runningClip = clips[0];
                }
                
            forceAnim.clip = runningClip;
                Debug.Log("[Athlete Animation] Configured clip: " + runningClip.name + " (Length: " + runningClip.length + "s)");
            }

            // --- Premium Athlete Texturing and Shading ---
            // Load high-definition custom textures for the biped runner from the asset folder
            string parentFolder = "Assets/Introduction/ASSETS/Meshy_AI_Blue_Kit_Number_7_biped/";
            Texture2D athleteDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(parentFolder + "Meshy_AI_Blue_Kit_Number_7_biped_texture_0.png");
            Texture2D athleteNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(parentFolder + "Meshy_AI_Blue_Kit_Number_7_biped_texture_0_normal.png");
            Texture2D athleteRoughness = AssetDatabase.LoadAssetAtPath<Texture2D>(parentFolder + "Meshy_AI_Blue_Kit_Number_7_biped_texture_0_roughness.png");
            Texture2D athleteMetallic = AssetDatabase.LoadAssetAtPath<Texture2D>(parentFolder + "Meshy_AI_Blue_Kit_Number_7_biped_texture_0_metallic.png");

            Material athleteMat = new Material(Shader.Find("Standard"));
            athleteMat.name = "AthleteRunnerPremiumMaterial";
            athleteMat.color = Color.white;
            
            if (athleteDiffuse != null)
            {
                athleteMat.SetTexture("_MainTex", athleteDiffuse);
            }
            if (athleteNormal != null)
            {
                athleteMat.SetTexture("_BumpMap", athleteNormal);
                athleteMat.EnableKeyword("_NORMALMAP");
            }
            if (athleteRoughness != null)
            {
                // Smoothness in standard shader is configured by albedo/metallic alpha or float slider
                athleteMat.SetFloat("_Glossiness", 0.15f); // Matte athletic wear look
            }
            if (athleteMetallic != null)
            {
                athleteMat.SetFloat("_Metallic", 0.05f);
            }

            // Apply material to every child renderer of the athlete
            Renderer[] athleteRenderers = athlete.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in athleteRenderers)
            {
                if (r != null)
                {
                    r.material = athleteMat;
                }
            }

            // Find and assign the Avatar from the FBX sub-assets to guarantee correct bone mapping and skeletal deformation
            Avatar athleteAvatar = null;
            if (subAssets != null)
            {
                foreach (Object subAsset in subAssets)
                {
                    if (subAsset is Avatar av && av != null)
                    {
                        athleteAvatar = av;
                        break;
                    }
                }
            }

            // IMPORTANT: Keep Animator ENABLED so the humanoid rig deforms hands, legs, etc.
            // ForcePlayAnimation now drives the rig via Playables API.
            Animator animator = athlete.GetComponent<Animator>();
            if (animator == null)
            {
                animator = athlete.AddComponent<Animator>();
                Debug.Log("[Athlete Rig] Animator component was missing on instantiated prefab. Added programmatically.");
            }

            if (athleteAvatar != null)
            {
                animator.avatar = athleteAvatar;
                Debug.Log("[Athlete Rig] Successfully loaded and assigned Avatar: " + athleteAvatar.name);
            }
            else
            {
                Debug.LogWarning("[Athlete Rig] No Avatar asset was found in the FBX sub-assets!");
            }
            animator.enabled = true;
            animator.applyRootMotion = false; // Disable root motion so TrackRunner controls the position

            // Add the TrackRunner script
            TrackRunner runner = athlete.GetComponent<TrackRunner>();
            if (runner == null)
            {
                runner = athlete.AddComponent<TrackRunner>();
            }
            
            // The default TrackRunner radius is 31.5 for the center of the track (outer is 36.5, inner is 26.5)
            // 31.5 is exactly the middle of the red track
            runner.straightLength = 100f;
            runner.radius = 31.5f;
            
            // Set speed to a realistic premium athletic run (7.5 m/s) and set animation speed to 1.6f to match
            runner.speed = 7.5f;
            forceAnim.animationSpeed = 1.6f;
            
            // rotationOffsetY = 0 means the athlete's native FBX forward (+Z or -Z) aligns with LookRotation.
            runner.rotationOffsetY = 0f;
            
            Debug.Log("Athlete Runner FBX instantiated and configured.");
            return athlete;
        }
        else
        {
            Debug.LogWarning("Athlete FBX not found at: " + fbxPath);
            return null;
        }
    }

    private static void SetupCamera(GameObject athlete)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
        }

        cam.farClipPlane = 300f; // Optimizes culling to only render the visible stadium area

        if (athlete != null)
        {
            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            
            follow.target = athlete.transform;
            follow.offset = new Vector3(0f, 2.5f, -6.5f); // Position camera much closer to athlete!
            follow.smoothSpeed = 5f;
            
            // Initial position so it doesn't snap abruptly
            cam.transform.position = athlete.transform.position + athlete.transform.TransformDirection(follow.offset);
            cam.transform.LookAt(athlete.transform.position + Vector3.up * 2f);
        }
        else
        {
            // Fallback Position
            cam.transform.position = new Vector3(0, 100f, -120f);
            cam.transform.rotation = Quaternion.Euler(40f, 0, 0);
            cam.farClipPlane = 2000f;
        }
    }

    private static GameObject CreateSmoothCylinder(string name, float radius, float height, Material mat, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = GenerateSmoothCylinderMesh(128, radius, height);
        
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        
        return obj;
    }

    private static Mesh GenerateSmoothCylinderMesh(int sides, float radius, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "SmoothCylinder";

        int vertexCount = sides * 2 + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[sides * 12];

        // Top center and bottom center
        vertices[0] = new Vector3(0, height / 2f, 0);
        vertices[1] = new Vector3(0, -height / 2f, 0);

        for (int i = 0; i < sides; i++)
        {
            float angle = i * 2f * Mathf.PI / sides;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[2 + i] = new Vector3(cos * radius, height / 2f, sin * radius);
            vertices[2 + sides + i] = new Vector3(cos * radius, -height / 2f, sin * radius);
        }

        // Generate triangles
        int triIdx = 0;
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            // Top cap
            triangles[triIdx++] = 0;
            triangles[triIdx++] = 2 + next;
            triangles[triIdx++] = 2 + i;

            // Bottom cap
            triangles[triIdx++] = 1;
            triangles[triIdx++] = 2 + sides + i;
            triangles[triIdx++] = 2 + sides + next;

            // Side quad triangle 1
            triangles[triIdx++] = 2 + i;
            triangles[triIdx++] = 2 + next;
            triangles[triIdx++] = 2 + sides + i;

            // Side quad triangle 2
            triangles[triIdx++] = 2 + next;
            triangles[triIdx++] = 2 + sides + next;
            triangles[triIdx++] = 2 + sides + i;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
