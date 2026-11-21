using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class EvaluationIncrementaleChecks
{
    private const BindingFlags Interne = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type TypeEvaluation = typeof(Evaluation).Assembly.GetType("EvaluationIncrementale", true);
    private static readonly MethodInfo Initialiser = TypeEvaluation.GetMethod("DepuisPosition", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo Appliquer = TypeEvaluation.GetMethod("Appliquer", Interne);
    private static readonly MethodInfo Evaluer = TypeEvaluation.GetMethod("Evaluer", Interne);
    private static readonly MethodInfo Cloner = typeof(object).GetMethod("MemberwiseClone", Interne);
    private static readonly PropertyInfo Phase = TypeEvaluation.GetProperty("MaterielPhase", Interne);
    private static readonly PropertyInfo Finale = TypeEvaluation.GetProperty("EstFinale", Interne);
    private static readonly MethodInfo Jouer = typeof(PositionEchecs).GetMethod("JouerTemporairement", Interne);
    private static readonly MethodInfo Annuler = typeof(PositionEchecs).GetMethod("AnnulerCoup", Interne);

    [MenuItem("Halloween Chess/Verifier l'evaluation incrementale")]
    public static void Run()
    {
        const string najdorf = "r3kb1r/1bqn1pp1/p3p2p/8/NpBN2PP/4QP2/PPP5/2KR3R w kq - 1 16";
        const string dragon = "2rq1rk1/p2bppbp/3p1np1/1p6/3NP1P1/1PN1BP2/P1PQ3P/1K1R3R w - - 1 16";
        var cas = new (string Fen, string Coup, int PhaseAvant, int PhaseApres)[]
        {
            ("6k1/8/8/8/8/1NN5/r7/RNN1K1NR w - - 0 1", "a1a2", 3100, 2600),
            ("6k1/P7/5q2/8/8/8/8/3QK3 w - - 0 1", "a7a8", 1800, 2700),
            ("1r2k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7b8", 500, 900),
            ("4K3/8/8/8/8/8/p7/1R2k3 b - - 0 1", "a2b1", 500, 900),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 22", "e1g1", 2000, 2000),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 22", "e1c1", 2000, 2000),
            ("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 12 22", "e8g8", 2000, 2000),
            ("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 12 22", "e8c8", 2000, 2000),
            ("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10", "e5d6", 0, 0),
            ("4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 10", "e4d3", 0, 0)
        };

        int coups = 0;
        var positions = new System.Collections.Generic.HashSet<string> { najdorf, dragon, PositionEchecs.FenInitial };
        foreach (var c in cas)
        {
            var position = PositionEchecs.DepuisFen(c.Fen);
            var termes = Initialiser.Invoke(null, new object[] { position });
            VerifierScore(position, termes);
            Check((int)Phase.GetValue(termes) == c.PhaseAvant, "phase après import du FEN");
            int x = c.Coup[0] - 'a', z = c.Coup[1] - '1';
            var arrivee = new Vector2Int(c.Coup[2] - 'a', c.Coup[3] - '1');
            Check(new ReglesEchecs(position).GetLegalMoves(x, z).Contains(arrivee), "coup ciblé légal " + c.Coup);
            VerifierCoup(position, termes, x, z, arrivee, profondeur: 0, phaseAttendue: c.PhaseApres);
            coups++;
            positions.Add(c.Fen);
        }

        foreach (string fen in positions)
        {
            var position = PositionEchecs.DepuisFen(fen);
            var termes = Initialiser.Invoke(null, new object[] { position });
            VerifierScore(position, termes);
            coups += VerifierArbre(position, termes, 2);
        }

        var initiale = new PositionEchecs();
        var termesInitiaux = Initialiser.Invoke(null, new object[] { initiale });
        coups += VerifierLigne(initiale, termesInitiaux,
            new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5c6", "d7c6", "e1g1" }, 0);

        VerifierInterruption(najdorf);
        VerifierInterruption(dragon);
        Debug.Log($"Évaluation incrémentale : {coups} coups/annulations vérifiés, scores, phases et FEN restaurés.");
    }

    private static int VerifierArbre(PositionEchecs position, object termes, int profondeur)
    {
        var regles = new ReglesEchecs(position);
        int coups = 0;
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece == '.' || char.IsLower(piece) != position.TourBlanc) continue;
                foreach (var arrivee in regles.GetLegalMoves(x, z))
                    coups += VerifierCoup(position, termes, x, z, arrivee, profondeur - 1);
            }
        return coups;
    }

    private static int VerifierLigne(PositionEchecs position, object termes, string[] ligne, int index)
    {
        if (index == ligne.Length) return 0;
        string coup = ligne[index];
        int x = coup[0] - 'a', z = coup[1] - '1';
        var arrivee = new Vector2Int(coup[2] - 'a', coup[3] - '1');
        Check(new ReglesEchecs(position).GetLegalMoves(x, z).Contains(arrivee), "coup de la ligne légal " + coup);
        return VerifierCoup(position, termes, x, z, arrivee, profondeur: 0,
            suite: enfant => VerifierLigne(position, enfant, ligne, index + 1));
    }

    private static int VerifierCoup(PositionEchecs position, object termes, int x, int z, Vector2Int arrivee,
        int profondeur, int? phaseAttendue = null, Func<object, int> suite = null)
    {
        string fen = position.ExporterFen();
        object enfant = Cloner.Invoke(termes, null);
        object etat = Jouer.Invoke(position, new object[] { x, z, arrivee.x, arrivee.y });
        int coups = 1;
        try
        {
            Appliquer.Invoke(enfant, new[] { position, etat });
            VerifierScore(position, enfant);
            if (phaseAttendue.HasValue)
                Check((int)Phase.GetValue(enfant) == phaseAttendue.Value, "phase après le coup ciblé");
            if (profondeur > 0)
                coups += VerifierArbre(position, enfant, profondeur);
            if (suite != null)
                coups += suite(enfant);
        }
        finally
        {
            Annuler.Invoke(position, new[] { etat });
        }
        Check(position.ExporterFen() == fen, "FEN restauré après annulation");
        VerifierScore(position, termes);
        return coups;
    }

    private static void VerifierScore(PositionEchecs position, object termes)
    {
        Check(termes.Equals(Initialiser.Invoke(null, new object[] { position })), "sommes identiques au recalcul depuis le plateau");
        foreach (bool blancs in new[] { true, false })
            Check((int)Evaluer.Invoke(termes, new object[] { position, blancs }) == Evaluation.Evaluer(position, blancs),
                "score égal à l'évaluation complète");
        Check((bool)Finale.GetValue(termes) == ((int)Phase.GetValue(termes) <= 2600), "seuil de finale inclusif");
    }

    private static void VerifierInterruption(string fen)
    {
        var position = PositionEchecs.DepuisFen(fen);
        object attendu = Initialiser.Invoke(null, new object[] { position });
        Type rechercheType = typeof(Minimax).GetNestedType("Recherche", BindingFlags.NonPublic);
        MethodInfo executer = rechercheType.GetMethod("ExecuterAvecBudget", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo evaluation = rechercheType.GetField("evaluationIncrementale", Interne);
        Minimax.Chercher(position, profondeur: 2);

        foreach (int budget in new[] { 50, 100, 200 })
        {
            object recherche = Activator.CreateInstance(rechercheType, CancellationToken.None, true, 500000, Minimax.PvsParDefaut);
            var resultat = (Minimax.Resultat)executer.Invoke(recherche, new object[] { position, Minimax.ProfondeurMax, budget });
            Check(position.ExporterFen() == fen, "FEN de travail restauré après le budget");
            Check(attendu.Equals(evaluation.GetValue(recherche)), "quatre sommes restaurées après le budget");
            if (!resultat.InterrompuParBudget || resultat.Profondeur == 0) continue;

            var prefixe = Minimax.ChercherAvecBudget(position, 60000, resultat.Profondeur);
            Check(prefixe.Profondeur == resultat.Profondeur && prefixe.Score == resultat.Score &&
                prefixe.MeilleurCoup?.ToString() == resultat.MeilleurCoup?.ToString(), "dernière itération validée conservée");
            long noeudsInterrompus = resultat.NoeudsVisites - prefixe.NoeudsVisites;
            // Le premier nœud est la racine. Un second confirme qu'un coup a été joué dans l'itération interrompue.
            if (noeudsInterrompus < 2) continue;
            Debug.Log($"[Évaluation et budget] {budget} ms | P{resultat.Profondeur} validée | {noeudsInterrompus} visites interrompues | sommes et FEN restaurés.");
            return;
        }
        throw new InvalidOperationException("Le test doit interrompre une itération après avoir joué un coup.");
    }

    private static void Check(bool valide, string message)
    {
        if (!valide) throw new InvalidOperationException("Évaluation incrémentale : " + message);
    }
}
