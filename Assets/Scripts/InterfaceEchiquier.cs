
using System.Collections.Generic;
using UnityEngine;

public class InterfaceEchiquier : MonoBehaviour
{
    [SerializeField] private Echiquier echiquier;
    [SerializeField] private DeplacementPiece deplacement;

    private Vector2Int? selection = null;
    private List<Vector2Int> casesDisponibles = new();

    private bool tourBlanc = true;

    private const int tailleCase = 60;

    private bool partieTerminee = false;
    private string messagePartie = "Tour des blancs";
    void OnGUI()
    {
        float debutX = (Screen.width - 8 * tailleCase) / 2f;
        float debutY = (Screen.height - 8 * tailleCase) / 2f;

        GUI.Label(
            new Rect(debutX, debutY - 35, 480, 30),
            messagePartie
        );

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Rect position = new Rect(
                    debutX + y * tailleCase,
                    debutY + x * tailleCase,
                    tailleCase,
                    tailleCase
                );

                char piece = echiquier.GetPiece(x, y);

                Color couleur = (x + y) % 2 == 0
                    ? new Color(0.9f, 0.85f, 0.7f)
                    : new Color(0.5f, 0.35f, 0.25f);

                Vector2Int caseActuelle = new Vector2Int(x, y);

                // Case sélectionnée
                if (selection.HasValue &&
                    selection.Value == caseActuelle)
                {
                    couleur = Color.yellow;
                }
                // Déplacement disponible
                else if (casesDisponibles.Contains(caseActuelle))
                {
                    couleur = Color.green;
                }

                Color ancienneCouleur = GUI.backgroundColor;
                GUI.backgroundColor = couleur;

                string texte = piece == '.'
                    ? ""
                    : piece.ToString();

                if (GUI.Button(position, texte))
                {
                    CliquerCase(x, y);
                }

                GUI.backgroundColor = ancienneCouleur;
            }
        }
    }



    void CliquerCase(int x, int y)
    {
        if (partieTerminee)
            return;

        Vector2Int caseCliquee = new Vector2Int(x, y);
        char piece = echiquier.GetPiece(x, y);

        // Effectuer un déplacement sélectionné
        if (selection.HasValue &&
            casesDisponibles.Contains(caseCliquee))
        {
            Vector2Int depart = selection.Value;

            if (deplacement.DeplacerPiece(
                depart.x, depart.y, x, y))
            {
                tourBlanc = !tourBlanc;

                if (deplacement.EstEchecEtMat(tourBlanc))
                {
                    partieTerminee = true;

                    messagePartie = tourBlanc
                        ? "ECHEC ET MAT ! Les noirs gagnent"
                        : "ECHEC ET MAT ! Les blancs gagnent";
                }
                else if (deplacement.EstPat(tourBlanc))
                {
                    partieTerminee = true;
                    messagePartie = "PAT ! Match nul";
                }
                else if (deplacement.RoiEnEchec(tourBlanc))
                {
                    messagePartie = tourBlanc
                        ? "ECHEC ! Roi blanc attaque"
                        : "ECHEC ! Roi noir attaque";
                }
                else
                {
                    messagePartie = tourBlanc
                        ? "Tour des blancs"
                        : "Tour des noirs";
                }

                Debug.Log(messagePartie);
            }

            selection = null;
            casesDisponibles.Clear();
            return;
        }

        // Sélectionner uniquement ses propres pièces
        if (piece != '.' &&
            char.IsLower(piece) == tourBlanc)
        {
            selection = caseCliquee;

            casesDisponibles =
                deplacement.GetCasesDisponibles(x, y);
        }
        else
        {
            selection = null;
            casesDisponibles.Clear();
        }
    }
}
