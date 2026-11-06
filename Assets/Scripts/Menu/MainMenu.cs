using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// Menu principal construit entièrement par le code (comme les générateurs du plateau) :
// il suffit de poser ce composant sur un GameObject vide de la scène MainMenu.
//
// Écran titre  : JOUER / QUITTER
// Écran partie : choix du mode (Classique / Halloween), choix de la difficulté de l'IA
//                (verrouillé tant que GameSettings.DifficulteDisponible est false), LANCER.
public class MainMenu : MonoBehaviour
{
    [Header("Ambiance")]
    [SerializeField, Range(0, 120)] private int nombreParticules = 55;
    [SerializeField, Range(0.1f, 3f)] private float dureeFondu = 0.6f;

    // Palette de chaque mode : le menu bascule en douceur de l'une à l'autre.
    private struct Theme
    {
        public Color fond;       // couleur du haut de l'écran
        public Color accent;     // titres, bordures, particules
        public Color accent2;    // deuxième couleur des particules
        public Color texte;
        public Color caseClaire; // emblème en damier des cartes
        public Color caseSombre;
    }

    private static readonly Theme ThemeClassique = new Theme
    {
        fond = new Color(0.13f, 0.09f, 0.06f),
        accent = new Color(1f, 0.78f, 0.38f),
        accent2 = new Color(1f, 0.93f, 0.75f),
        texte = new Color(0.96f, 0.91f, 0.8f),
        caseClaire = new Color(0.93f, 0.87f, 0.74f),
        caseSombre = new Color(0.2f, 0.13f, 0.08f)
    };

    private static readonly Theme ThemeHalloween = new Theme
    {
        fond = new Color(0.14f, 0.04f, 0.2f),
        accent = new Color(1f, 0.5f, 0.1f),
        accent2 = new Color(0.65f, 0.3f, 1f),
        texte = new Color(0.97f, 0.88f, 1f),
        caseClaire = new Color(1f, 0.52f, 0.12f),
        caseSombre = new Color(0.22f, 0.06f, 0.32f)
    };

    private static Theme ThemeDe(ModeDeJeu mode) => mode == ModeDeJeu.Halloween ? ThemeHalloween : ThemeClassique;

    private struct Particule
    {
        public RectTransform rect;
        public Image image;
        public float vitesse, phase, amplitude, x, alpha, melange;
    }

    private Font police;
    private Sprite spriteArrondi;
    private Sprite spriteRond;
    private Texture2D texDamier;
    private Texture2D texDegrade;
    private readonly List<Object> ressources = new List<Object>();

    private RectTransform racine;
    private Image fond;
    private RawImage damier;
    private Image halo;
    private Image fondu;
    private RectTransform zoneParticules;
    private readonly List<Particule> particules = new List<Particule>();

    private CanvasGroup ecranTitre;
    private CanvasGroup ecranPartie;
    private CanvasGroup ecranActif;
    private Text titre;
    private Outline contourTitre;
    private Text titrePartie;
    private Text messageErreur;
    private Image badgeBientot;
    private Button boutonJouer;

    private readonly List<MenuBouton> boutonsAccent = new List<MenuBouton>();
    private readonly Dictionary<ModeDeJeu, MenuBouton> cartesMode = new Dictionary<ModeDeJeu, MenuBouton>();
    private readonly Dictionary<DifficulteIA, MenuBouton> boutonsDifficulte = new Dictionary<DifficulteIA, MenuBouton>();
    private readonly List<Text> textesAccent = new List<Text>();

    private ModeDeJeu mode;
    private DifficulteIA difficulte;
    private Theme theme;
    private bool transition;

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private void Awake()
    {
        mode = GameSettings.Mode;
        difficulte = GameSettings.Difficulte;
        theme = ThemeDe(mode);

        police = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreerTextures();
        AssurerCamera();
        AssurerEventSystem();
        ConstruireInterface();
        AppliquerSelection();
        AppliquerTheme(1f);
    }

    private void Start()
    {
        AfficherEcran(ecranTitre, true);
        StartCoroutine(Fondu(1f, 0f));
    }

    private void OnDestroy()
    {
        foreach (Object o in ressources)
            if (o != null) Destroy(o);
    }

    private void AssurerCamera()
    {
        if (Camera.main != null) return;
        var go = new GameObject("Camera Menu") { tag = "MainCamera" };
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        go.AddComponent<AudioListener>();
    }

