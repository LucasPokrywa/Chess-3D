using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Unity 6 / URP. Controls the four EXISTING BoardCandleGenerator candles.
/// Front = white turn (amber); rear = black turn (violet).
/// No new Light components: the magic rings and oversized flames stay visible
/// even when URP Additional Lights are disabled.
///
/// Attach to HalloweenTurnCandles. Keep BoardCandleGenerator and
/// CandleFlameFlicker enabled; this script applies its effects in LateUpdate.
/// </summary>
public class HalloweenCandleTurnController : MonoBehaviour
{
    [Header("AUTO - Source du tour")]
    [SerializeField] private bool automaticTurn = true;
    [Tooltip("Optionnel : glisser le composant ChessGameController ici.")]
    [SerializeField] private MonoBehaviour chessGameController;
    [Tooltip("Optionnel : glisser ici le texte TMP 'Trait aux Blancs/Noirs'.")]
    [SerializeField] private TMP_Text turnTextTMP;
    [Tooltip("Seulement si ton interface utilise l'ancien UI Text.")]
    [SerializeField] private Text turnTextLegacy;
    [Tooltip("Si le controleur a un champ/propriete particulier, mettre son nom exact.")]
    [SerializeField] private string controllerTurnMemberName = "";
    [SerializeField] private bool whiteTurn = true;

    [Header("Bougies existantes")]
    [Tooltip("Optionnel : Board ou l'objet portant BoardCandleGenerator.")]
    [SerializeField] private Transform candlesRoot;

    [Header("VISIBILITE - Flamme et lumiere")]
    [SerializeField, Range(1f, 6f)] private float activeFlameSize = 3.2f;
    [SerializeField, Range(0.1f, 1f)] private float inactiveFlameSize = 0.30f;
    [SerializeField, Range(0f, 20f)] private float activeLightIntensity = 10f;
    [SerializeField, Range(0f, 2f)] private float inactiveLightIntensity = 0.06f;
    [SerializeField, Range(1f, 8f)] private float lightRange = 5.2f;
    [SerializeField, Range(0.1f, 3f)] private float transitionSeconds = 0.55f;

    [Header("Cercles magiques - visibles sans eclairage URP")]
    [SerializeField] private bool showMagicRings = true;
    [SerializeField, Range(0f, 3f)] private float ringBrightness = 1.8f;
    [SerializeField, Range(0f, 180f)] private float ringRotationSpeed = 35f;

    private static readonly Color WarmFlame = new Color(1.0f, 0.36f, 0.065f, 1f);
    private static readonly Color WarmCore = new Color(1.0f, 0.88f, 0.40f, 1f);
    private static readonly Color CoolFlame = new Color(0.43f, 0.12f, 1.0f, 1f);
    private static readonly Color CoolCore = new Color(0.87f, 0.64f, 1.0f, 1f);

    private sealed class Candle
    {
        public Transform root;
        public Transform flame;
        public Renderer outer;
        public Renderer core;
        public Light lightSource;
        public Renderer ringLower;
        public Renderer ringUpper;
        public Transform ringLowerTransform;
        public Transform ringUpperTransform;
        public MaterialPropertyBlock outerBlock = new MaterialPropertyBlock();
        public MaterialPropertyBlock coreBlock = new MaterialPropertyBlock();
        public MaterialPropertyBlock ringLowerBlock = new MaterialPropertyBlock();
        public MaterialPropertyBlock ringUpperBlock = new MaterialPropertyBlock();
    }

    private Candle[] whiteCandles;
    private Candle[] blackCandles;
    private Material ringMaterial;
    private Mesh ringMesh;
    private bool ready;
    private bool externalTurnProvided;
    private bool warningShown;
    private float blend;
    private float nextAutoCheck;
    private float nextControllerLookup;
    private string lastTurnSource = "";

