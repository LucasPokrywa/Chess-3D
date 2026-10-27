using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Façade de calcul uniquement : aucun Start/Update, aucune référence au contrôleur.
// Appeler explicitement ChoisirCoup(position) pour obtenir un résultat sans jouer.
public class ChessIA : MonoBehaviour
{
    [SerializeField, Range(1, 6)] private int profondeur = 3;

    public int Profondeur
    {
        get => profondeur;
        set => profondeur = Mathf.Clamp(value, 1, 6);
    }

    public Minimax.Resultat ChoisirCoup(PositionEchecs position, CancellationToken annulation = default)
    {
        return Minimax.Chercher(position, Mathf.Clamp(profondeur, 1, 6), annulation);
    }

    // Appeler sur le thread Unity : capturer les réglages et la position avant le calcul.
    public Task<Minimax.Resultat> ChoisirCoupAsync(PositionEchecs position, CancellationToken annulation = default)
    {
        if (position == null) throw new System.ArgumentNullException(nameof(position));
        int profondeurRecherche = Mathf.Clamp(profondeur, 1, 6);
        PositionEchecs copie = position.Copier();
        return Task.Run(() => Minimax.Chercher(copie, profondeurRecherche, annulation), annulation);
    }

    // Variante utile pour tester l'IA avec un FEN, sans importer celui-ci dans la scène.
    public Minimax.Resultat ChoisirCoupDepuisFen(string fen, CancellationToken annulation = default)
    {
        return ChoisirCoup(PositionEchecs.DepuisFen(fen), annulation);
    }
}
