using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class MoveOrderingChecks
{
    [MenuItem("Halloween Chess/Verifier le tri des coups")]
    public static void Run()
    {
        const string fen = "4k3/P7/8/3p4/3Q1q2/8/8/1N2K3 w - - 0 1";
        PositionEchecs p = PositionEchecs.DepuisFen(fen);
        Type rechercheType = typeof(Minimax).GetNestedType("Recherche", BindingFlags.NonPublic);
        object recherche = Activator.CreateInstance(rechercheType, BindingFlags.Instance | BindingFlags.Public,
            null, new object[] { CancellationToken.None, true, 128 }, null);
        MethodInfo generer = rechercheType.GetMethod("GenererCoups", BindingFlags.NonPublic | BindingFlags.Instance);
        var killers = (Minimax.Coup?[,])rechercheType.GetField("coupsKiller", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(recherche);
        var historique = (int[,,])rechercheType.GetField("historique", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(recherche);

        var promotion = Coup(0, 6, 0, 7);
        var bonneCapture = Coup(3, 3, 5, 3);
        var captureDefavorable = Coup(3, 3, 3, 4);
        var killer1 = Coup(1, 0, 2, 2);
        var killer2 = Coup(1, 0, 0, 2);
        var coupHistorique = Coup(3, 3, 2, 3);
        var autreCoup = Coup(1, 0, 3, 1);
        killers[0, 0] = killer1;
        killers[0, 1] = killer2;
        historique[0, 27, 26] = 500;

        List<Minimax.Coup> ordre = LireOrdre(generer, recherche, p, null);
        Avant(ordre, promotion, bonneCapture, "Promotion avant capture favorable");
        Avant(ordre, bonneCapture, killer1, "Capture favorable avant killer");
        Avant(ordre, killer1, killer2, "Premier killer avant second killer");
        Avant(ordre, killer2, coupHistorique, "Killer avant historique");
        Avant(ordre, coupHistorique, autreCoup, "Historique avant coup sans bonus");
        Avant(ordre, autreCoup, captureDefavorable, "Capture défavorable après coup calme");
        List<Minimax.Coup> avecTable = LireOrdre(generer, recherche, p, captureDefavorable);
        Check(avecTable[0].ToString() == captureDefavorable.ToString(), "Coup de table en premier, quelle que soit sa catégorie");
        Check(avecTable.Count == ordre.Count, "Aucun coup éliminé par le tri");
        var uniques = new HashSet<string>();
        foreach (Minimax.Coup coup in avecTable) Check(uniques.Add(coup.ToString()), "Aucun doublon");
        List<Minimax.Coup> repetes = LireOrdre(generer, recherche, p, null);
        for (int i = 0; i < ordre.Count; i++)
            Check(ordre[i].ToString() == repetes[i].ToString(), "Ordre déterministe après réutilisation du tampon");
        Check(p.ExporterFen() == fen, "Le tri ne modifie pas la position");

        // Comparer les scores à un Minimax exhaustif indépendant du tri et d'alpha-bêta.
        ComparerScore(PositionEchecs.FenInitial, 2);
        ComparerScore(fen, 2);
        ComparerScore("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10", 2);
        ComparerScore("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1", 2);
        Debug.Log("Tri des coups : tous les contrôles ont réussi.");
    }

    private static List<Minimax.Coup> LireOrdre(MethodInfo generer, object recherche,
        PositionEchecs p, Minimax.Coup? memorise)
    {
        var liste = (IEnumerable)generer.Invoke(recherche, new object[] { p, new ReglesEchecs(p), 0, memorise });
        var coups = new List<Minimax.Coup>();
        foreach (object candidat in liste)
            coups.Add((Minimax.Coup)candidat.GetType().GetField("Coup").GetValue(candidat));
        return coups;
    }

    private static Minimax.Coup Coup(int x, int z, int tx, int tz)
        => new Minimax.Coup(new Vector2Int(x, z), new Vector2Int(tx, tz));

    private static void Avant(List<Minimax.Coup> coups, Minimax.Coup a, Minimax.Coup b, string message)
    {
        int ia = coups.FindIndex(c => c.ToString() == a.ToString());
        int ib = coups.FindIndex(c => c.ToString() == b.ToString());
        Check(ia >= 0 && ib > ia, message);
    }

    private static void ComparerScore(string fen, int profondeur)
    {
        var p = PositionEchecs.DepuisFen(fen);
        int reference = Exhaustif(p, profondeur, 0);
        Check(Minimax.Chercher(p, profondeur).Score == reference, "Score identique avec table : " + fen);
        Check(Minimax.Chercher(p, profondeur, utiliserTable: false).Score == reference,
            "Score identique sans table : " + fen);
        Check(p.ExporterFen() == fen, "Position intacte après recherche");
    }

    private static int Exhaustif(PositionEchecs p, int profondeur, int distance)
    {
        var regles = new ReglesEchecs(p);
        var coups = new List<Minimax.Coup>();
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = p.GetPiece(x, z);
                if (piece == '.' || char.IsLower(piece) != p.TourBlanc) continue;
                foreach (Vector2Int arrivee in regles.GetLegalMoves(x, z)) coups.Add(Coup(x, z, arrivee.x, arrivee.y));
            }
        if (coups.Count == 0) return regles.IsInCheck(p.TourBlanc) ? -Evaluation.ScoreMat + distance : 0;
        if (profondeur == 0) return Evaluation.Evaluer(p, p.TourBlanc);
        int meilleur = int.MinValue;
        foreach (Minimax.Coup coup in coups)
        {
            var copie = p.Copier();
            Check(new ReglesEchecs(copie).JouerCoup(coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y),
                "Coup de référence légal");
            meilleur = Math.Max(meilleur, -Exhaustif(copie, profondeur - 1, distance + 1));
        }
        return meilleur;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Tri des coups : " + message);
    }
}
