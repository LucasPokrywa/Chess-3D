
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnLightIndicator : MonoBehaviour
{
    [Header("Synchronisation avec la partie")]
    [SerializeField] private bool followGameTurnText = true;
    [SerializeField] private bool whiteTurn = true;

    [Header("Couleurs")]
    [SerializeField]
    private Color whiteLightColor =
        new Color(1f, 0.92f, 0.75f);

    [SerializeField]
    private Color blackLightColor =
        new Color(0.55f, 0.30f, 1f);

    [SerializeField]
    private Color inactiveColor =
        new Color(0.10f, 0.08f, 0.17f);

    [Header("Luminosité")]
    [SerializeField, Range(0f, 6f)]
    private float activeEmission = 2f;

    private Renderer[] frontRenderers;
    private Renderer[] backRenderers;

    private TMP_Text tmpTurnLabel;
    private Text legacyTurnLabel;

    private bool initialized;
    private bool displayedWhiteTurn;
    private float nextTextCheck;

    private IEnumerator Start()
    {
        // Attend la génération du plateau.
        for (int attempt = 0; attempt < 300; attempt++)
        {
            Transform boardBase = FindBoardBase();

            if (boardBase != null &&
                TryInitializeRenderers(boardBase))
            {
                initialized = true;
                ApplyVisuals();

                Debug.Log(
                    "TurnLightIndicator : " +
                    "barre et 4 coins trouvés."
                );

                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "TurnLightIndicator : certains objets " +
            "du BoardBase sont introuvables."
        );
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (followGameTurnText &&
            Time.unscaledTime >= nextTextCheck)
        {
            nextTextCheck = Time.unscaledTime + 0.2f;

            bool detectedTurn;

            if (TryReadGameTurn(out detectedTurn))
            {
                whiteTurn = detectedTurn;
            }
        }

        // Permet aussi de tester en modifiant
        // White Turn dans l'Inspector.
        if (whiteTurn != displayedWhiteTurn)
        {
            ApplyVisuals();
        }
    }

    private Transform FindBoardBase()
    {
        GameObject board = GameObject.Find("Board");

        if (board != null)
        {
            return FindDeepChild(
                board.transform,
                "BoardBase"
            );
        }

        GameObject baseObject =
            GameObject.Find("BoardBase");

        return baseObject != null
            ? baseObject.transform
            : null;
    }

    private Transform FindDeepChild(
        Transform root,
        string childName)
    {
        foreach (Transform child in
            root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private Renderer FindRenderer(
        Transform root,
        string objectName)
    {
        Transform child =
            FindDeepChild(root, objectName);

        if (child == null)
            return null;

        return child.GetComponent<Renderer>() ??
            child.GetComponentInChildren<Renderer>(true);
    }

    private bool TryInitializeRenderers(
        Transform boardBase)
    {
        Renderer frontBar =
            FindRenderer(boardBase, "FrontBorder");

        Renderer frontLeft =
            FindRenderer(
                boardBase,
                "CornerPillar_-1_-1"
            );

        Renderer frontRight =
            FindRenderer(
                boardBase,
                "CornerPillar_1_-1"
            );

        Renderer backLeft =
            FindRenderer(
                boardBase,
                "CornerPillar_-1_1"
            );

        Renderer backRight =
            FindRenderer(
                boardBase,
                "CornerPillar_1_1"
            );

        if (frontBar == null ||
            frontLeft == null ||
            frontRight == null ||
            backLeft == null ||
            backRight == null)
        {
            return false;
        }

        frontRenderers = new Renderer[]
        {
            frontBar,
            frontLeft,
            frontRight
        };

        backRenderers = new Renderer[]
        {
            backLeft,
            backRight
        };

        foreach (Renderer r in frontRenderers)
            PrepareRenderer(r);

        foreach (Renderer r in backRenderers)
            PrepareRenderer(r);

        return true;
    }

    private void PrepareRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        // Material crée une instance propre
        // à cet objet pendant le mode Play.
        Material mat = renderer.material;

        if (mat.HasProperty("_EmissionColor"))
            mat.EnableKeyword("_EMISSION");
    }

    private bool TryReadGameTurn(out bool isWhite)
    {
        isWhite = whiteTurn;

        if (tmpTurnLabel == null &&
            legacyTurnLabel == null)
        {
            TMP_Text[] tmpTexts =
                FindObjectsByType<TMP_Text>(
                    FindObjectsSortMode.None
                );

            foreach (TMP_Text label in tmpTexts)
            {
                if (label.text.IndexOf(
                    "Trait aux",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tmpTurnLabel = label;
                    break;
                }
            }

            if (tmpTurnLabel == null)
            {
                Text[] texts =
                    FindObjectsByType<Text>(
                        FindObjectsSortMode.None
                    );

                foreach (Text label in texts)
                {
                    if (label.text.IndexOf(
                        "Trait aux",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        legacyTurnLabel = label;
                        break;
                    }
                }
            }
        }

        string turnText = "";

        if (tmpTurnLabel != null)
            turnText = tmpTurnLabel.text;
        else if (legacyTurnLabel != null)
            turnText = legacyTurnLabel.text;

        if (turnText.IndexOf(
            "Noirs",
            StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isWhite = false;
            return true;
        }

        if (turnText.IndexOf(
            "Blancs",
            StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isWhite = true;
            return true;
        }

        return false;
    }

    public void SetWhiteTurn()
    {
        SetTurn(true);
    }

    public void SetBlackTurn()
    {
        SetTurn(false);
    }

    public void SetTurn(bool isWhite)
    {
        whiteTurn = isWhite;
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (!initialized)
            return;

        displayedWhiteTurn = whiteTurn;

        SetGroup(
            frontRenderers,
            whiteTurn,
            whiteLightColor
        );

        SetGroup(
            backRenderers,
            !whiteTurn,
            blackLightColor
        );
    }

    private void SetGroup(
        Renderer[] renderers,
        bool active,
        Color activeColor)
    {
        if (renderers == null)
            return;

        Color baseColor =
            active ? activeColor : inactiveColor;

        Color emission =
            active
                ? activeColor * activeEmission
                : Color.black;

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            Material mat = r.material;

            MaterialPropertyBlock block =
                new MaterialPropertyBlock();

            r.GetPropertyBlock(block);

            // Change réellement la couleur du matériau,
            // pas seulement son émission.
            if (mat.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", baseColor);

            if (mat.HasProperty("_Color"))
                block.SetColor("_Color", baseColor);

            if (mat.HasProperty("_EmissionColor"))
                block.SetColor("_EmissionColor", emission);

            r.SetPropertyBlock(block);
        }
    }

    [ContextMenu("TEST - Tour des Blancs")]
    private void TestWhiteTurn()
    {
        followGameTurnText = false;
        SetWhiteTurn();
    }

    [ContextMenu("TEST - Tour des Noirs")]
    private void TestBlackTurn()
    {
        followGameTurnText = false;
        SetBlackTurn();
    }
}
