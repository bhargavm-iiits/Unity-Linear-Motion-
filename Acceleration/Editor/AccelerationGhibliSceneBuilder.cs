using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

namespace NCERT.Chapter2.VR
{
    public class AccelerationSceneBuilder : EditorWindow
    {
        private const string ScenePath = "Assets/Acceleration/Acceleration and it's equations/Acceleration.unity";
        private const string GhibliPrefabsDir = "Assets/Acceleration/Assets/3D set of stylized nature - GHIBLI style/Art/Prefabs/";
        private const string GhibliMaterialsDir = "Assets/Acceleration/Assets/3D set of stylized nature - GHIBLI style/Art/Materials/";
        private const string MidnightDefenderDir = "Assets/Acceleration/Assets/Meshy_AI_Midnight_Defender_0526005608_texture_fbx/";
        private const string GentleGiantDir = "Assets/Acceleration/Assets/Meshy_AI_Gentle_Giant_0526023406_texture_fbx/";

        [MenuItem("Tools/Setup Acceleration VR Ghibli Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Please exit Play Mode before running the Setup Ghibli Environment tool!");
                return;
            }

            Debug.Log("[AccelerationSceneBuilder] Initiating Pipeline-Adaptive Ghibli Setup...");

            // 1. Open the Scene
            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Could not open the scene at {ScenePath}. Error: {e.Message}");
                return;
            }

            // 2. Adapt Ghibli Pack Materials to Active Render Pipeline (Built-in or URP)
            ConvertMaterialsToActivePipeline();

            // 3. Cleanup Old Environment
            CleanupSceneObjects(scene);

            // 4. Setup Premium Lighting
            SetupGoldenHourLighting();

            // 5. Setup Custom Panoramic Pastel Skybox
            SetupPastelSkybox();

            // 6. Create Grassy Ground Plane
            SetupGrassyGround();

            // 7. Construct Procedural Highway Track
            SetupHighwayTrack();

            // 8. Setup 3D Distance Signboards
            SetupDistanceMarkers();

            // 9. Place Layered Ghibli Vegetation & Mossy Rocks
            SetupLayeredVegetation();

            // 10. Place Iconic Ghibli Scenic Landmarks
            SetupGhibliLandmarks();

            // 11. Instantiate & Configure Midnight Defender Vehicle & Displays
            SetupMidnightDefender();

            // 12. Instantiate & Configure Gentle Giant Elephant
            SetupGentleGiant();

            // 13. Setup Cinematic Camera
            SetupCinematicCamera();

            // 14. Setup Acceleration Title Screen Pop-up UI
            SetupTitlePopUp();

            // 15. Deep Clean Missing Script References
            int missingScriptsRemoved = CleanAllMissingScripts(scene);
            Debug.Log($"[Cleanup] Removed {missingScriptsRemoved} missing script references in active scene.");

            // 13. Save & Finalize Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[AccelerationSceneBuilder] Success! Ghibli VR Environment & Midnight Defender built flawlessly.</color>");

