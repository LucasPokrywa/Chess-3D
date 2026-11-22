using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Configuration versionnée : ne pas la modifier entre une mesure avant et après.
public static class IABenchmark
{
    private const string Menu = "Halloween Chess/Benchmark IA";
    private const string MenuBudget = "Halloween Chess/Benchmark IA - 500 ms";
    private const string MenuBudgetJeu = "Halloween Chess/Benchmark IA - 1000 ms";
    private const string VersionSuite = "IA-v1";
    private const int Profondeur = 4;
    private const int BudgetMs = 500;
    private const int ProfondeurMaxBudget = 10;
    private const int CapaciteTable = 500000;
    private const bool UtiliserTable = true;
    private const int Echauffements = 1;
    private const int Mesures = 3;

    private static readonly ProfilerMarker MarqueurRecherche = new ProfilerMarker("HalloweenChess.IA.Benchmark.Recherche");
    private static readonly ProfilerMarker MarqueurEchauffement = new ProfilerMarker("HalloweenChess.IA.Benchmark.Echauffement");
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private sealed class Cas
    {
        public readonly string Nom, Fen;
        public readonly ProfilerMarker Marqueur;
        /// <summary>
        /// Associe une position de la suite au nom affiché et à un marqueur de profilage stable.
        /// </summary>
        /// <param name="nom">Libellé qui identifie le cas dans le rapport ou une assertion.</param>
        /// <param name="identifiant">Suffixe stable utilisé dans le nom du marqueur de profilage.</param>
        /// <param name="fen">Position au format FEN à six champs.</param>
        public Cas(string nom, string identifiant, string fen)
        {
            Nom = nom; Fen = fen;
            Marqueur = new ProfilerMarker("HalloweenChess.IA.Benchmark." + identifiant);
        }
    }

