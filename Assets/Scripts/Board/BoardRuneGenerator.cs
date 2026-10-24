
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoardRuneGenerator : MonoBehaviour
{
    [Header("Plateau")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Header("Gravures")]
    [SerializeField, Range(1, 12)]
    private int runesPerSide = 7;

    [SerializeField, Range(0.08f, 0.16f)]
    private float runeSize = 0.13f;

    [SerializeField, Range(0.025f, 0.065f)]
    private float lineWidth = 0.046f;

    [SerializeField] private float runeHeight = 0.315f;
    [SerializeField] private int seed = 12345;

    [Header("Magie")]
    [SerializeField] private bool animateRunes = true;

    [SerializeField, Range(0.1f, 2f)]
    private float pulseSpeed = 0.65f;

    [SerializeField, Range(0.5f, 8f)]
    private float glowIntensity = 2.8f;

    private readonly List<GameObject> spawned =
        new List<GameObject>();

    private readonly List<Mesh> generatedMeshes =
        new List<Mesh>();

    private Material auraMaterial;
    private Material coreMaterial;

    private void Start()
    {
        GenerateRunes();
    }

    private void GenerateRunes()
    {
        if (boardSize < 1 || tileSize <= 0f)
            return;

        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError(
                "Shader Unlit introuvable.",
                this
            );
            return;
        }

        auraMaterial = new Material(shader)
        {
            name = "Runes - Aura (runtime)"
        };

        coreMaterial = new Material(shader)
        {
            name = "Runes - Coeur (runtime)"
        };

        SetMaterialColor(
            auraMaterial,
            new Color(0.40f, 0.07f, 0.85f)
        );

        SetMaterialColor(
            coreMaterial,
            new Color(0.92f, 0.72f, 1f)
        );

        Mesh[] auraMeshes = new Mesh[12];
        Mesh[] coreMeshes = new Mesh[12];

        for (int i = 0; i < 12; i++)
        {
            auraMeshes[i] = CreateGlyphMesh(
                i, lineWidth * 1.8f
            );

            coreMeshes[i] = CreateGlyphMesh(
                i, lineWidth * 0.78f
            );

            generatedMeshes.Add(auraMeshes[i]);
            generatedMeshes.Add(coreMeshes[i]);
        }

        float size = boardSize * tileSize;

        float center =
            (boardSize - 1) * tileSize * 0.5f;

        float edge = size * 0.5f + 0.22f;

        int count = 4 * runesPerSide;

        System.Random random =
            new System.Random(seed);

        List<int> glyphs = new List<int>(count);

        for (int i = 0; i < count; i++)
            glyphs.Add(i % 12);

        for (int i = glyphs.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            int tmp = glyphs[i];
            glyphs[i] = glyphs[j];
            glyphs[j] = tmp;
        }

        for (int i = 1; i < glyphs.Count; i++)
        {
            if (glyphs[i] != glyphs[i - 1])
                continue;

            for (int j = i + 1; j < glyphs.Count; j++)
            {
                if (glyphs[j] == glyphs[i - 1])
                    continue;

                int tmp = glyphs[i];
                glyphs[i] = glyphs[j];
                glyphs[j] = tmp;
                break;
            }
        }

        for (int side = 0; side < 4; side++)
        {
            for (int i = 0; i < runesPerSide; i++)
            {
                float t =
                    (i + 1f) / (runesPerSide + 1f);

                float along =
                    center - size * 0.5f + t * size;

                Vector3 position;

                switch (side)
                {
                    case 0:
                        position = new Vector3(
                            along,
                            runeHeight,
                            center - edge
                        );
                        break;

                    case 1:
                        position = new Vector3(
                            along,
                            runeHeight,
                            center + edge
                        );
                        break;

                    case 2:
                        position = new Vector3(
                            center - edge,
                            runeHeight,
                            along
                        );
                        break;

                    default:
                        position = new Vector3(
                            center + edge,
                            runeHeight,
                            along
                        );
                        break;
                }

                int type = glyphs[
                    side * runesPerSide + i
                ];

                GameObject rune = new GameObject(
                    $"Rune_{side}_{i}"
                );

                rune.transform.SetParent(
                    transform, false
                );

                rune.transform.localPosition = position;

                rune.transform.localRotation =
                    side >= 2
                    ? Quaternion.Euler(0f, 90f, 0f)
                    : Quaternion.identity;

                spawned.Add(rune);

                MeshRenderer aura = AddLayer(
                    rune.transform,
                    "Aura",
                    auraMeshes[type],
                    auraMaterial,
                    0f
                );

                MeshRenderer core = AddLayer(
                    rune.transform,
                    "Coeur",
                    coreMeshes[type],
                    coreMaterial,
                    0.006f
                );

                RunePulse pulse =
                    rune.AddComponent<RunePulse>();

                pulse.Configure(
                    aura,
                    core,
                    animateRunes,
                    pulseSpeed,
                    glowIntensity,
                    (float)random.NextDouble()
                        * Mathf.PI * 2f
                );
            }
        }
    }

    private static MeshRenderer AddLayer(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        float height)
    {
        GameObject layer = new GameObject(name);

        layer.transform.SetParent(parent, false);

        layer.transform.localPosition =
            new Vector3(0f, height, 0f);

        MeshFilter filter =
            layer.AddComponent<MeshFilter>();

        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            layer.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        return renderer;
    }

    private static void SetMaterialColor(
        Material material,
        Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private Mesh CreateGlyphMesh(
        int type,
        float width)
    {
        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        float s = runeSize;

        void L(
            float x1, float z1,
            float x2, float z2)
        {
            AddStroke(
                vertices,
                triangles,
                new Vector2(x1 * s, z1 * s),
                new Vector2(x2 * s, z2 * s),
                width
            );
        }

        switch (type)
        {
            case 0: // Trident
                L(0, -1, 0, 1);
                L(-0.85f, 0.85f, -0.85f, 0.10f);
                L(-0.85f, 0.10f, 0, -0.20f);
                L(0.85f, 0.85f, 0.85f, 0.10f);
                L(0.85f, 0.10f, 0, -0.20f);
                break;

            case 1: // Sablier
                L(-0.8f, 0.9f, 0.8f, 0.9f);
                L(-0.8f, 0.9f, 0.8f, -0.9f);
                L(0.8f, 0.9f, -0.8f, -0.9f);
                L(-0.8f, -0.9f, 0.8f, -0.9f);
                break;

            case 2: // Croissant anguleux
                L(0.55f, 1, -0.55f, 0.45f);
                L(-0.55f, 0.45f, -0.55f, -0.45f);
                L(-0.55f, -0.45f, 0.55f, -1);
                L(0.55f, -1, 0.15f, -0.35f);
                L(0.15f, -0.35f, 0.15f, 0.35f);
                L(0.15f, 0.35f, 0.55f, 1);
                break;

            case 3: // Oeil
                L(-1, 0, 0, 0.75f);
                L(0, 0.75f, 1, 0);
                L(1, 0, 0, -0.75f);
                L(0, -0.75f, -1, 0);
                L(0, -0.40f, 0, 0.40f);
                break;

            case 4: // Croix des anciens
                L(0, 1, 0, -1);
                L(-0.9f, 0.4f, 0.9f, 0.4f);
                L(-0.6f, -0.5f, 0.6f, -0.5f);
                break;

            case 5: // Fourche
                L(0, -1, 0, 0.1f);
                L(0, 0.1f, -0.9f, 0.95f);
                L(0, 0.1f, 0.9f, 0.95f);
                L(-0.55f, -0.4f, 0.55f, -0.4f);
                break;

            case 6: // Triangle scellé
                L(0, 1, -0.9f, -0.8f);
                L(-0.9f, -0.8f, 0.9f, -0.8f);
                L(0.9f, -0.8f, 0, 1);
                L(0, 1, 0, -0.8f);
                L(-0.45f, 0.05f, 0.45f, 0.05f);
                break;

            case 7: // Racines
                L(0, 1, 0, -1);
                L(0, 0.5f, -0.8f, 0.95f);
                L(0, 0.5f, 0.8f, 0.95f);
                L(0, -0.2f, -0.7f, -0.85f);
                L(0, -0.2f, 0.7f, -0.85f);
                break;

            case 8: // Portail
                L(-0.8f, -0.9f, -0.8f, 0.7f);
                L(-0.8f, 0.7f, 0, 1);
                L(0, 1, 0.8f, 0.7f);
                L(0.8f, 0.7f, 0.8f, -0.9f);
                L(-0.4f, -0.2f, 0.4f, -0.2f);
                break;

            case 9: // Etoile
                L(0, 1, 0.45f, 0);
                L(0.45f, 0, 0, -1);
                L(0, -1, -0.45f, 0);
                L(-0.45f, 0, 0, 1);
                L(-0.9f, 0, 0.9f, 0);
                break;

            case 10: // Rune lunaire
                L(-0.75f, 0.9f, 0.45f, 0.9f);
                L(-0.75f, 0.9f, -0.75f, -0.9f);
                L(-0.75f, -0.9f, 0.45f, -0.9f);
                L(0.45f, 0.9f, -0.15f, 0);
                L(-0.15f, 0, 0.45f, -0.9f);
                break;

            default: // Lame
                L(0, 1, -0.65f, 0.15f);
                L(-0.65f, 0.15f, 0, -0.15f);
                L(0, -0.15f, 0.65f, 0.15f);
                L(0.65f, 0.15f, 0, 1);
                L(0, -0.15f, 0, -1);
                L(-0.65f, -0.65f, 0.65f, -0.65f);
                break;
        }

        Mesh mesh = new Mesh
        {
            name = "MagicalRune_" + type
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        Vector3[] normals =
            new Vector3[vertices.Count];

        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;

        mesh.normals = normals;
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddStroke(
        List<Vector3> vertices,
        List<int> triangles,
        Vector2 start,
        Vector2 end,
        float width)
    {
        Vector2 delta = end - start;

        if (delta.sqrMagnitude < 0.000001f)
            return;

        Vector2 n = new Vector2(
            -delta.y,
            delta.x
        ).normalized * width * 0.5f;

        int a = vertices.Count;

        vertices.Add(new Vector3(
            start.x - n.x, 0, start.y - n.y
        ));

        vertices.Add(new Vector3(
            start.x + n.x, 0, start.y + n.y
        ));

        vertices.Add(new Vector3(
            end.x + n.x, 0, end.y + n.y
        ));

        vertices.Add(new Vector3(
            end.x - n.x, 0, end.y - n.y
        ));

        // Face supérieure
        triangles.Add(a);
        triangles.Add(a + 1);
        triangles.Add(a + 2);

        triangles.Add(a);
        triangles.Add(a + 2);
        triangles.Add(a + 3);

        // Face inférieure
        triangles.Add(a);
        triangles.Add(a + 2);
        triangles.Add(a + 1);

        triangles.Add(a);
        triangles.Add(a + 3);
        triangles.Add(a + 2);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in generatedMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }

        if (auraMaterial != null)
            Destroy(auraMaterial);

        if (coreMaterial != null)
            Destroy(coreMaterial);

        foreach (GameObject rune in spawned)
        {
            if (rune != null)
                Destroy(rune);
        }
    }
}
