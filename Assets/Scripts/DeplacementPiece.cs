
using System;
using System.Collections.Generic;
using UnityEngine;

public class DeplacementPiece : MonoBehaviour
{
    [SerializeField] private Echiquier echiquier;

    private bool DansPlateau(int x, int y)
    {
        return echiquier.EstDansPlateau(x, y);
    }

    private bool EstBlanc(char piece)
    {
        return char.IsLower(piece);
    }

    // Retourne les cases légales d'une pièce
    public List<Vector2Int> GetCasesDisponibles(int x, int y)
    {
        List<Vector2Int> cases = new List<Vector2Int>();

        if (!DansPlateau(x, y))
            return cases;

        if (echiquier.GetPiece(x, y) == '.')
            return cases;

        for (int nx = 0; nx < 8; nx++)
        {
            for (int ny = 0; ny < 8; ny++)
            {
                if (EstDeplacementLegal(x, y, nx, ny))
                    cases.Add(new Vector2Int(nx, ny));
            }
        }

        return cases;
    }

    // Vérifie les règles et la sécurité du roi
    public bool EstDeplacementLegal(
        int x1, int y1, int x2, int y2)
    {
        if (!EstDeplacementPossible(x1, y1, x2, y2))
            return false;

        char piece = echiquier.GetPiece(x1, y1);
        char capture = echiquier.GetPiece(x2, y2);

        // Simuler le déplacement
        echiquier.SetPiece(x2, y2, piece);
        echiquier.SetPiece(x1, y1, '.');

        bool roiEnEchec = RoiEnEchec(EstBlanc(piece));

        // Restaurer le plateau
        echiquier.SetPiece(x1, y1, piece);
        echiquier.SetPiece(x2, y2, capture);

        return !roiEnEchec;
    }

    // Règles individuelles des pièces
    private bool EstDeplacementPossible(
        int x1, int y1, int x2, int y2)
    {
        if (!DansPlateau(x1, y1) || !DansPlateau(x2, y2))
            return false;

        if (x1 == x2 && y1 == y2)
            return false;

        char piece = echiquier.GetPiece(x1, y1);
        char cible = echiquier.GetPiece(x2, y2);

        if (piece == '.')
            return false;

        // Impossible de capturer un allié
        if (cible != '.' &&
            EstBlanc(piece) == EstBlanc(cible))
            return false;

        // On ne capture jamais directement un roi
        if (char.ToUpperInvariant(cible) == 'R')
            return false;

        int dx = x2 - x1;
        int dy = y2 - y1;

        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);

        switch (char.ToUpperInvariant(piece))
        {
            case 'P':
                {
                    int direction = EstBlanc(piece) ? -1 : 1;
                    int depart = EstBlanc(piece) ? 6 : 1;

                    if (dy == 0 && cible == '.')
                    {
                        if (dx == direction)
                            return true;

                        if (x1 == depart &&
                            dx == 2 * direction &&
                            echiquier.EstCaseVide(
                                x1 + direction, y1))
                            return true;
                    }

                    return dx == direction &&
                           absY == 1 &&
                           cible != '.';
                }

            case 'C':
                return (absX == 2 && absY == 1) ||
                       (absX == 1 && absY == 2);

            case 'T':
                return (dx == 0 || dy == 0) &&
                       CheminLibre(x1, y1, x2, y2);

            case 'F':
                return absX == absY &&
                       CheminLibre(x1, y1, x2, y2);

            case 'D':
                // Dame = Tour + Fou
                bool horizontalOuVertical =
                    dx == 0 || dy == 0;

                bool diagonal = absX == absY;

                return (horizontalOuVertical || diagonal) &&
                       CheminLibre(x1, y1, x2, y2);

            case 'R':
                return absX <= 1 && absY <= 1;

            default:
                return false;
        }
    }

    // Vérifie toutes les cases entre départ et arrivée
    private bool CheminLibre(
        int x1, int y1, int x2, int y2)
    {
        int dx = Math.Sign(x2 - x1);
        int dy = Math.Sign(y2 - y1);

        int x = x1 + dx;
        int y = y1 + dy;

        while (x != x2 || y != y2)
        {
            if (!echiquier.EstCaseVide(x, y))
                return false;

            x += dx;
            y += dy;
        }

        return true;
    }

    // Vérifie si une pièce attaque une case
    private bool AttaqueCase(
        int x1, int y1, int x2, int y2)
    {
        char piece = echiquier.GetPiece(x1, y1);

        if (piece == '.')
            return false;

        int dx = x2 - x1;
        int dy = y2 - y1;

        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);

        if (dx == 0 && dy == 0)
            return false;

        switch (char.ToUpperInvariant(piece))
        {
            case 'P':
                int direction = EstBlanc(piece) ? -1 : 1;
                return dx == direction && absY == 1;

            case 'C':
                return (absX == 2 && absY == 1) ||
                       (absX == 1 && absY == 2);

            case 'T':
                return (dx == 0 || dy == 0) &&
                       CheminLibre(x1, y1, x2, y2);

            case 'F':
                return absX == absY &&
                       CheminLibre(x1, y1, x2, y2);

            case 'D':
                return (dx == 0 || dy == 0 ||
                        absX == absY) &&
                       CheminLibre(x1, y1, x2, y2);

            case 'R':
                return absX <= 1 && absY <= 1;
        }

        return false;
    }

    // Le roi de cette couleur est-il en échec ?
    public bool RoiEnEchec(bool blanc)
    {
        char roi = blanc ? 'r' : 'R';

        int roiX = -1;
        int roiY = -1;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (echiquier.GetPiece(x, y) == roi)
                {
                    roiX = x;
                    roiY = y;
                    break;
                }
            }

            if (roiX != -1)
                break;
        }

        // Position invalide : roi absent
        if (roiX == -1)
            return true;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                char piece = echiquier.GetPiece(x, y);

                if (piece == '.')
                    continue;

                if (EstBlanc(piece) == blanc)
                    continue;

                if (AttaqueCase(x, y, roiX, roiY))
                    return true;
            }
        }

        return false;
    }

    // Existe-t-il au moins un coup légal ?
    public bool PossedeCoupLegal(bool blanc)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                char piece = echiquier.GetPiece(x, y);

                if (piece == '.')
                    continue;

                if (EstBlanc(piece) != blanc)
                    continue;

                for (int nx = 0; nx < 8; nx++)
                {
                    for (int ny = 0; ny < 8; ny++)
                    {
                        if (EstDeplacementLegal(
                            x, y, nx, ny))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    public bool EstEchecEtMat(bool blanc)
    {
        return RoiEnEchec(blanc) &&
               !PossedeCoupLegal(blanc);
    }

    public bool EstPat(bool blanc)
    {
        return !RoiEnEchec(blanc) &&
               !PossedeCoupLegal(blanc);
    }

    // Effectuer un déplacement validé
    public bool DeplacerPiece(
        int x1, int y1, int x2, int y2)
    {
        if (!EstDeplacementLegal(x1, y1, x2, y2))
            return false;

        return echiquier.DeplacerPiece(x1, y1, x2, y2);
    }
}
