
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BoardSelector : MonoBehaviour
{
    [Header("Selection des cases")]
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private LayerMask selectableLayers = ~0;
    [SerializeField] private Material highlightMaterial;

    [Header("Reaction magique")]
    [SerializeField] private bool enableMagic = true;

    [SerializeField]
    private Color magicColor =
        new Color(0.64f, 0.30f, 1f, 1f);

    [SerializeField, Min(0.01f)]
    private float effectHeight = 0.12f;

    [SerializeField, Range(0.1f, 0.49f)]
    private float selectionRingRadius = 0.38f;

    [SerializeField, Range(0.5f, 3f)]
    private float waveMaxRadius = 1.55f;

    [SerializeField, Range(0.2f, 2f)]
    private float waveDuration = 0.85f;

    [SerializeField, Range(0.5f, 5f)]
    private float glowBrightness = 1.7f;

    [Header("Reaction des runes")]
    [SerializeField, Range(1, 12)]
    private int nearbyRunes = 5;

    [SerializeField, Range(0.1f, 3f)]
    private float runeBoost = 1.3f;

    private BoardTile selectedTile;

    private GameObject magicRoot;
    private Mesh magicRingMesh;
    private Material magicMaterial;

    private MeshRenderer selectionRing;
    private MeshRenderer waveOne;
    private MeshRenderer waveTwo;

    private MaterialPropertyBlock selectionTint;
    private MaterialPropertyBlock waveOneTint;
    private MaterialPropertyBlock waveTwoTint;

    private float waveTime;

    private void Awake()
    {
        if (selectionCamera == null)
            selectionCamera = Camera.main;

        if (highlightMaterial == null)
        {
            Debug.LogWarning(
                "BoardSelector : assigne HighlightTile " +
                "dans le champ Highlight Material.",
                this
            );
        }

        if (enableMagic)
            CreateMagicVisuals();
    }

    private void Update()
    {
        if (WasLeftClickPressed() && !PointerIsOverUI())
            TrySelectTile();

        if (magicRoot != null && magicRoot.activeSelf)
            UpdateMagicVisuals();
    }

    private void TrySelectTile()
    {
        if (selectionCamera == null)
            selectionCamera = Camera.main;

        if (selectionCamera == null)
            return;

        Ray ray = selectionCamera.ScreenPointToRay(
            GetMousePosition()
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            1000f,
            selectableLayers))
        {
            return;
        }

        BoardTile tile =
            hit.collider.GetComponent<BoardTile>();

        if (tile == null)
        {
            tile = hit.collider
                .GetComponentInParent<BoardTile>();
        }

        if (tile != null)
            SelectTile(tile);
    }

    public void SelectTile(BoardTile tile)
    {
        if (tile == null)
            return;

        if (selectedTile != null && selectedTile != tile)
            selectedTile.ResetHighlight();

        selectedTile = tile;

        // Correction : Highlight attend un Material.
        if (highlightMaterial != null)
            selectedTile.Highlight(highlightMaterial);

        if (!enableMagic)
            return;

        if (magicRoot == null)
            CreateMagicVisuals();

        if (magicRoot == null)
            return;

        Vector3 localPosition =
            transform.InverseTransformPoint(
                tile.transform.position
            );

        magicRoot.transform.localPosition =
            new Vector3(
                localPosition.x,
                localPosition.y + effectHeight,
                localPosition.z
            );

        magicRoot.SetActive(true);
        waveTime = 0f;

        PulseNearestRunes(
            tile.transform.position
        );
    }

    public void ClearSelection()
    {
        if (selectedTile != null)
        {
            selectedTile.ResetHighlight();
            selectedTile = null;
        }

        if (magicRoot != null)
            magicRoot.SetActive(false);
    }

    private void PulseNearestRunes(
        Vector3 worldPosition)
    {
        RunePulse[] allRunes =
            GetComponentsInChildren<RunePulse>(true);

        if (allRunes.Length == 0)
            return;

        List<RunePulse> nearest =
            new List<RunePulse>(allRunes);

        nearest.Sort((a, b) =>
            (a.transform.position - worldPosition)
                .sqrMagnitude.CompareTo(
                    (b.transform.position - worldPosition)
                        .sqrMagnitude
                )
        );

        int count = Mathf.Min(
            nearbyRunes,
            nearest.Count
        );

        for (int i = 0; i < count; i++)
        {
            float strength =
                runeBoost * (1f - 0.12f * i);

            nearest[i].TriggerMagicPulse(strength);
        }
    }

    private void CreateMagicVisuals()
    {
        if (magicRoot != null)
            return;

        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
        {
            Debug.LogWarning(
                "BoardSelector : shader transparent introuvable.",
                this
            );
            return;
        }

        magicMaterial = new Material(shader)
        {
            name = "SelectionMagique_Runtime"
        };

        magicMaterial.SetOverrideTag(
            "RenderType",
            "Transparent"
        );

        if (magicMaterial.HasProperty("_Surface"))
            magicMaterial.SetFloat("_Surface", 1f);

        if (magicMaterial.HasProperty("_Blend"))
            magicMaterial.SetFloat("_Blend", 0f);

        if (magicMaterial.HasProperty("_SrcBlend"))
        {
            magicMaterial.SetFloat(
                "_SrcBlend",
                (float)BlendMode.SrcAlpha
            );
        }

        if (magicMaterial.HasProperty("_DstBlend"))
        {
            magicMaterial.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha
            );
        }

        if (magicMaterial.HasProperty("_ZWrite"))
            magicMaterial.SetFloat("_ZWrite", 0f);

        if (magicMaterial.HasProperty("_Cull"))
        {
            magicMaterial.SetFloat(
                "_Cull",
                (float)CullMode.Off
            );
        }

        magicMaterial.EnableKeyword(
            "_SURFACE_TYPE_TRANSPARENT"
        );

        magicMaterial.renderQueue =
            (int)RenderQueue.Transparent;

        SetMaterialColor(
            magicMaterial,
            Color.white
        );

        magicRingMesh = BuildRingMesh(
            64, 0.13f
        );

        magicRoot = new GameObject(
            "SelectionMagique"
        );

        magicRoot.transform.SetParent(
            transform, false
        );

        selectionRing = CreateRingObject(
            "AnneauSelection", 0.015f
        );

        waveOne = CreateRingObject(
            "Onde1", 0.020f
        );

        waveTwo = CreateRingObject(
            "Onde2", 0.025f
        );

        selectionTint =
            new MaterialPropertyBlock();

        waveOneTint =
            new MaterialPropertyBlock();

        waveTwoTint =
            new MaterialPropertyBlock();

        magicRoot.SetActive(false);
    }

    private MeshRenderer CreateRingObject(
        string objectName,
        float height)
    {
        GameObject ring = new GameObject(objectName);

        ring.transform.SetParent(
            magicRoot.transform, false
        );

        ring.transform.localPosition =
            new Vector3(0f, height, 0f);

        MeshFilter filter =
            ring.AddComponent<MeshFilter>();

        filter.sharedMesh = magicRingMesh;

        MeshRenderer renderer =
            ring.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = magicMaterial;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        return renderer;
    }

    private void UpdateMagicVisuals()
    {
        waveTime += Time.deltaTime;

        float breathing =
            1f + 0.035f *
            Mathf.Sin(Time.time * 3.5f);

        selectionRing.transform.localScale =
            Vector3.one *
            (selectionRingRadius * breathing);

        SetRingTint(
            selectionRing,
            selectionTint,
            glowBrightness * 1.4f,
            0.9f
        );

        AnimateWave(
            waveOne,
            waveOneTint,
            waveTime
        );

        AnimateWave(
            waveTwo,
            waveTwoTint,
            waveTime - 0.15f
        );
    }

    private void AnimateWave(
        MeshRenderer renderer,
        MaterialPropertyBlock block,
        float time)
    {
        if (time < 0f || time >= waveDuration)
        {
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;

        float progress = Mathf.Clamp01(
            time / waveDuration
        );

        renderer.transform.localScale =
            Vector3.one *
            Mathf.Lerp(
                0.20f,
                waveMaxRadius,
                progress
            );

        float alpha =
            (1f - progress) *
            (1f - progress) * 0.75f;

        SetRingTint(
            renderer,
            block,
            glowBrightness * 1.8f,
            alpha
        );
    }

    private void SetRingTint(
        MeshRenderer renderer,
        MaterialPropertyBlock block,
        float brightness,
        float alpha)
    {
        Color color = magicColor * brightness;
        color.a = alpha;

        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);

        renderer.SetPropertyBlock(block);
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

    private static Mesh BuildRingMesh(
        int segments,
        float thickness)
    {
        Vector3[] vertices =
            new Vector3[segments * 2];

        int[] triangles =
            new int[segments * 12];

        float inner = 1f - thickness;

        for (int i = 0; i < segments; i++)
        {
            float angle =
                2f * Mathf.PI * i / segments;

            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(
                x * inner, 0f, z * inner
            );

            vertices[i * 2 + 1] = new Vector3(
                x, 0f, z
            );

            int a = i * 2;
            int b = ((i + 1) % segments) * 2;
            int t = i * 12;

            triangles[t] = a;
            triangles[t + 1] = a + 1;
            triangles[t + 2] = b + 1;

            triangles[t + 3] = a;
            triangles[t + 4] = b + 1;
            triangles[t + 5] = b;

            triangles[t + 6] = b + 1;
            triangles[t + 7] = a + 1;
            triangles[t + 8] = a;

            triangles[t + 9] = b;
            triangles[t + 10] = b + 1;
            triangles[t + 11] = a;
        }

        Mesh mesh = new Mesh
        {
            name = "MagicSelectionRing"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static bool PointerIsOverUI()
    {
        return EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }

    private static bool WasLeftClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private void OnDestroy()
    {
        if (magicRoot != null)
            Destroy(magicRoot);

        if (magicRingMesh != null)
            Destroy(magicRingMesh);

        if (magicMaterial != null)
            Destroy(magicMaterial);
    }
}
