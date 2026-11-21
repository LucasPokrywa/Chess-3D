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

    public int Profondeur
    {
        get => profondeur;
        set => profondeur = Mathf.Clamp(value, 1, Minimax.ProfondeurMax);
    }

    public int BudgetParCoupMs
    {
        get => Mathf.Clamp(budgetParCoupMs, 1, BudgetMaxMs);
        set => budgetParCoupMs = Mathf.Clamp(value, 1, BudgetMaxMs);
    }

    public Minimax.Resultat ChoisirCoup(PositionEchecs position, CancellationToken annulation = default,
        bool utiliserTable = true, int capaciteTable = 500000)
    {
        return Minimax.Chercher(position, Mathf.Clamp(profondeur, 1, Minimax.ProfondeurMax), annulation,
            utiliserTable, capaciteTable);
    }

    // Appeler sur le thread Unity : capturer les réglages et la position avant le calcul.
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
    public Minimax.Resultat ChoisirCoupDepuisFen(string fen, CancellationToken annulation = default,
        bool utiliserTable = true, int capaciteTable = 500000)
    {
        return ChoisirCoup(PositionEchecs.DepuisFen(fen), annulation, utiliserTable, capaciteTable);
    }
}
