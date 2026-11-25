using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

// Toujours utilisé par le menu et par la migration des composants existants.
public enum DifficulteIA
{
    // Les trois premières valeurs sont utilisées par le menu et ses préférences.
    Facile = 0,
    Moyen = 1,
    Difficile = 2,
    Expert = 3,
    [InspectorName("Personnalisé")]
    Personnalise = 4
}

[System.Serializable]
public sealed class ConfigurationDifficulteIA
{
    public string Nom = "Nouvelle difficulté";
    [Range(1, Minimax.ProfondeurMax)] public int Profondeur = 5;
    [Range(1, ChessIA.BudgetMaxMs)] public int TempsMs = ChessIA.BudgetEnJeuMs;

    [SerializeField, HideInInspector] private string id;
    internal string Id => id;
    internal void AttribuerIdentifiant() => id = System.Guid.NewGuid().ToString("N");
}

// Façade de calcul uniquement : aucun Start/Update, aucune référence au contrôleur.
// Appeler explicitement ChoisirCoup(position) pour obtenir un résultat sans jouer.
public class ChessIA : MonoBehaviour
{
    public const int BudgetEnJeuMs = 1000;
    internal const int BudgetMaxMs = 100_000;

    [Header("Difficulté")]
    [SerializeField] private List<ConfigurationDifficulteIA> configurations = new List<ConfigurationDifficulteIA>();
    [SerializeField, HideInInspector] private string difficulteActiveId;

    [Header("Personnalisé")]
    [FormerlySerializedAs("profondeur")]
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurPersonnalisee = 3;
    [SerializeField, Range(1, BudgetMaxMs)]
    [Tooltip("Budget par coup du niveau Personnalisé, en millisecondes.")]
    private int budgetParCoupMs = BudgetEnJeuMs;

    [Header("Recherche")]
    [SerializeField, Tooltip("A utilise le moteur de référence. D active la génération progressive et les bitboards hybrides.")]
    private ModeMoteurIA modeMoteur = ModeMoteurIA.A;

    // Noms conservés pour lire les anciennes scènes et les prefabs déjà sérialisés.
    [SerializeField, HideInInspector] private bool difficultesMigrees;
    [SerializeField, HideInInspector] private DifficulteIA difficulte = DifficulteIA.Personnalise;
    [SerializeField, HideInInspector] private int profondeurFacile = 5;
    [SerializeField, HideInInspector] private int budgetFacileMs = 1_000;
    [SerializeField, HideInInspector] private int profondeurMoyen = 6;
    [SerializeField, HideInInspector] private int budgetMoyenMs = 2_000;
    [SerializeField, HideInInspector] private int profondeurDifficile = 7;
    [SerializeField, HideInInspector] private int budgetDifficileMs = 5_000;
    [SerializeField, HideInInspector] private int profondeurExpert = 8;
    [SerializeField, HideInInspector] private int budgetExpertMs = 25_000;

    /// <summary>Mode capturé au lancement de chaque recherche.</summary>
    public ModeMoteurIA ModeMoteur
    {
        get => modeMoteur;
        set => modeMoteur = value;
    }

    private void OnValidate()
    {
        MigrerDifficultes();
        if (configurations == null) return;

        var identifiants = new HashSet<string>();
        foreach (ConfigurationDifficulteIA configuration in configurations)
        {
            if (configuration == null) continue;
            if (string.IsNullOrEmpty(configuration.Id) || !identifiants.Add(configuration.Id))
            {
                configuration.AttribuerIdentifiant();
                identifiants.Add(configuration.Id);
            }
        }
    }

    private void MigrerDifficultes()
    {
        if (difficultesMigrees) return;

        configurations = new List<ConfigurationDifficulteIA>
        {
            CreerConfiguration("Facile", profondeurFacile, budgetFacileMs),
            CreerConfiguration("Moyen", profondeurMoyen, budgetMoyenMs),
            CreerConfiguration("Difficile", profondeurDifficile, budgetDifficileMs),
            CreerConfiguration("Expert", profondeurExpert, budgetExpertMs)
        };
        int ancienNiveau = (int)difficulte;
        difficulteActiveId = ancienNiveau >= 0 && ancienNiveau < configurations.Count
            ? configurations[ancienNiveau].Id : "";
        difficultesMigrees = true;
    }

