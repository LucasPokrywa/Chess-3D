using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Minimax : MonoBehaviour
{
    public readonly struct Coup
    {
        public Vector2Int Depart { get; }
        public Vector2Int Arrivee { get; }

        public Coup(Vector2Int depart, Vector2Int arrivee)
        {
            Depart = depart;
            Arrivee = arrivee;
        }

        public override string ToString() =>
            $"{(char)('a' + Depart.x)}{Depart.y + 1}{(char)('a' + Arrivee.x)}{Arrivee.y + 1}";
    }

    public readonly struct Resultat
    {
        // Null si le joueur au trait n'a aucun coup légal (mat ou pat).
        public Coup? MeilleurCoup { get; }
        // Score du point de vue du joueur au trait dans la position de départ.
        public int Score { get; }
        public long NoeudsVisites { get; }
        public int Profondeur { get; }

        internal Resultat(Coup? coup, int score, long noeuds, int profondeur)
        {
            MeilleurCoup = coup;
            Score = score;
            NoeudsVisites = noeuds;
            Profondeur = profondeur;
        }
    }

    // Recherche synchrone, en demi-coups. Aucun composant Unity n'est créé ou modifié.
    // L'annulation lève OperationCanceledException et laisse la position intacte.
    public static Resultat Chercher(PositionEchecs position, int profondeur = 3,
        CancellationToken annulation = default)
    {
        if (position == null) throw new ArgumentNullException(nameof(position));
        if (profondeur < 1 || profondeur > 6)
            throw new ArgumentOutOfRangeException(nameof(profondeur), "Profondeur attendue : 1 à 6 demi-coups.");

        var recherche = new Recherche(annulation);
        return recherche.Executer(position.Copier(), profondeur);
    }

    // Tout l'état est propre à un appel : deux recherches ne partagent aucun compteur.
    private sealed class Recherche
    {
        private const int Infini = Evaluation.ScoreMat + 10000;
        private readonly CancellationToken annulation;
        private long noeuds;

        public Recherche(CancellationToken annulation) { this.annulation = annulation; }

        public Resultat Executer(PositionEchecs position, int profondeur)
        {
            annulation.ThrowIfCancellationRequested();
            noeuds++;
            var regles = new ReglesEchecs(position);
            List<Coup> coups = GenererCoups(position, regles);
            if (coups.Count == 0)
                return new Resultat(null, ScoreTerminal(position, regles, 0), noeuds, profondeur);

            Coup? meilleur = null;
            int alpha = -Infini;
            foreach (Coup coup in coups)
            {
                annulation.ThrowIfCancellationRequested();
                PositionEchecs copie = JouerSurCopie(position, coup);
                int score = -Explorer(copie, profondeur - 1, -Infini, -alpha, 1);
                if (!meilleur.HasValue || score > alpha)
                {
                    meilleur = coup;
                    alpha = score;
                }
            }
            return new Resultat(meilleur, alpha, noeuds, profondeur);
        }

        // Forme negamax de Minimax : chaque niveau maximise le score de son joueur.
        private int Explorer(PositionEchecs position, int profondeur, int alpha, int beta, int distance)
        {
            annulation.ThrowIfCancellationRequested();
            noeuds++;
            var regles = new ReglesEchecs(position);
            if (profondeur == 0)
            {
                // Vérifier le mat/pat même à l'horizon de recherche.
                if (!regles.HasAnyLegalMove(position.TourBlanc)) return ScoreTerminal(position, regles, distance);
                return Evaluation.Evaluer(position, position.TourBlanc);
            }
            List<Coup> coups = GenererCoups(position, regles);
            if (coups.Count == 0) return ScoreTerminal(position, regles, distance);
            int meilleur = -Infini;
            foreach (Coup coup in coups)
            {
                annulation.ThrowIfCancellationRequested();
                PositionEchecs copie = JouerSurCopie(position, coup);
                int score = -Explorer(copie, profondeur - 1, -beta, -alpha, distance + 1);
                if (score > meilleur) meilleur = score;
                if (score > alpha) alpha = score;
                if (alpha >= beta) break;
            }
            return meilleur;
        }

        private static int ScoreTerminal(PositionEchecs position, ReglesEchecs regles, int distance)
        {
            // Un mat proche est préféré ; en cas de défaite, retarder le mat.
            return regles.IsInCheck(position.TourBlanc) ? -Evaluation.ScoreMat + distance : 0;
        }

        private List<Coup> GenererCoups(PositionEchecs position, ReglesEchecs regles)
        {
            var coups = new List<Coup>();
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    annulation.ThrowIfCancellationRequested();
                    char piece = position.GetPiece(x, z);
                    if (piece == '.' || char.IsLower(piece) != position.TourBlanc) continue;
                    foreach (Vector2Int arrivee in regles.GetLegalMoves(x, z))
                        coups.Add(new Coup(new Vector2Int(x, z), arrivee));
                }
            // Captures et promotions en premier pour améliorer les coupures alpha-bêta.
            coups.Sort((a, b) =>
            {
                int ordre = Priorite(position, b).CompareTo(Priorite(position, a));
                if (ordre != 0) return ordre;
                int depart = (a.Depart.x * 8 + a.Depart.y).CompareTo(b.Depart.x * 8 + b.Depart.y);
                return depart != 0 ? depart :
                    (a.Arrivee.x * 8 + a.Arrivee.y).CompareTo(b.Arrivee.x * 8 + b.Arrivee.y);
            });
            return coups;
        }

        private static int Priorite(PositionEchecs position, Coup coup)
        {
            char piece = position.GetPiece(coup.Depart.x, coup.Depart.y);
            char cible = position.GetPiece(coup.Arrivee.x, coup.Arrivee.y);
            bool pion = char.ToUpperInvariant(piece) == 'P';
            int valeurCapture = Evaluation.ValeurPiece(cible);
            if (pion && coup.Arrivee == position.CaseEnPassant && cible == '.') valeurCapture = 100;
            int priorite = valeurCapture > 0 ? 10000 + 10 * valeurCapture - Evaluation.ValeurPiece(piece) : 0;
            if (pion && (coup.Arrivee.y == 0 || coup.Arrivee.y == 7)) priorite += 20000;
            return priorite;
        }

        private static PositionEchecs JouerSurCopie(PositionEchecs position, Coup coup)
        {
            PositionEchecs copie = position.Copier();
            // Les coups viennent de GetLegalMoves : inutile de les valider une seconde fois.
            // Ce moteur conserve la promotion automatique en dame de PositionEchecs.
            copie.AppliquerCoup(coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y);
            return copie;
        }
    }
}
