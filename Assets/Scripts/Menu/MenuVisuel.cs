using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

internal static class MenuVisuel
{
    internal static Font ChargerPolice() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    internal static EventSystem AssurerEventSystem()
    {
        var systeme = EventSystem.current != null ? EventSystem.current : Object.FindAnyObjectByType<EventSystem>();
        if (systeme != null) return systeme;

        var go = new GameObject("EventSystem");
        systeme = go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        return systeme;
    }

    internal static Sprite CreerSpriteArrondi(Texture2D texture)
    {
        const int rayon = 18;
        int taille = texture.width;
        for (int y = 0; y < taille; y++)
            for (int x = 0; x < taille; x++)
            {
                float dx = Mathf.Max(rayon - x - 0.5f, x + 0.5f - (taille - rayon), 0f);
                float dy = Mathf.Max(rayon - y - 0.5f, y + 0.5f - (taille - rayon), 0f);
                float a = Mathf.Clamp01(rayon - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(rayon + 2, rayon + 2, rayon + 2, rayon + 2));
    }

    internal static void DessinerDamier(Texture2D texture)
    {
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.SetPixels(new[] { Color.white, Color.clear, Color.clear, Color.white });
        texture.Apply();
    }

    internal static Outline AjouterReliefTitre(Text titre, Color accent)
    {
        Outline contour = titre.gameObject.AddComponent<Outline>();
        contour.effectDistance = new Vector2(3f, -3f);
        contour.effectColor = new Color(accent.r, accent.g, accent.b, 0.7f);
        var ombre = titre.gameObject.AddComponent<Shadow>();
        ombre.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ombre.effectDistance = new Vector2(0f, -10f);
        return contour;
    }

    internal static MenuBouton CreerBouton(Transform parent, string libelle, Vector2 position, Vector2 taille,
        int tailleTexte, Font police, Sprite arrondi, Color texteNormal, UnityAction action)
    {
        RectTransform rect = CreerRect("Bouton " + libelle, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = taille;

        Image bordure = CreerImage("Bordure", rect, arrondi, Color.white);
        bordure.type = Image.Type.Sliced;
        Etirer(bordure.rectTransform);
        bordure.rectTransform.offsetMin = new Vector2(-3f, -3f);
        bordure.rectTransform.offsetMax = new Vector2(3f, 3f);

        Image fond = CreerImage("Fond", rect, arrondi, Color.black);
        fond.type = Image.Type.Sliced;
        Etirer(fond.rectTransform);

        Button bouton = rect.gameObject.AddComponent<Button>();
        bouton.transition = Selectable.Transition.None;
        bouton.targetGraphic = fond;
        bouton.onClick.AddListener(action);

        MenuBouton visuel = rect.gameObject.AddComponent<MenuBouton>();
        visuel.fond = fond;
        visuel.bordure = bordure;
        visuel.bordureNormal = new Color(1f, 1f, 1f, 0.12f);
        visuel.fondNormal = new Color(0f, 0f, 0f, 0.55f);

        if (!string.IsNullOrEmpty(libelle))
        {
            Text texte = CreerTexte("Texte", rect, libelle, police, tailleTexte, FontStyle.Bold, texteNormal);
            Etirer(texte.rectTransform);
            texte.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
            visuel.texte = texte;
        }
        return visuel;
    }

    internal static void TeinterBouton(MenuBouton bouton, Color accent, Color texte)
    {
        bouton.bordureFocus = accent;
        bouton.fondFocus = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.9f);
        bouton.texteFocus = Color.white;
        bouton.texteNormal = texte;
    }

    internal static Text CreerTexte(string nom, Transform parent, string contenu, Font police, int taille,
        FontStyle style, Color couleur)
    {
        RectTransform rect = CreerRect(nom, parent);
        rect.sizeDelta = new Vector2(1400f, taille * 1.6f);
        Text texte = rect.gameObject.AddComponent<Text>();
        texte.font = police;
        texte.text = contenu;
        texte.fontSize = taille;
        texte.fontStyle = style;
        texte.color = couleur;
        texte.alignment = TextAnchor.MiddleCenter;
        texte.horizontalOverflow = HorizontalWrapMode.Overflow;
        texte.verticalOverflow = VerticalWrapMode.Overflow;
        texte.raycastTarget = false;
        return texte;
    }

    internal static Image CreerImage(string nom, Transform parent, Sprite sprite, Color couleur)
    {
        Image image = CreerRect(nom, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = couleur;
        return image;
    }

    internal static RectTransform CreerRect(string nom, Transform parent)
    {
        var go = new GameObject(nom, typeof(RectTransform)) { layer = 5 }; // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Etirer(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
