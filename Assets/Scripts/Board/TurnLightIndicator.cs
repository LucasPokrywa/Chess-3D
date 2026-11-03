
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class TurnLightIndicator : MonoBehaviour
{
    [Header("Tour - synchronisation")]
    [SerializeField] private bool automaticTurn = true;
    [SerializeField] private bool whiteTurn = true;

    [Header("Lumiere du salon")]
    [SerializeField, Range(0f, 2f)]
    private float ceilingLightIntensity = 0.30f;

    [SerializeField]
    private Color ambientRoomColor =
        new Color(0.27f, 0.25f, 0.25f);

    [Header("Spots de tour")]
    [SerializeField, Range(0f, 35f)]
    private float spotPower = 16f;

    [SerializeField, Range(30f, 110f)]
    private float coneAngle = 65f;

    [SerializeField, Range(5f, 25f)]
    private float spotRange = 13f;

    [SerializeField, Range(0.1f, 3f)]
    private float fadeSeconds = 1f;

    [Header("Halo doux sur la table")]
    [SerializeField]
    private bool enableTableLightPools = true;

    [SerializeField, Range(0f, 1f)]
    private float poolOpacity = 0.52f;

    [SerializeField]
    private float tableSurfaceY = -0.786f;

    private readonly Color ivory =
        new Color(1f, 0.68f, 0.37f, 1f);

    private readonly Color lavender =
        new Color(0.43f, 0.46f, 1f, 1f);

    private Light whiteSpot;
    private Light blackSpot;
    private Light ceiling;

    private float originalCeilingIntensity;
    private Color originalCeilingColor;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientColor;
    private bool savedOriginalLighting;

    private Material whitePoolMaterial;
    private Material blackPoolMaterial;
    private Texture2D radialTexture;
    private GameObject poolRoot;

    private TMP_Text turnTextTMP;
    private Text turnTextLegacy;
    private MonoBehaviour chessController;

    private float nextTurnCheck;
    private float whiteBlend = 1f;
    private bool ready;
    private bool hasReportedNoTurnSource;

    private IEnumerator Start()
    {
        GameObject board = null;

        for (int i = 0; i < 180; i++)
        {
            board = GameObject.Find("Board");

            if (board != null)
                break;

            yield return null;
        }

        if (board == null)
        {
            Debug.LogError(
                "TurnLightIndicator : Board introuvable."
            );

            yield break;
        }

        SaveAndAdjustRoomLight();

        Transform boardRoot = board.transform;

        whiteSpot = MakeSpot(
            boardRoot,
            "WhiteTurnSpot",
            new Vector3(3.5f, 3.35f, -1.55f),
            new Vector3(3.5f, 0.15f, 1.25f),
            ivory
        );

        blackSpot = MakeSpot(
            boardRoot,
            "BlackTurnSpot",
            new Vector3(3.5f, 3.35f, 8.55f),
            new Vector3(3.5f, 0.15f, 5.75f),
            lavender
        );

        if (enableTableLightPools)
            MakeLightPools(boardRoot);

        yield return null;

        bool detected;

        if (automaticTurn &&
            TryReadCurrentTurn(out detected))
        {
            whiteTurn = detected;
        }

        whiteBlend = whiteTurn ? 1f : 0f;

        ready = true;
        RefreshLights();

        Debug.Log(
            "TurnLightIndicator : spots et halos initialises."
        );
    }

    private void SaveAndAdjustRoomLight()
    {
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientColor = RenderSettings.ambientLight;
        savedOriginalLighting = true;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientRoomColor;

        GameObject go =
            GameObject.Find("Directional Light");

        if (go != null)
            ceiling = go.GetComponent<Light>();

        if (ceiling != null)
        {
            originalCeilingIntensity = ceiling.intensity;
            originalCeilingColor = ceiling.color;

            ceiling.intensity = ceilingLightIntensity;
            ceiling.color = new Color(
                1f, 0.90f, 0.76f
            );
        }
    }

    private Light MakeSpot(
        Transform parent,
        string name,
        Vector3 localPos,
        Vector3 localTarget,
        Color color)
    {
        Transform existing = parent.Find(name);

        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        go.transform.localPosition = localPos;

        go.transform.localRotation =
            Quaternion.LookRotation(
                localTarget - localPos,
                Vector3.up
            );

        Light light = go.GetComponent<Light>();

        if (light == null)
            light = go.AddComponent<Light>();

        light.type = LightType.Spot;
        light.color = color;
        light.range = spotRange;
        light.spotAngle = coneAngle;
        light.innerSpotAngle = coneAngle * 0.65f;

        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        light.enabled = true;
        light.intensity = 0f;

        return light;
    }

    private void MakeLightPools(Transform parent)
    {
        Transform old =
            parent.Find("TurnLightPools");

        if (old != null)
            Destroy(old.gameObject);

        poolRoot = new GameObject("TurnLightPools");

        poolRoot.transform.SetParent(parent, false);
        poolRoot.transform.localPosition = Vector3.zero;

        radialTexture = new Texture2D(
            128,
            128,
            TextureFormat.RGBA32,
            false
        );

        radialTexture.wrapMode = TextureWrapMode.Clamp;
        radialTexture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float u =
                    (x + 0.5f) / 128f * 2f - 1f;

                float v =
                    (y + 0.5f) / 128f * 2f - 1f;

                float d = Mathf.Clamp01(
                    Mathf.Sqrt(u * u + v * v)
                );

                float a = Mathf.Pow(1f - d, 2.4f);

                radialTexture.SetPixel(
                    x, y,
                    new Color(1f, 1f, 1f, a)
                );
            }
        }

        radialTexture.Apply();

        whitePoolMaterial =
            MakeTransparentMaterial("WarmTableLightPool");

        blackPoolMaterial =
            MakeTransparentMaterial("CoolTableLightPool");

        CreatePool(
            "WarmPool_White",
            new Vector3(
                3.5f,
                tableSurfaceY,
                -0.65f
            ),
            whitePoolMaterial
        );

        CreatePool(
            "CoolPool_Black",
            new Vector3(
                3.5f,
                tableSurfaceY,
                7.65f
            ),
            blackPoolMaterial
        );
    }

    private Material MakeTransparentMaterial(
        string materialName)
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material m = new Material(shader);

        m.name = materialName;
        m.renderQueue = (int)RenderQueue.Transparent;

        if (m.HasProperty("_Surface"))
            m.SetFloat("_Surface", 1f);

        if (m.HasProperty("_Blend"))
            m.SetFloat("_Blend", 0f);

        if (m.HasProperty("_SrcBlend"))
            m.SetFloat(
                "_SrcBlend",
                (float)BlendMode.SrcAlpha
            );

        if (m.HasProperty("_DstBlend"))
            m.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha
            );

        if (m.HasProperty("_ZWrite"))
            m.SetFloat("_ZWrite", 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        if (m.HasProperty("_BaseMap"))
            m.SetTexture("_BaseMap", radialTexture);

        if (m.HasProperty("_MainTex"))
            m.SetTexture("_MainTex", radialTexture);

        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", Color.clear);

        if (m.HasProperty("_Color"))
            m.SetColor("_Color", Color.clear);

        return m;
    }

    private void CreatePool(
        string name,
        Vector3 localPos,
        Material material)
    {
        GameObject go =
            GameObject.CreatePrimitive(PrimitiveType.Quad);

        go.name = name;
        go.transform.SetParent(
            poolRoot.transform,
            false
        );

        go.transform.localPosition = localPos;
        go.transform.localRotation =
            Quaternion.Euler(-90f, 0f, 0f);

        go.transform.localScale =
            new Vector3(5.6f, 2.65f, 1f);

        Collider collider =
            go.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        MeshRenderer renderer =
            go.GetComponent<MeshRenderer>();

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;
    }

    private void Update()
    {
        if (!ready)
            return;

        if (automaticTurn &&
            Time.unscaledTime >= nextTurnCheck)
        {
            nextTurnCheck = Time.unscaledTime + 0.2f;

            bool detected;

            if (TryReadCurrentTurn(out detected))
            {
                whiteTurn = detected;
            }
            else if (
                !hasReportedNoTurnSource &&
                Time.unscaledTime > 3f)
            {
                hasReportedNoTurnSource = true;

                Debug.LogWarning(
                    "TurnLightIndicator : tour automatique " +
                    "introuvable. Tester depuis le menu " +
                    "du composant ou connecter " +
                    "ChessGameController."
                );
            }
        }

        float target = whiteTurn ? 1f : 0f;

        whiteBlend = Mathf.MoveTowards(
            whiteBlend,
            target,
            Time.unscaledDeltaTime /
                Mathf.Max(0.1f, fadeSeconds)
        );

        RefreshLights();
    }

    private void RefreshLights()
    {
        if (whiteSpot == null || blackSpot == null)
            return;

        whiteSpot.intensity =
            spotPower * whiteBlend;

        blackSpot.intensity =
            spotPower * (1f - whiteBlend);

        whiteSpot.range = spotRange;
        blackSpot.range = spotRange;

        whiteSpot.spotAngle = coneAngle;
        blackSpot.spotAngle = coneAngle;

        whiteSpot.innerSpotAngle =
            coneAngle * 0.65f;

        blackSpot.innerSpotAngle =
            coneAngle * 0.65f;

        if (ceiling != null)
            ceiling.intensity = ceilingLightIntensity;

        RenderSettings.ambientLight = ambientRoomColor;

        SetPoolColor(
            whitePoolMaterial,
            ivory,
            poolOpacity * whiteBlend
        );

        SetPoolColor(
            blackPoolMaterial,
            lavender,
            poolOpacity * (1f - whiteBlend)
        );
    }

    private void SetPoolColor(
        Material mat,
        Color color,
        float opacity)
    {
        if (mat == null)
            return;

        color.a = opacity;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private bool TryReadCurrentTurn(
        out bool isWhite)
    {
        isWhite = whiteTurn;

        if (turnTextTMP == null &&
            turnTextLegacy == null)
        {
            foreach (
                TMP_Text text in
                FindObjectsByType<TMP_Text>(
                    FindObjectsSortMode.None
                ))
            {
                if (text.text.IndexOf(
                    "Trait aux",
                    StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                turnTextTMP = text;
                break;
            }

            if (turnTextTMP == null)
            {
                foreach (
                    Text text in
                    FindObjectsByType<Text>(
                        FindObjectsSortMode.None
                    ))
                {
                    if (text.text.IndexOf(
                        "Trait aux",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    turnTextLegacy = text;
                    break;
                }
            }
        }

        string shown =
            turnTextTMP != null
                ? turnTextTMP.text
                : turnTextLegacy != null
                    ? turnTextLegacy.text
                    : "";

        if (TryParseSide(shown, out isWhite))
            return true;

        if (chessController == null)
        {
            foreach (
                MonoBehaviour behaviour in
                FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                ))
            {
                if (behaviour != null &&
                    behaviour.GetType().Name ==
                    "ChessGameController")
                {
                    chessController = behaviour;
                    break;
                }
            }
        }

        if (chessController == null)
            return false;

        Type type = chessController.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        string[] names =
        {
            "WhiteToMove",
            "whiteToMove",
            "IsWhiteTurn",
            "isWhiteTurn",
            "WhiteTurn",
            "whiteTurn",
            "CurrentTurn",
            "currentTurn",
            "CurrentPlayer",
            "currentPlayer",
            "sideToMove",
            "SideToMove"
        };

        foreach (string name in names)
        {
            try
            {
                FieldInfo field =
                    type.GetField(name, flags);

                if (field != null &&
                    ParseTurnValue(
                        field.GetValue(chessController),
                        out isWhite))
                {
                    return true;
                }

                PropertyInfo property =
                    type.GetProperty(name, flags);

                if (property != null &&
                    property.GetIndexParameters().Length == 0 &&
                    ParseTurnValue(
                        property.GetValue(chessController),
                        out isWhite))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Continue avec le prochain membre.
            }
        }

        return false;
    }

    private bool ParseTurnValue(
        object value,
        out bool isWhite)
    {
        isWhite = whiteTurn;

        if (value is bool boolean)
        {
            isWhite = boolean;
            return true;
        }

        if (value == null)
            return false;

        return TryParseSide(
            value.ToString(),
            out isWhite
        );
    }

    private bool TryParseSide(
        string text,
        out bool isWhite)
    {
        isWhite = whiteTurn;

        if (string.IsNullOrEmpty(text))
            return false;

        if (text.IndexOf(
            "Noir",
            StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf(
            "Black",
            StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isWhite = false;
            return true;
        }

        if (text.IndexOf(
            "Blanc",
            StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf(
            "White",
            StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isWhite = true;
            return true;
        }

        return false;
    }

    public void SetTurn(bool isWhite)
    {
        whiteTurn = isWhite;
    }

    public void SetWhiteTurn()
    {
        SetTurn(true);
    }

    public void SetBlackTurn()
    {
        SetTurn(false);
    }

    [ContextMenu("TEST - Blancs (mode manuel)")]
    private void TestWhite()
    {
        automaticTurn = false;
        whiteTurn = true;
    }

    [ContextMenu("TEST - Noirs (mode manuel)")]
    private void TestBlack()
    {
        automaticTurn = false;
        whiteTurn = false;
    }

    [ContextMenu("Retour au tour automatique")]
    private void ResumeAutomatic()
    {
        automaticTurn = true;
    }

    private void OnDestroy()
    {
        if (savedOriginalLighting)
        {
            RenderSettings.ambientMode =
                originalAmbientMode;

            RenderSettings.ambientLight =
                originalAmbientColor;

            if (ceiling != null)
            {
                ceiling.intensity =
                    originalCeilingIntensity;

                ceiling.color =
                    originalCeilingColor;
            }
        }

        if (whitePoolMaterial != null)
            Destroy(whitePoolMaterial);

        if (blackPoolMaterial != null)
            Destroy(blackPoolMaterial);

        if (radialTexture != null)
            Destroy(radialTexture);
    }
}