    private void AssurerEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    private void CreerTextures()
    {
        // Rectangle arrondi découpé en 9 (bordures nettes quelle que soit la taille).
        const int t = 64, r = 18;
        var arrondi = NouvelleTexture(t, t, FilterMode.Bilinear);
        for (int y = 0; y < t; y++)
            for (int x = 0; x < t; x++)
            {
                float dx = Mathf.Max(r - x - 0.5f, x + 0.5f - (t - r), 0f);
                float dy = Mathf.Max(r - y - 0.5f, y + 0.5f - (t - r), 0f);
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                arrondi.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        arrondi.Apply();
        spriteArrondi = Sprite.Create(arrondi, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
        ressources.Add(spriteArrondi);

        // Disque flou (particules, halo).
        const int d = 64;
        var rond = NouvelleTexture(d, d, FilterMode.Bilinear);
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(d / 2f, d / 2f)) / (d / 2f);
                float a = Mathf.Clamp01(1f - dist);
                rond.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        rond.Apply();
        spriteRond = Sprite.Create(rond, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        ressources.Add(spriteRond);

        // Damier 2x2 répété en fond.
        texDamier = NouvelleTexture(2, 2, FilterMode.Point);
        texDamier.wrapMode = TextureWrapMode.Repeat;
        texDamier.SetPixels(new[] { Color.white, Color.clear, Color.clear, Color.white });
        texDamier.Apply();

        // Assombrissement vers le bas de l'écran.
        texDegrade = NouvelleTexture(1, 128, FilterMode.Bilinear);
        for (int y = 0; y < 128; y++)
            texDegrade.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Lerp(0.92f, 0f, y / 127f)));
        texDegrade.Apply();
    }

    private Texture2D NouvelleTexture(int largeur, int hauteur, FilterMode filtre)
    {
        var tex = new Texture2D(largeur, hauteur, TextureFormat.RGBA32, false)
        {
            filterMode = filtre,
            wrapMode = TextureWrapMode.Clamp
        };
        ressources.Add(tex);
        return tex;
    }

