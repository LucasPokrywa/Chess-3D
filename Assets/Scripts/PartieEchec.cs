
using System.Collections.Generic;
using UnityEngine;

public class PartieEchec : MonoBehaviour
{
    [SerializeField] private Echiquier echiquier;
    [SerializeField] private DeplacementPiece deplacement;

    private bool tourBlanc = true;

    void Start()
    {
        // L'échiquier est initialisé dans Echiquier.Start()
        Debug.Log("=== DÉBUT DE LA PARTIE ===");
        Debug.Log("Tour des blancs");

        echiquier.AfficherPlateau();
    }

    // Sélectionner une pièce
    public void SelectionnerPiece(int x, int y)
    {
        char piece = echiquier.GetPiece(x, y);

        if (piece == '.')
        {
            Debug.Log("Aucune pièce sur cette case");
            return;
        }

        if (char.IsLower(piece) != tourBlanc)
        {
            Debug.Log("Ce n'est pas votre tour !");
            return;
        }

        List<Vector2Int> cases =
            deplacement.GetCasesDisponibles(x, y);

        Debug.Log("Pièce sélectionnée : " + piece);

        foreach (Vector2Int position in cases)
        {
            Debug.Log(
                "Déplacement possible : " +
                position.x + ", " + position.y
            );
        }
    }

    // Jouer un coup
    public void JouerCoup(int x1, int y1, int x2, int y2)
    {
        if (!echiquier.EstDansPlateau(x1, y1))
            return;

        char piece = echiquier.GetPiece(x1, y1);

        if (piece == '.')
        {
            Debug.Log("Aucune pièce sélectionnée");
            return;
        }

        if (char.IsLower(piece) != tourBlanc)
        {
            Debug.Log("Ce n'est pas votre tour");
            return;
        }

        if (!deplacement.DeplacerPiece(x1, y1, x2, y2))
        {
            Debug.Log("Coup invalide !");
            return;
        }

        echiquier.AfficherPlateau();

        tourBlanc = !tourBlanc;

        Debug.Log(tourBlanc
            ? "Tour des blancs"
            : "Tour des noirs");
    }
}
