using System.Collections.Generic;
using UnityEngine;

// Moteur utilisable sur une position réelle ou une copie destinée à une IA.
public sealed class ReglesEchecs
{
    private enum FiltrePseudoCoups { Tous, Tactiques, Calmes }

    internal List<Vector2Int> PseudoCoupsPourPhase(int x, int z, char code, bool tactiques)
    {
        return GetPseudoMoves(new PieceMobile(code, x, z),
            tactiques ? FiltrePseudoCoups.Tactiques : FiltrePseudoCoups.Calmes);
    }

    // À appeler uniquement sur un candidat produit par PseudoCoupsPourPhase dans cette position.
    internal bool ValiderPseudoCoup(Vector2Int depart, Vector2Int arrivee)
    {
        var piece = new PieceMobile(position.GetPiece(depart.x, depart.y), depart.x, depart.y);
        return !LeavesKingInCheck(piece, arrivee);
    }

    private static readonly Unity.Profiling.ProfilerMarker MarqueurPseudoMoves =
    new Unity.Profiling.ProfilerMarker("HalloweenChess.IA.GetPseudoMoves");
    private readonly PositionEchecs position;
    private readonly List<Vector2Int> tamponPseudoMoves = new List<Vector2Int>(32);
    private const int size = 8;
    private Vector2Int enPassantSquare => position.CaseEnPassant;
    /// <summary>
    /// Attache les règles et leur tampon à un plateau précis. Les déplacements simulés affectent temporairement cette instance.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    public ReglesEchecs(PositionEchecs position) { this.position = position; }
    /// <summary>
    /// Lit la pièce sur sa case et construit une liste dédiée à l’appelant. Cette entrée est utilisée notamment par la sélection visuelle.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Destinations légales de la pièce ou liste vide.</returns>
    public List<Vector2Int> GetLegalMoves(int x, int z)
    {
        char code = position.GetPiece(x, z);
        return code == '.' ? new List<Vector2Int>() : GetLegalMoves(new PieceMobile(code, x, z));
    }
    /// <summary>
    /// Contrôle le trait et la destination avant d’appliquer un coup permanent. Un refus conserve la position.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Vrai si le coup a été appliqué.</returns>
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