    private static readonly Cas[] Positions =
    {
        new Cas("Ouverture", "Ouverture",
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"),
        new Cas("Milieu de jeu", "MilieuDeJeu",
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"),
        new Cas("Tactique (mat en un)", "Tactique",
            "7k/5Q2/6K1/8/8/8/8/8 w - - 0 1"),
        new Cas("Finale de pions", "Finale",
            "8/5pk1/6p1/8/4P3/5K2/8/8 w - - 0 35")
    };

    private static Session session;

    /// <summary>
    /// Vérifie la partition des visites et quelques résultats de référence à profondeur fixe ou sous budget.
    /// </summary>
    [MenuItem("Halloween Chess/Verifier les compteurs IA")]
    public static void VerifierCompteurs()
    {
        var initiale = PositionEchecs.DepuisFen(Positions[0].Fen);
        foreach (bool table in new[] { false, true })
        {
            Minimax.Resultat r = Minimax.Chercher(initiale, 1, utiliserTable: table);
            VerifierSomme(r);
            if (r.NoeudsRecherchePrincipale != 1 || r.NoeudsQuietude != 20 || r.InterrompuParBudget)
                throw new InvalidOperationException("Profondeur 1 initiale : une racine principale et 20 feuilles en quiétude attendues.");
        }

        foreach (string fen in new[] { "7k/6Q1/5K2/8/8/8/8/8 b - - 0 1", "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1" })
        {
            var p = PositionEchecs.DepuisFen(fen);
            foreach (Minimax.Resultat r in new[] { Minimax.Chercher(p, 1), Minimax.ChercherAvecBudget(p) })
            {
                VerifierSomme(r);
                if (r.NoeudsRecherchePrincipale != 1 || r.NoeudsQuietude != 0 || r.InterrompuParBudget)
                    throw new InvalidOperationException("Une racine terminale doit compter une visite principale uniquement.");
            }
        }

        // Regression du mode fixe, sur le même FEN que les deux benchmarks.
        var milieu = PositionEchecs.DepuisFen(Positions[1].Fen);
        Minimax.Resultat fixe = Minimax.Chercher(milieu, Profondeur);
        VerifierSomme(fixe);
        // Référence PVS avec le bonus constant de 30 centipions par pion passé.
        if (fixe.NoeudsVisites != 28581 || fixe.NoeudsRecherchePrincipale != 2827 || fixe.NoeudsQuietude != 25754 ||
            fixe.RelancesPvs != 9 || fixe.CoupuresAvantGeneration == 0 ||
            fixe.MeilleurCoup?.ToString() != "e2a6" || fixe.Score != -16)
            throw new InvalidOperationException($"Référence fixe modifiée : {fixe.NoeudsVisites} nœuds, {fixe.MeilleurCoup}, {fixe.Score}.");
        Minimax.Resultat budget = Minimax.ChercherAvecBudget(milieu, BudgetMs, ProfondeurMaxBudget);
        VerifierSomme(budget);
        if ((budget.Profondeur > 0) != (budget.TempsDerniereIterationMs > 0) ||
            budget.TempsDerniereIterationMs > budget.TempsMillisecondes + 1)
            throw new InvalidOperationException("Temps de dernière itération incohérent avec le résultat budgété.");
        if (milieu.ExporterFen() != Positions[1].Fen)
            throw new InvalidOperationException("Les recherches ont modifié le FEN d'origine.");
        Debug.Log("[Compteurs IA] OK : principal/quiétude, mat/pat et budget ; référence fixe 2827 + 25754 = 28581 / 9 relances PVS / e2a6 / -16.");
    }

    /// <summary>
    /// Refuse un résultat dont le principal et la quiétude ne reconstituent pas exactement le total des visites.
    /// </summary>
    /// <param name="resultat">Résultat de recherche dont les compteurs sont associés au même appel.</param>
    private static void VerifierSomme(Minimax.Resultat resultat)
    {
        if (resultat.NoeudsRecherchePrincipale < 0 || resultat.NoeudsQuietude < 0 ||
            resultat.NoeudsRecherchePrincipale + resultat.NoeudsQuietude != resultat.NoeudsVisites)
            throw new InvalidOperationException("La somme des compteurs principal et quiétude diffère du total.");
    }

    /// <summary>
    /// Démarre la suite versionnée en recherche fixe P4.
    /// </summary>
    [MenuItem(Menu)]
    public static void Lancer()
        => Demarrer(0);

    /// <summary>
    /// Démarre les mêmes positions avec un délai imposé de 500 ms par appel.
    /// </summary>
    [MenuItem(MenuBudget)]
    public static void Lancer500ms()
        => Demarrer(BudgetMs);

    /// <summary>
    /// Démarre les mêmes positions avec le délai historique de comparaison d’une seconde.
    /// </summary>
    [MenuItem(MenuBudgetJeu)]
    public static void Lancer1000ms()
        => Demarrer(ChessIA.BudgetEnJeuMs);

    /// <summary>
    /// Crée une session d’éditeur exclusive et libère ses ressources si le démarrage échoue.
    /// </summary>
    /// <param name="budgetMs">Délai en millisecondes. Pour le lanceur d’éditeur, zéro sélectionne le mode fixe.</param>
    private static void Demarrer(int budgetMs)
    {
        if (!PeutLancer()) return;
        session = new Session(budgetMs);
        try { session.Demarrer(); }
        catch (Exception erreur)
        {
            session.Terminer();
            Debug.LogException(erreur);
        }
    }

    /// <summary>
    /// Protège les commandes de menu contre une exécution concurrente, une compilation ou un changement de mode Play.
    /// </summary>
    /// <returns>Vrai si la suite peut être démarrée.</returns>
    [MenuItem(Menu, true)]
    [MenuItem(MenuBudget, true)]
    [MenuItem(MenuBudgetJeu, true)]
    private static bool PeutLancer() => session == null && !EditorApplication.isPlayingOrWillChangePlaymode &&
        !EditorApplication.isCompiling && !EditorApplication.isUpdating;

    private readonly struct Mesure
    {
        public readonly Minimax.Resultat Resultat;
        public readonly double TempsAppelMs;
        public double NoeudsParSeconde => TempsAppelMs > 0
            ? Resultat.NoeudsVisites * 1000.0 / TempsAppelMs : 0;
        /// <summary>
        /// Conserve un résultat et la durée externe de son appel comme une seule observation.
        /// </summary>
        /// <param name="resultat">Résultat de recherche dont les compteurs sont associés au même appel.</param>
        /// <param name="millisecondes">Durée externe de l’appel FEN et recherche.</param>
        public Mesure(Minimax.Resultat resultat, double millisecondes)
        {
            Resultat = resultat; TempsAppelMs = millisecondes;
        }
    }

    private sealed class Session
    {
        private Scene sceneTemporaire;
        private GameObject objetTemporaire;
        private ChessIA ia;
        private int position, iteration;
        private bool terminee;
        private readonly int budgetMs;
        private bool avecBudget => budgetMs > 0;
        private readonly Stopwatch chrono = new Stopwatch();
        private readonly Mesure[,] mesures = new Mesure[Positions.Length, Mesures];
        private readonly StringBuilder resume = new StringBuilder();

        /// <summary>
        /// Fixe le protocole de la session avant les échauffements. Une valeur nulle sélectionne le mode fixe.
        /// </summary>
        /// <param name="budgetMs">Délai en millisecondes. Pour le lanceur d’éditeur, zéro sélectionne le mode fixe.</param>
        public Session(int budgetMs) { this.budgetMs = budgetMs; }

        /// <summary>
        /// Choisit l’entrée fixe via ChessIA ou l’entrée budgétée directe selon le protocole. Chaque appel repart du FEN et d’une table neuve.
        /// </summary>
        /// <param name="cas">Position de référence à traiter dans la suite.</param>
        /// <returns>Résultat mesuré sur une position de la suite.</returns>
        private Minimax.Resultat Chercher(Cas cas)
        {
            // Même entrée de recherche budgétée que ChessIA.ChoisirCoupAsync en partie.
            // Appel synchrone ici pour garder toute la recherche sous le marqueur Editor.
            return avecBudget
                ? Minimax.ChercherAvecBudget(PositionEchecs.DepuisFen(cas.Fen),
                    budgetMs: budgetMs, profondeurMax: ProfondeurMaxBudget)
                : ia.ChoisirCoupDepuisFen(cas.Fen, utiliserTable: UtiliserTable, capaciteTable: CapaciteTable);
        }

        /// <summary>
        /// Crée un composant de mesure temporaire, prépare la synthèse et inscrit les rappels de la boucle d’éditeur.
        /// </summary>
        public void Demarrer()
        {
            if ((avecBudget ? ProfondeurMaxBudget : Profondeur) > Minimax.ProfondeurMax)
                throw new InvalidOperationException("La profondeur du moteur ne permet plus d'exécuter la suite " + VersionSuite);
            // Valider tous les cas avant de créer le composant et de commencer les mesures.
            foreach (Cas cas in Positions) PositionEchecs.DepuisFen(cas.Fen);
            sceneTemporaire = EditorSceneManager.NewPreviewScene();
            objetTemporaire = new GameObject("IA Benchmark temporaire") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(objetTemporaire, sceneTemporaire);
            ia = objetTemporaire.AddComponent<ChessIA>();
            ia.Profondeur = Profondeur;

            resume.AppendLine("[Benchmark IA] " + VersionSuite + " — " + DateTime.UtcNow.ToString("O", Invariant));
            resume.AppendLine($"Unity {Application.unityVersion} | {Application.platform} | CPU : {SystemInfo.processorType}");
            string mode = avecBudget ? $"budget={budgetMs} ms, profondeur maximale={ProfondeurMaxBudget}" : $"profondeur fixe={Profondeur}";
            resume.AppendLine($"Mode : {mode}, table={UtiliserTable}, capacité={CapaciteTable}, " +
                $"échauffements/position={Echauffements}, mesures/position={Mesures}. Table neuve à chaque appel.");
            resume.AppendLine("Temps = appel FEN + IA ; N/s = nœuds visités / durée de cet appel. Médiane de 3 appels.");
            if (avecBudget)
                resume.AppendLine("Profondeur = dernière itération entièrement terminée ; nœuds = toutes les itérations, y compris celle interrompue. " +
                    "Si profondeur=0, coup/score de secours. Le plafond est fixé à 10, indépendamment de l'Inspector du jeu.");
            Debug.Log(resume.ToString() +
                "Profiler : cible Editor, module CPU Usage, Hierarchy > HalloweenChess.IA.Benchmark.Recherche > GC Alloc. " +
                "Deep Profile doit rester désactivé pour comparer les temps.");

            EditorApplication.update += Avancer;
            EditorApplication.playModeStateChanged += SurChangementMode;
            AssemblyReloadEvents.beforeAssemblyReload += Interrompre;
            EditorApplication.quitting += Interrompre;
        }

        // Un appel par update : chaque recherche mesurée apparaît dans sa frame Editor.
        /// <summary>
        /// Effectue au plus un échauffement ou un essai par appel d’éditeur, avec marqueurs et contrôle de progression. Une recherche synchrone en cours va jusqu’à son terme.
        /// </summary>
        private void Avancer()
        {
            if (terminee) return;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                {
                    Interrompre();
                    return;
                }
                Cas cas = Positions[position];
                bool echauffement = iteration < Echauffements;
                string etape = echauffement ? "Échauffement" : $"Mesure {iteration - Echauffements + 1}/{Mesures}";
                float progression = (position * (Echauffements + Mesures) + iteration) /
                    (float)(Positions.Length * (Echauffements + Mesures));
                if (EditorUtility.DisplayCancelableProgressBar("Benchmark IA " + VersionSuite,
                    cas.Nom + " — " + etape + " (annulation entre deux recherches)", progression))
                {
                    Interrompre();
                    return;
                }

                // Ni l'affichage, ni les logs, ni la création du composant dans ce marqueur.
                // Inclut le parsing FEN, les copies et les allocations de la recherche.
                Minimax.Resultat resultat;
                if (echauffement)
                {
                    using (MarqueurEchauffement.Auto())
                        resultat = Chercher(cas);
                }
                else
                {
                    chrono.Restart();
                    using (MarqueurRecherche.Auto())
                    using (cas.Marqueur.Auto())
                        resultat = Chercher(cas);
                    chrono.Stop();
                    var mesure = new Mesure(resultat, chrono.Elapsed.TotalMilliseconds);
                    mesures[position, iteration - Echauffements] = mesure;
                    Debug.Log(Formater(cas, mesure, etape));
                }
                VerifierSomme(resultat);
                if (!avecBudget && resultat.Profondeur != Profondeur)
                    throw new InvalidOperationException($"Profondeur atteinte {resultat.Profondeur}, attendue {Profondeur}. Résultat non comparable.");

                iteration++;
                if (iteration < Echauffements + Mesures) return;
                ResumerPosition(cas);
                iteration = 0;
                position++;
                if (position < Positions.Length) return;
                Debug.Log(resume.ToString() + "[Benchmark IA] Terminé. Conserver ce résumé et la révision Git pour la comparaison avant/après.");
                Terminer();
            }
            catch (Exception erreur)
            {
                Terminer();
                Debug.LogError("[Benchmark IA] Échec : les résultats de cette exécution sont incomplets.");
                Debug.LogException(erreur);
            }
        }

        /// <summary>
        /// Sélectionne la mesure médiane et vérifie le déterminisme attendu du mode fixe avant d’ajouter la synthèse.
        /// </summary>
        /// <param name="cas">Position de référence à traiter dans la suite.</param>
        private void ResumerPosition(Cas cas)
        {
            var tries = new Mesure[Mesures];
            for (int i = 0; i < Mesures; i++) tries[i] = mesures[position, i];
            Array.Sort(tries, (a, b) => a.TempsAppelMs.CompareTo(b.TempsAppelMs));
            Mesure mediane = tries[Mesures / 2];
            for (int i = 1; !avecBudget && i < Mesures; i++)
                if (mesures[position, i].Resultat.NoeudsVisites != mesures[position, 0].Resultat.NoeudsVisites ||
                    mesures[position, i].Resultat.Score != mesures[position, 0].Resultat.Score)
                {
                    Debug.LogWarning("[Benchmark IA] " + cas.Nom + " : nœuds ou score variables entre répétitions ; vérifier le déterminisme.");
                    break;
                }
            resume.AppendLine(Formater(cas, mediane, "MÉDIANE"));
            resume.AppendLine("FEN : " + cas.Fen);
        }

        /// <summary>
        /// Met en forme la durée, le débit, la profondeur et les compteurs d’une observation. La durée externe inclut les préparatifs.
        /// </summary>
        /// <param name="cas">Position de référence à traiter dans la suite.</param>
        /// <param name="mesure">Observation individuelle retenue pour l’affichage.</param>
        /// <param name="etape">Libellé de l’essai, de l’échauffement ou de la synthèse.</param>
        /// <returns>Ligne de console associée à la position et à l’étape.</returns>
        private string Formater(Cas cas, Mesure mesure, string etape)
        {
            Minimax.Resultat r = mesure.Resultat;
            string coup = r.MeilleurCoup.HasValue ? r.MeilleurCoup.Value.ToString() : "aucun";
            return $"[Benchmark IA] {cas.Nom} | {etape} | " +
                $"appel={mesure.TempsAppelMs.ToString("F3", Invariant)} ms | Minimax={r.TempsMillisecondes} ms | " +
                $"profondeur={r.Profondeur} | budget interrompu={r.InterrompuParBudget} | " +
                (avecBudget ? $"dernière itération validée={r.TempsDerniereIterationMs.ToString("F3", Invariant)} ms | " : "") +
                $"coupures avant génération={r.CoupuresAvantGeneration} | " +
                $"nœuds={r.NoeudsVisites} (principal={r.NoeudsRecherchePrincipale}, quiétude={r.NoeudsQuietude}) | relances PVS={r.RelancesPvs} | " +
                $"N/s={mesure.NoeudsParSeconde.ToString("F0", Invariant)} | coup={coup} | score={r.Score} | " +
                $"table={r.EntreesTable}, réutilisations={r.ReutilisationsTable}, coupures={r.CoupuresTable}";
        }

        /// <summary>
        /// Interrompt la session lorsque l’éditeur quitte son état de mesure admissible.
        /// </summary>
        /// <param name="changement">Nouvel état du mode Play signalé par l’éditeur.</param>
        private void SurChangementMode(PlayModeStateChange changement)
        {
            if (changement == PlayModeStateChange.ExitingEditMode) Interrompre();
        }

        /// <summary>
        /// Journalise l’arrêt demandé et désabonne la session. L’annulation de la barre intervient entre deux recherches.
        /// </summary>
        private void Interrompre()
        {
            Debug.LogWarning("[Benchmark IA] Interrompu ; aucune synthèse complète pour cette exécution.");
            Terminer();
        }

        /// <summary>
        /// Retire les rappels, ferme la progression et détruit l’objet de mesure temporaire.
        /// </summary>
        public void Terminer()
        {
            if (terminee) return;
            terminee = true;
            EditorApplication.update -= Avancer;
            EditorApplication.playModeStateChanged -= SurChangementMode;
            AssemblyReloadEvents.beforeAssemblyReload -= Interrompre;
            EditorApplication.quitting -= Interrompre;
            EditorUtility.ClearProgressBar();
            try
            {
                try
                {
                    if (objetTemporaire != null) UnityEngine.Object.DestroyImmediate(objetTemporaire);
                }
                finally
                {
                    if (sceneTemporaire.IsValid()) EditorSceneManager.ClosePreviewScene(sceneTemporaire);
                }
            }
            finally { session = null; }
        }
    }
}
