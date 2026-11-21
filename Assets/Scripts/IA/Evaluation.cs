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

    private static bool EstPionPasse(int x, int z, bool blanc, ulong pionsAdverses)
        => (pionsAdverses & MasquesPionsPasses[blanc ? 0 : 1, z * 8 + x]) == 0;

    public static int ValeurPiece(char piece)
        => ValeurType(char.ToUpperInvariant(piece));

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

        internal ContributionCase(int phase, int score, int roiMilieu, int roiFinale)
        {
            Phase = phase;
            Score = score;
            RoiMilieu = roiMilieu;
            RoiFinale = roiFinale;
        }
    }

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

    public static int Evaluer(PositionEchecs position, bool pourLesBlancs)
    {
        int score = Evaluer(position);
        return pourLesBlancs ? score : -score;
    }
}
