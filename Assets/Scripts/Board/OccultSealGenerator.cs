
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class OccultSealGenerator : MonoBehaviour
{
    [Header("Plateau")]
    [SerializeField, Min(1)] private int boardSize = 8;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;

    [Header("Hauteur du sol")]
    [SerializeField] private float floorY = -0.94f;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.20f;

    [Tooltip("Hauteur au-dessus des dalles")]
    [SerializeField, Range(0.02f, 0.15f)]
    private float sealYOffset = 0.055f;

    [Header("Cadre rituel")]
    [SerializeField, Range(0.60f, 1.40f)]
    private float frameMargin = 0.74f;

    [SerializeField, Range(0.20f, 0.75f)]
    private float secondFrameGap = 0.36f;

    [SerializeField, Range(0.30f, 1.20f)]
    private float bevelSize = 0.65f;

    [SerializeField, Range(0.015f, 0.09f)]
    private float lineWidth = 0.039f;

    [Header("Fissures sur les dalles")]
    [SerializeField] private bool createCracks = true;

    [SerializeField, Range(0, 24)]
    private int crackCount = 10;

    [SerializeField, Range(0.15f, 1.0f)]
    private float crackLength = 0.55f;

    [SerializeField] private int seed = 24680;

    [Header("Magie subtile")]
    [SerializeField] private bool animateSeal = true;

    [SerializeField, Range(0.1f, 2.0f)]
    private float pulseSpeed = 0.42f;

    [SerializeField, Range(0f, 0.6f)]
    private float pulseAmplitude = 0.24f;

    [SerializeField, Range(0.3f, 2.0f)]
    private float brightness = 1.0f;

    [SerializeField]
    private Color violet =
        new Color(0.55f, 0.27f, 0.95f, 1f);

    [SerializeField]
    private Color ember =
        new Color(0.90f, 0.38f, 0.12f, 1f);

    private GameObject generatedRoot;

    private readonly List<Mesh> generatedMeshes =
        new List<Mesh>();

    private readonly List<Material> generatedMaterials =
        new List<Material>();

    private readonly List<MeshRenderer> purpleRenderers =
        new List<MeshRenderer>();

    private readonly List<MeshRenderer> emberRenderers =
        new List<MeshRenderer>();

    private readonly List<MaterialPropertyBlock> purpleBlocks =
        new List<MaterialPropertyBlock>();

    private readonly List<MaterialPropertyBlock> emberBlocks =
        new List<MaterialPropertyBlock>();

    private void Start()
    {
        Generate();
    }

    private void Generate()
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError(
                "OccultSeal : shader Unlit introuvable.",
                this
            );
            return;
        }

        generatedRoot = new GameObject(
            "Sceau_Rituel_Grave"
        );

        generatedRoot.transform.SetParent(
            transform, false
        );

        float center =
            (boardSize - 1) * tileSize * 0.5f;

        float halfBoard =
            boardSize * tileSize * 0.5f;

        float inner = halfBoard + frameMargin;
        float outer = inner + secondFrameGap;

        float y = floorY +
            floorThickness * 0.5f +
            sealYOffset;

        Vector2 c = new Vector2(center, center);

        StrokeMesh purpleHalo = new StrokeMesh();
        StrokeMesh purpleCore = new StrokeMesh();

        StrokeMesh amberHalo = new StrokeMesh();
        StrokeMesh amberCore = new StrokeMesh();

        Action<Vector2, Vector2, float> drawViolet =
            (a, b, w) =>
            {
                purpleHalo.Add(
                    a, b, y, w * 2.0f
                );

                purpleCore.Add(
                    a, b, y + 0.004f, w
                );
            };

        Action<Vector2, Vector2, float> drawAmber =
            (a, b, w) =>
            {
                amberHalo.Add(
                    a, b, y + 0.002f, w * 1.9f
                );

                amberCore.Add(
                    a, b, y + 0.006f, w
                );
            };

        Vector2[] innerOutline =
            CreateBeveledSquare(
                c, inner, bevelSize
            );

        Vector2[] outerOutline =
            CreateBeveledSquare(
                c,
                outer,
                bevelSize + secondFrameGap * 0.35f
            );

        // Gravure principale continue
        for (int i = 0; i < innerOutline.Length; i++)
        {
            drawViolet(
                innerOutline[i],
                innerOutline[
                    (i + 1) % innerOutline.Length
                ],
                lineWidth
            );
        }

        // Deuxieme contour fragmente
        for (int i = 0; i < outerOutline.Length; i++)
        {
            Vector2 a = outerOutline[i];
            Vector2 b = outerOutline[
                (i + 1) % outerOutline.Length
            ];

            drawViolet(
                Vector2.Lerp(a, b, 0.10f),
                Vector2.Lerp(a, b, 0.43f),
                lineWidth * 0.62f
            );

            drawViolet(
                Vector2.Lerp(a, b, 0.57f),
                Vector2.Lerp(a, b, 0.90f),
                lineWidth * 0.62f
            );
        }

        // Quatre petits symboles ambres
        float mid = (inner + outer) * 0.5f;

        Vector2[] directions =
        {
            new Vector2(0f, -1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f)
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 radial = directions[i];

            Vector2 tangent = new Vector2(
                -radial.y,
                radial.x
            );

            Vector2 p = c + radial * mid;

            float r = Mathf.Min(
                0.13f,
                secondFrameGap * 0.33f
            );

            Vector2 top = p + radial * r;
            Vector2 bottom = p - radial * r;
            Vector2 left = p - tangent * r;
            Vector2 right = p + tangent * r;

            drawAmber(
                top, right, lineWidth * 0.72f
            );

            drawAmber(
                right, bottom, lineWidth * 0.72f
            );

            drawAmber(
                bottom, left, lineWidth * 0.72f
            );

            drawAmber(
                left, top, lineWidth * 0.72f
            );
        }

        // Fissures dirigees vers l'exterieur
        if (createCracks && crackCount > 0)
        {
            System.Random random =
                new System.Random(seed);

            for (int i = 0; i < crackCount; i++)
            {
                int side = i % 4;

                Vector2 outward = directions[side];

                Vector2 tangent = new Vector2(
                    -outward.y,
                    outward.x
                );

                float along =
                    ((float)random.NextDouble()
                    * 1.6f - 0.8f) * halfBoard;

                Vector2 start =
                    c +
                    outward * (outer + 0.09f) +
                    tangent * along;

                Vector2 current = start;

                int segments = random.Next(2, 4);

                float length = crackLength *
                    (0.7f + 0.6f *
                    (float)random.NextDouble());

                for (int s = 0; s < segments; s++)
                {
                    Vector2 next =
                        current +
                        outward * (length / segments) +
                        tangent *
                        (((float)random.NextDouble()
                        - 0.5f) * 0.20f);

                    drawViolet(
                        current,
                        next,
                        lineWidth *
                        (0.60f - s * 0.10f)
                    );

                    current = next;
                }
            }
        }

        Material purpleHaloMaterial =
            NewMaterial(shader, "Sceau_Aura_Violette");

        Material purpleCoreMaterial =
            NewMaterial(shader, "Sceau_Trait_Violet");

        Material amberHaloMaterial =
            NewMaterial(shader, "Sceau_Aura_Ambree");

        Material amberCoreMaterial =
            NewMaterial(shader, "Sceau_Trait_Ambre");

        CreateMeshObject(
            "Aura_Violette",
            purpleHalo,
            purpleHaloMaterial,
            purpleRenderers
        );

        CreateMeshObject(
            "Gravure_Violette",
            purpleCore,
            purpleCoreMaterial,
            purpleRenderers
        );

        CreateMeshObject(
            "Aura_Ambree",
            amberHalo,
            amberHaloMaterial,
            emberRenderers
        );

        CreateMeshObject(
            "Glyphes_Ambres",
            amberCore,
            amberCoreMaterial,
            emberRenderers
        );

        foreach (MeshRenderer renderer in purpleRenderers)
        {
            purpleBlocks.Add(
                new MaterialPropertyBlock()
            );
        }

        foreach (MeshRenderer renderer in emberRenderers)
        {
            emberBlocks.Add(
                new MaterialPropertyBlock()
            );
        }

        ApplyColors(1f);
    }

    private static Vector2[] CreateBeveledSquare(
        Vector2 c,
        float h,
        float bevel)
    {
        float b = Mathf.Clamp(
            bevel,
            0.1f,
            h * 0.5f
        );

        return new[]
        {
            c + new Vector2(-h + b, -h),
            c + new Vector2( h - b, -h),
            c + new Vector2( h, -h + b),
            c + new Vector2( h,  h - b),
            c + new Vector2( h - b,  h),
            c + new Vector2(-h + b,  h),
            c + new Vector2(-h,  h - b),
            c + new Vector2(-h, -h + b)
        };
    }

    private Material NewMaterial(
        Shader shader,
        string name)
    {
        Material material = new Material(shader);
        material.name = name;

        generatedMaterials.Add(material);

        return material;
    }

    private void CreateMeshObject(
        string name,
        StrokeMesh source,
        Material material,
        List<MeshRenderer> renderers)
    {
        Mesh mesh = source.Build(name + "_Mesh");

        if (mesh == null)
            return;

        generatedMeshes.Add(mesh);

        GameObject obj = new GameObject(name);

        obj.transform.SetParent(
            generatedRoot.transform,
            false
        );

        MeshFilter filter =
            obj.AddComponent<MeshFilter>();

        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            obj.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        renderers.Add(renderer);
    }

    private void Update()
    {
        if (generatedRoot == null || !animateSeal)
            return;

        float wave = 0.5f + 0.5f *
            Mathf.Sin(
                Time.time *
                pulseSpeed *
                Mathf.PI * 2f
            );

        float pulse = Mathf.Lerp(
            1f - pulseAmplitude,
            1f + pulseAmplitude,
            wave
        );

        ApplyColors(pulse);
    }

    private void ApplyColors(float pulse)
    {
        for (int i = 0; i < purpleRenderers.Count; i++)
        {
            bool halo = i == 0;

            Color color = halo
                ? new Color(
                    violet.r * 0.26f,
                    violet.g * 0.26f,
                    violet.b * 0.26f
                )
                : violet;

            SetRendererColor(
                purpleRenderers[i],
                purpleBlocks[i],
                color * brightness * pulse
            );
        }

        for (int i = 0; i < emberRenderers.Count; i++)
        {
            bool halo = i == 0;

            Color color = halo
                ? new Color(
                    ember.r * 0.22f,
                    ember.g * 0.22f,
                    ember.b * 0.22f
                )
                : ember * 0.72f;

            SetRendererColor(
                emberRenderers[i],
                emberBlocks[i],
                color * brightness * pulse
            );
        }
    }

    private static void SetRendererColor(
        MeshRenderer renderer,
        MaterialPropertyBlock block,
        Color color)
    {
        if (renderer == null)
            return;

        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);

        renderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in generatedMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }

        foreach (Material material in generatedMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        if (generatedRoot != null)
            Destroy(generatedRoot);
    }

    private sealed class StrokeMesh
    {
        private readonly List<Vector3> vertices =
            new List<Vector3>();

        private readonly List<int> triangles =
            new List<int>();

        private readonly List<Vector2> uvs =
            new List<Vector2>();

        public void Add(
            Vector2 a,
            Vector2 b,
            float y,
            float width)
        {
            Vector2 delta = b - a;

            if (delta.sqrMagnitude < 0.0000001f)
                return;

            Vector2 normal = new Vector2(
                -delta.y,
                delta.x
            ).normalized *
            Mathf.Max(0.004f, width) * 0.5f;

            int n = vertices.Count;

            vertices.Add(new Vector3(
                a.x - normal.x,
                y,
                a.y - normal.y
            ));

            vertices.Add(new Vector3(
                a.x + normal.x,
                y,
                a.y + normal.y
            ));

            vertices.Add(new Vector3(
                b.x + normal.x,
                y,
                b.y + normal.y
            ));

            vertices.Add(new Vector3(
                b.x - normal.x,
                y,
                b.y - normal.y
            ));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            // Triangles visibles des deux cotes
            triangles.Add(n);
            triangles.Add(n + 1);
            triangles.Add(n + 2);

            triangles.Add(n);
            triangles.Add(n + 2);
            triangles.Add(n + 3);

            triangles.Add(n);
            triangles.Add(n + 2);
            triangles.Add(n + 1);

            triangles.Add(n);
            triangles.Add(n + 3);
            triangles.Add(n + 2);
        }

        public Mesh Build(string meshName)
        {
            if (vertices.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.name = meshName;

            if (vertices.Count > 65000)
            {
                mesh.indexFormat =
                    IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);

            Vector3[] normals =
                new Vector3[vertices.Count];

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
