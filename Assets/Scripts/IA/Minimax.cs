using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Minimax : MonoBehaviour
{
    public const int ProfondeurMax = 10;

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
        public long ReutilisationsTable { get; }
        public int EntreesTable { get; }
        // Visites nécessitant un calcul, y compris les feuilles évaluées (hors retours de table).
        public long NoeudsDeveloppes { get; }
        public long CoupuresTable { get; }
        public long TempsMillisecondes { get; }
        public long RemplacementsTable { get; }

        internal Resultat(Coup? coup, int score, long noeuds, int profondeur,
            long reutilisations = 0, int entrees = 0, long developpes = 0,
            long coupures = 0, long temps = 0, long remplacements = 0)
        {
            MeilleurCoup = coup;
            Score = score;
            NoeudsVisites = noeuds;
            Profondeur = profondeur;
            ReutilisationsTable = reutilisations;
            EntreesTable = entrees;
            NoeudsDeveloppes = developpes;
            CoupuresTable = coupures;
            TempsMillisecondes = temps;
            RemplacementsTable = remplacements;
        }
    }

    // Recherche synchrone, en demi-coups. Aucun composant Unity n'est créé ou modifié.
    // L'annulation lève OperationCanceledException et laisse la position intacte.
    public static Resultat Chercher(PositionEchecs position, int profondeur = 3,
        CancellationToken annulation = default, bool utiliserTable = true, int capaciteTable = 500000)
    {
        if (position == null) throw new ArgumentNullException(nameof(position));
        if (profondeur < 1 || profondeur > ProfondeurMax)
            throw new ArgumentOutOfRangeException(nameof(profondeur), $"Profondeur attendue : 1 à {ProfondeurMax} demi-coups.");

        if (capaciteTable < 1) throw new ArgumentOutOfRangeException(nameof(capaciteTable));
        var recherche = new Recherche(annulation, utiliserTable, capaciteTable);
        return recherche.Executer(position.Copier(), profondeur);
    }

    private enum TypeScore { Exact, BorneInferieure, BorneSuperieure }

    private struct EntreeTransposition
    {
        public int Profondeur;
        public int Score;
        public TypeScore Type;
        public Coup? MeilleurCoup;
    }

    // Encodage exact de 64 cases (4 bits par case) et des états spéciaux.
    // Aucun FEN/string alloué par nœud. Le dictionnaire vérifie l'égalité complète,
    // donc une collision de GetHashCode ne confond pas deux positions.
    private readonly struct ClePosition : IEquatable<ClePosition>
    {
        private readonly ulong a, b, c, d;
        private readonly int etat;
        public ClePosition(ulong a, ulong b, ulong c, ulong d, int etat)
        {
            this.a = a; this.b = b; this.c = c; this.d = d; this.etat = etat;
        }
        public bool Equals(ClePosition autre) => a == autre.a && b == autre.b &&
            c == autre.c && d == autre.d && etat == autre.etat;
        public override bool Equals(object autre) => autre is ClePosition cle && Equals(cle);
        public override int GetHashCode()
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)a) * 16777619; h = (h ^ (uint)(a >> 32)) * 16777619;
                h = (h ^ (uint)b) * 16777619; h = (h ^ (uint)(b >> 32)) * 16777619;
                h = (h ^ (uint)c) * 16777619; h = (h ^ (uint)(c >> 32)) * 16777619;
                h = (h ^ (uint)d) * 16777619; h = (h ^ (uint)(d >> 32)) * 16777619;
                return (int)((h ^ (uint)etat) * 16777619);
            }
        }
    }

    // Tout l'état, y compris la table, est propre à un appel et à son thread.
    private sealed class Recherche
    {
        private const int Infini = Evaluation.ScoreMat + 10000;
        private readonly CancellationToken annulation;
        private long noeuds;
        private long reutilisations;
        private long developpes, coupures, remplacements;
        private readonly System.Diagnostics.Stopwatch chrono = new System.Diagnostics.Stopwatch();
        private readonly Dictionary<ClePosition, EntreeTransposition> table;
        private readonly Queue<ClePosition> feuilles;
        private readonly Coup?[,] coupsKiller = new Coup?[ProfondeurMax + 1, 2];
        private readonly int[,,] historique = new int[2, 64, 64];
        private readonly int capaciteTable;

        public Recherche(CancellationToken annulation, bool utiliserTable, int capaciteTable)
        {
            this.annulation = annulation;
            this.capaciteTable = capaciteTable;
            if (utiliserTable)
            {
                table = new Dictionary<ClePosition, EntreeTransposition>();
                feuilles = new Queue<ClePosition>();
            }
        }

        public Resultat Executer(PositionEchecs position, int profondeur)
        {
            annulation.ThrowIfCancellationRequested();
            chrono.Start();
            noeuds++;
            developpes++;
            var regles = new ReglesEchecs(position);
            List<Coup> coups = GenererCoups(position, regles, 0);
            if (coups.Count == 0)
                return ResultatFinal(null, ScoreTerminal(position, regles, 0), profondeur);

            Coup? meilleur = null;
            int alpha = -Infini;
            foreach (Coup coup in coups)
            {
                annulation.ThrowIfCancellationRequested();
                int score = EvaluerCoup(position, coup, profondeur - 1, -Infini, -alpha, 1);
                if (!meilleur.HasValue || score > alpha)
                {
                    meilleur = coup;
                    alpha = score;
                }
            }
            return ResultatFinal(meilleur, alpha, profondeur);
        }

        private Resultat ResultatFinal(Coup? meilleur, int score, int profondeur)
        {
            chrono.Stop();
            return new Resultat(meilleur, score, noeuds, profondeur, reutilisations, table?.Count ?? 0,
                developpes, coupures, chrono.ElapsedMilliseconds, remplacements);
        }

        // Forme negamax de Minimax : chaque niveau maximise le score de son joueur.
        private int Explorer(PositionEchecs position, int profondeur, int alpha, int beta, int distance)
        {
            annulation.ThrowIfCancellationRequested();
            noeuds++;
            ClePosition cle = table != null ? CreerCle(position) : default;
            Coup? coupMemorise = null;
            if (table != null && table.TryGetValue(cle, out EntreeTransposition entree))
            {
                coupMemorise = entree.MeilleurCoup;
                if (entree.Profondeur >= profondeur)
                {
                    reutilisations++;
                    int scoreTable = LireScore(entree.Score, distance);
                    if (entree.Type == TypeScore.Exact) { coupures++; return scoreTable; }
                    if (entree.Type == TypeScore.BorneInferieure) alpha = Math.Max(alpha, scoreTable);
                    else beta = Math.Min(beta, scoreTable);
                    if (alpha >= beta) { coupures++; return scoreTable; }
                }
            }
            // Conserver la fenêtre réellement explorée après resserrement par la table.
            int alphaInitial = alpha, betaInitial = beta;
            developpes++;
            var regles = new ReglesEchecs(position);
            if (profondeur == 0)
            {
                // Vérifier le mat/pat même à l'horizon de recherche.
                int score = !regles.HasAnyLegalMove(position.TourBlanc)
                    ? ScoreTerminal(position, regles, distance)
                    : Evaluation.Evaluer(position, position.TourBlanc);
                Memoriser(cle, profondeur, score, TypeScore.Exact, null, distance);
                return score;
            }
            List<Coup> coups = GenererCoups(position, regles, distance);
            if (coups.Count == 0)
            {
                int score = ScoreTerminal(position, regles, distance);
                Memoriser(cle, profondeur, score, TypeScore.Exact, null, distance);
                return score;
            }
            PrioriserCoup(coups, coupMemorise);
            int meilleur = -Infini;
            Coup? meilleurCoup = null;
            foreach (Coup coup in coups)
            {
                annulation.ThrowIfCancellationRequested();
                int score = EvaluerCoup(position, coup, profondeur - 1, -beta, -alpha, distance + 1);
                if (score > meilleur)
                {
                    meilleur = score;
                    meilleurCoup = coup;
                }
                if (score > alpha) alpha = score;
                if (alpha >= beta)
                {
                    MemoriserCoupureCalme(position, coup, profondeur, distance);
                    break;
                }
            }
            TypeScore type = meilleur <= alphaInitial ? TypeScore.BorneSuperieure :
                meilleur >= betaInitial ? TypeScore.BorneInferieure : TypeScore.Exact;
            Memoriser(cle, profondeur, meilleur, type, meilleurCoup, distance);
            return meilleur;
        }

        private static ClePosition CreerCle(PositionEchecs position)
        {
            ulong a = 0, b = 0, c = 0, d = 0;
            const string pieces = "pcftdrPCFTDR";
            for (int z = 0; z < 8; z++)
                for (int x = 0; x < 8; x++)
                {
                    char piece = position.GetPiece(x, z);
                    if (piece == '.') continue;
                    int code = pieces.IndexOf(piece) + 1;
                    if (code == 0) throw new InvalidOperationException("Pièce logique inconnue.");
                    int index = z * 8 + x;
                    ulong valeur = (ulong)code << ((index & 15) * 4);
                    switch (index >> 4)
                    {
                        case 0: a |= valeur; break;
                        case 1: b |= valeur; break;
                        case 2: c |= valeur; break;
                        default: d |= valeur; break;
                    }
                }
            int ep = position.CaseEnPassant.x < 0 ? 0 :
                position.CaseEnPassant.x + position.CaseEnPassant.y * 8 + 1;
            // Même information que les quatre premiers champs FEN.
            // Ajouter l'historique si la nulle par répétition/50 coups est implémentée.
            int etat = (position.TourBlanc ? 1 : 0) | (position.DroitsRoque << 1) | (ep << 5);
            return new ClePosition(a, b, c, d, etat);
        }

        private void Memoriser(ClePosition cle, int profondeur, int score, TypeScore type,
            Coup? coup, int distance)
        {
            if (table == null) return;
            bool existe = table.TryGetValue(cle, out EntreeTransposition ancienne);
            if (existe && (ancienne.Profondeur > profondeur ||
                (ancienne.Profondeur == profondeur && ancienne.Type == TypeScore.Exact && type != TypeScore.Exact)))
                return;
            if (!existe && table.Count >= capaciteTable)
            {
                if (profondeur == 0) return;
                // Les feuilles ne doivent pas empêcher de stocker les sous-arbres coûteux.
                bool placeLiberee = false;
                while (feuilles.Count > 0)
                {
                    ClePosition victime = feuilles.Dequeue();
                    if (table.TryGetValue(victime, out EntreeTransposition candidate) && candidate.Profondeur == 0)
                    {
                        table.Remove(victime);
                        remplacements++;
                        placeLiberee = true;
                        break;
                    }
                }
                if (!placeLiberee) return;
            }
            table[cle] = new EntreeTransposition {
                Profondeur = profondeur, Score = StockerScore(score, distance), Type = type, MeilleurCoup = coup
            };
            if (!existe && profondeur == 0) feuilles.Enqueue(cle);
        }

        private static int StockerScore(int score, int distance)
        {
            if (score >= Evaluation.ScoreMat - 1000) return score + distance;
            if (score <= -Evaluation.ScoreMat + 1000) return score - distance;
            return score;
        }

        private static int LireScore(int score, int distance)
        {
            if (score >= Evaluation.ScoreMat - 1000) return score - distance;
            if (score <= -Evaluation.ScoreMat + 1000) return score + distance;
            return score;
        }

        private static void PrioriserCoup(List<Coup> coups, Coup? memorise)
        {
            if (!memorise.HasValue) return;
            for (int i = 0; i < coups.Count; i++)
                if (coups[i].Depart == memorise.Value.Depart && coups[i].Arrivee == memorise.Value.Arrivee)
                {
                    Coup coup = coups[i];
                    coups.RemoveAt(i);
                    coups.Insert(0, coup);
                    return;
                }
        }

        private static int ScoreTerminal(PositionEchecs position, ReglesEchecs regles, int distance)
        {
            // Un mat proche est préféré ; en cas de défaite, retarder le mat.
            return regles.IsInCheck(position.TourBlanc) ? -Evaluation.ScoreMat + distance : 0;
        }

        private List<Coup> GenererCoups(PositionEchecs position, ReglesEchecs regles, int distance)
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
                int ordre = PrioriteRecherche(position, b, distance).CompareTo(PrioriteRecherche(position, a, distance));
                if (ordre != 0) return ordre;
                int depart = (a.Depart.x * 8 + a.Depart.y).CompareTo(b.Depart.x * 8 + b.Depart.y);
                return depart != 0 ? depart :
                    (a.Arrivee.x * 8 + a.Arrivee.y).CompareTo(b.Arrivee.x * 8 + b.Arrivee.y);
            });
            return coups;
        }

        private int PrioriteRecherche(PositionEchecs position, Coup coup, int distance)
        {
            int tactique = Priorite(position, coup);
            if (tactique != 0) return tactique;
            if (MemeCoup(coupsKiller[distance, 0], coup)) return 9000;
            if (MemeCoup(coupsKiller[distance, 1], coup)) return 8000;
            return historique[position.TourBlanc ? 0 : 1,
                coup.Depart.y * 8 + coup.Depart.x, coup.Arrivee.y * 8 + coup.Arrivee.x];
        }

        private void MemoriserCoupureCalme(PositionEchecs position, Coup coup, int profondeur, int distance)
        {
            if (Priorite(position, coup) != 0) return;
            if (!MemeCoup(coupsKiller[distance, 0], coup))
            {
                coupsKiller[distance, 1] = coupsKiller[distance, 0];
                coupsKiller[distance, 0] = coup;
            }
            int couleur = position.TourBlanc ? 0 : 1;
            int depart = coup.Depart.y * 8 + coup.Depart.x;
            int arrivee = coup.Arrivee.y * 8 + coup.Arrivee.x;
            historique[couleur, depart, arrivee] = Math.Min(6000,
                historique[couleur, depart, arrivee] + profondeur * profondeur);
        }

        private static bool MemeCoup(Coup? a, Coup b) => a.HasValue &&
            a.Value.Depart == b.Depart && a.Value.Arrivee == b.Arrivee;

        private static int Priorite(PositionEchecs position, Coup coup)
        {
            char piece = position.GetPiece(coup.Depart.x, coup.Depart.y);
            char cible = position.GetPiece(coup.Arrivee.x, coup.Arrivee.y);
            bool pion = char.ToUpperInvariant(piece) == 'P';
            int valeurCapture = Evaluation.ValeurPiece(cible);
            if (pion && coup.Arrivee == position.CaseEnPassant && cible == '.') valeurCapture = 100;
            int priorite = valeurCapture > 0 ?
                10000 + 10 * valeurCapture - Math.Min(1000, Evaluation.ValeurPiece(piece)) : 0;
            if (pion && (coup.Arrivee.y == 0 || coup.Arrivee.y == 7)) priorite += 20000;
            return priorite;
        }

        private int EvaluerCoup(PositionEchecs position, Coup coup, int profondeur,
            int alpha, int beta, int distance)
        {
            PositionEchecs.EtatAvantCoup etat = position.JouerTemporairement(
                coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y);
            try { return -Explorer(position, profondeur, alpha, beta, distance); }
            finally { position.AnnulerCoup(etat); }
        }
    }
}