    private void ConstruireInterface()
    {
        var canvasGo = new GameObject("Menu Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        racine = (RectTransform)canvasGo.transform;

        // --- Fond animé ---
        fond = CreerImage("Fond", racine, null, theme.fond);
        Etirer(fond.rectTransform);
        fond.raycastTarget = false;

        damier = CreerRect("Damier", racine).gameObject.AddComponent<RawImage>();
        Etirer(damier.rectTransform);
        damier.texture = texDamier;
        damier.raycastTarget = false;

        halo = CreerImage("Halo", racine, spriteRond, Color.clear);
        halo.rectTransform.sizeDelta = new Vector2(1500f, 900f);
        halo.rectTransform.anchoredPosition = new Vector2(0f, 220f);
        halo.raycastTarget = false;

        RawImage degrade = CreerRect("Degrade", racine).gameObject.AddComponent<RawImage>();
        Etirer(degrade.rectTransform);
        degrade.texture = texDegrade;
        degrade.raycastTarget = false;

        zoneParticules = CreerRect("Particules", racine);
        Etirer(zoneParticules);
        CreerParticules();

        // --- Écrans ---
        ecranTitre = CreerEcran("Ecran Titre");
        ConstruireEcranTitre(ecranTitre.transform);

        ecranPartie = CreerEcran("Ecran Partie");
        ConstruireEcranPartie(ecranPartie.transform);

        // Fondu noir, au-dessus de tout.
        fondu = CreerImage("Fondu", racine, null, Color.black);
        Etirer(fondu.rectTransform);
        fondu.raycastTarget = true;
    }

    private void ConstruireEcranTitre(Transform parent)
    {
        titre = CreerTexte("Titre", parent, "CHESS 3D", 168, FontStyle.Bold, theme.texte);
        titre.rectTransform.anchoredPosition = new Vector2(0f, 230f);
        titre.rectTransform.sizeDelta = new Vector2(1600f, 220f);
        contourTitre = titre.gameObject.AddComponent<Outline>();
        contourTitre.effectDistance = new Vector2(3f, -3f);
        var ombre = titre.gameObject.AddComponent<Shadow>();
        ombre.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ombre.effectDistance = new Vector2(0f, -10f);

        Text sousTitre = CreerTexte("Sous-titre", parent, "UN ÉCHIQUIER, DEUX MONDES", 34, FontStyle.Bold, theme.accent);
        sousTitre.rectTransform.anchoredPosition = new Vector2(0f, 95f);
        textesAccent.Add(sousTitre);

        CreerSeparateur(parent, new Vector2(0f, 50f), 520f);

        boutonJouer = CreerBouton(parent, "JOUER", new Vector2(0f, -90f), new Vector2(460f, 92f), 44,
            () => AfficherEcran(ecranPartie, false));
        CreerBouton(parent, "QUITTER", new Vector2(0f, -210f), new Vector2(460f, 92f), 40, Quitter);

        Text aide = CreerTexte("Aide", parent, "ENTRÉE  valider     ·     ÉCHAP  retour", 22, FontStyle.Normal,
            new Color(1f, 1f, 1f, 0.45f));
        Ancrer(aide.rectTransform, new Vector2(0.5f, 0f));
        aide.rectTransform.anchoredPosition = new Vector2(0f, 60f);
    }

    private void ConstruireEcranPartie(Transform parent)
    {
        titrePartie = CreerTexte("Titre", parent, "CHOISIS TON PLATEAU", 64, FontStyle.Bold, theme.texte);
        titrePartie.rectTransform.anchoredPosition = new Vector2(0f, 410f);
        titrePartie.rectTransform.sizeDelta = new Vector2(1600f, 100f);
        titrePartie.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -6f);
        CreerSeparateur(parent, new Vector2(0f, 350f), 420f);

        // --- Mode de jeu ---
        cartesMode[ModeDeJeu.Normal] = CreerCarteMode(parent, ModeDeJeu.Normal, new Vector2(-290f, 120f),
            "CLASSIQUE", "Un salon feutré, des pièces\nd'ivoire et d'ébène.");
        cartesMode[ModeDeJeu.Halloween] = CreerCarteMode(parent, ModeDeJeu.Halloween, new Vector2(290f, 120f),
            "HALLOWEEN", "Une crypte maudite, liches,\ngriffons et citadelles damnées.");

        // --- Difficulté de l'IA ---
        Text label = CreerTexte("Label Difficulte", parent, "DIFFICULTÉ DE L'IA", 30, FontStyle.Bold, theme.accent);
        label.rectTransform.anchoredPosition = new Vector2(0f, -135f);
        textesAccent.Add(label);

        // Badge affiché tant que la difficulté n'est pas branchée sur l'IA.
        badgeBientot = CreerImage("Badge Bientot", parent, spriteArrondi, theme.accent);
        badgeBientot.type = Image.Type.Sliced;
        badgeBientot.pixelsPerUnitMultiplier = 2f;
        badgeBientot.rectTransform.anchoredPosition = new Vector2(235f, -135f);
        badgeBientot.rectTransform.sizeDelta = new Vector2(130f, 36f);
        badgeBientot.raycastTarget = false;
        Text texteBadge = CreerTexte("Texte", badgeBientot.transform, "BIENTÔT", 20, FontStyle.Bold,
            new Color(0.08f, 0.04f, 0.02f));
        Etirer(texteBadge.rectTransform);
        badgeBientot.gameObject.SetActive(!GameSettings.DifficulteDisponible);

        string[] noms = { "FACILE", "NORMAL", "DIFFICILE" };
        for (int i = 0; i < 3; i++)
        {
            var niveau = (DifficulteIA)i;
            MenuBouton b = CreerBoutonVisuel(parent, noms[i], new Vector2((i - 1) * 270f, -215f),
                new Vector2(250f, 70f), 28, () => ChoisirDifficulte(niveau));
            b.GetComponent<Button>().interactable = GameSettings.DifficulteDisponible;
            boutonsDifficulte[niveau] = b;
        }

        // --- Actions ---
        CreerBouton(parent, "RETOUR", new Vector2(-330f, -380f), new Vector2(300f, 86f), 34,
            () => AfficherEcran(ecranTitre, false));
        CreerBouton(parent, "LANCER LA PARTIE", new Vector2(170f, -380f), new Vector2(560f, 96f), 42,
            LancerPartie);

        messageErreur = CreerTexte("Erreur", parent, "", 24, FontStyle.Bold, new Color(1f, 0.4f, 0.35f));
        messageErreur.rectTransform.anchoredPosition = new Vector2(0f, -470f);
        messageErreur.rectTransform.sizeDelta = new Vector2(1600f, 40f);
    }

