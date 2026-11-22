using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EvaluationChecks
{
    /// <summary>
    /// Exécute les cas de pions passés, les cas tactiques et les contrôles de score associés.
    /// </summary>
    [MenuItem("Halloween Chess/Verifier les pions passes")]
    public static void Run()
    {
        MethodInfo estPasse = typeof(Evaluation).GetMethod("EstPionPasse", BindingFlags.Static | BindingFlags.NonPublic);
        int cas = 0;
        // Oracle géométrique indépendant : un pion adverse sur chacune des 64 cases.
        // Les rangées extrêmes vérifient aussi les décalages de bits aux limites.
        foreach (bool blanc in new[] { true, false })
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    Check((bool)estPasse.Invoke(null, new object[] { x, z, blanc, 0UL }), "Sans pion adverse : passé");
                    for (int ax = 0; ax < 8; ax++)
                        for (int az = 0; az < 8; az++)
                        {
                            bool devant = blanc ? az > z : az < z;
                            bool attendu = !(devant && Math.Abs(ax - x) <= 1);
                            ulong adversaire = 1UL << (az * 8 + ax);
                            bool obtenu = (bool)estPasse.Invoke(null, new object[] { x, z, blanc, adversaire });
                            Check(obtenu == attendu, $"Pion passé ({x},{z}), blanc={blanc}, adversaire=({ax},{az})");
                            cas++;
                        }
                }

        VerifierScore("4k3/8/8/8/8/8/P7/4K3 w - - 0 1", 130);
        VerifierScore("4k3/p7/8/8/8/8/8/4K3 b - - 0 1", -130);
        VerifierScore("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", 180); // Même bonus, avancée existante conservée.
        VerifierScore("4k3/8/8/8/8/8/7P/4K3 w - - 0 1", 130); // Colonne h sans débordement.
        VerifierScore("4k3/p7/8/8/P7/8/8/4K3 w - - 0 1", 20); // Deux pions sur la même colonne : aucun passé.
        VerifierScore("4k3/r7/8/8/8/8/P7/4K3 w - - 0 1", -370); // Une tour devant ne retire pas le bonus.
        VerifierScore(PositionEchecs.FenInitial, 0);

        // Les rois asymétriques distinguent les bonus de finale et de milieu de jeu.
        VerifierScore("8/4k3/4bbnn/8/8/8/NNBN4/2K5 w - - 0 1", -66); // Matériel : 2590.
        VerifierScore("8/4k3/4bbnn/8/8/8/NNBB4/2K5 w - - 0 1", -46); // Matériel : 2600, encore en finale.
        VerifierScore("8/4k3/4bbnn/8/8/8/BNBB4/2K5 w - - 0 1", 22);  // Matériel : 2610.

        VerifierCoup("Promotion blanche", "7k/P7/6K1/8/8/8/8/8 w - - 0 1", "a7a8", 4);
        VerifierCoup("Promotion noire", "8/8/8/8/8/6k1/p7/7K b - - 0 1", "a2a1", 4);
        VerifierCoup("Création d'un passé blanc", "7k/8/6K1/3p4/4P3/8/8/8 w - - 0 1", "e4d5", 4);
        VerifierCoup("Création d'un passé noir", "8/8/8/4p3/3P4/6k1/8/7K b - - 0 1", "e5d4", 4);
        VerifierCoup("Capture d'un passé noir", "7k/8/8/3p4/4K3/8/8/8 w - - 0 1", "e4d5", 4);
        VerifierCoup("Capture d'un passé blanc", "8/8/8/4k3/3P4/8/8/7K b - - 0 1", "e5d4", 4);
        // Deux captures gagnent le même pion. exd5 crée deux passés ; cxd5 n'en crée qu'un.
        foreach (int profondeur in new[] { 1, 2, 4 })
        {
            VerifierCoup("Deux passés blancs", "4k3/8/5p2/3p4/2P1P3/6P1/8/4K3 w - - 0 1", "e4d5", profondeur);
            VerifierCoup("Deux passés noirs", "4k3/8/6p1/2p1p3/3P4/5P2/8/4K3 b - - 0 1", "e5d4", profondeur);
        }
        Debug.Log($"Pions passés : {cas} cas géométriques, scores/symétrie et 8 finales avec/sans table réussis.");
    }

    /// <summary>
    /// Compare le recalcul de la position importée au score attendu et contrôle son FEN.
    /// </summary>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <param name="attendu">Résultat de référence du scénario.</param>
    private static void VerifierScore(string fen, int attendu)
    {
        var p = PositionEchecs.DepuisFen(fen);
        int obtenu = Evaluation.Evaluer(p);
        Check(obtenu == attendu, $"Score {fen} : attendu={attendu}, obtenu={obtenu}");
        Check(Evaluation.Evaluer(p, false) == -obtenu, "Signe du score du point de vue noir");
        var miroir = new PositionEchecs(false);
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = p.GetPiece(x, z);
                if (piece != '.') miroir.SetPiece(x, 7 - z, char.IsLower(piece) ? char.ToUpperInvariant(piece) : char.ToLowerInvariant(piece));
            }
        Check(Evaluation.Evaluer(miroir) == -obtenu, "Symétrie couleurs/rangées");
        Check(p.ExporterFen() == fen, "L'évaluation conserve le FEN complet");
    }

    /// <summary>
    /// Vérifie à profondeur fixe que le moteur atteint le coup attendu sur la position de test.
    /// </summary>
    /// <param name="nom">Libellé qui identifie le cas dans le rapport ou une assertion.</param>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <param name="attendu">Résultat de référence du scénario.</param>
    /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
    private static void VerifierCoup(string nom, string fen, string attendu, int profondeur)
    {
        var p = PositionEchecs.DepuisFen(fen);
        int? score = null;
        foreach (bool table in new[] { false, true })
        {
            var r = Minimax.Chercher(p, profondeur, utiliserTable: table);
            Check(r.MeilleurCoup?.ToString() == attendu, $"{nom} : attendu={attendu}, obtenu={r.MeilleurCoup}");
            Minimax.Coup coup = r.MeilleurCoup.Value;
            Check(new ReglesEchecs(p).GetLegalMoves(coup.Depart.x, coup.Depart.y).Contains(coup.Arrivee), "Coup annoncé légal");
            Check(!score.HasValue || r.Score == score.Value, "Même score avec/sans table");
            Check(r.NoeudsRecherchePrincipale + r.NoeudsQuietude == r.NoeudsVisites, "Somme des compteurs");
            Check(p.ExporterFen() == fen, "La recherche conserve le FEN complet");
            score = r.Score;
        }
        Debug.Log($"[Finale pions passés] {nom} | profondeur={profondeur} | coup={attendu} | score={score}");
    }

    /// <summary>
    /// Signale précisément un écart de score ou de comportement dans la suite d’évaluation.
    /// </summary>
    /// <param name="condition">Invariant attendu par le cas de test.</param>
    /// <param name="message">Contexte de diagnostic en cas d’échec de l’assertion.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Pions passés : " + message);
    }
}
