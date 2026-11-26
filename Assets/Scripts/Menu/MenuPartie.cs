using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MenuVisuel;

internal sealed class MenuPartie : MonoBehaviour
{
    private Texture2D textureArrondi;
    private Texture2D textureDamier;
    private Sprite arrondi;
    private Text titre;
    private Text motif;
    private MenuBouton boutonPrincipal;
    private MenuBouton boutonAbandon;
    private RectTransform boutonRetour;
    private RectTransform boutonQuitter;
    private UnityAction actionPrincipale;

    internal static MenuPartie Creer(Scene scene, ModeDeJeu mode, UnityAction abandonner, UnityAction retour,
        UnityAction quitter)
    {
        RectTransform racine = CreerRect("Menu de partie", null);
        SceneManager.MoveGameObjectToScene(racine.gameObject, scene);
        var menu = racine.gameObject.AddComponent<MenuPartie>();
        menu.Construire(racine, MainMenu.ThemeDe(mode), abandonner, retour, quitter);
        return menu;
    }

    internal void AfficherPause(UnityAction reprendre, bool peutAbandonner)
    {
        Afficher("PAUSE", "", "REPRENDRE", reprendre, peutAbandonner);
    }

    internal void AfficherFin(string resultat, string raison, UnityAction copier)
    {
        Afficher(resultat.ToUpperInvariant(), raison.ToUpperInvariant(), "COPIER LE PGN", copier, false);
    }

    private void Afficher(string entete, string raison, string libellePrincipal, UnityAction action, bool abandonVisible)
    {
        titre.text = entete;
        motif.text = raison;
        boutonPrincipal.texte.text = libellePrincipal;
        actionPrincipale = action;
        boutonAbandon.gameObject.SetActive(abandonVisible);
        boutonRetour.anchoredPosition = new Vector2(0f, abandonVisible ? -236f : -118f);
        boutonQuitter.anchoredPosition = new Vector2(0f, abandonVisible ? -354f : -236f);
        gameObject.SetActive(true);
        AssurerEventSystem().SetSelectedGameObject(boutonPrincipal.gameObject);
    }

    internal void Masquer()
    {
        EventSystem systeme = EventSystem.current;
        GameObject selection = systeme != null ? systeme.currentSelectedGameObject : null;
        if (selection != null && selection.transform.IsChildOf(transform))
            systeme.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    internal void IndiquerCopie() => boutonPrincipal.texte.text = "PGN COPIÉ";

    private void Construire(RectTransform racine, MainMenu.Theme theme, UnityAction abandonner, UnityAction retour,
        UnityAction quitter)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        Color fond = theme.fond;
        fond.a = 0.90f;
        Etirer(CreerImage("Fond", racine, null, fond).rectTransform);

        textureDamier = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        DessinerDamier(textureDamier);
        RawImage damier = CreerRect("Damier", racine).gameObject.AddComponent<RawImage>();
        Etirer(damier.rectTransform);
        damier.texture = textureDamier;
        damier.color = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.035f);
        damier.uvRect = new Rect(0f, 0f, 16f, 9f);
        damier.raycastTarget = false;

        Font police = ChargerPolice();
        titre = CreerTexte("Résultat", racine, "", police, 120, FontStyle.Bold, theme.texte);
        titre.rectTransform.anchoredPosition = new Vector2(0f, 275f);
        AjouterReliefTitre(titre, theme.accent);

        motif = CreerTexte("Motif", racine, "", police, 32, FontStyle.Bold, theme.accent);
        motif.rectTransform.anchoredPosition = new Vector2(0f, 151f);

        Color couleurLigne = theme.accent;
        couleurLigne.a = 0.35f;
        Image ligne = CreerImage("Séparateur", racine, null, couleurLigne);
        ligne.rectTransform.anchoredPosition = new Vector2(0f, 102f);
        ligne.rectTransform.sizeDelta = new Vector2(520f, 4f);
        ligne.raycastTarget = false;

        textureArrondi = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        arrondi = CreerSpriteArrondi(textureArrondi);

        MenuBouton AjouterBouton(string libelle, float y, UnityAction action)
        {
            MenuBouton bouton = CreerBouton(racine, libelle, new Vector2(0f, y), new Vector2(560f, 92f),
                38, police, arrondi, theme.texte, action);
            TeinterBouton(bouton, theme.accent, theme.texte);
            return bouton;
        }

        boutonPrincipal = AjouterBouton("Action principale", 0f, () => actionPrincipale());
        boutonAbandon = AjouterBouton("ABANDONNER", -118f, abandonner);
        boutonRetour = (RectTransform)AjouterBouton("RETOUR AU MENU", -236f, retour).transform;
        boutonQuitter = (RectTransform)AjouterBouton("QUITTER LE JEU", -354f, quitter).transform;
    }

    private void OnDestroy()
    {
        Destroy(arrondi);
        Destroy(textureArrondi);
        Destroy(textureDamier);
    }
}
