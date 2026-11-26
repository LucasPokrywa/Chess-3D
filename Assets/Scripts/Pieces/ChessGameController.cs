using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Gère la partie : sélection d'une pièce, affichage des coups possibles,
// déplacement, captures, roque, prise en passant, promotion et échec et mat.
// Coordonnées : x = colonne (a..h), z = rangée (1..8). Les blancs sont en z = 0/1.
[RequireComponent(typeof(ChessPieceSpawner), typeof(Echiquier), typeof(DeplacementPiece))]
[RequireComponent(typeof(ChessIA))]
public class ChessGameController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private BoardSelector boardSelector;
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Header("Animation")]
    [SerializeField, Range(0.05f, 1.5f)] private float moveDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float moveArcHeight = 0.35f;
    [SerializeField, Range(0f, 0.5f)] private float selectedLift = 0.12f;

    [Header("Indicateurs de coups")]
    [SerializeField] private Material markerMaterial;
    [SerializeField] private Color moveMarkerColor = new Color(0.64f, 0.30f, 1f, 0.75f);
    [SerializeField] private Color captureMarkerColor = new Color(1f, 0.35f, 0.15f, 0.85f);
    [SerializeField, Range(0.5f, 5f)] private float markerBrightness = 1.6f;

    [Header("Interface")]
    [SerializeField] private bool showStatus = true;
    [Tooltip("Affiche un bouton (et active Échap) pour revenir au menu principal.")]
    [SerializeField] private bool showMenuButton = true;

    [Header("Import / Export FEN (manuel)")]
    [UnityEngine.Serialization.FormerlySerializedAs("fenDepart")]
    [SerializeField, TextArea(2, 3)] private string fenAImporter = "";

    private ChessPieceSpawner spawner;
    private ChessPiece[,] grid;
    private int size;

    private Echiquier echiquier;
    private DeplacementPiece deplacement;
    private ChessIA chessIA;
    private CancellationTokenSource annulationIA;
    private bool joueurBlanc = true;
    private bool whiteToMove => echiquier.Position.TourBlanc;
    private bool TourDuJoueur => whiteToMove == joueurBlanc;
    private bool gameOver;
    private bool animating;
    private bool quittantLaPartie;
    private bool JoueurPeutJouer => isActiveAndEnabled && grid != null &&
        !quittantLaPartie && !gameOver && !animating && TourDuJoueur;
    private string status = "";
    private readonly Dictionary<string, int> occurrences = new Dictionary<string, int>();
    private readonly PartiePgn partiePgn = new PartiePgn();
    private ResultatPartie resultatPartie;
    private string motifFin;
    private string coupPgnEnCours;
    private MenuFinPartie menuFin;

    // Case sur laquelle une prise en passant est possible (ou -1).
    private Vector2Int enPassantSquare => echiquier.Position.CaseEnPassant;

    private ChessPiece selectedPiece;
    private readonly List<Vector2Int> selectedMoves = new List<Vector2Int>();

    private readonly Dictionary<Vector2Int, BoardTile> tiles = new Dictionary<Vector2Int, BoardTile>();

    private Mesh dotMesh;
    private Mesh ringMesh;
    private readonly List<MeshRenderer> markers = new List<MeshRenderer>();
    private MaterialPropertyBlock markerBlock;

    /// <summary>
    /// Récupère ou initialise les dépendances de scène, dont ChessIA, avant le démarrage de la partie.
    /// </summary>
    private void Awake()
    {
        spawner = GetComponent<ChessPieceSpawner>();
        echiquier = GetComponent<Echiquier>();
        if (echiquier == null) echiquier = gameObject.AddComponent<Echiquier>();
        deplacement = GetComponent<DeplacementPiece>();
        if (deplacement == null) deplacement = gameObject.AddComponent<DeplacementPiece>();
        deplacement.Configurer(echiquier);
        chessIA = GetComponent<ChessIA>();
        if (chessIA == null) chessIA = gameObject.AddComponent<ChessIA>();
        var lancement = GameSettings.PrendreParametresDeLancement(gameObject.scene.name);
        if (lancement.HasValue)
        {
            var (difficulte, blanc) = lancement.Value;
            joueurBlanc = blanc;
            chessIA.AppliquerDifficulteDuMenu(difficulte);
        }

        if (gameCamera == null)
            gameCamera = Camera.main;

        if (boardSelector == null)
            boardSelector = GetComponent<BoardSelector>();

        // Le contrôleur gère les clics lui-même ; BoardSelector garde ses effets visuels.
        if (boardSelector != null)
            boardSelector.HandleClicks = false;

        CreateMarkerAssets();
    }

    /// <summary>
    /// Affiche la position standard si le plateau est compatible. L’analyse part après la synchronisation de l’état et de la vue.
    /// </summary>
    private void Start()
    {
        size = spawner.BoardSize;
        if (size != 8) {
            Debug.LogError("Le moteur d'échecs nécessite un plateau de 8 x 8.", this);
            enabled = false;
            return;
        }
        if (!joueurBlanc)
        {
            // Les cases et les pièces gardent leurs coordonnées locales.
            float demiLargeur = (size - 1) * spawner.TileSize * 0.5f;
            Vector3 centre = transform.TransformPoint(new Vector3(demiLargeur, 0f, demiLargeur));
            transform.RotateAround(centre, transform.up, 180f);
        }
        if (!AfficherPosition(new PositionEchecs()))
        {
            gameOver = true;
            status = "Impossible de créer les pièces (voir la console)";
        }
    }

    /// <summary>
    /// Expose la position logique affichée aux commandes de diagnostic et de copie.
    /// </summary>
    /// <returns>FEN complet de la partie courante.</returns>
    public string ExporterFen() => echiquier.Position.ExporterFen();

    // Refuse un chargement pendant un déplacement ; un échec préserve la partie courante.
    /// <summary>
    /// Accepte un import manuel en mode Play hors déplacement animé. Un FEN rejeté ne remplace pas la partie en cours.
    /// </summary>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <returns>Vrai lorsque la position a été analysée et affichée.</returns>
    public bool ChargerFen(string fen)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || quittantLaPartie || animating ||
            spawner == null || spawner.BoardSize != 8) return false;
        PositionEchecs position;
        try { position = PositionEchecs.DepuisFen(fen); }
        catch (System.FormatException erreur)
        {
            Debug.LogError("FEN invalide : " + erreur.Message, this);
            return false;
        }
        return AfficherPosition(position);
    }

    /// <summary>
    /// Reconstruit la grille visuelle à partir de la position logique. Après succès, actualise le statut et déclenche l’analyse du trait.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <returns>Vrai si les pièces ont toutes pu être instanciées.</returns>
    private bool AfficherPosition(PositionEchecs position)
    {
        List<ChessPiece> pieces = spawner.SpawnPosition(position);
        if (pieces == null)
        {
            Debug.LogError("Un modèle de pièce requis par la position est manquant.", this);
            return false;
        }
        AnnulerAnalyseIA();
        occurrences.Clear();
        Deselect();
        if (grid != null)
            foreach (ChessPiece piece in grid)
                if (piece != null)
                {
                    piece.gameObject.SetActive(false);
                    Destroy(piece.gameObject);
                }
        size = 8;
        grid = new ChessPiece[size, size];
        foreach (ChessPiece piece in pieces) grid[piece.X, piece.Z] = piece;
        echiquier.DefinirPosition(position);
        partiePgn.Initialiser(position);
        resultatPartie = ResultatPartie.EnCours;
        coupPgnEnCours = null;
        if (menuFin != null) menuFin.Masquer();
        gameOver = false;
        UpdateStatus();
        return true;
    }

    /// <summary>
    /// Copie dans le presse-papiers le FEN du plateau visible en mode Play pour permettre une reproduction hors scène.
    /// </summary>
    [ContextMenu("FEN/Copier la position actuelle")]
    private void CopierFen()
    {
        if (!Application.isPlaying || grid == null) return;
        GUIUtility.systemCopyBuffer = ExporterFen();
        Debug.Log("FEN copié : " + ExporterFen(), this);
    }

    /// <summary>
    /// Soumet le texte de l’Inspector à l’import manuel de la partie.
    /// </summary>
    [ContextMenu("FEN/Importer le FEN saisi")]
    private void ChargerFenInspecteur()
    {
        ChargerFen(fenAImporter);
    }

    private void Update()
    {
        if (showMenuButton && WasEscapePressed())
        {
            RetourMenu();
            return;
        }

        if (!JoueurPeutJouer)
            return;

        if (WasLeftClickPressed() && !PointerIsOverUI() && !PointerIsOverHud())
            HandleClick();
    }

    // ------------------------------------------------------------------
    // Clics
    // ------------------------------------------------------------------

    private void HandleClick()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (gameCamera == null)
            return;

        Ray ray = gameCamera.ScreenPointToRay(GetMousePosition());

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, clickableLayers))
        {
            Deselect();
            return;
        }

        Vector2Int square;
        ChessPiece hitPiece = hit.collider.GetComponentInParent<ChessPiece>();

        if (hitPiece != null)
        {
            square = new Vector2Int(hitPiece.X, hitPiece.Z);
        }
        else
        {
            BoardTile tile = hit.collider.GetComponentInParent<BoardTile>();

            if (tile == null)
            {
                Deselect();
                return;
            }

            square = new Vector2Int(tile.X, tile.Z);
        }

        if (selectedPiece != null && selectedMoves.Contains(square))
        {
            JouerCoup(selectedPiece.X, selectedPiece.Z, square.x, square.y);
            return;
        }

        ChessPiece target = GetPiece(square.x, square.y);

        if (target != null && target.IsWhite == joueurBlanc && target != selectedPiece)
            Select(target);
        else
            Deselect();
    }

    private void Select(ChessPiece piece)
    {
        Deselect();

        selectedPiece = piece;
        selectedPiece.SetLifted(true, selectedLift);

        selectedMoves.AddRange(deplacement.GetCasesDisponibles(piece.X, piece.Z));

        BoardTile tile = GetTile(piece.X, piece.Z);

        if (boardSelector != null && tile != null)
            boardSelector.SelectTile(tile);

        ShowMarkers();
    }

    private void Deselect()
    {
        if (selectedPiece != null)
            selectedPiece.SetLifted(false, 0f);

        selectedPiece = null;
        selectedMoves.Clear();

        if (boardSelector != null)
            boardSelector.ClearSelection();

        HideMarkers();
    }

    // ------------------------------------------------------------------
    // Exécution d'un coup
    // ------------------------------------------------------------------

    /// <summary>
    /// Fournit une position indépendante pour un appelant d’analyse.
    /// </summary>
    /// <returns>Copie de la position du plateau courant.</returns>
    public PositionEchecs CopierPosition() => echiquier.Position.Copier();

    /// <summary>
    /// Joue un coup du camp attribué au joueur si c'est son tour.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Vrai si le coup est accepté et le déplacement démarré.</returns>
    public bool JouerCoup(int x, int z, int tx, int tz)
    {
        if (!JoueurPeutJouer || !echiquier.EstDansPlateau(tx, tz)) return false;
        ChessPiece piece = GetPiece(x, z);
        return piece != null && piece.IsWhite == joueurBlanc && MakeMove(piece, new Vector2Int(tx, tz));
    }

    /// <summary>
    /// Applique le coup via les règles communes, puis synchronise et anime les pièces visuelles.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="to">Destination logique du coup candidat.</param>
    /// <returns>Vrai si la position et l’animation ont été engagées.</returns>
    private bool MakeMove(ChessPiece piece, Vector2Int to)
    {
        int fromX = piece.X;
        int fromZ = piece.Z;
        int dir = piece.IsWhite ? 1 : -1;

        bool enPassant = piece.Type == PieceType.Pawn && to == enPassantSquare && grid[to.x, to.y] == null;
        string notation = PartiePgn.NotationAvantCoup(echiquier.Position, new Vector2Int(fromX, fromZ), to);
        if (!deplacement.DeplacerPiece(fromX, fromZ, to.x, to.y)) return false;
        coupPgnEnCours = notation;
        Deselect();
        animating = true;

        // Capture classique
        ChessPiece captured = grid[to.x, to.y];

        // Prise en passant
        if (enPassant)
        {
            captured = grid[to.x, to.y - dir];
            grid[to.x, to.y - dir] = null;
        }

        if (captured != null)
            captured.Capture(moveDuration * 0.8f);

        // Roque : on déplace aussi la tour
        if (piece.Type == PieceType.King && Mathf.Abs(to.x - fromX) == 2)
        {
            bool kingSide = to.x > fromX;
            int rookFromX = kingSide ? size - 1 : 0;
            int rookToX = kingSide ? to.x - 1 : to.x + 1;

            ChessPiece rook = grid[rookFromX, fromZ];

            if (rook != null)
            {
                grid[rookFromX, fromZ] = null;
                grid[rookToX, fromZ] = rook;
                rook.SetSquare(rookToX, fromZ);
                rook.HasMoved = true;
                rook.MoveTo(spawner.SquareToLocal(rookToX, fromZ), moveDuration, moveArcHeight);
            }
        }

        grid[fromX, fromZ] = null;
        grid[to.x, to.y] = piece;
        piece.SetSquare(to.x, to.y);
        piece.HasMoved = true;

        piece.MoveTo(
            spawner.SquareToLocal(to.x, to.y),
            moveDuration,
            moveArcHeight,
            () => OnMoveFinished(piece)
        );
        return true;
    }

    /// <summary>
    /// Termine l’affichage de la promotion éventuelle, débloque les interactions et recalcule l’état du prochain trait.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    private void OnMoveFinished(ChessPiece piece)
    {
        // Promotion automatique en dame
        bool promotes = piece.Type == PieceType.Pawn &&
            (piece.Z == size - 1 || piece.Z == 0);

        if (promotes)
        {
            ChessPiece queen = spawner.SpawnPiece(PieceType.Queen, piece.IsWhite, piece.X, piece.Z);

            if (queen != null)
            {
                queen.HasMoved = true;
                grid[piece.X, piece.Z] = queen;
                Destroy(piece.gameObject);
            }
        }

        animating = false;
        UpdateStatus();
    }

    /// <summary>
    /// Décide si la position est terminale avec les règles partagées. Actualise l’affichage et ne lance l’analyse que si le tour peut continuer.
    /// </summary>
    private void UpdateStatus()
    {
        if (quittantLaPartie) return;

        string side = NomDuCamp(whiteToMove);
        bool inCheck = deplacement.RoiEnEchec(whiteToMove);
        bool hasMoves = deplacement.PossedeCoupLegal(whiteToMove);
        if (coupPgnEnCours != null)
        {
            partiePgn.AjouterCoup(coupPgnEnCours, inCheck, hasMoves);
            coupPgnEnCours = null;
        }

        if (!hasMoves)
        {
            gameOver = true;
            resultatPartie = !inCheck ? ResultatPartie.Nulle : (whiteToMove ? ResultatPartie.Noirs : ResultatPartie.Blancs);
            motifFin = inCheck ? "Échec et mat" : "Pat";

            status = inCheck
                ? $"Échec et mat ! Victoire des {NomDuCamp(!whiteToMove)}"
                : "Pat : partie nulle";
        }
        else if (MotifNulle() is string motif)
        {
            gameOver = true;
            resultatPartie = ResultatPartie.Nulle;
            motifFin = motif;
            status = "Partie nulle : " + motif;
        }
        else
        {
            status = inCheck ? $"Trait aux {side} — Échec !" : $"Trait aux {side}";
        }

        Debug.Log(status, this);
        echiquier.AfficherPlateau();
        if (gameOver)
        {
            AnnulerAnalyseIA();
            AfficherMenuFin();
        }
        else DemarrerTourIA();
    }

    private string MotifNulle()
    {
        PositionEchecs position = echiquier.Position;
        if (position.DemiCoupsSansPriseNiPion >= 150)
            return "75 coups sans prise ni déplacement de pion";
        if (MaterielInsuffisant(position)) return "matériel insuffisant";

        // Les compteurs FEN ne font pas partie de l'identité d'une position.
        string[] champs = position.ExporterFen().Split(' ');
        Vector2Int ep = position.CaseEnPassant;
        if (ep.x >= 0)
        {
            var regles = new ReglesEchecs(position);
            char pion = whiteToMove ? 'p' : 'P';
            int rang = ep.y + (whiteToMove ? -1 : 1);
            bool PeutPrendre(int x) => position.GetPiece(x, rang) == pion &&
                regles.EstCoupLegalAuTrait(new Vector2Int(x, rang), ep);
            if (!PeutPrendre(ep.x - 1) && !PeutPrendre(ep.x + 1)) champs[3] = "-";
        }

        string cle = string.Join(" ", champs, 0, 4);
        occurrences.TryGetValue(cle, out int nombre);
        occurrences[cle] = ++nombre;
        return nombre == 5 ? "même position apparue cinq fois" : null;
    }

    private static bool MaterielInsuffisant(PositionEchecs position)
    {
        int mineures = 0;
        int couleurFous = -1;
        bool cavalier = false;
        for (int x = 0; x < 8; x++)
        {
            for (int z = 0; z < 8; z++)
            {
                switch (char.ToUpperInvariant(position.GetPiece(x, z)))
                {
                    case '.':
                    case 'R':
                        continue;
                    case 'C':
                        cavalier = true;
                        break;
                    case 'F':
                        int couleur = (x + z) & 1;
                        if (couleurFous >= 0 && couleurFous != couleur) return false;
                        couleurFous = couleur;
                        break;
                    default:
                        return false;
                }
                mineures++;
            }
        }
        return mineures <= 1 || !cavalier;
    }

    private void DemarrerTourIA()
    {
        if (!isActiveAndEnabled || grid == null || quittantLaPartie || gameOver ||
            animating || TourDuJoueur || annulationIA != null) return;

        annulationIA = new CancellationTokenSource();
        status = $"Trait aux {NomDuCamp(whiteToMove)} — l’IA réfléchit…";
        _ = JouerTourIAAsync(echiquier.Position, annulationIA);
    }

    private async Task JouerTourIAAsync(PositionEchecs position, CancellationTokenSource annulation)
    {
        try
        {
            // ChessIA copie la position sur le thread Unity. La continuation revient sur ce thread.
            Minimax.Resultat resultat = await chessIA.ChoisirCoupAsync(position, annulation.Token);
            if (annulationIA != annulation || !isActiveAndEnabled || quittantLaPartie || gameOver ||
                animating || TourDuJoueur || echiquier.Position != position) return;

            if (!resultat.MeilleurCoup.HasValue)
                throw new InvalidOperationException("Aucun coup rendu pour une position jouable.");

            Minimax.Coup coup = resultat.MeilleurCoup.Value;
            string depart = $"{(char)('a' + coup.Depart.x)}{coup.Depart.y + 1}";
            string arrivee = $"{(char)('a' + coup.Arrivee.x)}{coup.Arrivee.y + 1}";
            int numero = position.NumeroCoup;
            string camp = NomDuCamp(whiteToMove);
            ChessPiece piece = GetPiece(coup.Depart.x, coup.Depart.y);
            if (piece == null || piece.IsWhite == joueurBlanc || !echiquier.EstDansPlateau(coup.Arrivee.x, coup.Arrivee.y) ||
                !MakeMove(piece, coup.Arrivee))
                throw new InvalidOperationException($"Coup de l’IA refusé : {depart} → {arrivee}.");

            status = $"{camp} — {depart} → {arrivee}";
            Debug.Log($"[IA] Coup {numero} — {camp} : {depart} → {arrivee}" +
                $" | score {resultat.Score} centipions pour les {camp}" +
                $" | profondeur {resultat.Profondeur} | {resultat.TempsMillisecondes} ms" +
                $" | {resultat.NoeudsVisites} nœuds visités (principal {resultat.NoeudsRecherchePrincipale}, quiétude {resultat.NoeudsQuietude}), {resultat.NoeudsDeveloppes} calculés" +
                $" | table : {resultat.ReutilisationsTable} réutilisations, {resultat.CoupuresTable} coupures," +
                $" {resultat.EntreesTable} entrées, {resultat.RemplacementsTable} feuilles remplacées", this);
        }
        catch (OperationCanceledException) when (annulation.IsCancellationRequested) { }
        catch (Exception erreur)
        {
            if (annulationIA == annulation)
            {
                status = "Erreur pendant le tour de l’IA (voir la console)";
                Debug.LogException(erreur, this);
            }
        }
        finally
        {
            if (annulationIA == annulation) annulationIA = null;
            annulation.Dispose();
        }
    }

    private void AnnulerAnalyseIA()
    {
        CancellationTokenSource ancienneAnalyse = annulationIA;
        annulationIA = null;
        ancienneAnalyse?.Cancel();
    }

    /// <summary>
    /// Relance l’analyse si un plateau a déjà été instancié lors de la réactivation.
    /// </summary>
    private void OnEnable()
    {
        DemarrerTourIA();
    }

    /// <summary>
    /// Annule le calcul lié à l’ancienne scène ou au composant désactivé.
    /// </summary>
    private void OnDisable()
    {
        AnnulerAnalyseIA();
    }

    // ------------------------------------------------------------------
    // Règles
    // ------------------------------------------------------------------

    // Le tableau 3D ne sert qu'à retrouver les objets à animer.
    private ChessPiece GetPiece(int x, int z)
    {
        return echiquier.EstDansPlateau(x, z) ? grid[x, z] : null;
    }
    // ------------------------------------------------------------------
    // Cases du plateau
    // ------------------------------------------------------------------

    private BoardTile GetTile(int x, int z)
    {
        if (tiles.Count == 0)
        {
            foreach (BoardTile tile in GetComponentsInChildren<BoardTile>())
                tiles[new Vector2Int(tile.X, tile.Z)] = tile;
        }

        tiles.TryGetValue(new Vector2Int(x, z), out BoardTile result);
        return result;
    }

    // ------------------------------------------------------------------
    // Indicateurs de coups (mesh procédural)
    // ------------------------------------------------------------------

    private void ShowMarkers()
    {
        HideMarkers();

        if (markerMaterial == null)
            return;

        for (int i = 0; i < selectedMoves.Count; i++)
        {
            Vector2Int move = selectedMoves[i];

            bool isCapture = grid[move.x, move.y] != null ||
                (selectedPiece.Type == PieceType.Pawn && move == enPassantSquare);

            MeshRenderer marker = GetMarker(i);
            marker.GetComponent<MeshFilter>().sharedMesh = isCapture ? ringMesh : dotMesh;
            marker.transform.localPosition = spawner.SquareToLocal(move.x, move.y) + Vector3.up * 0.01f;
            marker.transform.localScale = Vector3.one * spawner.TileSize * (isCapture ? 0.46f : 0.16f);

            Color color = (isCapture ? captureMarkerColor : moveMarkerColor) * markerBrightness;
            color.a = isCapture ? captureMarkerColor.a : moveMarkerColor.a;

            markerBlock.SetColor("_BaseColor", color);
            marker.SetPropertyBlock(markerBlock);

            marker.gameObject.SetActive(true);
        }
    }

    private void HideMarkers()
    {
        foreach (MeshRenderer marker in markers)
            marker.gameObject.SetActive(false);
    }

    private MeshRenderer GetMarker(int index)
    {
        while (markers.Count <= index)
        {
            GameObject go = new GameObject($"MoveMarker_{markers.Count}");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            markers.Add(renderer);
        }

        return markers[index];
    }

    private void CreateMarkerAssets()
    {
        markerBlock = new MaterialPropertyBlock();
        dotMesh = BuildRingMesh(40, 0f, "MoveDot");
        ringMesh = BuildRingMesh(48, 0.82f, "CaptureRing");
    }

    // Anneau à plat (innerRatio = 0 donne un disque plein), rayon 1.
    private static Mesh BuildRingMesh(int segments, float innerRatio, string meshName)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        bool disc = innerRatio <= 0f;

        if (disc)
            vertices.Add(Vector3.zero);

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            if (disc)
            {
                vertices.Add(dir);
            }
            else
            {
                vertices.Add(dir * innerRatio);
                vertices.Add(dir);
            }
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            if (disc)
            {
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + i);
            }
            else
            {
                int a = i * 2;
                int b = next * 2;

                triangles.Add(a);
                triangles.Add(b + 1);
                triangles.Add(a + 1);

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    // ------------------------------------------------------------------
    // Interface et entrées
    // ------------------------------------------------------------------

    private static string NomDuCamp(bool blanc) => blanc ? "Blancs" : "Noirs";

    private void OnGUI()
    {
        if (resultatPartie != ResultatPartie.EnCours) return;

        DrawMenuButton();

        if (!showStatus || string.IsNullOrEmpty(status))
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        style.normal.textColor = new Color(0.85f, 0.7f, 1f);

        GUI.Label(new Rect(20, 16, 700, 40), $"Tu joues les {NomDuCamp(joueurBlanc)} · {status}", style);
    }

    private string TitreFin()
    {
        if (resultatPartie == ResultatPartie.Nulle) return "Partie nulle";
        return (resultatPartie == ResultatPartie.Blancs) == joueurBlanc ? "Victoire" : "Défaite";
    }

    private void AfficherMenuFin()
    {
        if (menuFin == null)
        {
            var scene = gameObject.scene;
            ModeDeJeu mode = scene.name == GameSettings.SceneHalloween ? ModeDeJeu.Halloween : ModeDeJeu.Normal;
            menuFin = MenuFinPartie.Creer(scene, mode, CopierPgn, RetourMenu, QuitterJeu);
        }
        menuFin.Afficher(TitreFin(), motifFin);
    }

    private void CopierPgn()
    {
        GUIUtility.systemCopyBuffer = partiePgn.Exporter(resultatPartie, joueurBlanc);
        menuFin.IndiquerCopie();
    }

    private void QuitterJeu()
    {
        quittantLaPartie = true;
        AnnulerAnalyseIA();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Bouton "Menu" en haut à droite (coordonnées IMGUI : origine en haut à gauche).
    private static Rect MenuButtonRect => new Rect(Screen.width - 150, 16, 130, 40);

    private void DrawMenuButton()
    {
        if (!showMenuButton) return;
        GUIStyle style = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        if (GUI.Button(MenuButtonRect, "Menu (Échap)", style))
            RetourMenu();
    }

    private void RetourMenu()
    {
        if (quittantLaPartie) return;
        if (GameSettings.SceneDisponible(GameSettings.SceneMenu))
        {
            quittantLaPartie = true;
            AnnulerAnalyseIA();
        }
        GameSettings.RetourMenu();
    }

    private bool PointerIsOverHud()
    {
        Vector2 mouse = GetMousePosition();
        return MusiqueAmbiance.PointeurSurVolume(mouse) ||
            (showMenuButton && MenuButtonRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y)));
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
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

    /// <summary>
    /// Arrête l’analyse restante et libère les ressources visuelles créées par le contrôleur.
    /// </summary>
    private void OnDestroy()
    {
        AnnulerAnalyseIA();
        if (menuFin != null) Destroy(menuFin.gameObject);
        if (dotMesh != null) Destroy(dotMesh);
        if (ringMesh != null) Destroy(ringMesh);
    }
}
