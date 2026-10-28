using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class TranspositionChecks
{
    [MenuItem("Halloween Chess/Verifier la table de transposition")]
    public static void Run()
    {
        // Tester aussi la clé : les compteurs ne changent pas les règles actuelles,
        // contrairement au trait, aux droits de roque et à la prise en passant.
        Type recherche = typeof(Minimax).GetNestedType("Recherche", BindingFlags.NonPublic);
        MethodInfo creerCle = recherche.GetMethod("CreerCle", BindingFlags.NonPublic | BindingFlags.Static);
        Func<string, object> cle = fen => creerCle.Invoke(null,
            new object[] { PositionEchecs.DepuisFen(fen) });
        const string roque = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
        Check(Equals(cle(roque), cle(roque.Replace("0 1", "12 25"))), "Compteurs exclus de la clé");
        Check(!Equals(cle(roque), cle(roque.Replace(" w ", " b "))), "Trait inclus dans la clé");
        Check(!Equals(cle(roque), cle(roque.Replace("KQkq", "Qkq"))), "Droits de roque inclus dans la clé");
        const string passant = "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10";
        Check(!Equals(cle(passant), cle(passant.Replace("d6", "-"))), "Prise en passant incluse dans la clé");
        Check(!Equals(cle(roque.Replace("KQkq", "Kkq")),
            cle(roque.Replace("KQkq", "Kkq").Replace("R3K2R", "1R2K2R"))), "Cases incluses dans la clé");
        VerifierAnnulations(PositionEchecs.FenInitial);
        VerifierAnnulations(roque);
        VerifierAnnulations(passant);
        VerifierAnnulations("4k3/P7/8/8/8/8/8/4K3 w - - 12 25");
        VerifierAnnulations("4k3/8/8/8/8/8/p7/4K3 b - - 17 25");

        Comparer(PositionEchecs.FenInitial, 3);
        Minimax.Resultat initial = Comparer(PositionEchecs.FenInitial, 4);
        Check(initial.ReutilisationsTable > 0, "Des positions initiales sont réutilisées");
        Comparer(roque, 3);
        Comparer(passant, 3);
        Comparer("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1", 3); // Mat en un.
        Comparer("7k/6Q1/5K2/8/8/8/8/8 b - - 0 1", 3); // Mat.
        Comparer("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", 3); // Pat.

        var p = new PositionEchecs();
        var petiteTable = Minimax.Chercher(p, 3, capaciteTable: 1);
        Check(petiteTable.EntreesTable <= 1, "Capacité respectée");
        Check(petiteTable.Score == Minimax.Chercher(p, 3, utiliserTable: false).Score,
            "Une table pleine préserve le score");

        MethodInfo stocker = recherche.GetMethod("StockerScore", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo lire = recherche.GetMethod("LireScore", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (int score in new[] { 123, Evaluation.ScoreMat - 5, -Evaluation.ScoreMat + 5 })
        {
            int stocke = (int)stocker.Invoke(null, new object[] { score, 2 });
            Check((int)lire.Invoke(null, new object[] { stocke, 2 }) == score, "Aller-retour du score de mat");
            int attendu = score > Evaluation.ScoreMat - 1000 ? score - 2 :
                score < -Evaluation.ScoreMat + 1000 ? score + 2 : score;
            Check((int)lire.Invoke(null, new object[] { stocke, 4 }) == attendu,
                "Distance au mat corrigée lors d'une transposition");
        }
        using (var annulation = new CancellationTokenSource())
        {
            annulation.Cancel();
            bool annule = false;
            try { Minimax.Chercher(p, 3, annulation.Token); }
            catch (OperationCanceledException) { annule = true; }
            Check(annule && p.ExporterFen() == PositionEchecs.FenInitial, "Annulation sans mutation");
        }
        Debug.Log("Table de transposition : tous les contrôles ont réussi.");
    }

    private static Minimax.Resultat Comparer(string fen, int profondeur)
    {
        PositionEchecs p = PositionEchecs.DepuisFen(fen);
        string avant = p.ExporterFen();
        var chrono = System.Diagnostics.Stopwatch.StartNew();
        Minimax.Resultat sans = Minimax.Chercher(p, profondeur, utiliserTable: false);
        long tempsSans = chrono.ElapsedMilliseconds;
        chrono.Restart();
        Minimax.Resultat avec = Minimax.Chercher(p, profondeur);
        long tempsAvec = chrono.ElapsedMilliseconds;
        Check(avec.Score == sans.Score, "Même score avec/sans table : " + fen);
        Check(avec.NoeudsVisites == avec.NoeudsDeveloppes + avec.CoupuresTable,
            "Chaque visite est développée ou résolue par la table");
        Check(avec.CoupuresTable <= avec.ReutilisationsTable, "Compteurs de table cohérents");
        Check(avec.MeilleurCoup.HasValue == sans.MeilleurCoup.HasValue, "Même état terminal");
        if (avec.MeilleurCoup.HasValue)
        {
            var coup = avec.MeilleurCoup.Value;
            Check(new ReglesEchecs(p).GetLegalMoves(coup.Depart.x, coup.Depart.y).Contains(coup.Arrivee),
                "Meilleur coup légal");
            // Vérifier la valeur du coup choisi avec une recherche indépendante sans table.
            var copie = p.Copier();
            Check(new ReglesEchecs(copie).JouerCoup(coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y),
                "Application du coup choisi");
            int scoreEnfant = Minimax.Chercher(copie, profondeur - 1, utiliserTable: false).Score;
            int scoreRacine = -scoreEnfant;
            if (scoreRacine >= Evaluation.ScoreMat - 1000) scoreRacine--;
            else if (scoreRacine <= -Evaluation.ScoreMat + 1000) scoreRacine++;
            Check(scoreRacine == avec.Score, "Le meilleur coup atteint le score annoncé");
        }
        Check(p.ExporterFen() == avant, "Position originale intacte");
        Debug.Log($"TT profondeur {profondeur} : sans table {sans.NoeudsVisites} nœuds / {tempsSans} ms ; " +
            $"avec table {avec.NoeudsVisites} nœuds / {tempsAvec} ms ; " +
            $"{avec.ReutilisationsTable} réutilisations, {avec.EntreesTable} entrées. FEN : {fen}");
        return avec;
    }

    private static void VerifierAnnulations(string fen)
    {
        var position = PositionEchecs.DepuisFen(fen);
        string avant = Signature(position);
        var regles = new ReglesEchecs(position);
        MethodInfo jouer = typeof(PositionEchecs).GetMethod("JouerTemporairement", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo annuler = typeof(PositionEchecs).GetMethod("AnnulerCoup", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece == '.' || char.IsLower(piece) != position.TourBlanc) continue;
                var coups = regles.GetLegalMoves(x, z);
                Check(Signature(position) == avant, "La génération légale restaure la position");
                foreach (Vector2Int arrivee in coups)
                {
                    var copie = position.Copier();
                    Check(new ReglesEchecs(copie).JouerCoup(x, z, arrivee.x, arrivee.y), "Coup légal sur copie");
                    object etat = jouer.Invoke(position, new object[] { x, z, arrivee.x, arrivee.y });
                    try { Check(Signature(position) == Signature(copie), "Simulation identique à un coup joué"); }
                    finally { annuler.Invoke(position, new[] { etat }); }
                    Check(Signature(position) == avant, "Annulation : cases, droits, trait et compteurs restaurés");
                }
            }
    }

    private static string Signature(PositionEchecs position)
    {
        string signature = position.ExporterFen();
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                PieceEtat piece = position.LirePiece(x, z);
                signature += piece == null ? "." : piece.HasMoved ? "1" : "0";
            }
        return signature;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Table de transposition : " + message);
    }
}