    private MenuBouton CreerCarteMode(Transform parent, ModeDeJeu modeCarte, Vector2 position,
        string nom, string description)
    {
        Theme t = ThemeDe(modeCarte);
        MenuBouton carte = CreerBoutonVisuel(parent, "", position, new Vector2(520f, 330f), 0,
            () => ChoisirMode(modeCarte));
        carte.name = "Carte " + nom;
        carte.echelleFocus = 1.04f;
        carte.bordureFocus = t.accent;
        carte.fondFocus = Color.Lerp(new Color(0f, 0f, 0f, 0.75f), t.caseSombre, 0.55f);
        carte.fondFocus.a = 0.85f;

        // Emblème : petit damier aux couleurs du mode.
        const int n = 4;
        const float c = 30f;
        Image cadre = CreerImage("Embleme", carte.transform, null, t.accent);
        cadre.rectTransform.anchoredPosition = new Vector2(0f, 70f);
        cadre.rectTransform.sizeDelta = new Vector2(n * c + 8f, n * c + 8f);
        cadre.raycastTarget = false;
        RectTransform embleme = cadre.rectTransform;
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                Image caseImg = CreerImage("Case", embleme, null, (x + y) % 2 == 0 ? t.caseSombre : t.caseClaire);
                caseImg.rectTransform.sizeDelta = new Vector2(c, c);
                caseImg.rectTransform.anchoredPosition = new Vector2((x - (n - 1) / 2f) * c, (y - (n - 1) / 2f) * c);
                caseImg.raycastTarget = false;
            }

        Text titreCarte = CreerTexte("Nom", carte.transform, nom, 46, FontStyle.Bold, t.texte);
        titreCarte.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        titreCarte.rectTransform.sizeDelta = new Vector2(500f, 60f);
        var contour = titreCarte.gameObject.AddComponent<Outline>();
        contour.effectColor = new Color(t.accent.r, t.accent.g, t.accent.b, 0.5f);

        Text desc = CreerTexte("Description", carte.transform, description, 24, FontStyle.Italic,
            new Color(t.texte.r, t.texte.g, t.texte.b, 0.8f));
        desc.rectTransform.anchoredPosition = new Vector2(0f, -122f);
        desc.rectTransform.sizeDelta = new Vector2(480f, 70f);

        carte.texte = titreCarte;
        carte.texteNormal = new Color(t.texte.r, t.texte.g, t.texte.b, 0.7f);
        carte.texteFocus = t.texte;
        return carte;
    }

    private void CreerParticules()
    {
        for (int i = 0; i < nombreParticules; i++)
        {
            Image img = CreerImage("Particule", zoneParticules, spriteRond, Color.clear);
            img.raycastTarget = false;
            float taille = Random.Range(6f, 22f);
            img.rectTransform.sizeDelta = new Vector2(taille, taille);
            Ancrer(img.rectTransform, Vector2.zero);
            var p = new Particule
            {
                rect = img.rectTransform,
                image = img,
                vitesse = Random.Range(25f, 90f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                amplitude = Random.Range(10f, 45f),
                x = Random.value,
                alpha = Random.Range(0.25f, 0.85f),
                melange = Random.value
            };
            p.rect.anchoredPosition = new Vector2(0f, Random.Range(-50f, 1100f));
            particules.Add(p);
        }
    }

    // ------------------------------------------------------------------
    // Boucle
    // ------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float temps = Time.unscaledTime;

        AppliquerTheme(1f - Mathf.Exp(-dt * 4f));

        // Damier qui glisse lentement en fond.
        float tuiles = 14f;
        float ratio = racine.rect.height > 0f ? racine.rect.width / racine.rect.height : 16f / 9f;
        damier.uvRect = new Rect(temps * 0.03f, temps * 0.015f, tuiles * ratio, tuiles);

        // Halo et titre qui "respirent".
        float pulse = 0.5f + 0.5f * Mathf.Sin(temps * 1.6f);
        Color h = theme.accent;
        h.a = Mathf.Lerp(0.12f, 0.22f, pulse);
        halo.color = h;
        if (titre != null) titre.rectTransform.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(temps * 1.6f));

        float largeur = racine.rect.width, hauteur = racine.rect.height;
        for (int i = 0; i < particules.Count; i++)
        {
            Particule p = particules[i];
            Vector2 pos = p.rect.anchoredPosition;
            pos.y += p.vitesse * dt;
            if (pos.y > hauteur + 30f)
            {
                pos.y = -30f;
                p.x = Random.value;
            }
            pos.x = p.x * largeur + Mathf.Sin(temps * 0.8f + p.phase) * p.amplitude;
            p.rect.anchoredPosition = pos;

            // Les particules s'éteignent en montant.
            Color c = Color.Lerp(theme.accent, theme.accent2, p.melange);
            float scintille = 0.7f + 0.3f * Mathf.Sin(temps * 3f + p.phase * 4f);
            c.a = p.alpha * scintille * Mathf.Clamp01(1.2f - pos.y / Mathf.Max(hauteur, 1f));
            p.image.color = c;
            particules[i] = p;
        }

        GererClavier();
    }

    private void GererClavier()
    {
        if (transition) return;
        if (ToucheRetour() && ecranActif == ecranPartie)
            AfficherEcran(ecranTitre, false);

        // Rend le focus au clavier s'il a été perdu (clic dans le vide).
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && ToucheNavigation())
            SelectionnerPremier();
    }

    private static bool ToucheRetour()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool ToucheNavigation()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
            k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
            k.tabKey.wasPressedThisFrame);
