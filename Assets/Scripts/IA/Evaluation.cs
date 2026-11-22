using System;
using UnityEngine;

public class Evaluation : MonoBehaviour
{
    // Scores en centipions. Le mat et le pat sont traités dans Minimax.
    public const int ScoreMat = 1000000;
    internal const int SeuilFinale = 2600;
    // Bonus constant : l'avancée du pion est déjà récompensée séparément.
    private const int BonusPionPasse = 30;
    private static readonly ulong[,] MasquesPionsPasses = CreerMasquesPionsPasses();

    /// <summary>
    /// Précalcule, pour chaque couleur et case, la colonne du pion et les colonnes adjacentes devant lui. Les cases de même rang sont exclues.
    /// </summary>
    /// <returns>Table indexée par couleur puis par rangée × 8 + colonne.</returns>
    private static ulong[,] CreerMasquesPionsPasses()
    {
        var masques = new ulong[2, 64];
        for (int couleur = 0; couleur < 2; couleur++)
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    int direction = couleur == 0 ? 1 : -1;
                    ulong masque = 0;
                    for (int devant = z + direction; devant >= 0 && devant < 8; devant += direction)
                        for (int colonne = Math.Max(0, x - 1); colonne <= Math.Min(7, x + 1); colonne++)
                            masque |= 1UL << (devant * 8 + colonne);
                    masques[couleur, z * 8 + x] = masque;
                }
        return masques;
    }

    /// <summary>
    /// Compare le masque des cases à surveiller avec les pions adverses. Le test dépend de la géométrie des pions et ignore la légalité d’une prise en passant.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="blanc">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <param name="pionsAdverses">Masque des pions du camp opposé.</param>
    /// <returns>Vrai si aucun pion adverse n’est présent dans le masque.</returns>
    private static bool EstPionPasse(int x, int z, bool blanc, ulong pionsAdverses)
        => (pionsAdverses & MasquesPionsPasses[blanc ? 0 : 1, z * 8 + x]) == 0;

    /// <summary>
    /// Normalise la casse du code français utilisé par le plateau avant de lire la valeur commune aux deux couleurs.
    /// </summary>
    /// <param name="piece">Pièce ou code de pièce à traiter.</param>
    /// <returns>Valeur matérielle positive en centipions ou zéro pour un code inconnu.</returns>
    public static int ValeurPiece(char piece)
        => ValeurType(char.ToUpperInvariant(piece));

    /// <summary>
    /// Fixe l’échelle matérielle utilisée par l’évaluation et les priorités de capture.
    /// </summary>
    /// <param name="type">Type de pièce normalisé ou catégorie de l’entrée étudiée.</param>
    /// <returns>Valeur en centipions sans signe de couleur.</returns>
    private static int ValeurType(char type)
    {
        switch (type)
        {
            case 'P': return 100;
            case 'C': return 320;
            case 'F': return 330;
            case 'T': return 500;
            case 'D': return 900;
            case 'R': return 20000;
            default: return 0;
        }
    }

    // Positif = avantage blanc, négatif = avantage noir.
    /// <summary>
    /// Reconstruit le matériel, les bonus de case et la phase en un parcours, puis ajoute les termes de structure globaux. Sert aussi de référence à l’évaluation incrémentale.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <returns>Score statique en centipions du point de vue des Blancs.</returns>
    public static int Evaluer(PositionEchecs position)
    {
        if (position == null) throw new ArgumentNullException(nameof(position));
        int materielSansPions = 0, roisMilieu = 0, roisFinale = 0;
        int score = 0, fousBlancs = 0, fousNoirs = 0;
        ulong pionsBlancs = 0, pionsNoirs = 0;
        // Une entrée par pion, sans allocation ni seconde lecture du plateau.
        Span<int> casesPions = stackalloc int[64];
        int nombrePions = 0;
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece == '.') continue;
                char type = char.ToUpperInvariant(piece);
                bool blanc = char.IsLower(piece);
                ContributionCase contribution = CalculerContribution(type, ValeurType(type), blanc, x, z);
                materielSansPions += contribution.Phase;
                roisMilieu += contribution.RoiMilieu;
                roisFinale += contribution.RoiFinale;
                score += contribution.Score;
                if (type == 'P')
                {
                    int index = z * 8 + x;
                    casesPions[nombrePions++] = index;
                    if (blanc) pionsBlancs |= 1UL << index;
                    else pionsNoirs |= 1UL << index;
                }
                else if (type == 'F')
                {
                    if (blanc) fousBlancs++;
                    else fousNoirs++;
                }
            }
        score += materielSansPions <= SeuilFinale ? roisFinale : roisMilieu;
        return score + BonusPionsEtFous(casesPions, nombrePions, pionsBlancs, pionsNoirs, fousBlancs, fousNoirs);
    }

    internal readonly struct ContributionCase
    {
        internal readonly int Phase, Score, RoiMilieu, RoiFinale;

        /// <summary>
        /// Range les quatre composantes qui peuvent être additionnées ou retirées indépendamment lors d’un déplacement.
        /// </summary>
        /// <param name="phase">Matériel hors pions et rois utilisé pour déterminer la phase.</param>
        /// <param name="score">Matériel et bonus hors roi, signés pour les Blancs.</param>
        /// <param name="roiMilieu">Bonus de case de roi en milieu de jeu, signé pour les Blancs.</param>
        /// <param name="roiFinale">Bonus de case de roi en finale, signé pour les Blancs.</param>
        internal ContributionCase(int phase, int score, int roiMilieu, int roiFinale)
        {
            Phase = phase;
            Score = score;
            RoiMilieu = roiMilieu;
            RoiFinale = roiFinale;
        }
    }

    /// <summary>
    /// Calcule la contribution d’une pièce selon sa case. La phase additionne le matériel hors pions et rois sans signe. Les trois termes de score sont signés pour les Blancs.
    /// </summary>
    /// <param name="type">Type de pièce normalisé ou catégorie de l’entrée étudiée.</param>
    /// <param name="valeur">Valeur matérielle non signée en centipions.</param>
    /// <param name="blanc">Vrai pour les Blancs, faux pour les Noirs.</param>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <returns>Matériel et bonus hors roi, deux bonus de roi et matériel de phase.</returns>
    internal static ContributionCase CalculerContribution(char type, int valeur, bool blanc, int x, int z)
    {
        int signe = blanc ? 1 : -1;
        int rangee = blanc ? z : 7 - z;
        int centreX = Math.Min(x, 7 - x);
        int centre = centreX + Math.Min(z, 7 - z);
        int bonus = 0, milieu = 0, finale = 0;
        switch (type)
        {
            case 'P': bonus = Math.Max(0, rangee - 1) * 10 + centreX * 3; break;
            case 'C': bonus = centre * 10; break;
            case 'F': bonus = centre * 5; break;
            case 'T': bonus = rangee == 6 ? 20 : 0; break;
            case 'D': bonus = centre * 2; break;
            case 'R':
                milieu = -rangee * 12;
                if (rangee == 0 && (x == 2 || x == 6)) milieu += 35;
                finale = centre * 8;
                break;
        }
        int phase = type != 'P' && type != 'R' ? valeur : 0;
        return new ContributionCase(phase, signe * (valeur + bonus), signe * milieu, signe * finale);
    }

    /// <summary>
    /// Reconstruit les masques des pions et le nombre de fous à partir du plateau courant. Le statut d’un pion peut changer lorsqu’un autre pion bouge.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <returns>Somme signée des bonus de pions passés et des paires de fous.</returns>
    internal static int EvaluerPionsEtFous(PositionEchecs position)
    {
        ulong pionsBlancs = 0, pionsNoirs = 0;
        int fousBlancs = 0, fousNoirs = 0;
        Span<int> casesPions = stackalloc int[64];
        int nombrePions = 0;
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece == 'p' || piece == 'P')
                {
                    int index = z * 8 + x;
                    casesPions[nombrePions++] = index;
                    if (piece == 'p') pionsBlancs |= 1UL << index;
                    else pionsNoirs |= 1UL << index;
                }
                else if (piece == 'f') fousBlancs++;
                else if (piece == 'F') fousNoirs++;
            }

        return BonusPionsEtFous(casesPions, nombrePions, pionsBlancs, pionsNoirs, fousBlancs, fousNoirs);
    }

    /// <summary>
    /// Combine les masques des deux camps et les effectifs de fous. Chaque structure reçoit son bonus une seule fois selon sa couleur.
    /// </summary>
    /// <param name="casesPions">Tampon des indices de case des pions des deux camps.</param>
    /// <param name="nombrePions">Nombre d’indices initialisés dans le tampon.</param>
    /// <param name="pionsBlancs">Masque des cases occupées par les pions blancs.</param>
    /// <param name="pionsNoirs">Masque des cases occupées par les pions noirs.</param>
    /// <param name="fousBlancs">Nombre de fous blancs sur le plateau.</param>
    /// <param name="fousNoirs">Nombre de fous noirs sur le plateau.</param>
    /// <returns>Score des structures, positif pour les Blancs et négatif pour les Noirs.</returns>
    private static int BonusPionsEtFous(ReadOnlySpan<int> casesPions, int nombrePions,
        ulong pionsBlancs, ulong pionsNoirs, int fousBlancs, int fousNoirs)
    {
        int score = 0;
        // Le statut de pion passé dépend des pions des deux couleurs.
        for (int i = 0; i < nombrePions; i++)
        {
            int index = casesPions[i];
            bool blanc = (pionsBlancs & (1UL << index)) != 0;
            if (EstPionPasse(index & 7, index >> 3, blanc, blanc ? pionsNoirs : pionsBlancs))
                score += blanc ? BonusPionPasse : -BonusPionPasse;
        }
        if (fousBlancs >= 2) score += 30;
        if (fousNoirs >= 2) score -= 30;
        return score;
    }

    /// <summary>
    /// Oriente le score du recalcul complet selon le camp demandé, indépendamment du trait de la position.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="pourLesBlancs">Vrai pour orienter le score du côté blanc, faux pour le côté noir.</param>
    /// <returns>Score statique du camp demandé, en centipions.</returns>
    public static int Evaluer(PositionEchecs position, bool pourLesBlancs)
    {
        int score = Evaluer(position);
        return pourLesBlancs ? score : -score;
    }
}
