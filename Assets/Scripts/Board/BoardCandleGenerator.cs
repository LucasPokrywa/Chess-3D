using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoardCandleGenerator : MonoBehaviour
{
    [Header("Plateau et piliers")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private float pillarTopY = 0.355f;
    [SerializeField] private float cornerOffsetExtra = 0.25f;
    [SerializeField] private float inwardOffset = 0.04f;

    [Header("Chandeliers")]
    [SerializeField] private float standHeight = 0.22f;
    [SerializeField] private float standRadius = 0.17f;
    [SerializeField] private float candleHeight = 0.45f;
    [SerializeField] private float candleRadius = 0.11f;

    [Header("Flammes 3D")]
    [SerializeField] private float flameHeight = 0.32f;
    [SerializeField] private float flameWidth = 0.14f;
    [SerializeField] private float lightIntensity = 1.2f;
    [SerializeField] private float lightRange = 2.4f;
    [SerializeField] private float flickerSpeed = 1.5f;

    private readonly List<Material> materials = new List<Material>();
    private Mesh flameMesh;
    private Material ironMaterial;
    private Material ironHighlightMaterial;
    private Material waxMaterial;
    private Material wickMaterial;
    private Material purpleFlameMaterial;
    private Material purpleCoreMaterial;
    private Material orangeFlameMaterial;
    private Material orangeCoreMaterial;

    private void Start()
    {
        GenerateCandles();
    }

    private void GenerateCandles()
    {
        if (boardSize <= 0 || tileSize <= 0f)
            return;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (lit == null) lit = Shader.Find("Standard");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");

        if (lit == null || unlit == null)
        {
            Debug.LogError("Impossible de trouver les shaders Lit/Unlit.", this);
            return;
        }

        ironMaterial = MakeMaterial(lit, "Fer noir", new Color(0.065f, 0.055f, 0.085f), 0.65f, 0.70f);
        ironHighlightMaterial = MakeMaterial(lit, "Fer patine", new Color(0.25f, 0.16f, 0.32f), 0.65f, 0.60f);
        waxMaterial = MakeMaterial(lit, "Cire ancienne", new Color(0.68f, 0.62f, 0.63f), 0.0f, 0.35f);
        wickMaterial = MakeMaterial(lit, "Meche", new Color(0.045f, 0.025f, 0.025f), 0.0f, 0.05f);

        purpleFlameMaterial = MakeMaterial(unlit, "Flamme violette", new Color(0.38f, 0.07f, 1.20f));
        purpleCoreMaterial = MakeMaterial(unlit, "Coeur violet", new Color(1.35f, 0.80f, 1.9f));
        orangeFlameMaterial = MakeMaterial(unlit, "Flamme orange", new Color(1.35f, 0.29f, 0.025f));
        orangeCoreMaterial = MakeMaterial(unlit, "Coeur orange", new Color(1.9f, 1.15f, 0.45f));

        flameMesh = CreateFlameMesh(12);

        float size = boardSize * tileSize;
        float center = (boardSize - 1) * tileSize * 0.5f;
        float corner = size * 0.5f + cornerOffsetExtra - inwardOffset;

        CreateCandle("Chandelier_AvantGauche", new Vector3(center - corner, pillarTopY, center - corner), true, 0.4f);
        CreateCandle("Chandelier_AvantDroit", new Vector3(center + corner, pillarTopY, center - corner), false, 1.8f);
        CreateCandle("Chandelier_ArriereGauche", new Vector3(center - corner, pillarTopY, center + corner), false, 3.2f);
        CreateCandle("Chandelier_ArriereDroit", new Vector3(center + corner, pillarTopY, center + corner), true, 4.6f);
    }

    private void CreateCandle(string name, Vector3 position, bool purple, float phase)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = position;

        // Pied et tige en fer noir : plusieurs niveaux pour une silhouette gothique.
        AddCylinder(root.transform, "Pied octogonal", new Vector3(0f, 0.018f, 0f), standRadius, 0.036f, ironMaterial);
        AddCylinder(root.transform, "Anneau inferieur", new Vector3(0f, 0.046f, 0f), standRadius * 0.83f, 0.021f, ironHighlightMaterial);
        AddCylinder(root.transform, "Tige", new Vector3(0f, standHeight * 0.52f, 0f), 0.036f, standHeight * 0.76f, ironMaterial);
        AddSphere(root.transform, "Noeud en fer", new Vector3(0f, standHeight * 0.60f, 0f), new Vector3(0.105f, 0.092f, 0.105f), ironHighlightMaterial);
        AddCylinder(root.transform, "Coupe sous la cire", new Vector3(0f, standHeight - 0.018f, 0f), standRadius * 0.73f, 0.045f, ironMaterial);
        AddCylinder(root.transform, "Liseré métallique", new Vector3(0f, standHeight + 0.012f, 0f), standRadius * 0.67f, 0.015f, ironHighlightMaterial);

        float waxBottom = standHeight + 0.020f;
        float waxTop = waxBottom + candleHeight;

        AddCylinder(root.transform, "Corps de cire", new Vector3(0f, waxBottom + candleHeight * 0.5f, 0f), candleRadius, candleHeight, waxMaterial);
        AddCylinder(root.transform, "Bord de cire fondu", new Vector3(0f, waxTop - 0.015f, 0f), candleRadius * 1.06f, 0.035f, waxMaterial);

        // Petites coulures disposées autour de la bougie.
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 0.5f + 0.3f;
            float length = candleHeight * (0.22f + i * 0.065f);
            Vector3 dripPosition = new Vector3(
                Mathf.Cos(angle) * candleRadius * 0.91f,
                waxTop - length * 0.52f,
                Mathf.Sin(angle) * candleRadius * 0.91f);

            AddSphere(root.transform, "Coulure_" + i, dripPosition,
                new Vector3(candleRadius * 0.26f, length, candleRadius * 0.24f), waxMaterial);
        }

        AddCylinder(root.transform, "Meche", new Vector3(0f, waxTop + 0.024f, 0f), 0.009f, 0.048f, wickMaterial);

        GameObject flamePivot = new GameObject("FlammeAnimee");
        flamePivot.transform.SetParent(root.transform, false);
        flamePivot.transform.localPosition = new Vector3(0f, waxTop + 0.045f, 0f);

        Material outerMaterial = purple ? purpleFlameMaterial : orangeFlameMaterial;
        Material coreMaterial = purple ? purpleCoreMaterial : orangeCoreMaterial;

        MeshRenderer outer = AddFlame(flamePivot.transform, "Flamme_exterieure", outerMaterial, Vector3.one, Vector3.zero);
        MeshRenderer core = AddFlame(flamePivot.transform, "Coeur_lumineux", coreMaterial,
            new Vector3(0.48f, 0.74f, 0.48f), new Vector3(0f, 0.030f, 0f));

        GameObject lightObject = new GameObject("Lumiere_douce");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, waxTop + 0.17f, 0f);
        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = purple ? new Color(0.65f, 0.25f, 1f) : new Color(1f, 0.42f, 0.11f);
        pointLight.intensity = lightIntensity;
        pointLight.range = lightRange;
        pointLight.shadows = LightShadows.None;

        CandleFlameFlicker flicker = flamePivot.AddComponent<CandleFlameFlicker>();
        flicker.Configure(flamePivot.transform, outer, core, pointLight,
            purple ? new Color(0.38f, 0.07f, 1.20f) : new Color(1.35f, 0.29f, 0.025f),
            purple ? new Color(1.35f, 0.80f, 1.90f) : new Color(1.90f, 1.15f, 0.45f),
            flickerSpeed, lightIntensity, phase);
    }

    private MeshRenderer AddFlame(Transform parent, string name, Material material, Vector3 scale, Vector3 position)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.AddComponent<MeshFilter>().sharedMesh = flameMesh;
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void AddCylinder(Transform parent, string name, Vector3 position, float radius, float height, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private static void AddSphere(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private Material MakeMaterial(Shader shader, string name, Color color, float metallic = 0f, float smoothness = 0f)
    {
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        materials.Add(material);
        return material;
    }

    private Mesh CreateFlameMesh(int sides)
    {
        // Surface 3D réellement volumique : toujours visible depuis la caméra.
        float[] heights = { 0f, 0.15f, 0.40f, 0.65f, 0.84f, 1f };
        float[] radii = { 0.24f, 0.78f, 1f, 0.66f, 0.35f, 0.015f };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int level = 0; level < heights.Length; level++)
        {
            float y = heights[level];
            float radius = radii[level] * flameWidth * 0.5f;
            float sway = 0.07f * flameWidth * y * y;
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Sin(a) * radius + sway,
                    y * flameHeight, Mathf.Cos(a) * radius));
            }
        }

        for (int level = 0; level < heights.Length - 1; level++)
        {
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                int a = level * sides + s;
                int b = level * sides + next;
                int c = (level + 1) * sides + s;
                int d = (level + 1) * sides + next;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }

        Mesh mesh = new Mesh { name = "FlammeGothique3D" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (flameMesh != null) Destroy(flameMesh);
        foreach (Material material in materials)
            if (material != null) Destroy(material);
    }
}
