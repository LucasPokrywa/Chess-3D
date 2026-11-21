using System;
using UnityEngine;
using Unity.Profiling;
public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

// Position indépendante des GameObjects : x = colonne, z = rangée, blancs en z=0/1.
// Minuscules = blancs ; P,C,F,T,D,R = pion, cavalier, fou, tour, dame, roi.
public sealed partial class PositionEchecs
{
    private char[,] plateau = new char[8, 8];
    private bool[,] dejaBouge = new bool[8, 8];
    public bool TourBlanc { get; private set; } = true;
    public Vector2Int CaseEnPassant { get; private set; } = new Vector2Int(-1, -1);
    // (-1, -1) si le roi est absent d'une position en cours d'édition.
    public Vector2Int CaseRoiBlanc { get; private set; } = new Vector2Int(-1, -1);
    public Vector2Int CaseRoiNoir { get; private set; } = new Vector2Int(-1, -1);
    public int DemiCoupsSansPriseNiPion { get; private set; }
    public int NumeroCoup { get; private set; } = 1;

    public PositionEchecs(bool initialiser = true)
    {
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++) plateau[x, z] = '.';
        if (!initialiser) return;
        const string rang = "tcfdrfct";
        for (int x = 0; x < 8; x++)
        {
            plateau[x, 0] = rang[x]; plateau[x, 1] = 'p';
            plateau[x, 6] = 'P'; plateau[x, 7] = char.ToUpperInvariant(rang[x]);
        }
        CaseRoiBlanc = new Vector2Int(4, 0);
        CaseRoiNoir = new Vector2Int(4, 7);
    }

    public bool EstDansPlateau(int x, int z) => x >= 0 && x < 8 && z >= 0 && z < 8;
    public char GetPiece(int x, int z) => EstDansPlateau(x, z) ? plateau[x, z] : '.';
    public bool EstCaseVide(int x, int z) => EstDansPlateau(x, z) && plateau[x, z] == '.';
    internal bool ADejaBouge(int x, int z) => dejaBouge[x, z];
    // Edition de position : une pièce ajoutée est considérée comme ayant bougé par défaut.
    public void SetPiece(int x, int z, char piece, bool aBouge = true)
    {
        if (!EstDansPlateau(x, z)) throw new ArgumentOutOfRangeException();
        char ancienne = plateau[x, z];
        // Un déplacement écrit d'abord l'arrivée, puis efface le départ :
        // ne pas effacer la nouvelle coordonnée en vidant l'ancienne case.
        if (ancienne == 'r' && CaseRoiBlanc.x == x && CaseRoiBlanc.y == z)
            CaseRoiBlanc = new Vector2Int(-1, -1);
        else if (ancienne == 'R' && CaseRoiNoir.x == x && CaseRoiNoir.y == z)
            CaseRoiNoir = new Vector2Int(-1, -1);
        plateau[x, z] = piece;
        dejaBouge[x, z] = aBouge;
        if (piece == 'r') CaseRoiBlanc = new Vector2Int(x, z);
        else if (piece == 'R') CaseRoiNoir = new Vector2Int(x, z);
    }

    private static readonly ProfilerMarker MarqueurLirePiece =
    new ProfilerMarker("HalloweenChess.IA.LirePiece");
    public PieceEtat LirePiece(int x, int z)
    {
        using (MarqueurLirePiece.Auto())
        {
            char code = GetPiece(x, z);
            return code == '.' ? null : new PieceEtat(code, x, z, dejaBouge[x, z]);
        }
    }
    public PositionEchecs Copier()
    {
        return new PositionEchecs(false) {
            plateau = (char[,])plateau.Clone(), dejaBouge = (bool[,])dejaBouge.Clone(),
            TourBlanc = TourBlanc, CaseEnPassant = CaseEnPassant,
            CaseRoiBlanc = CaseRoiBlanc, CaseRoiNoir = CaseRoiNoir,
            DemiCoupsSansPriseNiPion = DemiCoupsSansPriseNiPion, NumeroCoup = NumeroCoup
        };
    }

    // Sauvegarde uniquement les cases touchées : aucune copie de tableau ni allocation.
    // Réservé aux coups prévalidés et à une position appartenant au thread de recherche.
    internal readonly struct CaseSauvee
    {
        internal readonly int X, Z;
        internal readonly char Piece;
        internal readonly bool ABouge;
        internal CaseSauvee(PositionEchecs p, int x, int z)
        {
            X = x; Z = z; Piece = p.plateau[x, z]; ABouge = p.dejaBouge[x, z];
        }
    }

    internal readonly struct EtatAvantCoup
    {
        internal readonly CaseSauvee Depart, Arrivee, Extra1, Extra2;
        internal readonly bool Tour;
        internal readonly Vector2Int EnPassant;
        internal readonly Vector2Int RoiBlanc, RoiNoir;
        internal readonly int DemiCoups, Numero;
        internal EtatAvantCoup(PositionEchecs p, int x, int z, int tx, int tz)
        {
            Depart = new CaseSauvee(p, x, z);
            Arrivee = new CaseSauvee(p, tx, tz);
            Extra1 = Depart; Extra2 = Depart;
            char type = char.ToUpperInvariant(Depart.Piece);
            if (type == 'P' && new Vector2Int(tx, tz) == p.CaseEnPassant && Arrivee.Piece == '.')
                Extra1 = new CaseSauvee(p, tx, z);
            if (type == 'R' && Math.Abs(tx - x) == 2)
            {
                Extra1 = new CaseSauvee(p, tx > x ? 7 : 0, z);
                Extra2 = new CaseSauvee(p, tx > x ? tx - 1 : tx + 1, z);
            }
            Tour = p.TourBlanc; EnPassant = p.CaseEnPassant;
            RoiBlanc = p.CaseRoiBlanc; RoiNoir = p.CaseRoiNoir;
            DemiCoups = p.DemiCoupsSansPriseNiPion; Numero = p.NumeroCoup;
        }
    }

    // Vérification d'échec pour un coup ordinaire sans déplacement de roi.
    // Seule l'occupation des deux cases compte pour les attaques adverses.
    internal char DeplacerPourVerifierEchec(int x, int z, int tx, int tz)
    {
        char capture = plateau[tx, tz];
        plateau[tx, tz] = plateau[x, z];
        plateau[x, z] = '.';
        return capture;
    }

    internal void AnnulerVerificationEchec(int x, int z, int tx, int tz, char capture)
    {
        plateau[x, z] = plateau[tx, tz];
        plateau[tx, tz] = capture;
    }

    internal EtatAvantCoup JouerTemporairement(int x, int z, int tx, int tz)
    {
        var etat = new EtatAvantCoup(this, x, z, tx, tz);
        AppliquerCoup(x, z, tx, tz);
        return etat;
    }

    internal void AnnulerCoup(EtatAvantCoup etat)
    {
        RestaurerCase(etat.Depart); RestaurerCase(etat.Arrivee);
        RestaurerCase(etat.Extra1); RestaurerCase(etat.Extra2);
        TourBlanc = etat.Tour; CaseEnPassant = etat.EnPassant;
        CaseRoiBlanc = etat.RoiBlanc; CaseRoiNoir = etat.RoiNoir;
        DemiCoupsSansPriseNiPion = etat.DemiCoups; NumeroCoup = etat.Numero;
    }

    private void RestaurerCase(CaseSauvee sauvegarde)
    {
        plateau[sauvegarde.X, sauvegarde.Z] = sauvegarde.Piece;
        dejaBouge[sauvegarde.X, sauvegarde.Z] = sauvegarde.ABouge;
    }

    // Coup prévalidé par ReglesEchecs. Utilisé aussi sur les copies de simulation.
    internal void AppliquerCoup(int x, int z, int tx, int tz)
    {
        char piece = plateau[x, z];
        bool pion = char.ToUpperInvariant(piece) == 'P';
        bool capture = plateau[tx, tz] != '.' ||
            (pion && new Vector2Int(tx, tz) == CaseEnPassant);
        DemiCoupsSansPriseNiPion = pion || capture ? 0 :
            (DemiCoupsSansPriseNiPion < int.MaxValue ? DemiCoupsSansPriseNiPion + 1 : int.MaxValue);
        if (!TourBlanc && NumeroCoup < int.MaxValue) NumeroCoup++;
        if (pion && new Vector2Int(tx, tz) == CaseEnPassant && plateau[tx, tz] == '.')
            SetPiece(tx, z, '.');
        if (char.ToUpperInvariant(piece) == 'R' && Math.Abs(tx - x) == 2)
        {
            int tourDepart = tx > x ? 7 : 0;
            int tourArrivee = tx > x ? tx - 1 : tx + 1;
            SetPiece(tourArrivee, z, plateau[tourDepart, z]);
            SetPiece(tourDepart, z, '.');
        }
        CaseEnPassant = pion && Math.Abs(tz - z) == 2
            ? new Vector2Int(x, (z + tz) / 2) : new Vector2Int(-1, -1);
        if (pion && (tz == 0 || tz == 7)) piece = char.IsLower(piece) ? 'd' : 'D';
        SetPiece(tx, tz, piece);
        SetPiece(x, z, '.');
        TourBlanc = !TourBlanc;
    }
}

