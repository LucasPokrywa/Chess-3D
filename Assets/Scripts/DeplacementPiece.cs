using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Moteur utilisable sur une position réelle ou une copie destinée à une IA.
public sealed class ReglesEchecs
{
    private static readonly Unity.Profiling.ProfilerMarker MarqueurPseudoMoves =
    new Unity.Profiling.ProfilerMarker("HalloweenChess.IA.GetPseudoMoves");
    private readonly PositionEchecs position;
    private readonly List<Vector2Int> tamponPseudoMoves = new List<Vector2Int>(32);
    private const int size = 8;
    private Vector2Int enPassantSquare => position.CaseEnPassant;
    public ReglesEchecs(PositionEchecs position) { this.position = position; }
    public List<Vector2Int> GetLegalMoves(int x, int z)
    {
        char code = position.GetPiece(x, z);
        return code == '.' ? new List<Vector2Int>() : GetLegalMoves(new PieceMobile(code, x, z));
    }
    public bool JouerCoup(int x, int z, int tx, int tz)
    {
        char code = position.GetPiece(x, z);
        if (code == '.') return false;
        var piece = new PieceMobile(code, x, z);
        if (piece.IsWhite != position.TourBlanc ||
            !GetLegalMoves(piece).Contains(new Vector2Int(tx, tz))) return false;
        position.AppliquerCoup(x, z, tx, tz);
        return true;
    }
    // La génération ne construit pas le modèle public PieceEtat.
    private readonly struct PieceMobile
    {
        public readonly PieceType Type;
        public readonly bool IsWhite;
        public readonly int X, Z;

        public PieceMobile(char code, int x, int z)
        {
            Type = PieceEtat.TypeDuCode(code);
            IsWhite = char.IsLower(code);
            X = x;
            Z = z;
        }
    }

    private bool Inside(int x, int z)
    {
        return x >= 0 && z >= 0 && x < size && z < size;
    }
    private void AjouterCoupsLegaux(
    PieceMobile piece, List<Vector2Int> destination, bool tactiquesSeulement = false)
    {
        foreach (Vector2Int coup in GetPseudoMoves(piece, tactiquesSeulement))
        {
            if (!LeavesKingInCheck(piece, coup))
                destination.Add(coup);
        }
    }

    private List<Vector2Int> GetLegalMoves(PieceMobile piece)
    {
        var coups = new List<Vector2Int>();
        AjouterCoupsLegaux(piece , coups);
        return coups;
    }

    internal void GetLegalMoves(
    int x, int z, List<Vector2Int> destination, bool tactiquesSeulement = false)
    {
        destination.Clear();

        char code = position.GetPiece(x, z);
        if (code == '.')
            return;

        AjouterCoupsLegaux(new PieceMobile(code, x, z), destination, tactiquesSeulement);
    }

    // Le parcours tactique fournit le code de la pièce déjà lu sur sa case de départ.
    internal void GetLegalMovesTactiques(int x, int z, char code, List<Vector2Int> destination)
    {
        destination.Clear();
        AjouterCoupsLegaux(new PieceMobile(code, x, z), destination, tactiquesSeulement: true);
    }

    internal bool EstCoupLegalAuTrait(Vector2Int depart, Vector2Int arrivee)
    {
        char code = position.GetPiece(depart.x, depart.y);
        if (code == '.') return false;
        var piece = new PieceMobile(code, depart.x, depart.y);
        return piece.IsWhite == position.TourBlanc &&
            GetPseudoMoves(piece).Contains(arrivee) && !LeavesKingInCheck(piece, arrivee);
    }