#else
        return Input.GetAxisRaw("Vertical") != 0f || Input.GetAxisRaw("Horizontal") != 0f;
#endif
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    private void ChoisirMode(ModeDeJeu nouveau)
    {
        mode = nouveau;
        GameSettings.Mode = mode;
        AppliquerSelection();
    }

    private void ChoisirDifficulte(DifficulteIA niveau)
    {
        if (!GameSettings.DifficulteDisponible) return;
        difficulte = niveau;
        GameSettings.Difficulte = difficulte;
        AppliquerSelection();
    }

    private void AppliquerSelection()
    {
        foreach (var kv in cartesMode) kv.Value.Actif = kv.Key == mode;
        foreach (var kv in boutonsDifficulte)
            kv.Value.Actif = GameSettings.DifficulteDisponible && kv.Key == difficulte;
    }

    private void LancerPartie()
    {
        if (transition) return;
        string scene = GameSettings.SceneDuMode(mode);
        if (!GameSettings.SceneDisponible(scene))
        {
            messageErreur.text = $"La scène « {scene} » n'est pas dans les Build Settings.";
            Debug.LogError(messageErreur.text, this);
            return;
        }
        StartCoroutine(ChargerScene(scene));
    }

    private IEnumerator ChargerScene(string scene)
    {
        transition = true;
        yield return Fondu(0f, 1f);
        SceneManager.LoadScene(scene);
    }

    private void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    // Écrans et transitions
    // ------------------------------------------------------------------

    private void AfficherEcran(CanvasGroup ecran, bool immediat)
    {
        if (ecran == ecranActif || (transition && !immediat)) return;
        CanvasGroup precedent = ecranActif;
        ecranActif = ecran;
        messageErreur.text = "";
        if (immediat)
        {
            foreach (CanvasGroup g in new[] { ecranTitre, ecranPartie })
                Montrer(g, g == ecran ? 1f : 0f);
            SelectionnerPremier();
            return;
        }
        StartCoroutine(Basculer(precedent, ecran));
    }

    private IEnumerator Basculer(CanvasGroup sortant, CanvasGroup entrant)
    {
        transition = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        const float duree = 0.25f;
        for (float t = 0f; t < duree; t += Time.unscaledDeltaTime)
        {
            Montrer(sortant, 1f - t / duree);
            yield return null;
        }
        Montrer(sortant, 0f);
        for (float t = 0f; t < duree; t += Time.unscaledDeltaTime)
        {
            Montrer(entrant, t / duree);
            yield return null;
        }
        Montrer(entrant, 1f);
        transition = false;
        SelectionnerPremier();
    }

    private static void Montrer(CanvasGroup groupe, float alpha)
    {
        if (groupe == null) return;
        groupe.alpha = alpha;
        bool visible = alpha > 0.99f;
        groupe.interactable = visible;
        groupe.blocksRaycasts = visible;
        // Petit glissement vertical pendant l'apparition.
        groupe.transform.localPosition = new Vector3(0f, (1f - alpha) * -30f, 0f);
        groupe.gameObject.SetActive(alpha > 0f);
    }

    private void SelectionnerPremier()
    {
        if (EventSystem.current == null) return;
        GameObject cible = ecranActif == ecranPartie ? cartesMode[mode].gameObject : boutonJouer.gameObject;
        EventSystem.current.SetSelectedGameObject(cible);
    }

    private IEnumerator Fondu(float de, float vers)
    {
        fondu.gameObject.SetActive(true);
        for (float t = 0f; t < dureeFondu; t += Time.unscaledDeltaTime)
        {
            fondu.color = new Color(0f, 0f, 0f, Mathf.Lerp(de, vers, t / dureeFondu));
            yield return null;
        }
        fondu.color = new Color(0f, 0f, 0f, vers);
        fondu.gameObject.SetActive(vers > 0f);
    }

    // Rapproche les couleurs affichées de celles du mode choisi (1 = immédiat).
    private void AppliquerTheme(float progression)
    {
        Theme cible = ThemeDe(mode);
        theme.fond = Color.Lerp(theme.fond, cible.fond, progression);
        theme.accent = Color.Lerp(theme.accent, cible.accent, progression);
        theme.accent2 = Color.Lerp(theme.accent2, cible.accent2, progression);
        theme.texte = Color.Lerp(theme.texte, cible.texte, progression);

        fond.color = theme.fond;
        damier.color = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.035f);
        titre.color = theme.texte;
        titrePartie.color = theme.texte;
        contourTitre.effectColor = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.7f);
        foreach (Text t in textesAccent) t.color = theme.accent;
        badgeBientot.color = theme.accent;

        foreach (MenuBouton b in boutonsAccent)
        {
            b.bordureFocus = theme.accent;
            b.fondFocus = new Color(theme.accent.r * 0.35f, theme.accent.g * 0.35f, theme.accent.b * 0.35f, 0.9f);
            b.texteFocus = Color.white;
            b.texteNormal = theme.texte;
        }
    }

    // ------------------------------------------------------------------
    // Fabrique d'éléments d'interface
    // ------------------------------------------------------------------

    private CanvasGroup CreerEcran(string nom)
    {
        RectTransform rect = CreerRect(nom, racine);
        Etirer(rect);
        return rect.gameObject.AddComponent<CanvasGroup>();
    }

    private Button CreerBouton(Transform parent, string libelle, Vector2 position, Vector2 taille,
        int tailleTexte, UnityEngine.Events.UnityAction action)
    {
        return CreerBoutonVisuel(parent, libelle, position, taille, tailleTexte, action).GetComponent<Button>();
    }

    // Bouton au thème du menu : fond arrondi + bordure lumineuse + texte.
    private MenuBouton CreerBoutonVisuel(Transform parent, string libelle, Vector2 position, Vector2 taille,
        int tailleTexte, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreerRect("Bouton " + libelle, parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = taille;

        Image bordure = CreerImage("Bordure", rect, spriteArrondi, Color.white);
        bordure.type = Image.Type.Sliced;
        Etirer(bordure.rectTransform);
        bordure.rectTransform.offsetMin = new Vector2(-3f, -3f);
        bordure.rectTransform.offsetMax = new Vector2(3f, 3f);

        Image fondBouton = CreerImage("Fond", rect, spriteArrondi, Color.black);
        fondBouton.type = Image.Type.Sliced;
        Etirer(fondBouton.rectTransform);

        Button bouton = rect.gameObject.AddComponent<Button>();
        bouton.transition = Selectable.Transition.None;
        bouton.targetGraphic = fondBouton;
        bouton.onClick.AddListener(action);

        MenuBouton visuel = rect.gameObject.AddComponent<MenuBouton>();
        visuel.fond = fondBouton;
        visuel.bordure = bordure;
        visuel.bordureNormal = new Color(1f, 1f, 1f, 0.12f);
        visuel.fondNormal = new Color(0f, 0f, 0f, 0.55f);

        if (!string.IsNullOrEmpty(libelle))
        {
            Text texte = CreerTexte("Texte", rect, libelle, tailleTexte, FontStyle.Bold, theme.texte);
            Etirer(texte.rectTransform);
            texte.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
            visuel.texte = texte;
            boutonsAccent.Add(visuel);
        }
        return visuel;
    }

    private void CreerSeparateur(Transform parent, Vector2 position, float largeur)
    {
        Image ligne = CreerImage("Separateur", parent, spriteRond, Color.white);
        ligne.rectTransform.anchoredPosition = position;
        ligne.rectTransform.sizeDelta = new Vector2(largeur, 6f);
        ligne.raycastTarget = false;
        ligne.color = new Color(1f, 1f, 1f, 0.35f);
    }

    private Text CreerTexte(string nom, Transform parent, string contenu, int taille, FontStyle style, Color couleur)
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

    private static Image CreerImage(string nom, Transform parent, Sprite sprite, Color couleur)
    {
        RectTransform rect = CreerRect(nom, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = couleur;
        return image;
    }

    private static RectTransform CreerRect(string nom, Transform parent)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.layer = 5; // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Etirer(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Ancrer(RectTransform rect, Vector2 ancre)
    {
        rect.anchorMin = ancre;
        rect.anchorMax = ancre;
    }
}