            // Safe popup if not running in batch mode
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Scene Builder", 
                    "Ghibli nature environment, pipeline shaders, educational highway, and Midnight Defender vehicle set up successfully!", 
                    "Excellent");
            }
        }

        private static readonly string[] ColorProperties = {
            "_BaseColor",
            "_Base_Color",
            "_Base_color",
            "_Color"
        };

        private static Texture GetSerializedTexture(Material mat, string[] propertyNames)
        {
            if (mat == null) return null;
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs != null && texEnvs.isArray)
            {
                foreach (string targetName in propertyNames)
                {
                    for (int i = 0; i < texEnvs.arraySize; i++)
                    {
                        SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                        SerializedProperty nameProp = entry.FindPropertyRelative("first");
                        if (nameProp != null && nameProp.stringValue.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            SerializedProperty texProp = entry.FindPropertyRelative("second.m_Texture");
                            if (texProp != null && texProp.objectReferenceValue != null)
                            {
                                return texProp.objectReferenceValue as Texture;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static Color GetSerializedColor(Material mat, string[] propertyNames, Color defaultColor)
        {
            if (mat == null) return defaultColor;
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty colors = so.FindProperty("m_SavedProperties.m_Colors");
            if (colors != null && colors.isArray)
            {
                foreach (string targetName in propertyNames)
                {
                    for (int i = 0; i < colors.arraySize; i++)
                    {
                        SerializedProperty entry = colors.GetArrayElementAtIndex(i);
                        SerializedProperty nameProp = entry.FindPropertyRelative("first");
                        if (nameProp != null && nameProp.stringValue.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            SerializedProperty colorProp = entry.FindPropertyRelative("second");
                            if (colorProp != null)
                            {
                                return colorProp.colorValue;
                            }
                        }
                    }
                }
            }
            return defaultColor;
        }

        private static Texture SelectBestBaseColorTexture(Material mat)
        {
            if (mat == null) return null;
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null || !texEnvs.isArray) return null;

            string matName = mat.name.ToLower();
            Texture bestTex = null;
            float bestScore = -999f;

            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = entry.FindPropertyRelative("first");
                if (nameProp == null) continue;

                string propName = nameProp.stringValue;
                SerializedProperty texProp = entry.FindPropertyRelative("second.m_Texture");
                if (texProp == null || texProp.objectReferenceValue == null) continue;

                Texture tex = texProp.objectReferenceValue as Texture;
                if (tex == null) continue;

                string texName = tex.name.ToLower();
                float score = 0f;

                // 1. Exclude utility/normal/noise/displacement/opacity maps from main diffuse color
                if (texName.Contains("normal") || texName.EndsWith("_n") || texName.Contains("_n_") ||
                    texName.Contains("omg") || texName.Contains("opacity") || texName.Contains("noise") ||
                    texName.Contains("displacement") || texName.Contains("alpha") || texName.Contains("mask") ||
                    propName.ToLower().Contains("normal") || propName.ToLower().Contains("bump") ||
                    propName.ToLower().Contains("noise") || propName.ToLower().Contains("wind") ||
                    propName.ToLower().Contains("alpha") || propName.ToLower().Contains("mask") ||
                    propName.ToLower().Contains("omg"))
                {
                    score -= 500f;
                }

                // 2. Prioritize base color indicators in filename
                if (texName.Contains("bc") || texName.Contains("base_color") || texName.Contains("basecolor") || texName.Contains("diffuse"))
                {
                    score += 100f;
                }

                // 3. Name similarity between Material and Texture (Context-aware selection)
                if (matName.Contains("wagon") && texName.Contains("wagon")) score += 200f;
                else if (matName.Contains("altar") && texName.Contains("altar")) score += 200f;
                else if (matName.Contains("arch") && texName.Contains("arc")) score += 200f;
                else if (matName.Contains("fence") && texName.Contains("fence")) score += 200f;
                else if (matName.Contains("sign") && texName.Contains("sign")) score += 200f;
                else if (matName.Contains("lamp") && texName.Contains("light")) score += 200f;
                else if (matName.Contains("street") && texName.Contains("street")) score += 200f;
                else if (matName.Contains("stone") && texName.Contains("stone")) score += 200f;
                else if (matName.Contains("slab") && texName.Contains("slab")) score += 200f;
                else if (matName.Contains("box") && texName.Contains("box")) score += 200f;
                else if (matName.Contains("flower") && texName.Contains("flower")) score += 200f;
                else if (matName.Contains("leaves") && (texName.Contains("leave") || texName.Contains("leaf"))) score += 200f;
                else if (matName.Contains("gras") && texName.Contains("grass"))
                {
                    // Grass materials should prefer grass textures. If it is Grass_01.mat (clumps), prefer Grass Variation or leave/flowers over ground grass
                    if (!matName.Contains("ground") && texName.Contains("ground")) score -= 50f;
                    else score += 200f;
                }
                else if ((matName.Contains("tree") || matName.Contains("big_tree")) && !matName.Contains("leaves"))
                {
                    // Tree trunk materials should heavily prefer wood bark textures over green moss textures to render thick brown trunks!
                    if (texName.Contains("wood")) score += 400f;
                    else if (texName.Contains("moss") || texName.Contains("bark")) score += 150f;
                    if (texName.Contains("leave") || texName.Contains("grass")) score -= 300f;
                }
                else if (matName.Contains("rock"))
                {
                    // Rock materials should prefer rock textures
                    if (texName.Contains("rock") || texName.Contains("moss")) score += 200f;
                    if (texName.Contains("leave") || texName.Contains("grass")) score -= 150f;
                }

                // 4. Default priority if no specific match
                if (propName.Equals("_MainTex", System.StringComparison.OrdinalIgnoreCase)) score += 10f;
                if (propName.Equals("_BaseMap", System.StringComparison.OrdinalIgnoreCase)) score += 10f;
                if (propName.Equals("_Base_Color", System.StringComparison.OrdinalIgnoreCase)) score += 5f;
                if (propName.Equals("_Base_color", System.StringComparison.OrdinalIgnoreCase)) score += 5f;
                if (propName.Equals("_Texture2D", System.StringComparison.OrdinalIgnoreCase)) score += 3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTex = tex;
                }
            }

            // Fallback: if no texture was scored positively, return any texture that isn't a utility map
            if (bestTex == null || bestScore < -100f)
            {
                for (int i = 0; i < texEnvs.arraySize; i++)
                {
                    SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                    SerializedProperty texProp = entry.FindPropertyRelative("second.m_Texture");
                    if (texProp != null && texProp.objectReferenceValue != null)
                    {
                        Texture tex = texProp.objectReferenceValue as Texture;
                        if (tex != null && !tex.name.ToLower().Contains("normal") && !tex.name.ToLower().Contains("noise") && !tex.name.ToLower().Contains("omg"))
                        {
                            return tex;
                        }
                    }
                }
            }

            return bestTex;
        }

        private static Texture SelectBestNormalTexture(Material mat)
        {
            if (mat == null) return null;
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null || !texEnvs.isArray) return null;

            string matName = mat.name.ToLower();
            Texture bestNormal = null;
            float bestScore = -999f;

            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = entry.FindPropertyRelative("first");
                if (nameProp == null) continue;

                string propName = nameProp.stringValue;
                SerializedProperty texProp = entry.FindPropertyRelative("second.m_Texture");
                if (texProp == null || texProp.objectReferenceValue == null) continue;

                Texture tex = texProp.objectReferenceValue as Texture;
                if (tex == null) continue;

                string texName = tex.name.ToLower();
                float score = 0f;

                // 1. Must be a normal map
                if (texName.Contains("normal") || texName.EndsWith("_n") || texName.Contains("_n_") ||
                    propName.ToLower().Contains("normal") || propName.ToLower().Contains("bump"))
                {
                    score += 200f;
                }
                else
                {
                    score -= 500f; // Exclude non-normal textures from normal map channel!
                }

                // 2. Name similarity
                if (matName.Contains("wagon") && texName.Contains("wagon")) score += 100f;
                else if (matName.Contains("altar") && texName.Contains("altar")) score += 100f;
                else if (matName.Contains("arch") && texName.Contains("arc")) score += 100f;
                else if (matName.Contains("fence") && texName.Contains("fence")) score += 100f;
                else if (matName.Contains("sign") && texName.Contains("sign")) score += 100f;
                else if (matName.Contains("stone") && texName.Contains("stone")) score += 100f;
                else if (matName.Contains("slab") && texName.Contains("slab")) score += 100f;
                else if (matName.Contains("box") && texName.Contains("box")) score += 100f;
                else if (matName.Contains("flower") && texName.Contains("flower")) score += 100f;
                else if (matName.Contains("leaves") && (texName.Contains("leave") || texName.Contains("leaf"))) score += 100f;
                else if (matName.Contains("gras") && texName.Contains("grass")) score += 100f;
                else if ((matName.Contains("tree") || matName.Contains("big_tree")) && !matName.Contains("leaves"))
                {
                    if (texName.Contains("wood") || texName.Contains("moss")) score += 100f;
                }
                else if (matName.Contains("rock") && texName.Contains("rock")) score += 100f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestNormal = tex;
                }
            }

            return bestNormal;
        }

        private static void ConvertMaterialsToActivePipeline()
        {
            bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            
            if (isURP)
            {
                Debug.Log("<color=yellow>[Shader Conversion] URP Detected! Adapting standard materials to URP Lit...</color>");
            }
            else
            {
                Debug.Log("<color=yellow>[Shader Conversion] Built-in Render Pipeline (DX11) Detected! Adapting URP Shader Graphs to Custom Ghibli Shaders with Vertex Colors...</color>");
            }

            // Find all material files in Ghibli Materials directory
            string[] ghibliMatGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Acceleration/Assets/3D set of stylized nature - GHIBLI style/Art/Materials" });
            int convertedCount = 0;
            
            foreach (string guid in ghibliMatGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    bool needsConversion = false;
                    if (isURP)
                    {
                        needsConversion = mat.shader.name != "Universal Render Pipeline/Lit" && (mat.shader.name == "Standard" || mat.shader.name.Contains("Standard") || mat.shader.name == "Hidden/InternalErrorShader" || mat.shader.name == "");
                    }
                    else
                    {
                        // Convert if the shader is URP, is the error shader, is empty, or is the standard shader (to upgrade it to our custom vertex-color shaders)
                        needsConversion = mat.shader.name.Contains("Shader Graphs") || mat.shader.name == "Hidden/InternalErrorShader" || mat.shader.name == "Standard" || mat.shader.name == "" || mat.shader.name == "Custom/GhibliFoliage" || mat.shader.name == "Custom/GhibliOpaque";
                    }

                    if (needsConversion)
                    {
                        // 1. Direct Extract Textures and Colors using SerializedObject (completely warning-free and case-insensitive!)
                        Texture baseTex = SelectBestBaseColorTexture(mat);
                        Texture normalTex = SelectBestNormalTexture(mat);
                        Color baseCol = GetSerializedColor(mat, ColorProperties, Color.white);

                        Debug.Log($"[Shader Conversion Debug] Material: {mat.name}, Selected BaseTex: {(baseTex != null ? baseTex.name : "null")}, Selected NormalTex: {(normalTex != null ? normalTex.name : "null")}");

                        string nameLower = mat.name.ToLower();
                        bool isFoliage = nameLower.Contains("leaves") || nameLower.Contains("gras") || nameLower.Contains("flower") || nameLower.Contains("shrub") || nameLower.Contains("bush");

                        if (isFoliage)
                        {
                            // Assign a gorgeous, lush Ghibli Green tint to foliage to paint the grayscale shapes green!
                            baseCol = new Color(0.18f, 0.58f, 0.24f, 1.0f);
                        }
                        else if (baseTex != null)
                        {
                            // Override green baseColor tints on non-foliage elements (like wood trunks/rocks) to render artist textures cleanly
                            baseCol = Color.white;
                        }

                        // 2. Pre-emptively assign the correct renderQueue and target shader BEFORE changing the shader to silence all console validation warnings!
                        Shader targetShader = null;
                        if (isURP)
                        {
                            targetShader = Shader.Find("Universal Render Pipeline/Lit");
                            mat.renderQueue = isFoliage ? 2450 : -1;
                        }
                        else
                        {
                            // Assign Ghibli custom shaders supporting vertex colors, double-sided rendering, and cutout transparent layers
                            string targetShaderName = isFoliage ? "Custom/GhibliFoliage" : "Custom/GhibliOpaque";
                            targetShader = Shader.Find(targetShaderName);
                            if (targetShader == null)
                            {
                                targetShader = Shader.Find("Standard");
                            }
                            mat.renderQueue = isFoliage ? 2450 : -1;
                        }

                        if (targetShader == null)
                        {
                            Debug.LogError($"[Shader Conversion] Target shader not found! Cannot convert material: {mat.name}");
                            continue;
                        }

                        // Switch Shader to target
                        mat.shader = targetShader;

                        // 3. Apply extracted textures back based on target shader properties
                        if (isURP)
                        {
                            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
                            if (normalTex != null)
                            {
                                mat.SetTexture("_BumpMap", normalTex);
                                mat.EnableKeyword("_NORMALMAP");
                            }
                            mat.SetColor("_BaseColor", baseCol);
                        }
                        else
                        {
                            if (baseTex != null) mat.SetTexture("_MainTex", baseTex);
                            if (normalTex != null)
                            {
                                mat.SetTexture("_BumpMap", normalTex);
                                mat.EnableKeyword("_NORMALMAP");
                            }
                            
                            // Prevent invisible cutout materials by forcing solid alpha
                            baseCol.a = 1.0f;
                            mat.SetColor("_Color", baseCol);
                            
                            // Setup Nature material properties
                            mat.SetFloat("_Glossiness", 0f);
                            mat.SetFloat("_Metallic", 0f);

                            // Extract and assign Custom Emissive details (only enable emission for light-emitting lamps, not nature foliage!)
                            Texture emissionTex = GetSerializedTexture(mat, new[] { "_EmissionMap", "_Emissive" });
                            Color emissionCol = GetSerializedColor(mat, new[] { "_EmissionColor" }, Color.clear);
                            
                            string matNameLower = mat.name.ToLower();
                            bool isLight = matNameLower.Contains("lamp") || matNameLower.Contains("light");
                            
                            if (isLight && emissionCol != Color.clear && emissionCol.grayscale > 0.01f)
                            {
                                mat.SetColor("_EmissionColor", emissionCol);
                                mat.EnableKeyword("_EMISSION");
                                if (emissionTex != null) mat.SetTexture("_EmissionMap", emissionTex);
                            }
                            else
                            {
                                // Set emissive to solid black for foliage & trunks to prevent incorrect pink & cyan glowing distortions!
                                mat.SetColor("_EmissionColor", Color.black);
                                mat.DisableKeyword("_EMISSION");
                            }

                            // Dynamic foliage transparency and double-sided settings (Cutout)
                            if (isFoliage)
                            {
                                mat.SetFloat("_Mode", 1f); // 1 = Cutout
                                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                                mat.SetInt("_ZWrite", 1);
                                mat.EnableKeyword("_ALPHATEST_ON");
                                mat.DisableKeyword("_ALPHABLEND_ON");
                                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

                                // Extra safety cutout threshold clamp
                                if (mat.HasProperty("_Cutoff") && mat.GetFloat("_Cutoff") < 0.05f)
                                {
                                    mat.SetFloat("_Cutoff", 0.5f);
                                }

                                // Enable Double-Sided Rendering (VR Optimization!)
                                mat.SetInt("_Cull", 0); // 0 = Cull Off (Renders both sides)
                            }
                        }

                        EditorUtility.SetDirty(mat);
                        convertedCount++;
                    }
                }
            }
            
            Debug.Log($"[Shader Conversion] Successfully converted {convertedCount} materials.");
            AssetDatabase.SaveAssets();
        }

        private static Shader GetBaseShader()
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                return urpShader;
            }
            return Shader.Find("Standard");
        }

        private static void CleanupSceneObjects(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                if (root == null) continue;
                
                // Retain only vital nodes, destroy old procedurals
                if (root.name == "Main Camera" || root.name == "Directional Light")
                {
                    continue;
                }
                DestroyImmediate(root);
            }
        }

        private static void SetupGoldenHourLighting()
        {
            Light dirLight = null;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }

            if (dirLight == null)
            {
                GameObject lightGO = new GameObject("Directional Light");
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            // Rich Ghibli warm cream-gold sunlight settings
            dirLight.color = new Color(1.0f, 0.95f, 0.85f); 
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.8f;
            dirLight.transform.rotation = Quaternion.Euler(32f, -125f, 0f); // Nostalgic golden perspective shadows
            
            // Adjust RenderSettings Ambient Mode to rely on Skybox and ambient intensity
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.72f, 0.85f); // Beautiful soft sky blue ambient bounce
        }

        private static void SetupPastelSkybox()
        {
            // Create a custom procedural gradient skybox matching Ghibli paintings
            Material skyMat = new Material(Shader.Find("Skybox/Procedural"));
            skyMat.SetColor("_SkyTint", new Color(0.4f, 0.72f, 0.98f));      // Vibrant anime sky blue
            skyMat.SetColor("_GroundColor", new Color(0.24f, 0.58f, 0.28f));  // Lush field green
            skyMat.SetFloat("_AtmosphereThickness", 0.75f);                  // Soft thin atmosphere
            skyMat.SetFloat("_Exposure", 1.15f);                            // Crisp and bright exposure

            string matDir = "Assets/Acceleration/Materials";
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
                AssetDatabase.Refresh();
            }

            string skyMatPath = $"{matDir}/GhibliProceduralSkybox.mat";
            
            // Overwrite cleanly
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(skyMatPath);
            if (existingMat != null)
            {
                AssetDatabase.DeleteAsset(skyMatPath);
            }
            AssetDatabase.CreateAsset(skyMat, skyMatPath);
            
            RenderSettings.skybox = skyMat;
            DynamicGI.UpdateEnvironment();
        }

        private static void SetupGrassyGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GhibliGroundPlane";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(25f, 1f, 25f); // 250m x 250m base

            // Load grass material from Ghibli stylized package (auto-converted to Standard in Step 2!)
            string grassMatPath = $"{GhibliMaterialsDir}Grass_01.mat";
            Material grassMat = AssetDatabase.LoadAssetAtPath<Material>(grassMatPath);

            if (grassMat != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial = grassMat;
            }
            else
            {
                // Fallback high-quality flat Ghibli green standard material
                Material greenMat = new Material(GetBaseShader());
                greenMat.color = new Color(0.22f, 0.55f, 0.25f);
                if (greenMat.HasProperty("_Glossiness")) greenMat.SetFloat("_Glossiness", 0f);
                if (greenMat.HasProperty("_Smoothness")) greenMat.SetFloat("_Smoothness", 0f);
                if (greenMat.HasProperty("_Metallic")) greenMat.SetFloat("_Metallic", 0f);
                ground.GetComponent<Renderer>().sharedMaterial = greenMat;
            }
        }

        private static void SetupHighwayTrack()
        {
            GameObject highway = GameObject.CreatePrimitive(PrimitiveType.Plane);
            highway.name = "AccelerationHighway";
            // 240m long, 8m wide road along the Z-axis, centered at Z = 30m (stretches Z = -90m to Z = 150m)
            highway.transform.localScale = new Vector3(0.8f, 1f, 24f);
            highway.transform.position = new Vector3(0f, 0.015f, 30f); // Sits cleanly above grass

            Material roadMat = new Material(GetBaseShader());
            roadMat.color = new Color(0.22f, 0.24f, 0.27f); // Beautiful dark slate asphalt
            if (roadMat.HasProperty("_Glossiness")) roadMat.SetFloat("_Glossiness", 0.15f);
            if (roadMat.HasProperty("_Smoothness")) roadMat.SetFloat("_Smoothness", 0.15f);
            if (roadMat.HasProperty("_Metallic")) roadMat.SetFloat("_Metallic", 0.05f);
            highway.GetComponent<Renderer>().sharedMaterial = roadMat;

            // Highway lines parent
            GameObject linesParent = new GameObject("HighwayMarkings");
            linesParent.transform.position = new Vector3(0f, 0.025f, 0f);

            Material whiteMat = new Material(GetBaseShader());
            whiteMat.color = Color.white;
            if (whiteMat.HasProperty("_Glossiness")) whiteMat.SetFloat("_Glossiness", 0f);
            if (whiteMat.HasProperty("_Smoothness")) whiteMat.SetFloat("_Smoothness", 0f);
            if (whiteMat.HasProperty("_Metallic")) whiteMat.SetFloat("_Metallic", 0f);

            // Left solid side border
            GameObject leftBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBorder.name = "LeftEdgeLine";
            leftBorder.transform.SetParent(linesParent.transform);
            leftBorder.transform.position = new Vector3(-3.85f, 0f, 30f);
            leftBorder.transform.localScale = new Vector3(0.12f, 0.005f, 240f);
            leftBorder.GetComponent<Renderer>().sharedMaterial = whiteMat;
            DestroyImmediate(leftBorder.GetComponent<Collider>());

            // Right solid side border
            GameObject rightBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBorder.name = "RightEdgeLine";
            rightBorder.transform.SetParent(linesParent.transform);
            rightBorder.transform.position = new Vector3(3.85f, 0f, 30f);
            rightBorder.transform.localScale = new Vector3(0.12f, 0.005f, 240f);
            rightBorder.GetComponent<Renderer>().sharedMaterial = whiteMat;
            DestroyImmediate(rightBorder.GetComponent<Collider>());

            // Center dashed lane lines
            float dashLength = 3f;
            float dashGap = 4f;
            float roadSpan = 240f;
            float centerZ = 30f;

            for (float z = centerZ - roadSpan / 2f; z <= centerZ + roadSpan / 2f; z += (dashLength + dashGap))
            {
                GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = "CenterDashedLine";
                dash.transform.SetParent(linesParent.transform);
                dash.transform.position = new Vector3(0f, 0f, z);
                dash.transform.localScale = new Vector3(0.1f, 0.005f, dashLength);
                dash.GetComponent<Renderer>().sharedMaterial = whiteMat;
                DestroyImmediate(dash.GetComponent<Collider>());
            }
        }

        private static void SetupDistanceMarkers()
        {
            GameObject markersParent = new GameObject("DistanceMarkers");
            
            // Standardise 200m track (from Z = -70 to Z = 130)
            float startZ = -70f;
            float endZ = 130f;
            float interval = 10f;

            // Load wooden signboard prefab from Ghibli Nature assets
            string signPrefabPath = $"{GhibliPrefabsDir}Sign.prefab";
            GameObject signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(signPrefabPath);

            for (float z = startZ; z <= endZ; z += interval)
            {
                int meters = Mathf.RoundToInt(z - startZ);
                
                if (signPrefab != null)
                {
                    GameObject signObj = (GameObject)PrefabUtility.InstantiatePrefab(signPrefab);
                    signObj.name = $"DistanceSign_{meters}m";
                    signObj.transform.SetParent(markersParent.transform);
                    
                    // Place sign exactly at the left margin of the track (X = -4.6m)
                    signObj.transform.position = new Vector3(-4.6f, 0f, z);
                    signObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Perpendicular to road
                    signObj.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);

                    // Attach animated popup behavior so it pops up springily as the car approaches
                    signObj.AddComponent<DistanceMarkerPopUp>();
                }
                else
                {
                    // High-quality procedural wooden post fallback if prefab isn't resolved
                    GameObject fallbackGroup = new GameObject($"DistanceFallback_{meters}m");
                    fallbackGroup.transform.position = new Vector3(-4.6f, 0f, z);
                    fallbackGroup.transform.SetParent(markersParent.transform);

                    Material woodMat = new Material(GetBaseShader());
                    woodMat.color = new Color(0.4f, 0.25f, 0.12f);
                    if (woodMat.HasProperty("_Glossiness")) woodMat.SetFloat("_Glossiness", 0f);
                    if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0f);

                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "Post";
                    post.transform.SetParent(fallbackGroup.transform);
                    post.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    post.transform.localScale = new Vector3(0.12f, 0.65f, 0.12f);
                    post.GetComponent<Renderer>().sharedMaterial = woodMat;
                    DestroyImmediate(post.GetComponent<Collider>());

                    GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    board.name = "Board";
                    board.transform.SetParent(fallbackGroup.transform);
                    board.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                    board.transform.localScale = new Vector3(0.08f, 0.45f, 0.9f);
                    board.GetComponent<Renderer>().sharedMaterial = woodMat;
                    DestroyImmediate(board.GetComponent<Collider>());

                    // Attach animated popup behavior
                    fallbackGroup.AddComponent<DistanceMarkerPopUp>();
                }
            }
        }

        private static void SetupLayeredVegetation()
        {
            GameObject envParent = new GameObject("GhibliEnvironment");

            // Load beautiful Ghibli asset packs
            GameObject bigTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Big Tree.prefab");
            
            GameObject[] forestTrees = {
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Tree_01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Tree_02.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Tree_03.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Tree_04.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Tree_05.prefab")
            };

            GameObject[] shrubs = {
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Shrubs_01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Shrubs_02.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Shrubs_03.prefab")
            };

            GameObject[] grassClumps = {
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Gras_01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Gras_02.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Gras_03.prefab")
            };

            GameObject[] mossyRocks = {
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_02.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_03.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_04.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_05.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_06.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_07.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_08.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_09.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_10.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Rock_11.prefab")
            };

            // Programmatically gather all 25 flower prefabs
            System.Collections.Generic.List<GameObject> flowers = new System.Collections.Generic.List<GameObject>();
            for (int i = 1; i <= 9; i++)
            {
                for (int j = 1; j <= 3; j++)
                {
                    GameObject f = AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Flower_0{i}_{j}.prefab");
                    if (f != null) flowers.Add(f);
                }
            }
            for (int i = 10; i <= 13; i++)
            {
                GameObject f = AssetDatabase.LoadAssetAtPath<GameObject>($"{GhibliPrefabsDir}Flower_{i}.prefab");
                if (f != null) flowers.Add(f);
            }

            // Layer 1: Detail & Embellishment Zone (5m to 12m from center) - Low Details (Extended to 200m track end)
            GameObject detailGroup = new GameObject("DetailVegetation_Zone");
            detailGroup.transform.SetParent(envParent.transform);
            
            int detailCount = 480; // Scaled up to cover extended highway
            for (int i = 0; i < detailCount; i++)
            {
                float side = Random.value > 0.5f ? 1f : -1f;
                float x = side * Random.Range(5.2f, 12f);
                float z = Random.Range(-85f, 145f); // Extended Z
                Vector3 pos = new Vector3(x, 0f, z);

                float choice = Random.value;
                if (choice < 0.65f && flowers.Count > 0) // Colorful Ghibli Flowers
                {
                    GameObject flPrefab = flowers[Random.Range(0, flowers.Count)];
                    if (flPrefab == null) continue;
                    GameObject fl = (GameObject)PrefabUtility.InstantiatePrefab(flPrefab);
                    fl.transform.position = pos;
                    fl.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    fl.transform.localScale = Vector3.one * Random.Range(1.0f, 1.8f);
                    fl.transform.SetParent(detailGroup.transform);
                }
                else if (choice < 0.9f && grassClumps.Length > 0) // Fluffy Stylized Grass
                {
                    GameObject grPrefab = grassClumps[Random.Range(0, grassClumps.Length)];
                    if (grPrefab == null) continue;
                    GameObject gr = (GameObject)PrefabUtility.InstantiatePrefab(grPrefab);
                    gr.transform.position = pos;
                    gr.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    gr.transform.localScale = Vector3.one * Random.Range(1.5f, 2.8f);
                    gr.transform.SetParent(detailGroup.transform);
                }
                else if (mossyRocks.Length > 0) // Mossy pebbles / small stones
                {
                    GameObject rkPrefab = mossyRocks[Random.Range(0, mossyRocks.Length)];
                    if (rkPrefab == null) continue;
                    GameObject rk = (GameObject)PrefabUtility.InstantiatePrefab(rkPrefab);
                    rk.transform.position = pos;
                    rk.transform.rotation = Quaternion.Euler(Random.Range(-10, 10), Random.Range(0, 360f), Random.Range(-10, 10));
                    rk.transform.localScale = Vector3.one * Random.Range(0.4f, 0.9f);
                    rk.transform.SetParent(detailGroup.transform);
                }
            }

            // Layer 2: Mid-ground Scenic Zone (12m to 25m from center) (Extended to 200m track end)
            GameObject midGroup = new GameObject("Midground_Zone");
            midGroup.transform.SetParent(envParent.transform);
            
            int midCount = 140; // Scaled up
            for (int i = 0; i < midCount; i++)
            {
                float side = Random.value > 0.5f ? 1f : -1f;
                float x = side * Random.Range(12f, 25f);
                float z = Random.Range(-85f, 145f); // Extended Z
                Vector3 pos = new Vector3(x, 0f, z);

                float choice = Random.value;
                if (choice < 0.5f && forestTrees.Length > 0) // Standard Ghibli Forest Trees
                {
                    GameObject trPrefab = forestTrees[Random.Range(0, forestTrees.Length)];
                    if (trPrefab == null) continue;
                    GameObject tr = (GameObject)PrefabUtility.InstantiatePrefab(trPrefab);
                    tr.transform.position = pos;
                    tr.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    tr.transform.localScale = Vector3.one * Random.Range(1.3f, 2.2f);
                    tr.transform.SetParent(midGroup.transform);
                }
                else if (choice < 0.78f && shrubs.Length > 0) // Dense round shrubs
                {
                    GameObject shPrefab = shrubs[Random.Range(0, shrubs.Length)];
                    if (shPrefab == null) continue;
                    GameObject sh = (GameObject)PrefabUtility.InstantiatePrefab(shPrefab);
                    sh.transform.position = pos;
                    sh.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    sh.transform.localScale = Vector3.one * Random.Range(1.6f, 3.2f);
                    sh.transform.SetParent(midGroup.transform);
                }
                else if (mossyRocks.Length > 0) // Medium boulders
                {
                    GameObject rkPrefab = mossyRocks[Random.Range(0, mossyRocks.Length)];
                    if (rkPrefab == null) continue;
                    GameObject rk = (GameObject)PrefabUtility.InstantiatePrefab(rkPrefab);
                    rk.transform.position = pos;
                    rk.transform.rotation = Quaternion.Euler(Random.Range(-15, 15), Random.Range(0, 360f), Random.Range(-15, 15));
                    rk.transform.localScale = Vector3.one * Random.Range(1.4f, 2.5f);
                    rk.transform.SetParent(midGroup.transform);
                }
            }

            // Layer 3: Dense Background Forest Backdrop (25m to 85m from center) (Extended to 200m track end)
            GameObject forestGroup = new GameObject("BackgroundForest_Zone");
            forestGroup.transform.SetParent(envParent.transform);

            int forestCount = 280; // Scaled up for thickness
            for (int i = 0; i < forestCount; i++)
            {
                float side = Random.value > 0.5f ? 1f : -1f;
                float x = side * Random.Range(25f, 85f);
                float z = Random.Range(-100f, 150f); // Extended Z
                Vector3 pos = new Vector3(x, 0f, z);

                float choice = Random.value;
                if (choice < 0.22f && bigTreePrefab != null) // Massive Giant Oaks
                {
                    GameObject tr = (GameObject)PrefabUtility.InstantiatePrefab(bigTreePrefab);
                    tr.transform.position = pos;
                    tr.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    tr.transform.localScale = Vector3.one * Random.Range(2.8f, 4.8f);
                    tr.transform.SetParent(forestGroup.transform);
                }
                else if (choice < 0.85f && forestTrees.Length > 0) // Tall Ghibli Woods
                {
                    GameObject trPrefab = forestTrees[Random.Range(0, forestTrees.Length)];
                    if (trPrefab == null) continue;
                    GameObject tr = (GameObject)PrefabUtility.InstantiatePrefab(trPrefab);
                    tr.transform.position = pos;
                    tr.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
                    tr.transform.localScale = Vector3.one * Random.Range(2.1f, 3.8f);
                    tr.transform.SetParent(forestGroup.transform);
                }
                else if (mossyRocks.Length > 0) // Giant cliffs / boulders
                {
                    GameObject rkPrefab = mossyRocks[Random.Range(0, mossyRocks.Length)];
                    if (rkPrefab == null) continue;
                    GameObject rk = (GameObject)PrefabUtility.InstantiatePrefab(rkPrefab);
                    rk.transform.position = pos;
                    rk.transform.rotation = Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360f), Random.Range(-20, 20));
                    rk.transform.localScale = Vector3.one * Random.Range(4.5f, 7.8f);
                    rk.transform.SetParent(forestGroup.transform);
                }
            }
        }

        private static void SetupGhibliLandmarks()
        {
            GameObject propsParent = new GameObject("ScenicProps");

            // 1. Street Lamps - Lining the linear highway nicely
            string lampPath = $"{GhibliPrefabsDir}Street_Lamp.prefab";
            GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lampPath);

            if (lampPrefab != null)
            {
                // Place lamp posts every 30m on both sides
                for (float z = -70f; z <= 130f; z += 30f)
                {
                    // Left Lamp
                    GameObject lampL = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                    lampL.name = $"StreetLamp_Left_{z}";
                    lampL.transform.position = new Vector3(-4.2f, 0f, z);
                    lampL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    lampL.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                    lampL.transform.SetParent(propsParent.transform);

                    // Right Lamp
                    GameObject lampR = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                    lampR.name = $"StreetLamp_Right_{z}";
                    lampR.transform.position = new Vector3(4.2f, 0f, z);
                    lampR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    lampR.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                    lampR.transform.SetParent(propsParent.transform);
                }
            }

            // 2. Majestic Ghibli Archway over the start line (Z = -70m)
            string archPath = $"{GhibliPrefabsDir}Arch.prefab";
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(archPath);
            if (archPrefab != null)
            {
                GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab);
                arch.name = "StartLineArchway";
                arch.transform.position = new Vector3(0f, 0f, -70.2f);
                arch.transform.rotation = Quaternion.identity;
                arch.transform.localScale = new Vector3(4.8f, 4.2f, 4.2f); // Perfect span over 8m highway
                arch.transform.SetParent(propsParent.transform);
            }

            // 3. Nostalgic Ghibli Wagon parked on a scenic grass side lookout
            string wagonPath = $"{GhibliPrefabsDir}Wagon.prefab";
            GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wagonPath);
            if (wagonPrefab != null)
            {
                GameObject wagon = (GameObject)PrefabUtility.InstantiatePrefab(wagonPrefab);
                wagon.name = "NostalgicScenicWagon";
                wagon.transform.position = new Vector3(8.8f, 0.1f, -32f);
                wagon.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
                wagon.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);
                wagon.transform.SetParent(propsParent.transform);
            }

            // 4. Mysterious Ancient Altar tucked away deep in the forest
            string altarPath = $"{GhibliPrefabsDir}Altar.prefab";
            GameObject altarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(altarPath);
            if (altarPrefab != null)
            {
                GameObject altar = (GameObject)PrefabUtility.InstantiatePrefab(altarPrefab);
                altar.name = "DeepForestAltar";
                altar.transform.position = new Vector3(-38f, 0f, 28f);
                altar.transform.rotation = Quaternion.Euler(0f, 125f, 0f);
                altar.transform.localScale = new Vector3(3.2f, 3.2f, 3.2f);
                altar.transform.SetParent(propsParent.transform);
            }

            // 5. Stylized Fences bounding the scenic wagon lookout
            string fencePath = $"{GhibliPrefabsDir}Fence_01.prefab";
            GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fencePath);
            if (fencePrefab != null)
            {
                // Place 3 fences along the edge of the scenic lookout (Z from -38 to -26, X = 11)
                for (float z = -38f; z <= -24f; z += 5f)
                {
                    GameObject fence = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab);
                    fence.name = $"ScenicFence_{z}";
                    fence.transform.position = new Vector3(10.5f, 0f, z);
                    fence.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    fence.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
                    fence.transform.SetParent(propsParent.transform);
                }
            }
        }

        private static void SetupMidnightDefender()
        {
            // 1. Load the Midnight Defender FBX Model
            string fbxPath = $"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture.fbx";
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (fbxPrefab == null)
            {
                Debug.LogError($"Could not resolve Midnight Defender FBX at {fbxPath}");
                return;
            }

            // Clean missing scripts off prefab before instantiating
            CleanGameObjectRecursively(fbxPrefab);

            // 2. Instantiate Vehicle Prefab
            GameObject vehicle = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            vehicle.name = "Midnight_Defender_Vehicle";
            vehicle.SetActive(true);
            
            // Clean newly instantiated object
            CleanGameObjectRecursively(vehicle);

            // 3. Setup Custom High-End Material using original Meshy maps (configured for active render pipeline!)
            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture.png");
            Texture2D emissionTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture_emission.png");
            Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture_metallic.png");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture_normal.png");
            Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MidnightDefenderDir}Meshy_AI_Midnight_Defender_0526005608_texture_roughness.png");

            Material vehicleMat = new Material(GetBaseShader());
            vehicleMat.name = "MidnightDefender_PremiumMaterial";
            vehicleMat.color = Color.white;

            if (diffuseTex != null) vehicleMat.SetTexture("_MainTex", diffuseTex);
            
            if (metallicTex != null)
            {
                vehicleMat.SetTexture("_MetallicGlossMap", metallicTex);
                vehicleMat.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                if (vehicleMat.HasProperty("_Metallic")) vehicleMat.SetFloat("_Metallic", 0.8f);
            }

            // Set Smoothness cleanly for active shader
            if (roughnessTex != null)
            {
                if (vehicleMat.HasProperty("_Glossiness")) vehicleMat.SetFloat("_Glossiness", 0.7f);
                if (vehicleMat.HasProperty("_Smoothness")) vehicleMat.SetFloat("_Smoothness", 0.7f);
            }

            if (normalTex != null)
            {
                vehicleMat.SetTexture("_BumpMap", normalTex);
                vehicleMat.EnableKeyword("_NORMALMAP");
            }

            if (emissionTex != null)
            {
                vehicleMat.SetTexture("_EmissionMap", emissionTex);
                vehicleMat.SetColor("_EmissionColor", new Color(0.2f, 0.75f, 1.0f) * 1.5f); // Gorgeous neon blue electric glow!
                vehicleMat.EnableKeyword("_EMISSION");
            }

            // Apply premium material to all renderers recursively
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.sharedMaterial = vehicleMat;
                }
            }

            // Set scale to exactly (200f, 200f, 200f) as specified in Inspector
            vehicle.transform.localScale = new Vector3(200f, 200f, 200f);

            // Sit cleanly ON top of the elevated road at Y = 1.0f as specified in Inspector
            float yOffset = 1.0f;

            // Center of road (X=0), start of track (Z=-70)
            vehicle.transform.position = new Vector3(0f, yOffset, -70f);
            
            // Set exact rotation to X = -90, Y = -180, Z = 90 as specified in Inspector
            vehicle.transform.rotation = Quaternion.Euler(-90f, -180f, 90f);

            // Strip active colliders or rigidbodies to bypass collision lag
            foreach (Collider c in vehicle.GetComponentsInChildren<Collider>(true))
            {
                if (c != null) DestroyImmediate(c);
            }
            foreach (Rigidbody rb in vehicle.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb != null) DestroyImmediate(rb);
            }

            // 6. Attach Simulation Controller Script (configured for 200m in 20s: a = 1.0 m/s^2)
            MidnightDefenderController controller = vehicle.AddComponent<MidnightDefenderController>();
            controller.initialVelocity = 0f;
            controller.acceleration = 1.0f; // Exact 1.0 m/s^2 to cover 200m in 20s
            controller.maxSpeed = 25f;
            controller.trackStart = -70f;
            controller.trackEnd = 130f; // 200m total track end
            
            Debug.Log("[MidnightDefender] Vehicle instantiated, PBR materials aligned, and controller script attached.");
        }

        private static void SetupGentleGiant()
        {
            // 1. Load the Elephant Model
            string fbxPath = $"{GentleGiantDir}Meshy_AI_Gentle_Giant_0526023406_texture.fbx";
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (fbxPrefab == null)
            {
                Debug.LogError($"Could not resolve Gentle Giant FBX at {fbxPath}");
                return;
            }

            CleanGameObjectRecursively(fbxPrefab);

            // 2. Instantiate Elephant
            GameObject elephant = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            elephant.name = "Gentle_Giant_Elephant";
            elephant.SetActive(true);
            CleanGameObjectRecursively(elephant);

            // 3. Setup Custom PBR Material using original Meshy textures
            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GentleGiantDir}Meshy_AI_Gentle_Giant_0526023406_texture.png");
            Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GentleGiantDir}Meshy_AI_Gentle_Giant_0526023406_texture_metallic.png");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GentleGiantDir}Meshy_AI_Gentle_Giant_0526023406_texture_normal.png");
            Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GentleGiantDir}Meshy_AI_Gentle_Giant_0526023406_texture_roughness.png");

            Material mat = new Material(GetBaseShader());
            mat.name = "GentleGiant_Material";
            mat.color = Color.white;

            if (diffuseTex != null) mat.SetTexture("_MainTex", diffuseTex);
            if (metallicTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (roughnessTex != null)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            }
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Apply material to all renderers
            Renderer[] renderers = elephant.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null) r.sharedMaterial = mat;
            }

            // 4. Position and Scale exactly as requested in Inspector
            elephant.transform.localScale = new Vector3(300f, 300f, 300f);

            // Starting position: left side of the road in the jungle (X = -8m) at Y = 2m, Z = 75m
            elephant.transform.position = new Vector3(-8f, 2f, 75f);
            
            // Set exact rotation to X = -90, Y = 0, Z = -270 as specified in Inspector
            elephant.transform.rotation = Quaternion.Euler(-90f, 0f, -270f);

            // Strip colliders/rigidbodies to prevent collision physics artifacts
            foreach (Collider c in elephant.GetComponentsInChildren<Collider>(true))
            {
                if (c != null) DestroyImmediate(c);
            }
            foreach (Rigidbody rb in elephant.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb != null) DestroyImmediate(rb);
            }
            
            Debug.Log("[GentleGiant] Elephant successfully placed at Z = 85m (145m from start).");
        }

        private static Bounds GetLocalBounds(GameObject go)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
            bool boundsInitialized = false;

            if (filters != null && filters.Length > 0)
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] != null && filters[i].sharedMesh != null)
                    {
                        if (!boundsInitialized)
                        {
                            bounds = filters[i].sharedMesh.bounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(filters[i].sharedMesh.bounds);
                        }
                    }
                }
            }

            if (!boundsInitialized)
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

        private static void SetupCinematicCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
            }

            // Perfectly centered starting perspective looking through the starting archway down the highway
            cam.transform.position = new Vector3(0f, 3.2f, -78.5f); // Center of road, elevated, 8.5m back from starting line (Z = -70m)
            cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);  // Centered, downward angle looking at starting area
            
            cam.farClipPlane = 250f;
            cam.nearClipPlane = 0.1f;

            // Attach CameraFollow script so the camera smoothly tracks the car from behind
            CameraFollow follow = cam.gameObject.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<CameraFollow>();
            }
            
            GameObject vehicle = GameObject.Find("Midnight_Defender_Vehicle");
            if (vehicle != null)
            {
                follow.target = vehicle.transform;
            }
            follow.offset = new Vector3(0f, 1.6f, -5.5f); // Stay closer behind the vehicle
        }

        private static int CleanAllMissingScripts(Scene scene)
        {
            int cleanedCount = 0;
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                cleanedCount += CleanGameObjectRecursively(root);
            }
            return cleanedCount;
        }

        private static int CleanGameObjectRecursively(GameObject go)
        {
            if (go == null) return 0;

            int cleaned = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            
            foreach (Transform child in go.transform)
            {
                cleaned += CleanGameObjectRecursively(child.gameObject);
            }

            return cleaned;
        }

        private static void SetupTitlePopUp()
        {
            // 1. Create Canvas
            GameObject canvasObj = new GameObject("AccelerationTitleCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Create Text GameObject
            GameObject textObj = new GameObject("TitleText");
            textObj.transform.SetParent(canvasObj.transform, false);

            Text text = textObj.AddComponent<Text>();
            text.text = "ACCELERATION";
            
            // Load standard font
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 110;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 3. Add Outline component (white border)
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);

            // 4. Position Text RectTransform
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            // 5. Attach animated popup & fade component
            textObj.AddComponent<TitlePopUpFade>();
        }

        // Standard batch mode builder method invoked by automated systems
        public static void BuildSceneBatch()
        {
            Debug.Log("[AccelerationSceneBuilder] BatchMode execution triggered.");
            BuildScene();
            EditorApplication.Exit(0);
        }
    }
}