// Données d'une pièce logique, sans référence à un composant Unity.
public sealed class PieceEtat
{
    public PieceType Type { get; }
    public bool IsWhite { get; }
    public int X { get; }
    public int Z { get; }
    public bool HasMoved { get; }
    public PieceEtat(char code, int x, int z, bool moved)
    {
        IsWhite = char.IsLower(code); X = x; Z = z; HasMoved = moved;
        Type = TypeDuCode(code);
    }

    internal static PieceType TypeDuCode(char code)
    {
        return char.ToUpperInvariant(code) switch
        {
            'P' => PieceType.Pawn,
            'C' => PieceType.Knight,
            'F' => PieceType.Bishop,
            'T' => PieceType.Rook,
            'D' => PieceType.Queen,
            'R' => PieceType.King,
            _ => throw new ArgumentException("Code de pièce inconnu", nameof(code))
        };
    }
}

public class Echiquier : MonoBehaviour
{
    // Initialisé avant Awake : utilisable quelle que soit l'ordre des composants.
    public PositionEchecs Position { get; private set; } = new PositionEchecs();
    public void InitialiserPlateau() => Position = new PositionEchecs();
    internal void DefinirPosition(PositionEchecs position) => Position = position;
    public string ExporterFen() => Position.ExporterFen();
    public bool EstDansPlateau(int x, int z) => Position.EstDansPlateau(x, z);
    public char GetPiece(int x, int z) => Position.GetPiece(x, z);
    public void SetPiece(int x, int z, char piece) => Position.SetPiece(x, z, piece);
    public bool EstCaseVide(int x, int z) => Position.EstCaseVide(x, z);
    public bool DeplacerPiece(int x, int z, int tx, int tz) => new ReglesEchecs(Position).JouerCoup(x, z, tx, tz);
    public void AfficherPlateau()
    {
        string texte = $"Échiquier — coup {Position.NumeroCoup}, trait aux {(Position.TourBlanc ? "blancs" : "noirs")}\n";
        for (int z = 7; z >= 0; z--) {
            texte += (z + 1) + "  ";
            for (int x = 0; x < 8; x++) texte += GetPiece(x, z) + " ";
            texte += "\n";
        }
        texte += "   a b c d e f g h\nMinuscules : blancs | Majuscules : noirs | . : case vide";
        Debug.Log(texte, this);
    }
}
