using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Façade de calcul uniquement : aucun Start/Update, aucune référence au contrôleur.
// Appeler explicitement ChoisirCoup(position) pour obtenir un résultat sans jouer.
public class ChessIA : MonoBehaviour
{
    public const int BudgetEnJeuMs = 1000;
    private const int BudgetMaxMs = 10000;
    [SerializeField, Range(1, Minimax.ProfondeurMax)] private int profondeur = 3;
    [SerializeField, Range(1, BudgetMaxMs)]
    [Tooltip("Budget total de réflexion par coup, en millisecondes.")]
    private int budgetParCoupMs = BudgetEnJeuMs;

    /// <summary>Plafond lu par la prochaine recherche, borné par le maximum du moteur à l’écriture.</summary>
    public int Profondeur
    {
        get => profondeur;
        set => profondeur = Mathf.Clamp(value, 1, Minimax.ProfondeurMax);
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
        return Minimax.Chercher(position, Mathf.Clamp(profondeur, 1, Minimax.ProfondeurMax), annulation,
            utiliserTable, capaciteTable);
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

        int profondeurMax = Mathf.Clamp(profondeur, 1, Minimax.ProfondeurMax);
        int budgetMs = BudgetParCoupMs;
        PositionEchecs copie = position.Copier();

        return Task.Run(
            () => Minimax.ChercherAvecBudget(
                copie,
                budgetMs: budgetMs,
                profondeurMax: profondeurMax,
                annulation: annulation),
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