    private IEnumerator Start()
    {
        // Candles are instantiated in BoardCandleGenerator.Start().
        for (int attempt = 0; attempt < 120; attempt++)
        {
            Candle frontLeft = FindCandle("Chandelier_AvantGauche");
            Candle frontRight = FindCandle("Chandelier_AvantDroit");
            Candle rearLeft = FindCandle("Chandelier_ArriereGauche");
            Candle rearRight = FindCandle("Chandelier_ArriereDroit");

            if (frontLeft != null && frontRight != null &&
                rearLeft != null && rearRight != null)
            {
                whiteCandles = new[] { frontLeft, frontRight };
                blackCandles = new[] { rearLeft, rearRight };

                if (showMagicRings)
                    CreateMagicRings();

                bool detected;
                if (automaticTurn && TryDetectTurn(out detected))
                    whiteTurn = detected;

                blend = whiteTurn ? 1f : 0f;
                ready = true;
                Debug.Log("Halloween bougies : 4 bougies trouvees. " +
                          "Blancs = avant / Noirs = arriere.", this);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }

        Debug.LogError("Halloween bougies : impossible de trouver les 4 chandeliers. " +
                       "Verifier que BoardCandleGenerator est actif dans cette scene.", this);
    }

    private Candle FindCandle(string candleName)
    {
        Transform root = null;

        if (candlesRoot != null)
        {
            foreach (Transform child in candlesRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == candleName)
                {
                    root = child;
                    break;
                }
            }
        }
        else
        {
            // Also works if the candles are under BoardBase or another parent.
            foreach (Transform child in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (child.name == candleName)
                {
                    root = child;
                    break;
                }
            }
        }

        if (root == null) return null;

        Transform flame = root.Find("FlammeAnimee");
        Transform outer = flame != null ? flame.Find("Flamme_exterieure") : null;
        Transform core = flame != null ? flame.Find("Coeur_lumineux") : null;
        Transform lightObject = root.Find("Lumiere_douce");

        if (flame == null || outer == null || core == null || lightObject == null)
            return null;

        Renderer outerRenderer = outer.GetComponent<Renderer>();
        Renderer coreRenderer = core.GetComponent<Renderer>();
        Light light = lightObject.GetComponent<Light>();
        if (outerRenderer == null || coreRenderer == null || light == null)
            return null;

        return new Candle
        {
            root = root,
            flame = flame,
            outer = outerRenderer,
            core = coreRenderer,
            lightSource = light
        };
    }

