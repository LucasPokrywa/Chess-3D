using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

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

// Façade de calcul uniquement : aucun Start/Update, aucune référence au contrôleur.
// Appeler explicitement ChoisirCoup(position) pour obtenir un résultat sans jouer.
public class ChessIA : MonoBehaviour
{
    public const int BudgetEnJeuMs = 1000;
    private const int BudgetMaxMs = 100_000;

    [Header("Difficulté")]
    [SerializeField] private DifficulteIA difficulte = DifficulteIA.Personnalise;
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurFacile = 2;
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurMoyen = 4;
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurDifficile = 6;
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurExpert = 8;
    [FormerlySerializedAs("profondeur")]
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeurPersonnalisee = 3;

    [Header("Recherche")]
    [SerializeField, Range(1, BudgetMaxMs)]
    [Tooltip("Budget total de réflexion par coup, en millisecondes.")]
    private int budgetParCoupMs = BudgetEnJeuMs;
    [SerializeField, Tooltip("A utilise le moteur de référence. D active la génération progressive et les bitboards hybrides.")]
    private ModeMoteurIA modeMoteur = ModeMoteurIA.A;

    /// <summary>Mode capturé au lancement de chaque recherche.</summary>
    public ModeMoteurIA ModeMoteur
    {
        get => modeMoteur;
        set => modeMoteur = value;
    }

    /// <summary>Niveau utilisé à partir de la prochaine recherche.</summary>
    public DifficulteIA Difficulte
    {
        get => difficulte;
        set => difficulte = value;
    }

    /// <summary>Plafond effectif du niveau courant. Une écriture directe sélectionne le niveau personnalisé.</summary>
    public int Profondeur
    {
        get => Mathf.Clamp(difficulte switch
        {
            DifficulteIA.Facile => profondeurFacile,
            DifficulteIA.Moyen => profondeurMoyen,
            DifficulteIA.Difficile => profondeurDifficile,
            DifficulteIA.Expert => profondeurExpert,
            _ => profondeurPersonnalisee
        }, 1, Minimax.ProfondeurMax);
        set
        {
            profondeurPersonnalisee = Mathf.Clamp(value, 1, Minimax.ProfondeurMax);
            Difficulte = DifficulteIA.Personnalise;
        }
    }

    /// <summary>Budget total par appel, en millisecondes. Les écritures n’affectent pas une tâche déjà lancée.</summary>
    public int BudgetParCoupMs
    {
        get => Mathf.Clamp(budgetParCoupMs, 1, BudgetMaxMs);
        set => budgetParCoupMs = Mathf.Clamp(value, 1, BudgetMaxMs);
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

        int profondeurMax = Profondeur;
        int budgetMs = BudgetParCoupMs;
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