        /// <summary>
        /// Prépare les quelques données nécessaires au générateur sans instancier de PieceEtat.
        /// </summary>
        /// <param name="code">Caractère français du plateau, dont la casse encode le camp.</param>
        /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
        /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
        public PieceMobile(char code, int x, int z)
        {
            Type = PieceEtat.TypeDuCode(code);
            IsWhite = char.IsLower(code);
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Protège les accès des générateurs et des parcours de rayons en bord de plateau.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Vrai pour une case du plateau 8 × 8.</returns>
    private bool Inside(int x, int z)
    {
        return x >= 0 && z >= 0 && x < size && z < size;
    }
    /// <summary>
    /// Ajoute au tampon de destination les pseudo-coups qui laissent le roi de la pièce en sécurité. L’ordre des candidats retenus est conservé.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="destination">Tampon de destinations fourni par l’appelant.</param>
    /// <param name="tactiquesSeulement">Vrai pour limiter les destinations aux captures, prises en passant et promotions.</param>
    private void AjouterCoupsLegaux(
    PieceMobile piece, List<Vector2Int> destination, bool tactiquesSeulement = false)
    {
        foreach (Vector2Int coup in GetPseudoMoves(piece,
            tactiquesSeulement ? FiltrePseudoCoups.Tactiques : FiltrePseudoCoups.Tous))
        {
            if (!LeavesKingInCheck(piece, coup))
                destination.Add(coup);
        }
    }

    /// <summary>
    /// Isole les résultats des tampons réutilisables internes pour les appelants qui ont besoin d’une liste dédiée.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <returns>Destinations vérifiées de la pièce.</returns>
    private List<Vector2Int> GetLegalMoves(PieceMobile piece)
    {
        var coups = new List<Vector2Int>();
        AjouterCoupsLegaux(piece , coups);
        return coups;
    }

    /// <summary>
    /// Réinitialise la liste de l’appelant avant d’y générer les coups de la pièce présente, avec filtrage du roi.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="destination">Tampon vidé puis rempli par cette méthode.</param>
    /// <param name="tactiquesSeulement">Vrai pour limiter les destinations aux captures, prises en passant et promotions.</param>
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
    /// <summary>
    /// Réutilise le code déjà chargé par le parcours du moteur. Vide le tampon fourni puis produit les captures, prises en passant et promotions légales.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="code">Caractère français du plateau, dont la casse encode le camp.</param>
    /// <param name="destination">Tampon vidé puis rempli de destinations légales tactiques.</param>
    internal void GetLegalMovesTactiques(int x, int z, char code, List<Vector2Int> destination)
    {
        destination.Clear();
        AjouterCoupsLegaux(new PieceMobile(code, x, z), destination, tactiquesSeulement: true);
    }

    /// <summary>
    /// Valide un seul couple de cases au lieu de construire tous les coups légaux. La vérification du roi annule toute simulation.
    /// </summary>
    /// <param name="depart">Case de départ exprimée en coordonnées logiques.</param>
    /// <param name="arrivee">Case de destination exprimée en coordonnées logiques.</param>
    /// <returns>Vrai si la pièce est au trait, atteint la cible et protège son roi.</returns>
    internal bool EstCoupLegalAuTrait(Vector2Int depart, Vector2Int arrivee)
    {
        char code = position.GetPiece(depart.x, depart.y);
        if (code == '.') return false;
        var piece = new PieceMobile(code, depart.x, depart.y);
        return piece.IsWhite == position.TourBlanc &&
            GetPseudoMoves(piece).Contains(arrivee) && !LeavesKingInCheck(piece, arrivee);
    }


    /// <summary>
    /// Construit les pseudo-coups d’une pièce à la fois et s’arrête au premier candidat légal. Sert à distinguer un terminal d’un nœud sans tactique.
    /// </summary>
    /// <param name="white">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <returns>Vrai dès qu’une pièce du camp peut jouer.</returns>
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
    /// <summary>
    /// Simule brièvement un coup ordinaire sur deux cases. Utilise l’état complet pour les coups de roi, les promotions et la prise en passant. Les deux chemins restaurent leur simulation.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="to">Destination logique du coup candidat.</param>
    /// <returns>Vrai lorsque le roi du camp qui joue reste ou devient attaqué.</returns>
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

        // Les coups spéciaux peuvent modifier d’autres cases ou déplacer le roi mémorisé.
        PositionEchecs.EtatAvantCoup etat = position.JouerTemporairement(piece.X, piece.Z, to.x, to.y);
        try { return IsInCheck(piece.IsWhite); }
        finally { position.AnnulerCoup(etat); }
    }
    /// <summary>
    /// Lit la case de roi conservée par la position, puis interroge les attaques adverses.
    /// </summary>
    /// <param name="white">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <returns>Vrai si le roi est attaqué ou absent du plateau.</returns>
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
    /// <summary>
    /// Recherche les attaques de pions, cavaliers, rois et pièces à rayons. C’est un test géométrique d’attaque, utilisé aussi sur les cases de transit du roque.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="byWhite">Vrai si les attaques des Blancs doivent être recherchées.</param>
    /// <returns>Vrai dès qu’une pièce du camp indiqué attaque la case.</returns>
    private bool IsSquareAttacked(int x, int z, bool byWhite)
    {
        MasquesAttaque masques = position.MasquesActifs;
        return masques != null ? masques.EstAttaque(x, z, byWhite) : AttaqueGrille(x, z, byWhite);
    }

    private bool AttaqueGrille(int x, int z, bool byWhite)
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

    /// <summary>
    /// Suit chaque direction jusqu’au bord ou à la première pièce. Une pièce interposée arrête le rayon même si elle ne correspond pas à l’attaquant visé.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="dirs">Vecteurs unitaires des rayons à parcourir.</param>
    /// <param name="codePiece">Code de l’attaquant attendu sur ces directions.</param>
    /// <param name="codeDame">Code de la dame du même camp, également autorisée sur ces directions.</param>
    /// <returns>Vrai si un rayon rencontre en premier la pièce attendue ou la dame.</returns>
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