    public bool HasAnyLegalMove(bool white)
    {
        for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++) {
                char code = position.GetPiece(x, z);
                if (code == '.' || char.IsLower(code) != white)
                    continue;

                var piece = new PieceMobile(code, x, z);
                // Un seul coup suffit : ne pas calculer toute la liste des coups légaux.
                foreach (Vector2Int move in GetPseudoMoves(piece))
                    if (!LeavesKingInCheck(piece, move)) return true;
            }
        return false;
    }

    // Joue le coup "à blanc" sur la grille pour vérifier que le roi reste en sécurité.
    private bool LeavesKingInCheck(PieceMobile piece, Vector2Int to)
    {
        bool coupSpecial = piece.Type == PieceType.King ||
            (piece.Type == PieceType.Pawn && (to == enPassantSquare || to.y == 0 || to.y == 7));
        if (!coupSpecial)
        {
            char capture = position.DeplacerPourVerifierEchec(piece.X, piece.Z, to.x, to.y);
            try { return IsInCheck(piece.IsWhite); }
            finally { position.AnnulerVerificationEchec(piece.X, piece.Z, to.x, to.y, capture); }
        }

        PositionEchecs.EtatAvantCoup etat = position.JouerTemporairement(piece.X, piece.Z, to.x, to.y);
        try { return IsInCheck(piece.IsWhite); }
        finally { position.AnnulerCoup(etat); }
    }
    public bool IsInCheck(bool white)
    {
        Vector2Int king = white ? position.CaseRoiBlanc : position.CaseRoiNoir;
        return king.x < 0 || IsSquareAttacked(king.x, king.y, !white);
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
            if (position.GetPiece(x - dx, z - pawnDir) == (byWhite ? 'p' : 'P'))
                return true;
        }

        // Cavaliers
        foreach (Vector2Int j in KnightJumps)
        {
            if (position.GetPiece(x + j.x, z + j.y) == (byWhite ? 'c' : 'C'))
                return true;
        }

        // Roi adverse
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;

                if (position.GetPiece(x + dx, z + dz) == (byWhite ? 'r' : 'R'))
                    return true;
            }
        }

        // Lignes (tour / dame) et diagonales (fou / dame)
        char codeDame = byWhite ? 'd' : 'D';
        if (RayAttacked(x, z, StraightDirs, byWhite ? 't' : 'T', codeDame))
            return true;

        if (RayAttacked(x, z, DiagonalDirs, byWhite ? 'f' : 'F', codeDame))
            return true;

        return false;
    }

    private bool RayAttacked(int x, int z, Vector2Int[] dirs, char codePiece, char codeDame)
    {
        foreach (Vector2Int d in dirs)
        {
            int cx = x + d.x;
            int cz = z + d.y;

            while (Inside(cx, cz))
            {
                char p = position.GetPiece(cx, cz);

                if (p != '.')
                {
                    if (p == codePiece || p == codeDame)
                        return true;

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }

        return false;
    }

    private List<Vector2Int> GetPseudoMoves(PieceMobile piece, bool tactiquesSeulement = false)
    {
        using var mesure = MarqueurPseudoMoves.Auto(); 
        List<Vector2Int> moves = tamponPseudoMoves;
        moves.Clear();
        int x = piece.X;
        int z = piece.Z;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, moves, tactiquesSeulement);
                break;

            case PieceType.Knight:
                foreach (Vector2Int j in KnightJumps)
                    AddIfFreeOrEnemy(piece, x + j.x, z + j.y, moves, tactiquesSeulement);
                break;

            case PieceType.Bishop:
                AddSlides(piece, DiagonalDirs, moves, tactiquesSeulement);
                break;

            case PieceType.Rook:
                AddSlides(piece, StraightDirs, moves, tactiquesSeulement);
                break;

            case PieceType.Queen:
                AddSlides(piece, StraightDirs, moves, tactiquesSeulement);
                AddSlides(piece, DiagonalDirs, moves, tactiquesSeulement);
                break;

            case PieceType.King:
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx != 0 || dz != 0)
                            AddIfFreeOrEnemy(piece, x + dx, z + dz, moves, tactiquesSeulement);
                    }
                }

                if (!tactiquesSeulement) AddCastling(piece, moves);
                break;
        }

        return moves;
    }

    private void AddPawnMoves(PieceMobile pawn, List<Vector2Int> moves, bool tactiquesSeulement)
    {
        int dir = pawn.IsWhite ? 1 : -1;
        int startRank = pawn.IsWhite ? 1 : size - 2;
        int x = pawn.X;
        int z = pawn.Z;

        // En mode tactique, seule une avance qui promeut mérite d'être produite.
        if ((!tactiquesSeulement || z + dir == 0 || z + dir == size - 1) &&
            Inside(x, z + dir) && position.GetPiece(x, z + dir) == '.')
        {
            moves.Add(new Vector2Int(x, z + dir));

            if (!tactiquesSeulement && z == startRank && Inside(x, z + 2 * dir) && position.GetPiece(x, z + 2 * dir) == '.')
                moves.Add(new Vector2Int(x, z + 2 * dir));
        }

        for (int dx = -1; dx <= 1; dx += 2)
        {
            int tx = x + dx;
            int tz = z + dir;

            if (!Inside(tx, tz))
                continue;

            char cible = position.GetPiece(tx, tz);
            var arrivee = new Vector2Int(tx, tz);
            if (cible != '.')
            {
                if (char.IsLower(cible) != pawn.IsWhite && cible != 'r' && cible != 'R')
                    moves.Add(arrivee);
            }
            else if (arrivee == enPassantSquare)
            {
                char adjacent = position.GetPiece(tx, z);
                if (adjacent == (pawn.IsWhite ? 'P' : 'p'))
                    moves.Add(arrivee);
            }
        }
    }

    private void AddCastling(PieceMobile king, List<Vector2Int> moves)
    {
        if (king.X != 4 || king.Z != (king.IsWhite ? 0 : 7) ||
            position.ADejaBouge(king.X, king.Z) || IsInCheck(king.IsWhite))
            return;

        int z = king.Z;
        bool enemy = !king.IsWhite;

        // Petit roque
        if (IsCastlingRook(size - 1, z, king.IsWhite) &&
            PathEmpty(king.X + 1, size - 2, z) &&
            !IsSquareAttacked(king.X + 1, z, enemy) &&
            !IsSquareAttacked(king.X + 2, z, enemy))
        {
            moves.Add(new Vector2Int(king.X + 2, z));
        }

        // Grand roque
        if (IsCastlingRook(0, z, king.IsWhite) &&
            PathEmpty(1, king.X - 1, z) &&
            !IsSquareAttacked(king.X - 1, z, enemy) &&
            !IsSquareAttacked(king.X - 2, z, enemy))
        {
            moves.Add(new Vector2Int(king.X - 2, z));
        }
    }

    private bool IsCastlingRook(int x, int z, bool blanc)
    {
        return position.GetPiece(x, z) == (blanc ? 't' : 'T') && !position.ADejaBouge(x, z);
    }

    private bool PathEmpty(int fromX, int toX, int z)
    {
        for (int x = fromX; x <= toX; x++)
        {
            if (position.GetPiece(x, z) != '.')
                return false;
        }

        return true;
    }

    private void AddSlides(PieceMobile piece, Vector2Int[] dirs, List<Vector2Int> moves, bool tactiquesSeulement)
    {
        foreach (Vector2Int d in dirs)
        {
            int cx = piece.X + d.x;
            int cz = piece.Z + d.y;

            while (Inside(cx, cz))
            {
                char cible = position.GetPiece(cx, cz);

                if (cible == '.')
                {
                    // Continuer le rayon jusqu'au premier obstacle sans lister les coups calmes.
                    if (!tactiquesSeulement) moves.Add(new Vector2Int(cx, cz));
                }
                else
                {
                    if (char.IsLower(cible) != piece.IsWhite && cible != 'r' && cible != 'R')
                        moves.Add(new Vector2Int(cx, cz));

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }
    }

    private void AddIfFreeOrEnemy(PieceMobile piece, int x, int z, List<Vector2Int> moves, bool tactiquesSeulement)
    {
        if (!Inside(x, z)) return;

        char cible = position.GetPiece(x, z);

        if ((cible == '.' && !tactiquesSeulement) ||
            (cible != '.' && char.IsLower(cible) != piece.IsWhite && cible != 'r' && cible != 'R'))
        {
            moves.Add(new Vector2Int(x, z));
        }
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
