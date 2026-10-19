
using UnityEngine;

public class Echiquier : MonoBehaviour
{
    private char[,] plateau = new char[8, 8];

    void Awake()
    {
        InitialiserPlateau();
    }   

    // Initialiser les pièces
    public void InitialiserPlateau()
    {
        plateau = new char[,]
        {
            { 'T', 'C', 'F', 'D', 'R', 'F', 'C', 'T' },
            { 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P' },
            { '.', '.', '.', '.', '.', '.', '.', '.' },
            { '.', '.', '.', '.', '.', '.', '.', '.' },
            { '.', '.', '.', '.', '.', '.', '.', '.' },
            { '.', '.', '.', '.', '.', '.', '.', '.' },
            { 'p', 'p', 'p', 'p', 'p', 'p', 'p', 'p' },
            { 't', 'c', 'f', 'd', 'r', 'f', 'c', 't' }
        };
    }

    // Vérifier si une position est valide
    public bool EstDansPlateau(int x, int y)
    {
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }

    // Récupérer une pièce
    public char GetPiece(int x, int y)
    {
        if (!EstDansPlateau(x, y))
        {
            Debug.LogWarning("Position invalide");
            return '\0';
        }

        return plateau[x, y];
    }

    // Modifier une case
    public void SetPiece(int x, int y, char piece)
    {
        if (!EstDansPlateau(x, y))
            return;

        plateau[x, y] = piece;
    }

    // Vérifier si une case est vide
    public bool EstCaseVide(int x, int y)
    {
        return EstDansPlateau(x, y) && plateau[x, y] == '.';
    }

    // Déplacer une pièce
    public bool DeplacerPiece(int x1, int y1, int x2, int y2)
    {
        if (!EstDansPlateau(x1, y1) || !EstDansPlateau(x2, y2))
            return false;

        if (EstCaseVide(x1, y1))
            return false;

        if (x1 == x2 && y1 == y2)
            return false;

        char piece = plateau[x1, y1];

        plateau[x2, y2] = piece;
        plateau[x1, y1] = '.';

        return true;
    }

    // Afficher le plateau dans la console Unity
    public void AfficherPlateau()
    {
        string affichage = "";

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                affichage += plateau[x, y] + " ";
            }

            affichage += "\n";
        }

        Debug.Log(affichage);
    }
}
