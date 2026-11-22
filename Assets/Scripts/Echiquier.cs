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

    /// <summary>
    /// Installe les codes de départ et les cases de roi si une position initiale est demandée. Le chemin vide est utilisé par le lecteur FEN et la copie.
    /// </summary>
    /// <param name="initialiser">Vrai pour placer les pièces de départ, faux pour créer une grille vide.</param>
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

    /// <summary>
    /// Valide les deux coordonnées logiques avant un accès au tableau.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Vrai si la case appartient au plateau.</returns>
    public bool EstDansPlateau(int x, int z) => x >= 0 && x < 8 && z >= 0 && z < 8;
    /// <summary>
    /// Lit le code brut de la grille sur la case. L’appelant fournit des coordonnées valides.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Code français de pièce ou point pour une case vide.</returns>
    public char GetPiece(int x, int z) => EstDansPlateau(x, z) ? plateau[x, z] : '.';
    /// <summary>
    /// Teste le contenu brut d’une case valide.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Vrai si la case contient le code du vide.</returns>
    public bool EstCaseVide(int x, int z) => EstDansPlateau(x, z) && plateau[x, z] == '.';
    /// <summary>
    /// Expose l’indicateur de la case, utilisé notamment pour vérifier les droits de roque.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Vrai si l’indicateur de mouvement est levé.</returns>
    internal bool ADejaBouge(int x, int z) => dejaBouge[x, z];
    // Edition de position : une pièce ajoutée est considérée comme ayant bougé par défaut.
    /// <summary>
    /// Met à jour le code de la case et son indicateur de mouvement. Maintient également les coordonnées mémorisées des rois.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="aBouge">Indicateur de mouvement à associer à la case.</param>
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
    /// <summary>
    /// Duplique les deux tableaux et tous les champs d’état nécessaires aux règles et au FEN.
    /// </summary>
    /// <returns>Position indépendante que la recherche peut modifier.</returns>
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
        /// <summary>
        /// Capture l’ancien contenu et l’indicateur de mouvement d’une case avant sa modification.
        /// </summary>
        /// <param name="p">Position logique soumise au contrôle.</param>
        /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
        /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
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
        /// <summary>
        /// Enregistre départ, arrivée et éventuelles cases supplémentaires, ainsi que le trait, les droits implicites, les rois et les compteurs utiles à l’annulation.
        /// </summary>
        /// <param name="p">Position logique soumise au contrôle.</param>
        /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
        /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
        /// <param name="tx">Colonne logique de destination.</param>
        /// <param name="tz">Rangée logique de destination.</param>
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
    /// <summary>
    /// Déplace uniquement les codes sur deux cases pour un contrôle d’échec ordinaire. Les rois et coups spéciaux empruntent la simulation complète.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Ancien code de la destination que l’appelant doit rétablir.</returns>
    internal char DeplacerPourVerifierEchec(int x, int z, int tx, int tz)
    {
        char capture = plateau[tx, tz];
        plateau[tx, tz] = plateau[x, z];
        plateau[x, z] = '.';
        return capture;
    }

    /// <summary>
    /// Rétablit les deux cases modifiées par la simulation courte. Le trait et le reste de la position n’ont pas été changés.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <param name="capture">Code de destination sauvegardé avant la simulation courte.</param>
    internal void AnnulerVerificationEchec(int x, int z, int tx, int tz, char capture)
    {
        plateau[x, z] = plateau[tx, tz];
        plateau[tx, tz] = capture;
    }

    /// <summary>
    /// Capture l’état d’annulation puis joue un coup déjà prévalidé. L’appelant rétablit cet état en ordre inverse.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Instantané requis pour annuler le coup et mettre à jour l’évaluation.</returns>
    internal EtatAvantCoup JouerTemporairement(int x, int z, int tx, int tz)
    {
        var etat = new EtatAvantCoup(this, x, z, tx, tz);
        AppliquerCoup(x, z, tx, tz);
        return etat;
    }

    /// <summary>
    /// Restaure les cases sauvegardées, le trait, les rois, la case en passant et les compteurs de la position.
    /// </summary>
    /// <param name="etat">Instantané pris immédiatement avant le coup à annuler.</param>
    internal void AnnulerCoup(EtatAvantCoup etat)
    {
        RestaurerCase(etat.Depart); RestaurerCase(etat.Arrivee);
        RestaurerCase(etat.Extra1); RestaurerCase(etat.Extra2);
        TourBlanc = etat.Tour; CaseEnPassant = etat.EnPassant;
        CaseRoiBlanc = etat.RoiBlanc; CaseRoiNoir = etat.RoiNoir;
        DemiCoupsSansPriseNiPion = etat.DemiCoups; NumeroCoup = etat.Numero;
    }

    /// <summary>
    /// Réécrit le code et l’indicateur exacts de la case sauvegardée sans recalculer les autres champs.
    /// </summary>
    /// <param name="sauvegarde">Code, coordonnées et indicateur antérieurs de la case.</param>
    private void RestaurerCase(CaseSauvee sauvegarde)
    {
        plateau[sauvegarde.X, sauvegarde.Z] = sauvegarde.Piece;
        dejaBouge[sauvegarde.X, sauvegarde.Z] = sauvegarde.ABouge;
    }

    // Coup prévalidé par ReglesEchecs. Utilisé aussi sur les copies de simulation.
    /// <summary>
    /// Applique un coup prévalidé, y compris ses effets sur la tour, le pion capturé en passant ou la promotion. Met à jour les compteurs et inverse le trait.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
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

    /// <summary>
    /// Convertit un code français en type de pièce indépendamment de sa couleur. Le générateur peut ainsi fonctionner directement sur les caractères.
    /// </summary>
    /// <param name="code">Caractère français du plateau, dont la casse encode le camp.</param>
    /// <returns>Type reconnu ou valeur de repli prévue pour les autres codes.</returns>
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
