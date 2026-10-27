
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HauntedWispsGenerator : MonoBehaviour
{
    [Header("Echiquier")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Header("Placement hors du plateau")]
    [SerializeField] private float sideDistance = 1.65f;
    [SerializeField] private float rearDistance = 2.2f;
    [SerializeField] private float flightHeight = 1.05f;
    [SerializeField] private float sidePathRadius = 1.45f;

    [Header("Aspect des esprits")]
    [SerializeField] private float spiritSize = 0.17f;
    [SerializeField] private float brightness = 2.3f;
    [SerializeField]
    private Color spiritColor =
        new Color(0.58f, 0.27f, 1f, 1f);

    [SerializeField] private int trailParticles = 6;
    [SerializeField] private bool smallPointLights = false;

    [Header("Mouvement")]
    [SerializeField] private float movementSpeed = 0.11f;
    [SerializeField] private float floatAmplitude = 0.16f;
    [SerializeField] private float pulseSpeed = 1.3f;

    private readonly List<Spirit> spirits =
        new List<Spirit>();

    private readonly List<Material> materials =
        new List<Material>();

    private GameObject generatedRoot;

    private sealed class Spirit
    {
        public Vector3 center;
        public float radiusX;
        public float radiusZ;
        public float phase;
        public float heightOffset;

        public Transform core;
        public Renderer coreRenderer;
        public Transform[] trails;
        public Renderer[] trailRenderers;

        public MaterialPropertyBlock coreBlock;
        public MaterialPropertyBlock[] trailBlocks;

        public Light light;
    }

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
                "HauntedWisps : shader Unlit introuvable.",
                this
            );
            return;
        }

        Material spiritMaterial = new Material(shader);
        spiritMaterial.name = "SpectralLight_Runtime";

        SetMaterialColor(spiritMaterial, Color.white);
        materials.Add(spiritMaterial);

        generatedRoot = new GameObject(
            "Ames_Errantes_Generees"
        );

        generatedRoot.transform.SetParent(transform, false);

        float c = (boardSize - 1) * tileSize * 0.5f;
        float edge = boardSize * tileSize * 0.5f;

        float left = c - edge - sideDistance;
        float right = c + edge + sideDistance;
        float back = c + edge + rearDistance;

        // Trajectoires à l'extérieur des cases
        CreateSpirit(
            "Ame_Gauche",
            new Vector3(left, flightHeight, c),
            0.28f,
            sidePathRadius,
            0f,
            spiritMaterial
        );

        CreateSpirit(
            "Ame_Droite",
            new Vector3(right, flightHeight + 0.14f, c),
            0.28f,
            sidePathRadius,
            0.34f,
            spiritMaterial
        );

        CreateSpirit(
            "Ame_Arriere",
            new Vector3(c, flightHeight + 0.20f, back),
            1.35f,
            0.28f,
            0.68f,
            spiritMaterial
        );
    }

    private void CreateSpirit(
        string name,
        Vector3 center,
        float rx,
        float rz,
        float phase,
        Material sharedMaterial)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(
            generatedRoot.transform, false
        );

        int count = Mathf.Max(0, trailParticles);

        Spirit s = new Spirit
        {
            center = center,
            radiusX = rx,
            radiusZ = rz,
            phase = phase,
            heightOffset = phase * Mathf.PI * 2f,
            trails = new Transform[count],
            trailRenderers = new Renderer[count],
            trailBlocks = new MaterialPropertyBlock[count],
            coreBlock = new MaterialPropertyBlock()
        };

        s.core = MakeOrb(
            "Coeur",
            root.transform,
            sharedMaterial,
            out s.coreRenderer
        );

        for (int i = 0; i < count; i++)
        {
            s.trails[i] = MakeOrb(
                "Trace_" + i,
                root.transform,
                sharedMaterial,
                out Renderer trailRenderer
            );

            s.trailRenderers[i] = trailRenderer;
            s.trailBlocks[i] =
                new MaterialPropertyBlock();
        }

        if (smallPointLights)
        {
            GameObject lamp = new GameObject(
                "Lumiere_spirituelle"
            );

            lamp.transform.SetParent(
                root.transform, false
            );

            s.light = lamp.AddComponent<Light>();
            s.light.type = LightType.Point;
            s.light.color = spiritColor;
            s.light.range = 1.7f;
            s.light.intensity = 0.35f;
            s.light.shadows = LightShadows.None;
        }

        spirits.Add(s);
    }

    private static Transform MakeOrb(
        string name,
        Transform parent,
        Material sharedMaterial,
        out Renderer renderer)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        obj.name = name;
        obj.transform.SetParent(parent, false);

        Collider col = obj.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        renderer = obj.GetComponent<Renderer>();
        renderer.sharedMaterial = sharedMaterial;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return obj.transform;
    }

    private void Update()
    {
        for (int n = 0; n < spirits.Count; n++)
        {
            Spirit s = spirits[n];

            float t = Mathf.Repeat(
                Time.time * movementSpeed + s.phase,
                1f
            );

            Vector3 p = GetPosition(s, t);

            Vector3 future = GetPosition(
                s,
                Mathf.Repeat(t + 0.004f, 1f)
            );

            Vector3 direction = future - p;

            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector3.forward;

            float pulse = 0.83f + 0.17f * Mathf.Sin(
                Time.time * pulseSpeed * Mathf.PI * 2f
                + s.heightOffset
            );

            s.core.localPosition = p;
            s.core.localRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

            s.core.localScale = new Vector3(
                spiritSize,
                spiritSize,
                spiritSize * 1.45f
            );

            SetColor(
                s.coreRenderer,
                s.coreBlock,
                spiritColor * brightness * pulse
            );

            // Petite traînée lumineuse
            for (int i = 0; i < s.trails.Length; i++)
            {
                float older = Mathf.Repeat(
                    t - (i + 1) * 0.018f,
                    1f
                );

                float factor = 1f -
                    (i + 1f) / (s.trails.Length + 1f);

                s.trails[i].localPosition =
                    GetPosition(s, older);

                s.trails[i].localScale = Vector3.one *
                    (spiritSize *
                    Mathf.Lerp(0.16f, 0.67f, factor));

                SetColor(
                    s.trailRenderers[i],
                    s.trailBlocks[i],
                    spiritColor * brightness *
                    factor * factor * 0.85f
                );
            }

            if (s.light != null)
            {
                s.light.transform.localPosition = p;
                s.light.intensity = 0.28f + 0.12f * pulse;
            }
        }
    }

    // Boucle de quatre courbes de Bézier cubiques
    private Vector3 GetPosition(Spirit s, float t)
    {
        float scaled = Mathf.Repeat(t, 1f) * 4f;

        int segment = Mathf.FloorToInt(scaled);
        float u = scaled - segment;

        float a0 = segment * Mathf.PI * 0.5f;
        float a1 = (segment + 1) * Mathf.PI * 0.5f;

        const float k = 0.55228475f;

        Vector2 p0 = new Vector2(
            Mathf.Cos(a0) * s.radiusX,
            Mathf.Sin(a0) * s.radiusZ
        );

        Vector2 p3 = new Vector2(
            Mathf.Cos(a1) * s.radiusX,
            Mathf.Sin(a1) * s.radiusZ
        );

        Vector2 p1 = p0 + new Vector2(
            -Mathf.Sin(a0) * s.radiusX,
            Mathf.Cos(a0) * s.radiusZ
        ) * k;

        Vector2 p2 = p3 - new Vector2(
            -Mathf.Sin(a1) * s.radiusX,
            Mathf.Cos(a1) * s.radiusZ
        ) * k;

        float v = 1f - u;

        Vector2 point =
            v * v * v * p0 +
            3f * v * v * u * p1 +
            3f * v * u * u * p2 +
            u * u * u * p3;

        float bob = Mathf.Sin(
            t * Mathf.PI * 4f + s.heightOffset
        ) * floatAmplitude;

        return s.center +
            new Vector3(point.x, bob, point.y);
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

    private static void SetColor(
        Renderer renderer,
        MaterialPropertyBlock block,
        Color color)
    {
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        foreach (Material material in materials)
        {
            if (material != null)
                Destroy(material);
        }

        if (generatedRoot != null)
            Destroy(generatedRoot);
    }
}
