
using UnityEngine;

public class BoardBaseGenerator : MonoBehaviour
{
    [Header("Dimensions")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Header("Matériaux")]
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Material borderMaterial;

    private void Start()
    {
        GenerateBase();
    }

    private void GenerateBase()
    {
        float size = boardSize * tileSize;
        float center = (boardSize - 1) * tileSize / 2f;

        // Base principale
        CreateBox(
            "MainStoneBase",
            new Vector3(center, -0.35f, center),
            new Vector3(size + 0.9f, 0.5f, size + 0.9f),
            baseMaterial
        );

        // Moulure inférieure
        CreateBox(
            "LowerMolding",
            new Vector3(center, -0.65f, center),
            new Vector3(size + 1.1f, 0.12f, size + 1.1f),
            borderMaterial
        );

        // Moulure supérieure
        CreateBox(
            "UpperMolding",
            new Vector3(center, -0.04f, center),
            new Vector3(size + 0.75f, 0.12f, size + 0.75f),
            borderMaterial
        );

        // Bordures supérieures
        float offset = size / 2f + 0.22f;
        float borderY = 0.16f;

        CreateBox(
            "FrontBorder",
            new Vector3(center, borderY, center - offset),
            new Vector3(size + 0.85f, 0.22f, 0.35f),
            borderMaterial
        );

        CreateBox(
            "BackBorder",
            new Vector3(center, borderY, center + offset),
            new Vector3(size + 0.85f, 0.22f, 0.35f),
            borderMaterial
        );

        CreateBox(
            "LeftBorder",
            new Vector3(center - offset, borderY, center),
            new Vector3(0.35f, 0.22f, size),
            borderMaterial
        );

        CreateBox(
            "RightBorder",
            new Vector3(center + offset, borderY, center),
            new Vector3(0.35f, 0.22f, size),
            borderMaterial
        );

        // Piliers aux quatre angles
        float corner = size / 2f + 0.25f;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                CreateBox(
                    $"CornerPillar_{x}_{z}",
                    new Vector3(
                        center + x * corner,
                        0.08f,
                        center + z * corner
                    ),
                    new Vector3(0.42f, 0.55f, 0.42f),
                    baseMaterial
                );
            }
        }
    }

    private void CreateBox(
        string objectName,
        Vector3 position,
        Vector3 size,
        Material material)
    {
        GameObject obj = new GameObject(objectName);

        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = position;

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();

        filter.sharedMesh = CreateBoxMesh(size);
        renderer.sharedMaterial = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = size;
    }

    private Mesh CreateBoxMesh(Vector3 size)
    {
        float x = size.x / 2f;
        float y = size.y / 2f;
        float z = size.z / 2f;

        Vector3[] corners =
        {
            new Vector3(-x,-y,-z),
            new Vector3(x,-y,-z),
            new Vector3(x,-y,z),
            new Vector3(-x,-y,z),
            new Vector3(-x,y,-z),
            new Vector3(x,y,-z),
            new Vector3(x,y,z),
            new Vector3(-x,y,z)
        };

        int[,] faces =
        {
            {4,7,6,5},
            {1,2,3,0},
            {0,4,5,1},
            {2,6,7,3},
            {3,7,4,0},
            {1,5,6,2}
        };

        Vector3[] vertices = new Vector3[24];
        int[] triangles = new int[36];

        for (int f = 0; f < 6; f++)
        {
            int v = f * 4;
            int t = f * 6;

            for (int i = 0; i < 4; i++)
                vertices[v + i] = corners[faces[f, i]];

            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;

            triangles[t + 3] = v;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = "GothicProceduralMesh";

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