    /// <summary>
    /// Réemploie un tampon interne selon le type de pièce. Un pseudo-coup respecte le déplacement, puis doit encore passer le contrôle du roi.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="filtre">Famille de destinations à produire.</param>
    /// <returns>Liste interne réutilisée dès le prochain appel.</returns>
    private List<Vector2Int> GetPseudoMoves(PieceMobile piece, FiltrePseudoCoups filtre = FiltrePseudoCoups.Tous)
    {
        using var mesure = MarqueurPseudoMoves.Auto(); 
        List<Vector2Int> moves = tamponPseudoMoves;
        moves.Clear();
        int x = piece.X;
        int z = piece.Z;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, moves, filtre);
                break;

            case PieceType.Knight:
                foreach (Vector2Int j in KnightJumps)
                    AddIfFreeOrEnemy(piece, x + j.x, z + j.y, moves, filtre);
                break;

            case PieceType.Bishop:
                AddSlides(piece, DiagonalDirs, moves, filtre);
                break;

            case PieceType.Rook:
                AddSlides(piece, StraightDirs, moves, filtre);
                break;

            case PieceType.Queen:
                AddSlides(piece, StraightDirs, moves, filtre);
                AddSlides(piece, DiagonalDirs, moves, filtre);
                break;

            case PieceType.King:
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx != 0 || dz != 0)
                            AddIfFreeOrEnemy(piece, x + dx, z + dz, moves, filtre);
                    }
                }

                if (filtre != FiltrePseudoCoups.Tactiques) AddCastling(piece, moves);
                break;
        }

        return moves;
    }

    /// <summary>
    /// Produit avancées, captures et prise en passant avec lecture directe des codes de cases. Le mode tactique conserve les avancées qui promeuvent et les captures de toutes sortes.
    /// </summary>
    /// <param name="pawn">Pion dont les destinations sont générées.</param>
    /// <param name="moves">Liste de candidats à compléter dans l’ordre de parcours.</param>
    /// <param name="filtre">Famille de destinations à produire.</param>
    private void AddPawnMoves(PieceMobile pawn, List<Vector2Int> moves, FiltrePseudoCoups filtre)
    {
        int dir = pawn.IsWhite ? 1 : -1;
        int startRank = pawn.IsWhite ? 1 : size - 2;
        int x = pawn.X;
        int z = pawn.Z;

        // En mode tactique, seule une avance qui promeut mérite d'être produite.
        bool promotion = z + dir == 0 || z + dir == size - 1;
        bool inclureAvance = filtre == FiltrePseudoCoups.Tous ||
            (filtre == FiltrePseudoCoups.Tactiques ? promotion : !promotion);
        if (inclureAvance && Inside(x, z + dir) && position.GetPiece(x, z + dir) == '.')
        {
            moves.Add(new Vector2Int(x, z + dir));

            if (filtre != FiltrePseudoCoups.Tactiques && z == startRank && Inside(x, z + 2 * dir) && position.GetPiece(x, z + 2 * dir) == '.')
                moves.Add(new Vector2Int(x, z + 2 * dir));
        }

        if (filtre == FiltrePseudoCoups.Calmes) return;
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

    /// <summary>
    /// Ajoute les roques dont les pièces ont leurs droits et dont les cases sont libres et sûres. Le roi au départ, en transit et à l’arrivée doit être protégé.
    /// </summary>
    /// <param name="king">Roi du camp dont les droits de roque sont testés.</param>
    /// <param name="moves">Liste de candidats à compléter dans l’ordre de parcours.</param>
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

    /// <summary>
    /// Vérifie la pièce située à l’extrémité du plateau et son indicateur de mouvement.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="blanc">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <returns>Vrai pour une tour du bon camp qui a encore son droit.</returns>
    private bool IsCastlingRook(int x, int z, bool blanc)
    {
        return position.GetPiece(x, z) == (blanc ? 't' : 'T') && !position.ADejaBouge(x, z);
    }

    /// <summary>
    /// Parcourt les cases intérieures de l’intervalle horizontal entre le roi et la tour.
    /// </summary>
    /// <param name="fromX">Colonne de départ, exclue du test des cases à traverser.</param>
    /// <param name="toX">Colonne de destination, exclue du test des cases à traverser.</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Vrai si les cases à traverser sont libres.</returns>
    private bool PathEmpty(int fromX, int toX, int z)
    {
        for (int x = fromX; x <= toX; x++)
        {
            if (position.GetPiece(x, z) != '.')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Suit les directions de la pièce jusqu’au premier obstacle. Le mode tactique traverse les cases vides sans les ajouter comme destinations.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="dirs">Vecteurs unitaires des rayons à parcourir.</param>
    /// <param name="moves">Liste de candidats à compléter dans l’ordre de parcours.</param>
    /// <param name="filtre">Famille de destinations à produire.</param>
    private void AddSlides(PieceMobile piece, Vector2Int[] dirs, List<Vector2Int> moves, FiltrePseudoCoups filtre)
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
                    if (filtre != FiltrePseudoCoups.Tactiques)
                        moves.Add(new Vector2Int(cx, cz));
                }
                else
                {
                    if (filtre != FiltrePseudoCoups.Calmes &&
                        char.IsLower(cible) != piece.IsWhite && cible != 'r' && cible != 'R')
                        moves.Add(new Vector2Int(cx, cz));

                    break;
                }

                cx += d.x;
                cz += d.y;
            }
        }
    }

    /// <summary>
    /// Ajoute une destination de saut ou de roi si elle est accessible et respecte le mode tactique. La sûreté du roi est vérifiée plus tard.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="moves">Liste de candidats à compléter dans l’ordre de parcours.</param>
    /// <param name="filtre">Famille de destinations à produire.</param>
    private void AddIfFreeOrEnemy(PieceMobile piece, int x, int z, List<Vector2Int> moves, FiltrePseudoCoups filtre)
    {
        if (!Inside(x, z)) return;

        char cible = position.GetPiece(x, z);

        if (cible == '.' ? filtre != FiltrePseudoCoups.Tactiques :
            filtre != FiltrePseudoCoups.Calmes && char.IsLower(cible) != piece.IsWhite && cible != 'r' && cible != 'R')
        {
            moves.Add(new Vector2Int(x, z));
        }
    }

}

