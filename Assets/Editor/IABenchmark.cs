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
    private const string VersionSuite = "IA-v1";
    private const int Profondeur = 4;
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

    [MenuItem(Menu)]
    public static void Lancer()
    {
        if (!PeutLancer()) return;
        session = new Session();
        try { session.Demarrer(); }
        catch (Exception erreur)
        {
            session.Terminer();
            Debug.LogException(erreur);
        }
    }

    [MenuItem(Menu, true)]
    private static bool PeutLancer() => session == null && !EditorApplication.isPlayingOrWillChangePlaymode &&
        !EditorApplication.isCompiling && !EditorApplication.isUpdating;

    private readonly struct Mesure
    {
        public readonly Minimax.Resultat Resultat;
        public readonly double TempsAppelMs;
        public double NoeudsParSeconde => TempsAppelMs > 0
            ? Resultat.NoeudsVisites * 1000.0 / TempsAppelMs : 0;
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
        private readonly Stopwatch chrono = new Stopwatch();
        private readonly Mesure[,] mesures = new Mesure[Positions.Length, Mesures];
        private readonly StringBuilder resume = new StringBuilder();

        public void Demarrer()
        {
            if (Profondeur > Minimax.ProfondeurMax)
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
            resume.AppendLine($"Profondeur fixe={Profondeur}, table={UtiliserTable}, capacité={CapaciteTable}, " +
                $"échauffements/position={Echauffements}, mesures/position={Mesures}. Table neuve à chaque appel.");
            resume.AppendLine("Temps = appel FEN + IA ; N/s = nœuds visités / durée de cet appel. Médiane de 3 appels.");
            Debug.Log(resume.ToString() +
                "Profiler : cible Editor, module CPU Usage, Hierarchy > HalloweenChess.IA.Benchmark.Recherche > GC Alloc. " +
                "Deep Profile doit rester désactivé pour comparer les temps.");

            EditorApplication.update += Avancer;
            EditorApplication.playModeStateChanged += SurChangementMode;
            AssemblyReloadEvents.beforeAssemblyReload += Interrompre;
            EditorApplication.quitting += Interrompre;
        }

        // Un appel par update : chaque recherche mesurée apparaît dans sa frame Editor.
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
                        resultat = ia.ChoisirCoupDepuisFen(cas.Fen, utiliserTable: UtiliserTable, capaciteTable: CapaciteTable);
                }
                else
                {
                    chrono.Restart();
                    using (MarqueurRecherche.Auto())
                    using (cas.Marqueur.Auto())
                        resultat = ia.ChoisirCoupDepuisFen(cas.Fen, utiliserTable: UtiliserTable, capaciteTable: CapaciteTable);
                    chrono.Stop();
                    var mesure = new Mesure(resultat, chrono.Elapsed.TotalMilliseconds);
                    mesures[position, iteration - Echauffements] = mesure;
                    Debug.Log(Formater(cas, mesure, etape));
                }
                if (resultat.Profondeur != Profondeur)
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

        private void ResumerPosition(Cas cas)
        {
            var tries = new Mesure[Mesures];
            for (int i = 0; i < Mesures; i++) tries[i] = mesures[position, i];
            Array.Sort(tries, (a, b) => a.TempsAppelMs.CompareTo(b.TempsAppelMs));
            Mesure mediane = tries[Mesures / 2];
            for (int i = 1; i < Mesures; i++)
                if (mesures[position, i].Resultat.NoeudsVisites != mesures[position, 0].Resultat.NoeudsVisites ||
                    mesures[position, i].Resultat.Score != mesures[position, 0].Resultat.Score)
                {
                    Debug.LogWarning("[Benchmark IA] " + cas.Nom + " : nœuds ou score variables entre répétitions ; vérifier le déterminisme.");
                    break;
                }
            resume.AppendLine(Formater(cas, mediane, "MÉDIANE"));
            resume.AppendLine("FEN : " + cas.Fen);
        }

        private static string Formater(Cas cas, Mesure mesure, string etape)
        {
            Minimax.Resultat r = mesure.Resultat;
            string coup = r.MeilleurCoup.HasValue ? r.MeilleurCoup.Value.ToString() : "aucun";
            return $"[Benchmark IA] {cas.Nom} | {etape} | " +
                $"appel={mesure.TempsAppelMs.ToString("F3", Invariant)} ms | Minimax={r.TempsMillisecondes} ms | " +
                $"profondeur={r.Profondeur} | nœuds={r.NoeudsVisites} | " +
                $"N/s={mesure.NoeudsParSeconde.ToString("F0", Invariant)} | coup={coup} | score={r.Score} | " +
                $"table={r.EntreesTable}, réutilisations={r.ReutilisationsTable}, coupures={r.CoupuresTable}";
        }

        private void SurChangementMode(PlayModeStateChange changement)
        {
            if (changement == PlayModeStateChange.ExitingEditMode) Interrompre();
        }

        private void Interrompre()
        {
            Debug.LogWarning("[Benchmark IA] Interrompu ; aucune synthèse complète pour cette exécution.");
            Terminer();
        }

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
