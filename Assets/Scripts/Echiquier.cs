using System;
using UnityEngine;

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
    }

    public bool EstDansPlateau(int x, int z) => x >= 0 && x < 8 && z >= 0 && z < 8;
    public char GetPiece(int x, int z) => EstDansPlateau(x, z) ? plateau[x, z] : '.';
    public bool EstCaseVide(int x, int z) => EstDansPlateau(x, z) && plateau[x, z] == '.';
    // Edition de position : une pièce ajoutée est considérée comme ayant bougé par défaut.
    public void SetPiece(int x, int z, char piece, bool aBouge = true)
    {
        if (!EstDansPlateau(x, z)) throw new ArgumentOutOfRangeException();
        plateau[x, z] = piece;
        dejaBouge[x, z] = aBouge;
    }
    public PieceEtat LirePiece(int x, int z)
    {
        char code = GetPiece(x, z);
        return code == '.' ? null : new PieceEtat(code, x, z, dejaBouge[x, z]);
    }
    public PositionEchecs Copier()
    {
        return new PositionEchecs(false) {
            plateau = (char[,])plateau.Clone(), dejaBouge = (bool[,])dejaBouge.Clone(),
            TourBlanc = TourBlanc, CaseEnPassant = CaseEnPassant,
            DemiCoupsSansPriseNiPion = DemiCoupsSansPriseNiPion, NumeroCoup = NumeroCoup
        };
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
        switch (char.ToUpperInvariant(code))
        {
            case 'P': Type = PieceType.Pawn; break;
            case 'C': Type = PieceType.Knight; break;
            case 'F': Type = PieceType.Bishop; break;
            case 'T': Type = PieceType.Rook; break;
            case 'D': Type = PieceType.Queen; break;
            case 'R': Type = PieceType.King; break;
            default: throw new ArgumentException("Code de pièce inconnu", nameof(code));
        }
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
