
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HalloweenCryptPropsGenerator : MonoBehaviour
{
    [Header("Dimensions du plateau")]
    [SerializeField, Min(1)] private int boardSize = 8;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;

    [Header("Hauteur du sol")]
    [SerializeField] private float floorY = -0.94f;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.20f;

    [Header("Placement : pres des bordures")]
    [Tooltip("Distance entre le bord des cases et le centre des decorations")]
    [SerializeField, Min(0.35f)] private float minimumSideOffset = 0.75f;
    [SerializeField, Min(0.35f)] private float maximumSideOffset = 1.10f;
    [SerializeField, Min(0f)] private float frontZOffset = 1.15f;
    [SerializeField, Min(0f)] private float backZOffset = 1.15f;

    [Header("Decorations")]
    [SerializeField] private bool createSkulls = true;
    [SerializeField, Range(0, 8)] private int skullCount = 4;
    [SerializeField] private bool createBones = true;
    [SerializeField, Range(0, 24)] private int boneCount = 7;
    [SerializeField] private bool createRubble = true;
    [SerializeField, Range(0, 70)] private int rubbleCount = 18;
    [SerializeField, Range(0.5f, 1.6f)] private float propScale = 1.15f;
    [SerializeField] private bool subtlePurpleEyes = true;
    [SerializeField] private int seed = 20261024;

    private GameObject generatedRoot;
    private Material boneMaterial;
    private Material socketMaterial;
    private Material eyeMaterial;
    private Material[] stoneMaterials;

    private readonly List<Material> generatedMaterials =
        new List<Material>();

    private readonly List<Vector3> skullPositions =
        new List<Vector3>();

    private System.Random random;

    private void Start()
    {
        GenerateDecorations();
    }

    private void GenerateDecorations()
    {
        random = new System.Random(seed);
        skullPositions.Clear();

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = Shader.Find("Standard");

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
            unlit = Shader.Find("Unlit/Color");

        if (lit == null || unlit == null)
        {
            Debug.LogError(
                "HalloweenCryptProps : shaders introuvables.",
                this
            );
            return;
        }

        boneMaterial = MakeMaterial(
            lit,
            "Os_Ancien",
            new Color(0.76f, 0.71f, 0.65f)
        );

        socketMaterial = MakeMaterial(
            lit,
            "Orbites_Sombres",
            new Color(0.05f, 0.03f, 0.065f)
        );

        eyeMaterial = MakeMaterial(
            unlit,
            "Lueur_Oeil_Violette",
            new Color(1.0f, 0.43f, 1.7f)
        );

        // Legere emission pour rendre les os visibles
        // dans la crypte, sans ajouter de lumieres.
        if (boneMaterial.HasProperty("_EmissionColor"))
        {
            boneMaterial.EnableKeyword("_EMISSION");
            boneMaterial.SetColor(
                "_EmissionColor",
                new Color(0.045f, 0.040f, 0.05f)
            );
        }

        stoneMaterials = new[]
        {
            MakeMaterial(
                lit,
                "Pierre_Ardoise",
                new Color(0.18f, 0.18f, 0.24f)
            ),
            MakeMaterial(
                lit,
                "Pierre_Grise",
                new Color(0.25f, 0.23f, 0.29f)
            ),
            MakeMaterial(
                lit,
                "Pierre_Sombre",
                new Color(0.14f, 0.14f, 0.19f)
            )
        };

        generatedRoot = new GameObject(
            "Decorations_Halloween_Generees"
        );

        generatedRoot.transform.SetParent(transform, false);

        if (createSkulls)
        {
            Transform group = NewGroup(
                "Cranes", generatedRoot.transform
            ).transform;

            for (int i = 0; i < skullCount; i++)
            {
                Vector3 pos = GetSidePosition(i, skullCount, true);
                skullPositions.Add(pos);
                CreateSkull(group, i, pos);
            }
        }

        if (createBones)
        {
            Transform group = NewGroup(
                "Ossements", generatedRoot.transform
            ).transform;

            for (int i = 0; i < boneCount; i++)
            {
                Vector3 pos = GetSidePosition(i, boneCount, false);

                if (NearSkull(pos, 0.50f))
                    pos.z += i % 2 == 0 ? 0.55f : -0.55f;

                CreateBone(group, i, pos);
            }
        }

        if (createRubble)
        {
            Transform group = NewGroup(
                "Pierres_Cassees",
                generatedRoot.transform
            ).transform;

            for (int i = 0; i < rubbleCount; i++)
            {
                Vector3 pos = GetSidePosition(i, rubbleCount, false);

                if (NearSkull(pos, 0.45f))
                    pos.z += i % 2 == 0 ? 0.48f : -0.48f;

                CreateRubble(group, i, pos);
            }
        }
    }

    private Vector3 GetSidePosition(
        int index,
        int count,
        bool evenlySpaced)
    {
        float center =
            (boardSize - 1) * tileSize * 0.5f;

        float halfBoard =
            boardSize * tileSize * 0.5f;

        float min = Mathf.Max(0.35f, minimumSideOffset);
        float max = Mathf.Max(min, maximumSideOffset);

        float sideOffset = Mathf.Lerp(
            min, max, (float)random.NextDouble()
        );

        bool left = index % 2 == 0;

        float x = center +
            (left ? -1f : 1f) * (halfBoard + sideOffset);

        float start =
            center - halfBoard + frontZOffset;

        float end =
            center + halfBoard - backZOffset;

        if (end < start)
        {
            start = center - halfBoard;
            end = center + halfBoard;
        }

        float t = evenlySpaced
            ? (index + 0.65f) / (count + 0.3f)
            : (float)random.NextDouble();

        float z = Mathf.Lerp(
            start, end, Mathf.Clamp01(t)
        );

        float groundTop =
            floorY + floorThickness * 0.5f;

        return new Vector3(
            x, groundTop + 0.035f, z
        );
    }

    private bool NearSkull(Vector3 pos, float distance)
    {
        foreach (Vector3 skull in skullPositions)
        {
            Vector2 a = new Vector2(pos.x, pos.z);
            Vector2 b = new Vector2(skull.x, skull.z);

            if (Vector2.Distance(a, b) < distance * propScale)
                return true;
        }

        return false;
    }

    private void CreateSkull(
        Transform parent,
        int index,
        Vector3 position)
    {
        GameObject skull = NewGroup(
            "Crane_" + index, parent
        );

        skull.transform.localPosition = position;

        skull.transform.localRotation = Quaternion.Euler(
            0f,
            Range(-28f, 28f),
            Range(-7f, 7f)
        );

        skull.transform.localScale =
            Vector3.one * propScale;

        Primitive(
            "Calotte", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(0f, 0.30f, 0.025f),
            new Vector3(0.48f, 0.43f, 0.43f),
            boneMaterial
        );

        Primitive(
            "Machoire", PrimitiveType.Cube,
            skull.transform,
            new Vector3(0f, 0.13f, -0.075f),
            new Vector3(0.32f, 0.14f, 0.27f),
            boneMaterial
        );

        Primitive(
            "Pommette_Gauche", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(-0.17f, 0.20f, -0.105f),
            new Vector3(0.16f, 0.13f, 0.15f),
            boneMaterial
        );

        Primitive(
            "Pommette_Droite", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(0.17f, 0.20f, -0.105f),
            new Vector3(0.16f, 0.13f, 0.15f),
            boneMaterial
        );

        Primitive(
            "Orbite_Gauche", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(-0.104f, 0.305f, -0.192f),
            new Vector3(0.128f, 0.123f, 0.043f),
            socketMaterial
        );

        Primitive(
            "Orbite_Droite", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(0.104f, 0.305f, -0.192f),
            new Vector3(0.128f, 0.123f, 0.043f),
            socketMaterial
        );

        Primitive(
            "Nez", PrimitiveType.Sphere,
            skull.transform,
            new Vector3(0f, 0.225f, -0.222f),
            new Vector3(0.055f, 0.073f, 0.023f),
            socketMaterial
        );

        for (int i = 0; i < 5; i++)
        {
            Primitive(
                "Dent_" + i,
                PrimitiveType.Cube,
                skull.transform,
                new Vector3(
                    (i - 2) * 0.054f,
                    0.103f,
                    -0.216f
                ),
                new Vector3(0.042f, 0.07f, 0.026f),
                boneMaterial
            );
        }

        if (subtlePurpleEyes && index % 2 == 0)
        {
            Primitive(
                "Reflet_Gauche", PrimitiveType.Sphere,
                skull.transform,
                new Vector3(-0.104f, 0.305f, -0.222f),
                new Vector3(0.027f, 0.028f, 0.013f),
                eyeMaterial
            );

            Primitive(
                "Reflet_Droit", PrimitiveType.Sphere,
                skull.transform,
                new Vector3(0.104f, 0.305f, -0.222f),
                new Vector3(0.027f, 0.028f, 0.013f),
                eyeMaterial
            );
        }
    }

    private void CreateBone(
        Transform parent,
        int index,
        Vector3 position)
    {
        GameObject bone = NewGroup(
            "Os_" + index, parent
        );

        bone.transform.localPosition = position;

        bone.transform.localRotation = Quaternion.Euler(
            0f, Range(0f, 360f), 0f
        );

        bone.transform.localScale =
            Vector3.one * propScale;

        float length = Range(0.32f, 0.57f);
        float half = length * 0.5f;

        const float y = 0.057f;

        GameObject shaft = Primitive(
            "Tige", PrimitiveType.Cylinder,
            bone.transform,
            new Vector3(0f, y, 0f),
            new Vector3(0.077f, length * 0.5f, 0.077f),
            boneMaterial
        );

        shaft.transform.localRotation =
            Quaternion.Euler(0f, 0f, 90f);

        foreach (int sign in new[] { -1, 1 })
        {
            Primitive(
                "Articulation_" + sign + "_A",
                PrimitiveType.Sphere,
                bone.transform,
                new Vector3(sign * half, y, -0.043f),
                new Vector3(0.15f, 0.12f, 0.12f),
                boneMaterial
            );

            Primitive(
                "Articulation_" + sign + "_B",
                PrimitiveType.Sphere,
                bone.transform,
                new Vector3(sign * half, y, 0.043f),
                new Vector3(0.15f, 0.12f, 0.12f),
                boneMaterial
            );
        }
    }

    private void CreateRubble(
        Transform parent,
        int index,
        Vector3 pos)
    {
        float width = Range(0.10f, 0.27f) * propScale;
        float height = Range(0.055f, 0.18f) * propScale;
        float depth = Range(0.10f, 0.28f) * propScale;

        GameObject stone = Primitive(
            "Pierre_" + index,
            PrimitiveType.Cube,
            parent,
            pos + new Vector3(0f, height * 0.45f, 0f),
            new Vector3(width, height, depth),
            stoneMaterials[
                random.Next(stoneMaterials.Length)
            ]
        );

        stone.transform.localRotation = Quaternion.Euler(
            Range(-13f, 13f),
            Range(0f, 180f),
            Range(-13f, 13f)
        );
    }

    private Material MakeMaterial(
        Shader shader,
        string name,
        Color color)
    {
        Material mat = new Material(shader)
        {
            name = name + "_Runtime"
        };

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.12f);

        generatedMaterials.Add(mat);

        return mat;
    }

    private static GameObject NewGroup(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject Primitive(
        string name,
        PrimitiveType type,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);

        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Collider collider = go.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        return go;
    }

    private float Range(float min, float max)
    {
        return Mathf.Lerp(
            min, max, (float)random.NextDouble()
        );
    }

    private void OnDestroy()
    {
        if (generatedRoot != null)
            Destroy(generatedRoot);

        foreach (Material material in generatedMaterials)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
