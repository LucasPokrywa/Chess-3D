using UnityEditor;
using UnityEngine;

public static class TestBudgetIA
{
    [MenuItem("Halloween Chess/Tester IA - 500 ms")]
    private static void TesterBudgetNormal()
    {
        Tester(500);
    }

    [MenuItem("Halloween Chess/Tester IA - 1 ms")]
    private static void TesterBudgetCourt()
    {
        Tester(1);
    }

    [MenuItem("Halloween Chess/Tester IA - Quietude")]
    private static void TesterQuietude()
    {
        const string fen = "6k1/8/4p3/3p4/8/8/8/3Q2K1 w - - 0 1";
        var position = PositionEchecs.DepuisFen(fen);
        var resultat = Minimax.Chercher(position, profondeur: 1);

        Debug.Log($"[Test quiétude] Coup = {resultat.MeilleurCoup} | Score = {resultat.Score}");
    }

    [MenuItem("Halloween Chess/Tester IA - Suites de quietude")]
    private static void TesterSuitesQuietude()
    {
        TesterReprisesSuccessives();
        TesterSortieEchecSansCapture();
    }

    private static void TesterReprisesSuccessives()
    {
        const string fen = "6k1/8/4pn2/3p4/8/8/6B1/3R2K1 w - - 0 1";
        var position = PositionEchecs.DepuisFen(fen);
        var regles = new ReglesEchecs(position);

        // On force la prise de pion par la tour blanche.
        if (!regles.JouerCoup(3, 0, 3, 4)) // d1d5
        {
            Debug.LogError("[Quiétude - reprises] Le coup de préparation d1d5 est illégal.");
            return;
        }

        // Les Noirs jouent. La branche e6d5 peut continuer par g2d5, puis f6d5.
        var resultat = Minimax.Chercher(position, profondeur: 1);
        string coup = resultat.MeilleurCoup?.ToString();

        if (coup == "e6d5" || coup == "f6d5")
            Debug.Log($"[Quiétude - reprises] OK | Coup : {coup} | Score noir : {resultat.Score}");
        else
            Debug.LogError($"[Quiétude - reprises] Reprise sur d5 attendue | Coup obtenu : {coup}");
    }

    private static void TesterSortieEchecSansCapture()
    {
        const string fen = "7k/R7/5K2/8/8/8/8/8 w - - 0 1";

        // Après Ta8+, le roi noir a une seule réponse : h8h7.
        var apresEchec = PositionEchecs.DepuisFen(fen);
        var regles = new ReglesEchecs(apresEchec);

        if (!regles.JouerCoup(0, 6, 0, 7)) // a7a8
        {
            Debug.LogError("[Quiétude - échec] Le coup de préparation a7a8 est illégal.");
            return;
        }

        var reponses = regles.GetLegalMoves(7, 7);
        if (!regles.IsInCheck(false) ||
            reponses.Count != 1 ||
            reponses[0] != new Vector2Int(7, 6))
        {
            Debug.LogError("[Quiétude - échec] La seule réponse attendue est h8h7.");
            return;
        }

        // On analyse avant Ta8+ : la réponse noire est au-delà de la profondeur 1.
        var resultat = Minimax.Chercher(PositionEchecs.DepuisFen(fen), profondeur: 1);

        if (System.Math.Abs(resultat.Score) >= Evaluation.ScoreMat - 1000)
            Debug.LogError($"[Quiétude - échec] Faux mat détecté | Score : {resultat.Score}");
        else
            Debug.Log($"[Quiétude - échec] OK | Coup : {resultat.MeilleurCoup} | Score : {resultat.Score}");
    }
    private static void Tester(int budgetMs)
    {
        const string fen =
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        var position = PositionEchecs.DepuisFen(fen);
        var resultat = Minimax.ChercherAvecBudget(position, budgetMs: budgetMs);

        Debug.Log(
            $"[Test budget IA] Budget = {budgetMs} ms | " +
            $"Temps = {resultat.TempsMillisecondes} ms | " +
            $"Profondeur terminée = {resultat.Profondeur} | " +
            $"Coup = {resultat.MeilleurCoup} | Score = {resultat.Score}");
    }
}