    private static ConfigurationDifficulteIA CreerConfiguration(string nom, int profondeur, int tempsMs)
    {
        var configuration = new ConfigurationDifficulteIA { Nom = nom, Profondeur = profondeur, TempsMs = tempsMs };
        configuration.AttribuerIdentifiant();
        return configuration;
    }

    private (int profondeur, int tempsMs) ReglagesActifs()
    {
        MigrerDifficultes();
        ConfigurationDifficulteIA configuration = null;
        if (!string.IsNullOrEmpty(difficulteActiveId) && configurations != null)
            configuration = configurations.Find(element => element?.Id == difficulteActiveId);

        return (Mathf.Clamp(configuration?.Profondeur ?? profondeurPersonnalisee, 1, Minimax.ProfondeurMax),
            Mathf.Clamp(configuration?.TempsMs ?? budgetParCoupMs, 1, BudgetMaxMs));
    }

    /// <summary>Plafond effectif du niveau courant. Une écriture directe sélectionne le niveau personnalisé.</summary>
    public int Profondeur
    {
        get => ReglagesActifs().profondeur;
        set
        {
            MigrerDifficultes();
            profondeurPersonnalisee = Mathf.Clamp(value, 1, Minimax.ProfondeurMax);
            difficulteActiveId = "";
        }
    }

    /// <summary>Budget effectif du niveau courant, en millisecondes. Une écriture directe sélectionne le niveau personnalisé.</summary>
    public int BudgetParCoupMs
    {
        get => ReglagesActifs().tempsMs;
        set
        {
            MigrerDifficultes();
            budgetParCoupMs = Mathf.Clamp(value, 1, BudgetMaxMs);
            difficulteActiveId = "";
        }
    }

    /// <summary>
    /// Lance une recherche synchrone à la profondeur du composant. L’appelant conserve sa position puisque Minimax travaille sur une copie.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
    /// <param name="utiliserTable">Autorise la lecture et l’écriture de la table de transposition.</param>
    /// <param name="capaciteTable">Nombre maximal d’entrées de la table de cet appel.</param>
    /// <returns>Coup légal et score du camp au trait, ou aucun coup si la position est terminale.</returns>
    public Minimax.Resultat ChoisirCoup(PositionEchecs position, CancellationToken annulation = default,
        bool utiliserTable = true, int capaciteTable = 500000)
    {
        return Minimax.Chercher(position, Profondeur, annulation,
            utiliserTable, capaciteTable, mode: ModeMoteur);
    }

    // Appeler sur le thread Unity : capturer les réglages et la position avant le calcul.
    /// <summary>
    /// Capture la position et les réglages sur le thread Unity avant de lancer le calcul en arrière-plan. Aucun objet de scène n’est manipulé par la tâche.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
    /// <returns>Tâche qui rend la dernière itération validée ou le résultat de secours. Une annulation externe annule la tâche.</returns>
    public Task<Minimax.Resultat> ChoisirCoupAsync(
        PositionEchecs position,
        CancellationToken annulation = default)
    {
        if (position == null)
            throw new System.ArgumentNullException(nameof(position));

        var (profondeurMax, budgetMs) = ReglagesActifs();
        ModeMoteurIA mode = ModeMoteur;
        PositionEchecs copie = position.Copier();

        return Task.Run(
            () => Minimax.ChercherAvecBudget(
                copie,
                budgetMs: budgetMs,
                profondeurMax: profondeurMax,
                annulation: annulation,
                mode: mode),
            annulation);
    }

    // Variante utile pour tester l'IA avec un FEN, sans importer celui-ci dans la scène.
    /// <summary>
    /// Convertit les six champs FEN en position logique puis délègue au mode fixe. Un FEN invalide déclenche une FormatException.
    /// </summary>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
    /// <param name="utiliserTable">Autorise la lecture et l’écriture de la table de transposition.</param>
    /// <param name="capaciteTable">Nombre maximal d’entrées de la table de cet appel.</param>
    /// <returns>Résultat exprimé du point de vue du trait indiqué dans le FEN.</returns>
    public Minimax.Resultat ChoisirCoupDepuisFen(string fen, CancellationToken annulation = default,
        bool utiliserTable = true, int capaciteTable = 500000)
    {
        return ChoisirCoup(PositionEchecs.DepuisFen(fen), annulation, utiliserTable, capaciteTable);
    }
}
