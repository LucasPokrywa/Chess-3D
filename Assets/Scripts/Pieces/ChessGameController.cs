using System.Collections;
using System.Collections.Generic;
using System.Threading;
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
    [SerializeField] private Color moveMarkerColor = new Color(0.64f, 0.30f, 1f, 0.75f);
    [SerializeField] private Color captureMarkerColor = new Color(1f, 0.35f, 0.15f, 0.85f);
    [SerializeField, Range(0.5f, 5f)] private float markerBrightness = 1.6f;

    [Header("Interface")]
    [SerializeField] private bool showStatus = true;

    [Header("Import / Export FEN (manuel)")]
    [UnityEngine.Serialization.FormerlySerializedAs("fenDepart")]
    [SerializeField, TextArea(2, 3)] private string fenAImporter = "";

    private ChessPieceSpawner spawner;
    private ChessPiece[,] grid;
    private int size;

    private Echiquier echiquier;
    private DeplacementPiece deplacement;
    private ChessIA chessIA;
    private Coroutine analyseIA;
    private CancellationTokenSource annulationIA;
    private bool whiteToMove => echiquier.Position.TourBlanc;
    private bool gameOver;
    private bool animating;
    private string status = "";

    // Case sur laquelle une prise en passant est possible (ou -1).
    private Vector2Int enPassantSquare => echiquier.Position.CaseEnPassant;

    private ChessPiece selectedPiece;
    private readonly List<Vector2Int> selectedMoves = new List<Vector2Int>();

    private readonly Dictionary<Vector2Int, BoardTile> tiles = new Dictionary<Vector2Int, BoardTile>();

    private Material markerMaterial;
    private Mesh dotMesh;
    private Mesh ringMesh;
    private readonly List<MeshRenderer> markers = new List<MeshRenderer>();
    private MaterialPropertyBlock markerBlock;

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

        if (gameCamera == null)
            gameCamera = Camera.main;

        if (boardSelector == null)
            boardSelector = GetComponent<BoardSelector>();

        // Le contrôleur gère les clics lui-même ; BoardSelector garde ses effets visuels.
        if (boardSelector != null)
            boardSelector.HandleClicks = false;

        CreateMarkerAssets();
    }

    private void Start()
    {
        size = spawner.BoardSize;
        if (size != 8) {
            Debug.LogError("Le moteur d'échecs nécessite un plateau de 8 x 8.", this);
            enabled = false;
            return;
        }
        if (!AfficherPosition(new PositionEchecs()))
        {
            gameOver = true;
            status = "Impossible de créer les pièces (voir la console)";
        }
    }

    public string ExporterFen() => echiquier.Position.ExporterFen();

    // Refuse un chargement pendant un déplacement ; un échec préserve la partie courante.
    public bool ChargerFen(string fen)
    {
        if (!Application.isPlaying || animating || spawner == null || spawner.BoardSize != 8) return false;
        PositionEchecs position;
        try { position = PositionEchecs.DepuisFen(fen); }
        catch (System.FormatException erreur)
        {
            Debug.LogError("FEN invalide : " + erreur.Message, this);
            return false;
        }
        return AfficherPosition(position);
    }

    private bool AfficherPosition(PositionEchecs position)
    {
        List<ChessPiece> pieces = spawner.SpawnPosition(position);
        if (pieces == null)
        {
            Debug.LogError("Un modèle de pièce requis par la position est manquant.", this);
            return false;
        }
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
        gameOver = false;
        UpdateStatus();
        return true;
    }

    [ContextMenu("FEN/Copier la position actuelle")]
    private void CopierFen()
    {
        if (!Application.isPlaying || grid == null) return;
        GUIUtility.systemCopyBuffer = ExporterFen();
        Debug.Log("FEN copié : " + ExporterFen(), this);
    }

    [ContextMenu("FEN/Importer le FEN saisi")]
    private void ChargerFenInspecteur()
    {
        ChargerFen(fenAImporter);
    }

    private void Update()
    {
        if (gameOver || animating)
            return;

        if (WasLeftClickPressed() && !PointerIsOverUI())
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
            MakeMove(selectedPiece, square);
            return;
        }

        ChessPiece target = GetPiece(square.x, square.y);

        if (target != null && target.IsWhite == whiteToMove && target != selectedPiece)
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

    // Une IA réfléchit sur une copie puis soumet son choix via JouerCoup.
    public PositionEchecs CopierPosition() => echiquier.Position.Copier();

    public bool JouerCoup(int x, int z, int tx, int tz)
    {
        if (!enabled || gameOver || animating || grid == null ||
            !echiquier.EstDansPlateau(tx, tz)) return false;
        ChessPiece piece = GetPiece(x, z);
        return piece != null && MakeMove(piece, new Vector2Int(tx, tz));
    }

    private bool MakeMove(ChessPiece piece, Vector2Int to)
    {
        int fromX = piece.X;
        int fromZ = piece.Z;
        int dir = piece.IsWhite ? 1 : -1;

        bool enPassant = piece.Type == PieceType.Pawn && to == enPassantSquare && grid[to.x, to.y] == null;
        if (!deplacement.DeplacerPiece(fromX, fromZ, to.x, to.y)) return false;
        AnnulerAnalyseIA();
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

    private void UpdateStatus()
    {
        string side = whiteToMove ? "Blancs" : "Noirs";
        bool inCheck = deplacement.RoiEnEchec(whiteToMove);
        bool hasMoves = deplacement.PossedeCoupLegal(whiteToMove);

        if (!hasMoves)
        {
            gameOver = true;

            status = inCheck
                ? $"Échec et mat ! Victoire des {(whiteToMove ? "Noirs" : "Blancs")}"
                : "Pat : partie nulle";
        }
        else
        {
            status = inCheck ? $"Trait aux {side} — Échec !" : $"Trait aux {side}";
        }

        Debug.Log(status, this);
        echiquier.AfficherPlateau();
        DemarrerAnalyseIA();
    }

    private void DemarrerAnalyseIA()
    {
        AnnulerAnalyseIA();
        if (!isActiveAndEnabled || grid == null || animating) return;
        if (gameOver)
        {
            Debug.Log("[IA] Aucun coup légal — " + status, this);
            return;
        }
        annulationIA = new CancellationTokenSource();
        analyseIA = StartCoroutine(AfficherMeilleurCoupIA(echiquier.Position.Copier(), annulationIA));
    }

    private IEnumerator AfficherMeilleurCoupIA(PositionEchecs position, CancellationTokenSource annulation)
    {
        // La recherche tourne en arrière-plan ; les logs restent sur le thread Unity.
        var calcul = chessIA.ChoisirCoupAsync(position, annulation.Token);
        while (!calcul.IsCompleted) yield return null;

        analyseIA = null;
        annulationIA = null;
        annulation.Dispose();
        if (calcul.IsCanceled) yield break;
        if (calcul.IsFaulted)
        {
            Debug.LogException(calcul.Exception.GetBaseException(), this);
            yield break;
        }
        Minimax.Resultat resultat = calcul.Result;
        string couleur = position.TourBlanc ? "Blancs" : "Noirs";
        if (resultat.MeilleurCoup.HasValue)
        {
            Minimax.Coup coup = resultat.MeilleurCoup.Value;
            string depart = $"{(char)('a' + coup.Depart.x)}{coup.Depart.y + 1}";
            string arrivee = $"{(char)('a' + coup.Arrivee.x)}{coup.Arrivee.y + 1}";
            Debug.Log($"[IA] Coup {position.NumeroCoup} — {couleur} : meilleur coup {depart} → {arrivee}" +
                $" | score {resultat.Score} centipions pour les {couleur}" +
                $" | profondeur {resultat.Profondeur} | {resultat.TempsMillisecondes} ms" +
                $" | {resultat.NoeudsVisites} nœuds visités, {resultat.NoeudsDeveloppes} calculés" +
                $" | table : {resultat.ReutilisationsTable} réutilisations, {resultat.CoupuresTable} coupures," +
                $" {resultat.EntreesTable} entrées, {resultat.RemplacementsTable} feuilles remplacées", this);
        }
        else Debug.Log($"[IA] {couleur} : aucun coup légal.", this);
    }

    private void AnnulerAnalyseIA()
    {
        if (annulationIA != null)
        {
            annulationIA.Cancel();
            annulationIA.Dispose();
            annulationIA = null;
        }
        if (analyseIA != null)
        {
            StopCoroutine(analyseIA);
            analyseIA = null;
        }
    }

    private void OnEnable()
    {
        if (grid != null) DemarrerAnalyseIA();
    }

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
            markerBlock.SetColor("_Color", color);
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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
            return;

        markerMaterial = new Material(shader) { name = "MoveMarker_Runtime" };
        markerMaterial.SetOverrideTag("RenderType", "Transparent");

        if (markerMaterial.HasProperty("_Surface")) markerMaterial.SetFloat("_Surface", 1f);
        if (markerMaterial.HasProperty("_Blend")) markerMaterial.SetFloat("_Blend", 0f);
        if (markerMaterial.HasProperty("_SrcBlend")) markerMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (markerMaterial.HasProperty("_DstBlend")) markerMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (markerMaterial.HasProperty("_ZWrite")) markerMaterial.SetFloat("_ZWrite", 0f);
        if (markerMaterial.HasProperty("_Cull")) markerMaterial.SetFloat("_Cull", (float)CullMode.Off);

        markerMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        markerMaterial.renderQueue = (int)RenderQueue.Transparent;
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

    private void OnGUI()
    {
        if (!showStatus || string.IsNullOrEmpty(status))
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        style.normal.textColor = new Color(0.85f, 0.7f, 1f);

        GUI.Label(new Rect(20, 16, 700, 40), status, style);
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
        AnnulerAnalyseIA();
        if (markerMaterial != null) Destroy(markerMaterial);
        if (dotMesh != null) Destroy(dotMesh);
        if (ringMesh != null) Destroy(ringMesh);
    }
}
