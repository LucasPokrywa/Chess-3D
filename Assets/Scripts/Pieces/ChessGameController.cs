using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Gère la partie : sélection d'une pièce, affichage des coups possibles,
// déplacement, captures, roque, prise en passant, promotion et échec et mat.
// Coordonnées : x = colonne (a..h), z = rangée (1..8). Les blancs sont en z = 0/1.
[RequireComponent(typeof(ChessPieceSpawner))]
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

    private ChessPieceSpawner spawner;
    private ChessPiece[,] grid;
    private int size;

    private bool whiteToMove = true;
    private bool gameOver;
    private bool animating;
    private string status = "";

    // Case sur laquelle une prise en passant est possible (ou -1).
    private Vector2Int enPassantSquare = new Vector2Int(-1, -1);

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
        grid = new ChessPiece[size, size];

        foreach (ChessPiece piece in spawner.SpawnStartingPosition())
            grid[piece.X, piece.Z] = piece;

        UpdateStatus();
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

        selectedMoves.AddRange(GetLegalMoves(piece));

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

    private void MakeMove(ChessPiece piece, Vector2Int to)
    {
        int fromX = piece.X;
        int fromZ = piece.Z;
        int dir = piece.IsWhite ? 1 : -1;

        Deselect();
        animating = true;

        // Capture classique
        ChessPiece captured = grid[to.x, to.y];

        // Prise en passant
        if (piece.Type == PieceType.Pawn && to == enPassantSquare && captured == null)
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

        // Nouvelle case de prise en passant
        if (piece.Type == PieceType.Pawn && Mathf.Abs(to.y - fromZ) == 2)
            enPassantSquare = new Vector2Int(fromX, fromZ + dir);
        else
            enPassantSquare = new Vector2Int(-1, -1);

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

        whiteToMove = !whiteToMove;
        animating = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string side = whiteToMove ? "Blancs" : "Noirs";
        bool inCheck = IsInCheck(whiteToMove);
        bool hasMoves = HasAnyLegalMove(whiteToMove);

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
    }

    // ------------------------------------------------------------------
    // Règles
    // ------------------------------------------------------------------

    private ChessPiece GetPiece(int x, int z)
    {
        return Inside(x, z) ? grid[x, z] : null;
    }

    private bool Inside(int x, int z)
    {
        return x >= 0 && z >= 0 && x < size && z < size;
    }

    private List<Vector2Int> GetLegalMoves(ChessPiece piece)
    {
        List<Vector2Int> legal = new List<Vector2Int>();

        foreach (Vector2Int move in GetPseudoMoves(piece))
        {
            if (!LeavesKingInCheck(piece, move))
                legal.Add(move);
        }

        return legal;
    }

    private bool HasAnyLegalMove(bool white)
    {
        foreach (ChessPiece piece in grid)
        {
            if (piece != null && piece.IsWhite == white && GetLegalMoves(piece).Count > 0)
                return true;
        }

        return false;
    }

    // Joue le coup "à blanc" sur la grille pour vérifier que le roi reste en sécurité.
    private bool LeavesKingInCheck(ChessPiece piece, Vector2Int to)
    {
        int fromX = piece.X;
        int fromZ = piece.Z;
        int dir = piece.IsWhite ? 1 : -1;

        ChessPiece captured = grid[to.x, to.y];
        Vector2Int capturedSquare = to;

        if (piece.Type == PieceType.Pawn && to == enPassantSquare && captured == null)
        {
            capturedSquare = new Vector2Int(to.x, to.y - dir);
            captured = grid[capturedSquare.x, capturedSquare.y];
        }

        grid[capturedSquare.x, capturedSquare.y] = null;
        grid[fromX, fromZ] = null;
        grid[to.x, to.y] = piece;

        Vector2Int kingSquare = piece.Type == PieceType.King
            ? to
            : FindKing(piece.IsWhite);

        bool inCheck = kingSquare.x >= 0 &&
            IsSquareAttacked(kingSquare.x, kingSquare.y, !piece.IsWhite);

        grid[to.x, to.y] = null;
        grid[fromX, fromZ] = piece;
        grid[capturedSquare.x, capturedSquare.y] = captured;

        return inCheck;
    }

    private bool IsInCheck(bool white)
    {
        Vector2Int king = FindKing(white);
        return king.x >= 0 && IsSquareAttacked(king.x, king.y, !white);
    }

    private Vector2Int FindKing(bool white)
    {
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                ChessPiece p = grid[x, z];

                if (p != null && p.IsWhite == white && p.Type == PieceType.King)
                    return new Vector2Int(x, z);
            }
        }

        return new Vector2Int(-1, -1);
    }

    private static readonly Vector2Int[] KnightJumps =
    {
        new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2)
    };

    private static readonly Vector2Int[] StraightDirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    // La case (x, z) est-elle attaquée par une pièce de la couleur donnée ?
    private bool IsSquareAttacked(int x, int z, bool byWhite)
    {
        // Pions
        int pawnDir = byWhite ? 1 : -1;

        for (int dx = -1; dx <= 1; dx += 2)
        {
            ChessPiece p = GetPiece(x - dx, z - pawnDir);

            if (p != null && p.IsWhite == byWhite && p.Type == PieceType.Pawn)
                return true;
        }

        // Cavaliers
        foreach (Vector2Int j in KnightJumps)
        {
            ChessPiece p = GetPiece(x + j.x, z + j.y);

            if (p != null && p.IsWhite == byWhite && p.Type == PieceType.Knight)
                return true;
        }

        // Roi adverse
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;

                ChessPiece p = GetPiece(x + dx, z + dz);

                if (p != null && p.IsWhite == byWhite && p.Type == PieceType.King)
                    return true;
            }
        }

        // Lignes (tour / dame) et diagonales (fou / dame)
        if (RayAttacked(x, z, StraightDirs, byWhite, PieceType.Rook))
            return true;

        if (RayAttacked(x, z, DiagonalDirs, byWhite, PieceType.Bishop))
            return true;

        return false;
    }

    private bool RayAttacked(int x, int z, Vector2Int[] dirs, bool byWhite, PieceType slider)
    {
        foreach (Vector2Int d in dirs)
        {
            int cx = x + d.x;
            int cz = z + d.y;

            while (Inside(cx, cz))
            {
                ChessPiece p = grid[cx, cz];

                if (p != null)
                {
                    if (p.IsWhite == byWhite && (p.Type == slider || p.Type == PieceType.Queen))
                        return true;

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }

        return false;
    }

    private List<Vector2Int> GetPseudoMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        int x = piece.X;
        int z = piece.Z;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, moves);
                break;

            case PieceType.Knight:
                foreach (Vector2Int j in KnightJumps)
                    AddIfFreeOrEnemy(piece, x + j.x, z + j.y, moves);
                break;

            case PieceType.Bishop:
                AddSlides(piece, DiagonalDirs, moves);
                break;

            case PieceType.Rook:
                AddSlides(piece, StraightDirs, moves);
                break;

            case PieceType.Queen:
                AddSlides(piece, StraightDirs, moves);
                AddSlides(piece, DiagonalDirs, moves);
                break;

            case PieceType.King:
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx != 0 || dz != 0)
                            AddIfFreeOrEnemy(piece, x + dx, z + dz, moves);
                    }
                }

                AddCastling(piece, moves);
                break;
        }

        return moves;
    }

    private void AddPawnMoves(ChessPiece pawn, List<Vector2Int> moves)
    {
        int dir = pawn.IsWhite ? 1 : -1;
        int startRank = pawn.IsWhite ? 1 : size - 2;
        int x = pawn.X;
        int z = pawn.Z;

        if (Inside(x, z + dir) && grid[x, z + dir] == null)
        {
            moves.Add(new Vector2Int(x, z + dir));

            if (z == startRank && Inside(x, z + 2 * dir) && grid[x, z + 2 * dir] == null)
                moves.Add(new Vector2Int(x, z + 2 * dir));
        }

        for (int dx = -1; dx <= 1; dx += 2)
        {
            int tx = x + dx;
            int tz = z + dir;

            if (!Inside(tx, tz))
                continue;

            ChessPiece target = grid[tx, tz];

            if (target != null && target.IsWhite != pawn.IsWhite)
                moves.Add(new Vector2Int(tx, tz));
            else if (target == null && enPassantSquare == new Vector2Int(tx, tz))
                moves.Add(new Vector2Int(tx, tz));
        }
    }

    private void AddCastling(ChessPiece king, List<Vector2Int> moves)
    {
        if (king.HasMoved || IsInCheck(king.IsWhite))
            return;

        int z = king.Z;
        bool enemy = !king.IsWhite;

        // Petit roque
        ChessPiece rook = GetPiece(size - 1, z);

        if (IsCastlingRook(rook, king) &&
            PathEmpty(king.X + 1, size - 2, z) &&
            !IsSquareAttacked(king.X + 1, z, enemy) &&
            !IsSquareAttacked(king.X + 2, z, enemy))
        {
            moves.Add(new Vector2Int(king.X + 2, z));
        }

        // Grand roque
        rook = GetPiece(0, z);

        if (IsCastlingRook(rook, king) &&
            PathEmpty(1, king.X - 1, z) &&
            !IsSquareAttacked(king.X - 1, z, enemy) &&
            !IsSquareAttacked(king.X - 2, z, enemy))
        {
            moves.Add(new Vector2Int(king.X - 2, z));
        }
    }

    private static bool IsCastlingRook(ChessPiece rook, ChessPiece king)
    {
        return rook != null && rook.Type == PieceType.Rook &&
            rook.IsWhite == king.IsWhite && !rook.HasMoved;
    }

    private bool PathEmpty(int fromX, int toX, int z)
    {
        for (int x = fromX; x <= toX; x++)
        {
            if (grid[x, z] != null)
                return false;
        }

        return true;
    }

    private void AddSlides(ChessPiece piece, Vector2Int[] dirs, List<Vector2Int> moves)
    {
        foreach (Vector2Int d in dirs)
        {
            int cx = piece.X + d.x;
            int cz = piece.Z + d.y;

            while (Inside(cx, cz))
            {
                ChessPiece p = grid[cx, cz];

                if (p == null)
                {
                    moves.Add(new Vector2Int(cx, cz));
                }
                else
                {
                    if (p.IsWhite != piece.IsWhite)
                        moves.Add(new Vector2Int(cx, cz));

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }
    }

    private void AddIfFreeOrEnemy(ChessPiece piece, int x, int z, List<Vector2Int> moves)
    {
        if (!Inside(x, z))
            return;

        ChessPiece p = grid[x, z];

        if (p == null || p.IsWhite != piece.IsWhite)
            moves.Add(new Vector2Int(x, z));
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
        if (markerMaterial != null) Destroy(markerMaterial);
        if (dotMesh != null) Destroy(dotMesh);
        if (ringMesh != null) Destroy(ringMesh);
    }
}
