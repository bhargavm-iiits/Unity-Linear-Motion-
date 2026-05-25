using UnityEngine;

public class EnvironmentGenerator : MonoBehaviour
{
    [Header("Environment Settings")]
    public float planeSize = 100f;
    public int numberOfTrees = 100;
    public float treeScaleMin = 0.5f;
    public float treeScaleMax = 2f;

    void Start()
    {
        GeneratePlane();
        GenerateTrees();
    }

    void GeneratePlane()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = new Vector3(planeSize / 10f, 1f, planeSize / 10f); // Unity plane default size is 10x10
        plane.name = "GroundPlane";

        // Optional: Set to a green color if material allows
        Renderer renderer = plane.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.2f, 0.6f, 0.2f);
            renderer.material = groundMat;
        }
    }

    void GenerateTrees()
    {
        GameObject treeParent = new GameObject("Trees");

        for (int i = 0; i < numberOfTrees; i++)
        {
            float randomX = Random.Range(-planeSize / 2f, planeSize / 2f);
            float randomZ = Random.Range(-planeSize / 2f, planeSize / 2f);
            Vector3 position = new Vector3(randomX, 0, randomZ);

            CreateTree(position, treeParent.transform);
        }
    }

    void CreateTree(Vector3 position, Transform parent)
    {
        GameObject tree = new GameObject("Tree");
        tree.transform.position = position;
        tree.transform.parent = parent;
        float randomScale = Random.Range(treeScaleMin, treeScaleMax);
        tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

        // Trunk
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.parent = tree.transform;
        trunk.transform.localPosition = new Vector3(0, 1f, 0); // half height
        trunk.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        
        Renderer trunkRenderer = trunk.GetComponent<Renderer>();
        if (trunkRenderer != null)
        {
            Material trunkMat = new Material(Shader.Find("Standard"));
            trunkMat.color = new Color(0.4f, 0.2f, 0.1f);
            trunkRenderer.material = trunkMat;
        }

        // Leaves
        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.transform.parent = tree.transform;
        leaves.transform.localPosition = new Vector3(0, 2.5f, 0);
        leaves.transform.localScale = new Vector3(2f, 2f, 2f);

        Renderer leavesRenderer = leaves.GetComponent<Renderer>();
        if (leavesRenderer != null)
        {
            Material leavesMat = new Material(Shader.Find("Standard"));
            leavesMat.color = new Color(0.1f, 0.5f, 0.1f);
            leavesRenderer.material = leavesMat;
        }
    }
}