    private void Update()
    {
        if (!ready) return;

        if (automaticTurn && !externalTurnProvided &&
            Time.unscaledTime >= nextAutoCheck)
        {
            nextAutoCheck = Time.unscaledTime + 0.15f;

            bool detected;
            if (TryDetectTurn(out detected))
            {
                if (detected != whiteTurn)
                    Debug.Log("Halloween bougies : tour des " +
                              (detected ? "BLANCS" : "NOIRS") +
                              " (" + lastTurnSource + ")", this);
                whiteTurn = detected;
            }
            else if (!warningShown && Time.unscaledTime > 4f)
            {
                warningShown = true;
                Debug.LogWarning(
                    "Halloween bougies : source du tour introuvable. " +
                    "Glisse ChessGameController ou le TMP 'Trait aux Blancs/Noirs' " +
                    "dans l'Inspector. Sinon, appelle SetTurn(bool) " +
                    "au changement de joueur.", this);
            }
        }

        float target = whiteTurn ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target,
            Time.unscaledDeltaTime / Mathf.Max(0.1f, transitionSeconds));
    }

    private void LateUpdate()
    {
        if (!ready) return;
        ApplyCandles(whiteCandles, blend, WarmFlame, WarmCore, 0f);
        ApplyCandles(blackCandles, 1f - blend, CoolFlame, CoolCore, 1.7f);
    }

    private void ApplyCandles(Candle[] candles, float active, Color flameColor,
                              Color coreColor, float phase)
    {
        for (int n = 0; n < candles.Length; n++)
        {
            Candle candle = candles[n];
            if (candle == null || candle.lightSource == null) continue;

            float wave = 0.93f + 0.07f * Mathf.Sin(Time.unscaledTime * 12f + n + phase);
            float brightness = Mathf.Lerp(0.04f, 2.6f, active) * wave;

            // CandleFlameFlicker.Update() sets the scale every frame.
            // We modify it AFTER that, without disabling the natural flicker.
            if (candle.flame != null)
            {
                float size = Mathf.Lerp(inactiveFlameSize, activeFlameSize, active);
                size *= 1f + active * 0.065f * Mathf.Sin(Time.unscaledTime * 7f + n);
                candle.flame.localScale = Vector3.Scale(
                    candle.flame.localScale, new Vector3(size, size, size));
            }

            candle.lightSource.enabled = true;
            candle.lightSource.type = LightType.Point;
            candle.lightSource.color = flameColor;
            candle.lightSource.intensity = Mathf.Lerp(
                inactiveLightIntensity, activeLightIntensity, active) * wave;
            candle.lightSource.range = lightRange;
            candle.lightSource.shadows = LightShadows.None;

            SetRendererColor(candle.outer, candle.outerBlock, flameColor * brightness);
            SetRendererColor(candle.core, candle.coreBlock, coreColor * brightness);

            if (!showMagicRings) continue;
            float ringGlow = active * ringBrightness *
                             (0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f + n));
            Color ringColor = coreColor * ringGlow;

            if (candle.ringLower != null)
            {
                candle.ringLower.enabled = active > 0.07f;
                SetRendererColor(candle.ringLower, candle.ringLowerBlock, ringColor);
                candle.ringLowerTransform.localRotation = Quaternion.Euler(
                    0f, Time.unscaledTime * ringRotationSpeed, 0f);
            }
            if (candle.ringUpper != null)
            {
                candle.ringUpper.enabled = active > 0.07f;
                SetRendererColor(candle.ringUpper, candle.ringUpperBlock,
                    ringColor * 0.68f);
                candle.ringUpperTransform.localRotation = Quaternion.Euler(
                    0f, -Time.unscaledTime * ringRotationSpeed * 0.7f, 0f);
                candle.ringUpperTransform.localPosition = new Vector3(
                    0f, 1.04f + 0.07f * Mathf.Sin(Time.unscaledTime * 2.6f + n), 0f);
            }
        }
    }

    private static void SetRendererColor(Renderer renderer, MaterialPropertyBlock block,
                                         Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private void CreateMagicRings()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("Halloween bougies : shader Unlit introuvable, " +
                             "cercles desactives.", this);
            return;
        }

        ringMaterial = new Material(shader) { name = "Halloween_CandleTurn_Rings" };
        if (ringMaterial.HasProperty("_BaseColor"))
            ringMaterial.SetColor("_BaseColor", Color.white);
        if (ringMaterial.HasProperty("_Color"))
            ringMaterial.SetColor("_Color", Color.white);

        ringMesh = CreateRingMesh(0.36f, 0.46f, 48);
        foreach (Candle candle in whiteCandles) AddRings(candle);
        foreach (Candle candle in blackCandles) AddRings(candle);
    }

    private void AddRings(Candle candle)
    {
        candle.ringLower = AddRing(candle, "CandleTurn_Ring_Lower",
            new Vector3(0f, 0.75f, 0f), 1f, out candle.ringLowerTransform);
        candle.ringUpper = AddRing(candle, "CandleTurn_Ring_Upper",
            new Vector3(0f, 1.04f, 0f), 0.77f, out candle.ringUpperTransform);
    }

    private Renderer AddRing(Candle candle, string name, Vector3 pos,
                             float scale, out Transform ringTransform)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(candle.root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * scale;
        ringTransform = go.transform;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = ringMesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments)
    {
        Vector3[] vertices = new Vector3[segments * 2];
        int[] tris = new int[segments * 12];

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(cs * innerRadius, 0f, sn * innerRadius);
            vertices[i * 2 + 1] = new Vector3(cs * outerRadius, 0f, sn * outerRadius);

            int a = i * 2;
            int b = a + 1;
            int c = ((i + 1) % segments) * 2;
            int d = c + 1;
            int k = i * 12;
            // Both sides, so the rings remain visible from a low-angle camera.
            tris[k] = a; tris[k + 1] = c; tris[k + 2] = b;
            tris[k + 3] = b; tris[k + 4] = c; tris[k + 5] = d;
            tris[k + 6] = a; tris[k + 7] = b; tris[k + 8] = c;
            tris[k + 9] = b; tris[k + 10] = d; tris[k + 11] = c;
        }

        Mesh mesh = new Mesh { name = "HalloweenTurn_CircleMesh" };
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private bool TryDetectTurn(out bool isWhite)
    {
        isWhite = whiteTurn;

        // An explicit UI reference is the most trustworthy source.
        if (turnTextTMP != null && TryParseTurn(turnTextTMP.text, out isWhite))
        {
            lastTurnSource = "TMP assigne";
            return true;
        }
        if (turnTextLegacy != null && TryParseTurn(turnTextLegacy.text, out isWhite))
        {
            lastTurnSource = "UI Text assigne";
            return true;
        }

        if (chessGameController == null && Time.unscaledTime >= nextControllerLookup)
        {
            nextControllerLookup = Time.unscaledTime + 1.5f;
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component != null &&
                    component.GetType().Name == "ChessGameController")
                {
                    chessGameController = component;
                    break;
                }
            }
        }

        if (chessGameController != null &&
            TryReadController(chessGameController, out isWhite))
        {
            lastTurnSource = "ChessGameController";
            return true;
        }

        foreach (TMP_Text label in FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label != null && LooksLikeTurnLabel(label.text) &&
                TryParseTurn(label.text, out isWhite))
            {
                turnTextTMP = label;
                lastTurnSource = "TMP detecte automatiquement";
                return true;
            }
        }
        foreach (Text label in FindObjectsByType<Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label != null && LooksLikeTurnLabel(label.text) &&
                TryParseTurn(label.text, out isWhite))
            {
                turnTextLegacy = label;
                lastTurnSource = "UI Text detecte automatiquement";
                return true;
            }
        }
        return false;
    }

    private static bool LooksLikeTurnLabel(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Contains(text, "trait") || Contains(text, "tour") ||
               Contains(text, "turn") || Contains(text, "move");
    }

    private static bool Contains(string text, string part)
    {
        return text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryParseTurn(string text, out bool isWhite)
    {
        isWhite = true;
        if (string.IsNullOrEmpty(text)) return false;
        if (Contains(text, "noir") || Contains(text, "black"))
        {
            isWhite = false;
            return true;
        }
        if (Contains(text, "blanc") || Contains(text, "white"))
        {
            isWhite = true;
            return true;
        }
        return false;
    }

    private bool TryReadController(MonoBehaviour controller, out bool isWhite)
    {
        isWhite = whiteTurn;
        Type type = controller.GetType();
        const BindingFlags flags = BindingFlags.Instance |
                                   BindingFlags.Public | BindingFlags.NonPublic;

        if (!string.IsNullOrWhiteSpace(controllerTurnMemberName) &&
            TryReadMember(controller, type, controllerTurnMemberName, flags, out isWhite))
            return true;

        string[] likelyMembers =
        {
            "IsWhiteTurn", "isWhiteTurn", "WhiteToMove", "whiteToMove",
            "CurrentTurn", "currentTurn", "WhiteTurn", "whiteTurn",
            "CurrentPlayer", "currentPlayer", "SideToMove", "sideToMove",
            "PlayerToMove", "playerToMove", "isWhitesTurn", "isWhiteToMove",
            "IsBlackTurn", "isBlackTurn", "blackTurn", "BlackToMove"
        };
        foreach (string name in likelyMembers)
            if (TryReadMember(controller, type, name, flags, out isWhite))
                return true;

        string[] likelyMethods = { "GetCurrentTurn", "GetCurrentPlayer", "IsWhiteTurn", "IsWhiteToMove" };
        foreach (string name in likelyMethods)
        {
            try
            {
                MethodInfo method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
                if (method != null && ParseMemberValue(name, method.Invoke(controller, null), out isWhite))
                    return true;
            }
            catch (Exception) { /* Another naming convention; continue. */ }
        }
        return false;
    }

    private bool TryReadMember(MonoBehaviour controller, Type type, string name,
                               BindingFlags flags, out bool isWhite)
    {
        isWhite = whiteTurn;
        try
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null &&
                ParseMemberValue(name, field.GetValue(controller), out isWhite))
                return true;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0 &&
                ParseMemberValue(name, property.GetValue(controller, null), out isWhite))
                return true;
        }
        catch (Exception) { /* Unreadable property: ignore and try another. */ }
        return false;
    }

    private static bool ParseMemberValue(string memberName, object value, out bool isWhite)
    {
        isWhite = true;
        if (value == null) return false;
        if (value is bool boolean)
        {
            // 'isBlackTurn' has the inverse meaning of 'isWhiteTurn'.
            bool inverted = Contains(memberName, "black") || Contains(memberName, "noir");
            isWhite = inverted ? !boolean : boolean;
            return true;
        }
        return TryParseTurn(value.ToString(), out isWhite);
    }

    /// <summary>
    /// Recommended for an exact integration: call this every time the game changes turn.
    /// true = Whites, false = Blacks. Takes priority over automatic UI scanning.
    /// </summary>
    public void SetTurn(bool whitesTurn)
    {
        whiteTurn = whitesTurn;
        externalTurnProvided = true;
        automaticTurn = true;
    }

    public void SetWhiteTurn() { SetTurn(true); }
    public void SetBlackTurn() { SetTurn(false); }

    [ContextMenu("TEST : Allumer les BLANCS (avant)")]
    private void TestWhite()
    {
        automaticTurn = false;
        externalTurnProvided = false;
        whiteTurn = true;
    }

    [ContextMenu("TEST : Allumer les NOIRS (arriere)")]
    private void TestBlack()
    {
        automaticTurn = false;
        externalTurnProvided = false;
        whiteTurn = false;
    }

    [ContextMenu("Retour AUTO : lire le controleur ou le texte")]
    private void ResumeAuto()
    {
        automaticTurn = true;
        externalTurnProvided = false;
        warningShown = false;
        nextAutoCheck = 0f;
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
        if (ringMesh != null) Destroy(ringMesh);
    }
}
