using System.Collections.Generic;
using UnityEngine;

// Moteur utilisable sur une position réelle ou une copie destinée à une IA.
public sealed class ReglesEchecs
{
    private readonly PositionEchecs position;
    private const int size = 8;
    private Vector2Int enPassantSquare => position.CaseEnPassant;
    public ReglesEchecs(PositionEchecs position) { this.position = position; }
    public List<Vector2Int> GetLegalMoves(int x, int z)
    {
        PieceEtat piece = GetPiece(x, z);
        return piece == null ? new List<Vector2Int>() : GetLegalMoves(piece);
    }
    public bool JouerCoup(int x, int z, int tx, int tz)
    {
        PieceEtat piece = GetPiece(x, z);
        if (piece == null || piece.IsWhite != position.TourBlanc ||
            !GetLegalMoves(piece).Contains(new Vector2Int(tx, tz))) return false;
        position.AppliquerCoup(x, z, tx, tz);
        return true;
    }
    private PieceEtat GetPiece(int x, int z)
    {
        return position.LirePiece(x, z);
    }

    private bool Inside(int x, int z)
    {
        return x >= 0 && z >= 0 && x < size && z < size;
    }

    private List<Vector2Int> GetLegalMoves(PieceEtat piece)
    {
        List<Vector2Int> legal = new List<Vector2Int>();

        foreach (Vector2Int move in GetPseudoMoves(piece))
        {
            if (!LeavesKingInCheck(piece, move))
                legal.Add(move);
        }

        return legal;
    }

    public bool HasAnyLegalMove(bool white)
    {
        for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++) {
                PieceEtat piece = GetPiece(x, z);
                if (piece != null && piece.IsWhite == white && GetLegalMoves(piece).Count > 0)
                    return true;
            }
        return false;
    }

    // Joue le coup "à blanc" sur la grille pour vérifier que le roi reste en sécurité.
    private bool LeavesKingInCheck(PieceEtat piece, Vector2Int to)
    {
        PositionEchecs copie = position.Copier();
        copie.AppliquerCoup(piece.X, piece.Z, to.x, to.y);
        return new ReglesEchecs(copie).IsInCheck(piece.IsWhite);
    }
    public bool IsInCheck(bool white)
    {
        Vector2Int king = FindKing(white);
        return king.x < 0 || IsSquareAttacked(king.x, king.y, !white);
    }

    private Vector2Int FindKing(bool white)
    {
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                PieceEtat p = GetPiece(x, z);

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
            PieceEtat p = GetPiece(x - dx, z - pawnDir);

            if (p != null && p.IsWhite == byWhite && p.Type == PieceType.Pawn)
                return true;
        }

        // Cavaliers
        foreach (Vector2Int j in KnightJumps)
        {
            PieceEtat p = GetPiece(x + j.x, z + j.y);

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

                PieceEtat p = GetPiece(x + dx, z + dz);

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
                PieceEtat p = GetPiece(cx, cz);

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

    private List<Vector2Int> GetPseudoMoves(PieceEtat piece)
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

    private void AddPawnMoves(PieceEtat pawn, List<Vector2Int> moves)
    {
        int dir = pawn.IsWhite ? 1 : -1;
        int startRank = pawn.IsWhite ? 1 : size - 2;
        int x = pawn.X;
        int z = pawn.Z;

        if (Inside(x, z + dir) && GetPiece(x, z + dir) == null)
        {
            moves.Add(new Vector2Int(x, z + dir));

            if (z == startRank && Inside(x, z + 2 * dir) && GetPiece(x, z + 2 * dir) == null)
                moves.Add(new Vector2Int(x, z + 2 * dir));
        }

        for (int dx = -1; dx <= 1; dx += 2)
        {
            int tx = x + dx;
            int tz = z + dir;

            if (!Inside(tx, tz))
                continue;

            PieceEtat target = GetPiece(tx, tz);

            if (target != null && target.IsWhite != pawn.IsWhite && target.Type != PieceType.King)
                moves.Add(new Vector2Int(tx, tz));
            else if (target == null && enPassantSquare == new Vector2Int(tx, tz) && GetPiece(tx, z) != null && GetPiece(tx, z).Type == PieceType.Pawn && GetPiece(tx, z).IsWhite != pawn.IsWhite)
                moves.Add(new Vector2Int(tx, tz));
        }
    }

    private void AddCastling(PieceEtat king, List<Vector2Int> moves)
    {
        if (king.HasMoved || king.X != 4 || king.Z != (king.IsWhite ? 0 : 7) || IsInCheck(king.IsWhite))
            return;

        int z = king.Z;
        bool enemy = !king.IsWhite;

        // Petit roque
        PieceEtat rook = GetPiece(size - 1, z);

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

    private static bool IsCastlingRook(PieceEtat rook, PieceEtat king)
    {
        return rook != null && rook.Type == PieceType.Rook &&
            rook.IsWhite == king.IsWhite && !rook.HasMoved;
    }

    private bool PathEmpty(int fromX, int toX, int z)
    {
        for (int x = fromX; x <= toX; x++)
        {
            if (GetPiece(x, z) != null)
                return false;
        }

        return true;
    }

    private void AddSlides(PieceEtat piece, Vector2Int[] dirs, List<Vector2Int> moves)
    {
        foreach (Vector2Int d in dirs)
        {
            int cx = piece.X + d.x;
            int cz = piece.Z + d.y;

            while (Inside(cx, cz))
            {
                PieceEtat p = GetPiece(cx, cz);

                if (p == null)
                {
                    moves.Add(new Vector2Int(cx, cz));
                }
                else
                {
                    if (p.IsWhite != piece.IsWhite && p.Type != PieceType.King)
                        moves.Add(new Vector2Int(cx, cz));

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }
    }

    private void AddIfFreeOrEnemy(PieceEtat piece, int x, int z, List<Vector2Int> moves)
    {
        if (!Inside(x, z))
            return;

        PieceEtat p = GetPiece(x, z);

        if (p == null || p.IsWhite != piece.IsWhite && p.Type != PieceType.King)
            moves.Add(new Vector2Int(x, z));
    }

}

[RequireComponent(typeof(Echiquier))]
public class DeplacementPiece : MonoBehaviour
{
    [SerializeField] private Echiquier echiquier;
    public void Configurer(Echiquier plateau) { echiquier = plateau; }
    private ReglesEchecs Regles => new ReglesEchecs((echiquier != null ? echiquier : GetComponent<Echiquier>()).Position);
    public List<Vector2Int> GetCasesDisponibles(int x, int z) => Regles.GetLegalMoves(x, z);
    public bool EstDeplacementLegal(int x, int z, int tx, int tz) => GetCasesDisponibles(x, z).Contains(new Vector2Int(tx, tz));
    public bool DeplacerPiece(int x, int z, int tx, int tz) => Regles.JouerCoup(x, z, tx, tz);
    public bool RoiEnEchec(bool blanc) => Regles.IsInCheck(blanc);
    public bool PossedeCoupLegal(bool blanc) => Regles.HasAnyLegalMove(blanc);
    public bool EstEchecEtMat(bool blanc) => RoiEnEchec(blanc) && !PossedeCoupLegal(blanc);
    public bool EstPat(bool blanc) => !RoiEnEchec(blanc) && !PossedeCoupLegal(blanc);
}
