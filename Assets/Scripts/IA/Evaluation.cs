using System;
using UnityEngine;

public class Evaluation : MonoBehaviour
{
    // Scores en centipions. Le mat et le pat sont traités dans Minimax.
    public const int ScoreMat = 1000000;

    public static int ValeurPiece(char piece)
    {
        switch (char.ToUpperInvariant(piece))
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
        int materielSansPions = 0;
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                char type = char.ToUpperInvariant(piece);
                if (type != 'R' && type != 'P') materielSansPions += ValeurPiece(piece);
            }

        bool finale = materielSansPions <= 2600;
        int score = 0, fousBlancs = 0, fousNoirs = 0;
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece == '.') continue;
                bool blanc = char.IsLower(piece);
                int rangee = blanc ? z : 7 - z;
                // 0 au bord, 3 sur les colonnes/rangées centrales.
                int centreX = Math.Min(x, 7 - x);
                int centreZ = Math.Min(z, 7 - z);
                int bonus = 0;
                switch (char.ToUpperInvariant(piece))
                {
                    case 'P':
                        bonus = Math.Max(0, rangee - 1) * 10 + centreX * 3;
                        break;
                    case 'C':
                        bonus = (centreX + centreZ) * 10;
                        break;
                    case 'F':
                        bonus = (centreX + centreZ) * 5;
                        if (blanc) fousBlancs++; else fousNoirs++;
                        break;
                    case 'T':
                        bonus = rangee == 6 ? 20 : 0;
                        break;
                    case 'D':
                        bonus = (centreX + centreZ) * 2;
                        break;
                    case 'R':
                        // Roi actif en finale, protégé en début/milieu de partie.
                        bonus = finale ? (centreX + centreZ) * 8 : -rangee * 12;
                        if (!finale && rangee == 0 && (x == 2 || x == 6)) bonus += 35;
                        break;
                }
                int valeur = ValeurPiece(piece) + bonus;
                score += blanc ? valeur : -valeur;
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
