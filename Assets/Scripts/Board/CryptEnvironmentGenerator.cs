
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CryptEnvironmentGenerator : MonoBehaviour
{
    [Header("Plateau")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Header("Sol en pierre")]
    [SerializeField, Min(4)] private int floorTilesX = 14;
    [SerializeField, Min(4)] private int floorTilesZ = 14;
    [SerializeField, Min(0.5f)] private float floorTileSize = 1.3f;
    [SerializeField] private float floorY = -0.94f;
    [SerializeField, Min(0.05f)] private float floorThickness = 0.20f;
    [SerializeField, Range(0f, 0.08f)] private float floorRandomHeight = 0.025f;
    [SerializeField, Range(0f, 0.08f)] private float floorGap = 0.025f;

    [Header("Ambiance")]
    [SerializeField] private bool darkenCameraBackground = true;
    [SerializeField]
    private Color backgroundColor =
        new Color(0.018f, 0.012f, 0.032f, 1f);

    [SerializeField] private bool createWalls = false;
    [SerializeField, Min(1f)] private float wallHeight = 4.5f;

    [Header("Brume autour du plateau")]
    [SerializeField] private bool createFog = true;

    [SerializeField]
    private Color fogColor =
        new Color(0.47f, 0.16f, 0.80f, 1f);

    [SerializeField, Range(0.01f, 0.3f)]
    private float fogOpacity = 0.10f;

    [SerializeField, Min(0.2f)]
    private float fogParticleSize = 1.25f;

    [SerializeField, Range(4, 30)]
    private int particlesPerSide = 14;

    [SerializeField] private float fogY = -0.65f;
    [SerializeField] private float fogDistanceFromEdge = 0.95f;

    [Header("Poussiere magique")]
    [SerializeField] private bool createMagicDust = true;

    [SerializeField, Range(4, 50)]
    private int dustParticleCount = 18;

    [Header("Generation")]
    [SerializeField] private int seed = 12345;

    private readonly List<GameObject> generatedObjects =
        new List<GameObject>();

    private readonly List<Material> generatedMaterials =
        new List<Material>();

    private Texture2D softParticleTexture;

    private Camera modifiedCamera;
    private CameraClearFlags originalClearFlags;
    private Color originalBackground;

    private void Start()
    {
        Generate();
    }

    private void Generate()
    {
        float center = (boardSize - 1) * tileSize * 0.5f;
        float halfBoard = boardSize * tileSize * 0.5f;

        CreateFloor(center);

        if (createWalls)
            CreateDistantWalls(center);

        if (darkenCameraBackground)
            SetCameraBackground();

        if (!createFog && !createMagicDust)
            return;

        // On évite d'afficher des particules opaques
        // si le shader transparent URP est introuvable.
        Shader particleShader = Shader.Find(
            "Universal Render Pipeline/Particles/Unlit"
        );

        if (particleShader == null)
        {
            Debug.LogWarning(
                "Shader URP/Particles/Unlit introuvable : " +
                "brume desactivee pour eviter les rectangles opaques.",
                this
            );

            return;
        }

        softParticleTexture = CreateSoftCircleTexture();

        if (createFog)
        {
            Material fogMaterial =
                CreateTransparentParticleMaterial(
                    particleShader,
                    "CryptFog_Runtime",
                    softParticleTexture
                );

            float distance =
                halfBoard + fogDistanceFromEdge;

            float length =
                boardSize * tileSize + 1.8f;

            CreateFogStrip(
                "Brume_Avant",
                new Vector3(center, fogY, center - distance),
                new Vector3(length, 0.1f, 0.65f),
                fogMaterial
            );

            CreateFogStrip(
                "Brume_Arriere",
                new Vector3(center, fogY, center + distance),
                new Vector3(length, 0.1f, 0.65f),
                fogMaterial
            );

            CreateFogStrip(
                "Brume_Gauche",
                new Vector3(center - distance, fogY, center),
                new Vector3(0.65f, 0.1f, length),
                fogMaterial
            );

            CreateFogStrip(
                "Brume_Droite",
                new Vector3(center + distance, fogY, center),
                new Vector3(0.65f, 0.1f, length),
                fogMaterial
            );
        }

        if (createMagicDust)
        {
            Material dustMaterial =
                CreateTransparentParticleMaterial(
                    particleShader,
                    "MagicDust_Runtime",
                    softParticleTexture
                );

            CreateDust(
                new Vector3(center, 0.75f, center),
                dustMaterial
            );
        }
    }

    private void CreateFloor(float center)
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError(
                "Impossible de trouver un shader pour le sol.",
                this
            );

            return;
        }

        Color[] colors =
        {
            new Color(0.16f, 0.16f, 0.20f),
            new Color(0.19f, 0.18f, 0.23f),
            new Color(0.13f, 0.14f, 0.18f),
            new Color(0.17f, 0.15f, 0.20f),
            new Color(0.14f, 0.15f, 0.19f)
        };

        Material[] stones = new Material[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            stones[i] = new Material(shader)
            {
                name = "CryptStone_" + i
            };

            SetColor(stones[i], colors[i]);

            if (stones[i].HasProperty("_Smoothness"))
                stones[i].SetFloat("_Smoothness", 0.06f);

            generatedMaterials.Add(stones[i]);
        }

        GameObject root = NewObject(
            "Sol_de_la_crypte",
            transform
        );

        System.Random rng = new System.Random(seed);

        float x0 = center -
            (floorTilesX - 1) * floorTileSize * 0.5f;

        float z0 = center -
            (floorTilesZ - 1) * floorTileSize * 0.5f;

        for (int x = 0; x < floorTilesX; x++)
        {
            for (int z = 0; z < floorTilesZ; z++)
            {
                GameObject tile =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube
                    );

                tile.name = "Dalle_" + x + "_" + z;

                tile.transform.SetParent(
                    root.transform,
                    false
                );

                float lift =
                    ((float)rng.NextDouble() * 2f - 1f)
                    * floorRandomHeight;

                tile.transform.localPosition =
                    new Vector3(
                        x0 + x * floorTileSize,
                        floorY + lift,
                        z0 + z * floorTileSize
                    );

                tile.transform.localScale =
                    new Vector3(
                        floorTileSize - floorGap,
                        floorThickness,
                        floorTileSize - floorGap
                    );

                tile.GetComponent<MeshRenderer>()
                    .sharedMaterial =
                    stones[rng.Next(stones.Length)];

                Collider collider =
                    tile.GetComponent<Collider>();

                if (collider != null)
                    Destroy(collider);
            }
        }
    }

    private void CreateDistantWalls(float center)
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return;

        Material stone = new Material(shader)
        {
            name = "CryptWalls_Runtime"
        };

        SetColor(
            stone,
            new Color(0.075f, 0.07f, 0.095f)
        );

        generatedMaterials.Add(stone);

        float width = floorTilesX * floorTileSize;
        float depth = floorTilesZ * floorTileSize;

        float y = floorY + wallHeight * 0.5f;

        CreateWall(
            "Mur_fond",
            new Vector3(
                center,
                y,
                center + depth * 0.5f
            ),
            new Vector3(
                width,
                wallHeight,
                0.35f
            ),
            stone
        );

        CreateWall(
            "Mur_gauche",
            new Vector3(
                center - width * 0.5f,
                y,
                center
            ),
            new Vector3(
                0.35f,
                wallHeight,
                depth
            ),
            stone
        );

        CreateWall(
            "Mur_droit",
            new Vector3(
                center + width * 0.5f,
                y,
                center
            ),
            new Vector3(
                0.35f,
                wallHeight,
                depth
            ),
            stone
        );
    }

    private void CreateWall(
        string objectName,
        Vector3 pos,
        Vector3 scale,
        Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(
            PrimitiveType.Cube
        );

        go.name = objectName;

        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        go.GetComponent<MeshRenderer>()
            .sharedMaterial = mat;

        Collider col = go.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        generatedObjects.Add(go);
    }

    private void SetCameraBackground()
    {
        modifiedCamera = Camera.main;

        if (modifiedCamera == null)
            return;

        originalClearFlags = modifiedCamera.clearFlags;
        originalBackground = modifiedCamera.backgroundColor;

        modifiedCamera.clearFlags =
            CameraClearFlags.SolidColor;

        modifiedCamera.backgroundColor =
            backgroundColor;
    }

    private static Texture2D CreateSoftCircleTexture()
    {
        const int resolution = 64;

        Texture2D texture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );

        texture.name = "SoftParticleTexture_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels =
            new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u =
                    (x + 0.5f) / resolution * 2f - 1f;

                float v =
                    (y + 0.5f) / resolution * 2f - 1f;

                float r = Mathf.Sqrt(u * u + v * v);

                float alpha = Mathf.Pow(
                    Mathf.Clamp01(1f - r),
                    2.1f
                );

                pixels[y * resolution + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private Material CreateTransparentParticleMaterial(
        Shader shader,
        string name,
        Texture2D texture)
    {
        Material material = new Material(shader)
        {
            name = name
        };

        material.SetOverrideTag(
            "RenderType",
            "Transparent"
        );

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);

        material.SetFloat(
            "_SrcBlend",
            (float)BlendMode.SrcAlpha
        );

        material.SetFloat(
            "_DstBlend",
            (float)BlendMode.OneMinusSrcAlpha
        );

        material.SetFloat(
            "_SrcBlendAlpha",
            (float)BlendMode.One
        );

        material.SetFloat(
            "_DstBlendAlpha",
            (float)BlendMode.OneMinusSrcAlpha
        );

        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_AlphaClip", 0f);

        material.EnableKeyword(
            "_SURFACE_TYPE_TRANSPARENT"
        );

        material.DisableKeyword("_ALPHATEST_ON");

        material.renderQueue =
            (int)RenderQueue.Transparent;

        material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        SetColor(material, Color.white);

        generatedMaterials.Add(material);

        return material;
    }

    private void CreateFogStrip(
        string name,
        Vector3 position,
        Vector3 boxSize,
        Material material)
    {
        GameObject obj = NewObject(name, transform);
        obj.transform.localPosition = position;

        ParticleSystem ps =
            obj.AddComponent<ParticleSystem>();

        ParticleSystemRenderer renderer =
            obj.GetComponent<ParticleSystemRenderer>();

        renderer.sharedMaterial = material;
        renderer.renderMode =
            ParticleSystemRenderMode.Billboard;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        var main = ps.main;

        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 7f;
        main.startSpeed = 0.035f;
        main.startSize = fogParticleSize;

        main.startColor = new Color(
            fogColor.r,
            fogColor.g,
            fogColor.b,
            fogOpacity
        );

        main.maxParticles = particlesPerSide;

        main.simulationSpace =
            ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = particlesPerSide / 7f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.10f;
        noise.frequency = 0.30f;
        noise.scrollSpeed = 0.08f;

        var color = ps.colorOverLifetime;
        color.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        color.color = gradient;

        ps.Play();
    }

    private void CreateDust(
        Vector3 position,
        Material material)
    {
        GameObject obj = NewObject(
            "Poussieres_magiques",
            transform
        );

        obj.transform.localPosition = position;

        ParticleSystem ps =
            obj.AddComponent<ParticleSystem>();

        ParticleSystemRenderer renderer =
            obj.GetComponent<ParticleSystemRenderer>();

        renderer.sharedMaterial = material;

        renderer.renderMode =
            ParticleSystemRenderMode.Billboard;

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        var main = ps.main;

        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 5f;
        main.startSpeed = 0.055f;
        main.startSize = 0.075f;

        main.startColor = new Color(
            0.85f, 0.68f, 1f, 0.65f
        );

        main.maxParticles = dustParticleCount;

        var emission = ps.emission;

        emission.rateOverTime =
            dustParticleCount / 5f;

        var shape = ps.shape;

        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        shape.scale = new Vector3(
            boardSize * tileSize + 1f,
            1.0f,
            boardSize * tileSize + 1f
        );

        var color = ps.colorOverLifetime;
        color.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        color.color = gradient;

        ps.Play();
    }

    private GameObject NewObject(
        string name,
        Transform parent)
    {
        GameObject obj = new GameObject(name);

        obj.transform.SetParent(parent, false);

        generatedObjects.Add(obj);

        return obj;
    }

    private static void SetColor(
        Material material,
        Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void OnDestroy()
    {
        if (modifiedCamera != null)
        {
            modifiedCamera.clearFlags =
                originalClearFlags;

            modifiedCamera.backgroundColor =
                originalBackground;
        }

        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }

        foreach (Material mat in generatedMaterials)
        {
            if (mat != null)
                Destroy(mat);
        }

        if (softParticleTexture != null)
            Destroy(softParticleTexture);
    }
}
