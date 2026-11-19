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