[RequireComponent(typeof(Echiquier))]
public class DeplacementPiece : MonoBehaviour
{
    [SerializeField] private Echiquier echiquier;
    /// <summary>
    /// Associe la façade de scène à l’échiquier dont elle doit lire et modifier la position logique.
    /// </summary>
    /// <param name="plateau">Échiquier de la scène qui détient la position.</param>
    public void Configurer(Echiquier plateau) { echiquier = plateau; }
    private ReglesEchecs Regles => new ReglesEchecs((echiquier != null ? echiquier : GetComponent<Echiquier>()).Position);
    /// <summary>
    /// Expose à la sélection visuelle le même générateur légal que celui utilisé par le moteur.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Destinations légales de la pièce sélectionnée.</returns>
    public List<Vector2Int> GetCasesDisponibles(int x, int z) => Regles.GetLegalMoves(x, z);
    public bool EstDeplacementLegal(int x, int z, int tx, int tz) => GetCasesDisponibles(x, z).Contains(new Vector2Int(tx, tz));
    /// <summary>
    /// Passe la demande de la scène à l’application légale d’un coup dans la position.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Vrai lorsque la position logique a été modifiée.</returns>
    public bool DeplacerPiece(int x, int z, int tx, int tz) => Regles.JouerCoup(x, z, tx, tz);
    /// <summary>
    /// Expose au contrôleur la même détection d’échec que celle employée pendant la recherche.
    /// </summary>
    /// <param name="blanc">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <returns>Vrai lorsque le camp est en échec.</returns>
    public bool RoiEnEchec(bool blanc) => Regles.IsInCheck(blanc);
    /// <summary>
    /// Interroge la présence d’au moins un coup légal pour décider si la partie continue.
    /// </summary>
    /// <param name="blanc">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <returns>Vrai si le camp peut jouer.</returns>
    public bool PossedeCoupLegal(bool blanc) => Regles.HasAnyLegalMove(blanc);
    public bool EstEchecEtMat(bool blanc) => RoiEnEchec(blanc) && !PossedeCoupLegal(blanc);
    public bool EstPat(bool blanc) => !RoiEnEchec(blanc) && !PossedeCoupLegal(blanc);
}
