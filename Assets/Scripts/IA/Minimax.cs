using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Minimax : MonoBehaviour
{
    public const int ProfondeurMax = 10;
    public const bool PvsParDefaut = true;

    public readonly struct Coup
    {
        public Vector2Int Depart { get; }
        public Vector2Int Arrivee { get; }

        /// <summary>
        /// Construit un couple de cases logiques. C’est au générateur ou à l’appelant de valider le coup.
        /// </summary>
        /// <param name="depart">Case de départ exprimée en coordonnées logiques.</param>
        /// <param name="arrivee">Case de destination exprimée en coordonnées logiques.</param>
        public Coup(Vector2Int depart, Vector2Int arrivee)
        {
            Depart = depart;
            Arrivee = arrivee;
        }

        /// <summary>
        /// Convertit les coordonnées internes en deux cases successives, utiles dans les traces et les tests.
        /// </summary>
        /// <returns>Notation de quatre caractères telle que e2e4.</returns>
        public override string ToString() =>
            $"{(char)('a' + Depart.x)}{Depart.y + 1}{(char)('a' + Arrivee.x)}{Arrivee.y + 1}";
    }

    public readonly struct Resultat
    {
        // Null si le joueur au trait n'a aucun coup légal (mat ou pat).
        public Coup? MeilleurCoup { get; }
        // Score du point de vue du joueur au trait dans la position de départ.
        public int Score { get; }
        // Principal + quiétude, toutes itérations comprises (y compris celle interrompue).
        public long NoeudsVisites { get; }
        // Une feuille transférée à la quiétude compte uniquement en quiétude.
        public long NoeudsRecherchePrincipale { get; }
        // Inclut les enfants évalués après une évasion à la limite de quiétude.
        public long NoeudsQuietude { get; }
        public long RelancesPvs { get; }
        public long CoupuresAvantGeneration { get; }
        // Temps cumulé depuis le début de la recherche budgétée, 0 si aucune itération validée.
        public double TempsDerniereIterationMs { get; }
        // Le budget a expiré avant de valider l'itération suivante ; pas une annulation externe.
        public bool InterrompuParBudget { get; }
        public int Profondeur { get; }
        public long ReutilisationsTable { get; }
        public int EntreesTable { get; }
        // Visites nécessitant un calcul, y compris les feuilles évaluées (hors retours de table).
        public long NoeudsDeveloppes { get; }
        public long CoupuresTable { get; }
        public long TempsMillisecondes { get; }
        public long RemplacementsTable { get; }

        /// <summary>
        /// Réunit le résultat validé et les compteurs du travail réellement fourni. Les nœuds peuvent inclure une itération abandonnée.
        /// </summary>
        /// <param name="coup">Coup choisi, ou absence de coup pour une position terminale.</param>
        /// <param name="score">Valeur ou borne issue de la recherche, en centipions hors mat.</param>
        /// <param name="noeuds">Nombre total de visites, principal et quiétude inclus.</param>
        /// <param name="profondeur">Dernière profondeur validée ou profondeur fixe de l’appel.</param>
        /// <param name="reutilisations">Nombre de lectures de table de profondeur suffisante.</param>
        /// <param name="entrees">Nombre final d’entrées présentes dans la table.</param>
        /// <param name="developpes">Nombre de visites ayant nécessité un calcul.</param>
        /// <param name="coupures">Nombre de retours directs obtenus via la table.</param>
        /// <param name="temps">Durée interne écoulée en millisecondes.</param>
        /// <param name="remplacements">Nombre de feuilles retirées pour récupérer de la capacité.</param>
        /// <param name="principaux">Nombre de visites de la recherche normale.</param>
        /// <param name="quietude">Nombre de visites en quiétude, évasions à la limite incluses.</param>
        /// <param name="interrompuParBudget">Vrai si le délai a interrompu une itération avant sa validation.</param>
        /// <param name="relancesPvs">Nombre de confirmations en fenêtre normale après un essai PVS.</param>
        /// <param name="tempsDerniereIterationMs">Temps cumulé jusqu’à la dernière validation, zéro si aucune.</param>
        /// <param name="coupuresAvantGeneration">Nombre de coupures obtenues en jouant le coup de table avant la liste complète.</param>
        internal Resultat(Coup? coup, int score, long noeuds, int profondeur,
            long reutilisations = 0, int entrees = 0, long developpes = 0,
            long coupures = 0, long temps = 0, long remplacements = 0,
            long principaux = 0, long quietude = 0, bool interrompuParBudget = false, long relancesPvs = 0,
            double tempsDerniereIterationMs = 0, long coupuresAvantGeneration = 0)
        {
            MeilleurCoup = coup;
            Score = score;
            NoeudsVisites = noeuds;
            NoeudsRecherchePrincipale = principaux;
            NoeudsQuietude = quietude;
            RelancesPvs = relancesPvs;
            TempsDerniereIterationMs = tempsDerniereIterationMs;
            CoupuresAvantGeneration = coupuresAvantGeneration;
            InterrompuParBudget = interrompuParBudget;
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
    /// <summary>
    /// Vérifie les limites et lance une recherche directe à profondeur fixe sur une copie. Une annulation externe remonte à l’appelant.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
    /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
    /// <param name="utiliserTable">Autorise la lecture et l’écriture de la table de transposition.</param>
    /// <param name="capaciteTable">Nombre maximal d’entrées de la table de cet appel.</param>
    /// <param name="utiliserPvs">Active les fenêtres étroites PVS aux nœuds internes.</param>
    /// <returns>Résultat de la profondeur demandée ou résultat terminal si le trait n’a aucun coup.</returns>
    public static Resultat Chercher(PositionEchecs position, int profondeur = 3,
        CancellationToken annulation = default, bool utiliserTable = true, int capaciteTable = 500000,
        bool utiliserPvs = PvsParDefaut)
    {
        if (position == null) throw new ArgumentNullException(nameof(position));
        if (profondeur < 1 || profondeur > ProfondeurMax)
            throw new ArgumentOutOfRangeException(nameof(profondeur), $"Profondeur attendue : 1 à {ProfondeurMax} demi-coups.");

        if (capaciteTable < 1) throw new ArgumentOutOfRangeException(nameof(capaciteTable));
        var recherche = new Recherche(annulation, utiliserTable, capaciteTable, utiliserPvs);
        return recherche.Executer(position.Copier(), profondeur);
    }
    /// <summary>
    /// Prépare une recherche à approfondissement itératif. Chaque appel dispose d’une table neuve de 500 000 entrées et d’une copie de la position.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="budgetMs">Budget total du moteur, en millisecondes strictement positives.</param>
    /// <param name="profondeurMax">Plafond de recherche normale en demi-coups.</param>
    /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
    /// <param name="utiliserPvs">Active les fenêtres étroites PVS aux nœuds internes.</param>
    /// <returns>Dernière profondeur complète, coup légal de secours à P0 ou résultat terminal.</returns>
    public static Resultat ChercherAvecBudget(
            PositionEchecs position,
            int budgetMs = 500,
            int profondeurMax = ProfondeurMax,
            CancellationToken annulation = default,
            bool utiliserPvs = PvsParDefaut)
    {
        if (position == null)
            throw new ArgumentNullException(nameof(position));

        if (budgetMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(budgetMs));

        if (profondeurMax < 1 || profondeurMax > ProfondeurMax)
            throw new ArgumentOutOfRangeException(nameof(profondeurMax));

        annulation.ThrowIfCancellationRequested();

        var recherche = new Recherche(annulation, true, 500000, utiliserPvs);
        return recherche.ExecuterAvecBudget(position.Copier(), profondeurMax, budgetMs);
    }
    private enum TypeScore { Exact, BorneInferieure, BorneSuperieure }

    private struct EntreeTransposition
    {
        public int Profondeur;
        public int Score;
        public TypeScore Type;
        public Coup? MeilleurCoup;
    }

    private readonly struct CoupTrie
    {
        public readonly Coup Coup;
        public readonly int Priorite;
        public readonly bool EstCalme;
        /// <summary>
        /// Associe au coup ses données de tri précalculées. La distinction calme ou tactique guide le tri et les heuristiques de coupure.
        /// </summary>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <param name="priorite">Valeur de tri précalculée pour ce candidat.</param>
        /// <param name="estCalme">Vrai si le coup ne capture ni ne promeut.</param>
        public CoupTrie(Coup coup, int priorite, bool estCalme)
        {
            Coup = coup; Priorite = priorite; EstCalme = estCalme;
        }
    }

    // Encodage exact de 64 cases (4 bits par case) et des états spéciaux.
    // Aucun FEN/string alloué par nœud. Le dictionnaire vérifie l'égalité complète,
    // donc une collision de GetHashCode ne confond pas deux positions.
    private readonly struct ClePosition : IEquatable<ClePosition>
    {
        private readonly ulong a, b, c, d;
        private readonly int etat;
        /// <summary>
        /// Assemble les quatre blocs du plateau et l’état utile à la légalité dans une clé de valeur.
        /// </summary>
        /// <param name="a">Premier bloc de seize cases de la grille.</param>
        /// <param name="b">Deuxième bloc de seize cases de la grille.</param>
        /// <param name="c">Troisième bloc de seize cases de la grille.</param>
        /// <param name="d">Quatrième bloc de seize cases de la grille.</param>
        /// <param name="etat">Bits du trait, des droits de roque et de la case en passant.</param>
        public ClePosition(ulong a, ulong b, ulong c, ulong d, int etat)
        {
            this.a = a; this.b = b; this.c = c; this.d = d; this.etat = etat;
        }
        /// <summary>
        /// Compare la totalité du plateau codé et de l’état utile, indépendamment du hachage.
        /// </summary>
        /// <param name="autre">Valeur ou objet à comparer à cette clé.</param>
        /// <returns>Vrai lorsque les deux valeurs représentent la même clé.</returns>
        public bool Equals(ClePosition autre) => a == autre.a && b == autre.b &&
            c == autre.c && d == autre.d && etat == autre.etat;
        /// <summary>
        /// Vérifie le type de l’objet puis délègue à la comparaison forte de la clé.
        /// </summary>
        /// <param name="autre">Valeur ou objet à comparer à cette clé.</param>
        /// <returns>Vrai lorsque les deux valeurs représentent la même clé.</returns>
        public override bool Equals(object autre) => autre is ClePosition cle && Equals(cle);
        /// <summary>
        /// Mélange les blocs et l’état pour distribuer les entrées du dictionnaire. Une collision est départagée par Equals.
        /// </summary>
        /// <returns>Entier de hachage, distinct de l’identité complète de la position.</returns>
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
        private const int ExtensionMaxQuietude = 4;
        private readonly CancellationToken annulation;
        private long noeuds;
        private long noeudsPrincipaux, noeudsQuietude, relancesPvs;
        private bool interrompuParBudget;
        private double tempsDerniereIterationMs;
        private long coupuresAvantGeneration;
        private ReglesEchecs regles;
        private EvaluationIncrementale evaluationIncrementale;
        private long reutilisations;
        private long developpes, coupures, remplacements;
        private readonly System.Diagnostics.Stopwatch chrono = new System.Diagnostics.Stopwatch();
        private readonly Dictionary<ClePosition, EntreeTransposition> table;
        private readonly Queue<ClePosition> feuilles;
        private readonly Coup?[,] coupsKiller = new Coup?[ProfondeurMax + ExtensionMaxQuietude + 1, 2];
        private readonly List<CoupTrie>[] coupsParNiveau = new List<CoupTrie>[ProfondeurMax + ExtensionMaxQuietude + 1]; 
        private readonly int[,,] historique = new int[2, 64, 64];
        // Un tampon par niveau : les appels enfants ne modifient pas la liste du parent.
        private readonly List<Vector2Int> tamponCoupsLegaux = new List<Vector2Int>(32);
        private static readonly Comparison<CoupTrie> ComparaisonCoups = ComparerCoups;
        private readonly int capaciteTable;
        private readonly bool utiliserPvs;

        /// <summary>
        /// Recompose les sommes de départ avant tout déplacement temporaire. Le plateau reste la référence de l’état.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        private void InitialiserEvaluation(PositionEchecs position)
        {
            evaluationIncrementale = EvaluationIncrementale.DepuisPosition(position);
        }

        /// <summary>
        /// Initialise une recherche avec les options de table demandées et la PVS par défaut.
        /// </summary>
        /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
        /// <param name="utiliserTable">Autorise la lecture et l’écriture de la table de transposition.</param>
        /// <param name="capaciteTable">Nombre maximal d’entrées de la table de cet appel.</param>
        public Recherche(CancellationToken annulation, bool utiliserTable, int capaciteTable)
            : this(annulation, utiliserTable, capaciteTable, PvsParDefaut)
        {
        }

        /// <summary>
        /// Crée l’état propre à un seul appel. Les tampons, heuristiques, compteurs et entrées de table ne sont pas partagés entre tâches.
        /// </summary>
        /// <param name="annulation">Jeton d’annulation externe, distinct de l’expiration du budget.</param>
        /// <param name="utiliserTable">Autorise la lecture et l’écriture de la table de transposition.</param>
        /// <param name="capaciteTable">Nombre maximal d’entrées de la table de cet appel.</param>
        /// <param name="utiliserPvs">Active les fenêtres étroites PVS aux nœuds internes.</param>
        public Recherche(CancellationToken annulation, bool utiliserTable, int capaciteTable, bool utiliserPvs)
        {
            this.annulation = annulation;
            this.capaciteTable = capaciteTable;
            this.utiliserPvs = utiliserPvs;
            if (utiliserTable)
            {
                table = new Dictionary<ClePosition, EntreeTransposition>();
                feuilles = new Queue<ClePosition>();
            }
        }

        /// <summary>
        /// Cherche directement à la profondeur demandée, sans boucle d’approfondissement. Le chronomètre couvre l’initialisation et le calcul.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <returns>Meilleur coup de cette passe ou score terminal.</returns>
        public Resultat Executer(PositionEchecs position, int profondeur)
        {
            annulation.ThrowIfCancellationRequested();
            chrono.Start();
            InitialiserEvaluation(position);
            noeuds++;
            noeudsPrincipaux++;
            developpes++;

            regles = new ReglesEchecs(position);
            List<CoupTrie> coups = GenererCoups(position, regles, 0);

            if (coups.Count == 0)
                return ResultatFinal(null, ScoreTerminal(position, regles, 0), profondeur);

            var (meilleurCoup, score) = ChercherProfondeur(position, coups, profondeur);
            return ResultatFinal(meilleurCoup, score, profondeur);
        }

        private int budgetMs;

        private sealed class BudgetExpireException : Exception
        {
        }

        /// <summary>
        /// Observe d’abord l’annulation externe, puis l’échéance si un budget est actif. Les exceptions remontent à travers les blocs de restauration.
        /// </summary>
        private void VerifierArret()
        {
            annulation.ThrowIfCancellationRequested();

            if (budgetMs > 0 && chrono.Elapsed.TotalMilliseconds >= budgetMs)
                throw new BudgetExpireException();
        }

        /// <summary>
        /// Conserve un coup légal initial, puis valide chaque itération seulement après une dernière vérification de l’échéance. Une expiration interne laisse intact le dernier résultat validé.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="profondeurMax">Plafond de recherche normale en demi-coups.</param>
        /// <param name="budgetMillisecondes">Durée totale de la recherche itérative, en millisecondes.</param>
        /// <returns>Résultat de la dernière itération entière, du secours à P0 ou d’une racine terminale.</returns>
        /// <remarks>La mesure de la dernière validation est distincte de la durée totale. L’annulation externe n’est pas traitée comme une expiration du budget.</remarks>
        public Resultat ExecuterAvecBudget(
            PositionEchecs position, int profondeurMax, int budgetMillisecondes)
        {
            annulation.ThrowIfCancellationRequested();
            budgetMs = budgetMillisecondes;
            chrono.Start();
            InitialiserEvaluation(position);

            regles = new ReglesEchecs(position);
            List<CoupTrie> coups = GenererCoups(position, regles, 0);

            if (coups.Count == 0)
            {
                noeuds++;
                noeudsPrincipaux++;
                developpes++;
                return ResultatFinal(null, ScoreTerminal(position, regles, 0), 0);
            }

            // Solution de secours si la profondeur 1 ne se termine pas.
            Coup? meilleurCoup = coups[0].Coup;
            int meilleurScore = evaluationIncrementale.Evaluer(position, position.TourBlanc);
            int profondeurTerminee = 0;

            try
            {
                for (int profondeur = 1; profondeur <= profondeurMax; profondeur++)
                {
                    VerifierArret();
                    noeuds++;
                    noeudsPrincipaux++;
                    developpes++;

                    if (profondeur > 1)
                        coups = GenererCoups(position, regles, 0, meilleurCoup);

                    var (coup, score) = ChercherProfondeur(position, coups, profondeur);
                    VerifierArret();

                    meilleurCoup = coup;
                    meilleurScore = score;
                    profondeurTerminee = profondeur;
                    // La publication intervient après la vérification du délai. Une itération interrompue ne remplace jamais ce résultat.
                    tempsDerniereIterationMs = chrono.Elapsed.TotalMilliseconds;
                }
            }
            catch (BudgetExpireException)
            {
                interrompuParBudget = true;
                // Le résultat de la dernière itération complète est déjà enregistré.
            }

            annulation.ThrowIfCancellationRequested();
            return ResultatFinal(meilleurCoup, meilleurScore, profondeurTerminee);
        }

        /// <summary>
        /// Compare les coups de racine en conservant le meilleur score courant dans alpha. La fenêtre de l’enfant reste de la forme (-infini, -alpha), sans test PVS distinct à cette racine.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="coups">Coups de racine déjà ordonnés pour cette itération.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <returns>Coup retenu et score de même point de vue que la position de départ.</returns>
        private (Coup? MeilleurCoup, int Score) ChercherProfondeur(
            PositionEchecs position, List<CoupTrie> coups, int profondeur)
        {
            Coup? meilleur = null;
            int alpha = -Infini;

            foreach (CoupTrie candidat in coups)
            {
                VerifierArret();

                Coup coup = candidat.Coup;
                int score = EvaluerCoup(position, coup, profondeur - 1, -Infini, -alpha, 1);

                if (!meilleur.HasValue || score > alpha)
                {
                    meilleur = coup;
                    alpha = score;
                }
            }

            return (meilleur, alpha);
        }

        /// <summary>
        /// Arrête le chronomètre et capture le résultat ainsi que l’état des compteurs accumulés.
        /// </summary>
        /// <param name="meilleur">Coup sélectionné, éventuellement absent dans une position terminale.</param>
        /// <param name="score">Valeur ou borne issue de la recherche, en centipions hors mat.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <returns>Valeur qui peut être transférée de la tâche de calcul au thread Unity.</returns>
        private Resultat ResultatFinal(Coup? meilleur, int score, int profondeur)
        {
            chrono.Stop();
            return new Resultat(meilleur, score, noeuds, profondeur, reutilisations, table?.Count ?? 0,
                developpes, coupures, chrono.ElapsedMilliseconds, remplacements,
                noeudsPrincipaux, noeudsQuietude, interrompuParBudget, relancesPvs,
                tempsDerniereIterationMs, coupuresAvantGeneration);
        }

        // Forme negamax de Minimax : chaque niveau maximise le score de son joueur.
        /// <summary>
        /// Cherche un nœud Negamax du point de vue du camp au trait. Il exploite les bornes de la table, essaye son coup légal en priorité, puis applique alpha-bêta et la PVS interne. À profondeur zéro, la visite est déléguée à la quiétude.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <param name="alpha">Borne inférieure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="beta">Seuil de coupure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <returns>Score exact si la recherche le détermine dans la fenêtre utile, sinon borne supérieure ou inférieure.</returns>
        /// <remarks>En Negamax, l’enfant reçoit (-bêta, -alpha) et son résultat change de signe. En PVS, la fenêtre enfant devient (-alpha - 1, -alpha). Seul un dépassement strict d’alpha en dessous de bêta déclenche une confirmation.</remarks>
        private int Explorer(PositionEchecs position, int profondeur, int alpha, int beta, int distance)
        {
            if (profondeur == 0)
                return ChercherQuietude(position, alpha, beta, distance, ExtensionMaxQuietude);
            VerifierArret();
            noeuds++;
            noeudsPrincipaux++;
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
            // Une borne de table peut fermer la fenêtre sans fournir une valeur exacte.
            // Conserver la fenêtre réellement explorée après resserrement par la table.
            int alphaInitial = alpha, betaInitial = beta;
            developpes++;
            int meilleur = -Infini;
            Coup? meilleurCoup = null;
            // Le premier coup se cherche dans la fenêtre courante avant de générer les autres.
            if (coupMemorise is Coup coupTable && regles.EstCoupLegalAuTrait(coupTable.Depart, coupTable.Arrivee))
            {
                VerifierArret();
                meilleur = EvaluerCoup(position, coupTable, profondeur - 1, -beta, -alpha, distance + 1);
                meilleurCoup = coupTable;
                alpha = Math.Max(alpha, meilleur);
                if (alpha >= beta)
                {
                    if (Priorite(position, coupTable) == 0)
                        MemoriserCoupureCalme(position, coupTable, profondeur, distance);
                    Memoriser(cle, profondeur, meilleur, TypeScore.BorneInferieure, coupTable, distance);
                    coupuresAvantGeneration++;
                    return meilleur;
                }
            }

            Coup? coupDejaCherche = meilleurCoup;
            List<CoupTrie> coups = GenererCoups(position, regles, distance);
            if (coups.Count == 0 && !coupDejaCherche.HasValue)
            {
                int score = ScoreTerminal(position, regles, distance);
                Memoriser(cle, profondeur, score, TypeScore.Exact, null, distance);
                return score;
            }
            foreach (CoupTrie candidat in coups)
            {
                Coup coup = candidat.Coup;
                if (MemeCoup(coupDejaCherche, coup)) continue;
                VerifierArret();
                int score;
                if (!utiliserPvs || !meilleurCoup.HasValue || beta == alpha + 1)
                {
                    score = EvaluerCoup(position, coup, profondeur - 1, -beta, -alpha, distance + 1);
                }
                else
                {
                    // Fenêtre nulle en negamax : tester si le coup améliore alpha.
                    score = EvaluerCoup(position, coup, profondeur - 1, -alpha - 1, -alpha, distance + 1);
                    if (score > alpha && score < beta)
                    {
                        VerifierArret();
                        relancesPvs++;
                        score = EvaluerCoup(position, coup, profondeur - 1, -beta, -alpha, distance + 1);
                    }
                }
                if (score > meilleur)
                {
                    meilleur = score;
                    meilleurCoup = coup;
                }
                if (score > alpha) alpha = score;
                if (alpha >= beta)
                {
                    if (candidat.EstCalme) MemoriserCoupureCalme(position, coup, profondeur, distance);
                    break;
                }
            }
            // Un échec bas donne une borne supérieure. Une coupure bêta donne une borne inférieure.
            TypeScore type = meilleur <= alphaInitial ? TypeScore.BorneSuperieure :
                meilleur >= betaInitial ? TypeScore.BorneInferieure : TypeScore.Exact;
            Memoriser(cle, profondeur, meilleur, type, meilleurCoup, distance);
            return meilleur;
        }

        /// <summary>
        /// Stabilise une feuille par les captures et promotions ou, si le roi est en échec, par toutes les évasions. À la limite en échec, examine une réponse puis note son état terminal ou statique, sans poursuivre récursivement.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="alpha">Borne inférieure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="beta">Seuil de coupure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <param name="demiCoupsRestants">Demi-coups récursifs de quiétude encore disponibles.</param>
        /// <returns>Score ou borne du camp au trait. Le score statique peut être conservé hors échec si les règles de fin de partie le permettent.</returns>
        /// <remarks>L’entrée en quiétude ne consomme aucun demi-coup supplémentaire. Chaque déplacement récursif réduit le compteur d’une unité. Les positions issues d’une évasion bornée comptent aussi comme visites de quiétude.</remarks>
        private int ChercherQuietude(
            PositionEchecs position,
            int alpha,
            int beta,
            int distance,
            int demiCoupsRestants)
        {
            VerifierArret();
            noeuds++;
            noeudsQuietude++;
            developpes++;

            bool enEchec = regles.IsInCheck(position.TourBlanc);
            int meilleur = enEchec
                ? -Infini
                : evaluationIncrementale.Evaluer(position, position.TourBlanc);
            // Le score statique sert de repli hors échec. En échec, chaque réponse légale doit être examinée jusqu’à une éventuelle coupure.
            bool evasionALaLimite = enEchec && demiCoupsRestants == 0;

            if (!enEchec && (demiCoupsRestants == 0 || meilleur >= beta))
            {
                if (!regles.HasAnyLegalMove(position.TourBlanc))
                    return ScoreTerminal(position, regles, distance);

                return meilleur;
            }

            List<CoupTrie> coups = GenererCoups(position, regles, distance, tactiquesSeulement: !enEchec);

            if (coups.Count == 0)
            {
                if (enEchec)
                    return ScoreTerminal(position, regles, distance);

                return regles.HasAnyLegalMove(position.TourBlanc) ? meilleur : 0;
            }

            if (!enEchec)
                alpha = Math.Max(alpha, meilleur);

            foreach (CoupTrie candidat in coups)
            {
                // EstCalme vaut false pour les captures et les promotions.
                if (!enEchec && candidat.EstCalme)
                    continue;

                VerifierArret();

                Coup coup = candidat.Coup;
                var evaluationAvant = evaluationIncrementale;
                var etat = position.JouerTemporairement(
                    coup.Depart.x, coup.Depart.y,
                    coup.Arrivee.x, coup.Arrivee.y);

                int score;
                try
                {
                    evaluationIncrementale.Appliquer(position, etat);
                    if (evasionALaLimite)
                    {
                        // Une seule réponse supplémentaire, sans rappeler la quiétude :
                        // même un contre-échec ne déclenche pas une extension récursive.
                        VerifierArret();
                        noeuds++;
                        noeudsQuietude++;
                        developpes++;
                        bool partieContinue = regles.HasAnyLegalMove(position.TourBlanc);
                        VerifierArret();
                        score = -(partieContinue
                            ? evaluationIncrementale.Evaluer(position, position.TourBlanc)
                            : ScoreTerminal(position, regles, distance + 1));
                    }
                    else
                    {
                        // Inverser les bornes et le score avec le trait. Seul un coup joué consomme la profondeur de quiétude.
                        score = -ChercherQuietude(
                            position, -beta, -alpha, distance + 1, demiCoupsRestants - 1);
                    }
                }
                finally
                {
                    position.AnnulerCoup(etat);
                    evaluationIncrementale = evaluationAvant;
                }

                meilleur = Math.Max(meilleur, score);
                alpha = Math.Max(alpha, score);

                if (alpha >= beta)
                    break;
            }

            return meilleur;
        }
        /// <summary>
        /// Reconstruit la clé à partir des 64 cases, du trait, des droits de roque et de la case en passant. Les compteurs FEN ne participent pas aux règles modélisées par cette clé.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <returns>Valeur comparable qui tient dans quatre blocs de cases et un entier d’état.</returns>
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

        /// <summary>
        /// Écrit une entrée avec sa profondeur et le type de score. Protège une entrée plus profonde ou exacte à profondeur égale. À capacité pleine, l’ajout d’un nœud interne peut recycler une feuille disponible.
        /// </summary>
        /// <param name="cle">Clé complète de la position à enregistrer.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <param name="score">Valeur ou borne issue de la recherche, en centipions hors mat.</param>
        /// <param name="type">Score exact, borne inférieure ou borne supérieure.</param>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <remarks>La quiétude actuelle ne met pas ses scores en table. Le chemin de recyclage ne libère une place que s’il trouve une clé encore associée à une profondeur zéro.</remarks>
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

        /// <summary>
        /// Normalise un score de mat avant stockage pour qu’il soit indépendant de la distance depuis la racine courante.
        /// </summary>
        /// <param name="score">Valeur ou borne issue de la recherche, en centipions hors mat.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <returns>Score normalisé, ou score statique inchangé.</returns>
        private static int StockerScore(int score, int distance)
        {
            if (score >= Evaluation.ScoreMat - 1000) return score + distance;
            if (score <= -Evaluation.ScoreMat + 1000) return score - distance;
            return score;
        }

        /// <summary>
        /// Rétablit la distance à la racine dans un score de mat précédemment normalisé.
        /// </summary>
        /// <param name="score">Valeur ou borne issue de la recherche, en centipions hors mat.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <returns>Score utilisable dans la fenêtre du nœud actuel.</returns>
        private static int LireScore(int score, int distance)
        {
            if (score >= Evaluation.ScoreMat - 1000) return score - distance;
            if (score <= -Evaluation.ScoreMat + 1000) return score + distance;
            return score;
        }

        /// <summary>
        /// Évalue une position dont l’appelant a établi l’absence de coup légal. Favorise les mats rapides et les défaites tardives.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="regles">Générateur légal lié à cette même position.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <returns>Zéro pour le pat ou score de mat négatif pour le camp au trait.</returns>
        private static int ScoreTerminal(PositionEchecs position, ReglesEchecs regles, int distance)
        {
            // Un mat proche est préféré ; en cas de défaite, retarder le mat.
            return regles.IsInCheck(position.TourBlanc) ? -Evaluation.ScoreMat + distance : 0;
        }

        /// <summary>
        /// Remplit et trie le tampon d’un niveau. La pièce lue lors du parcours est réutilisée pour la génération tactique. Le tampon ne doit pas être conservé après une nouvelle génération au même niveau.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="regles">Générateur légal lié à cette même position.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <param name="memorise">Coup optionnel auquel donner la priorité maximale.</param>
        /// <param name="tactiquesSeulement">Vrai pour limiter les destinations aux captures, prises en passant et promotions.</param>
        /// <returns>Liste réutilisable de coups légaux dans l’ordre de recherche.</returns>
        /// <remarks>Les captures jugées défavorables sont classées après les coups calmes, mais restent dans la liste. La promotion reçoit la priorité tactique la plus élevée.</remarks>
        private List<CoupTrie> GenererCoups(
            PositionEchecs position, ReglesEchecs regles, int distance,
            Coup? memorise = null, bool tactiquesSeulement = false)
        {
            List<CoupTrie> coups = coupsParNiveau[distance];
            if (coups == null) coupsParNiveau[distance] = coups = new List<CoupTrie>(32);
            coups.Clear();
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    annulation.ThrowIfCancellationRequested();
                    char piece = position.GetPiece(x, z);
                    if (piece == '.' || char.IsLower(piece) != position.TourBlanc) continue;
                    if (tactiquesSeulement)
                        regles.GetLegalMovesTactiques(x, z, piece, tamponCoupsLegaux);
                    else
                        regles.GetLegalMoves(x, z, tamponCoupsLegaux);
                    foreach (Vector2Int arrivee in tamponCoupsLegaux)
                    {
                        var coup = new Coup(new Vector2Int(x, z), arrivee);
                        // Une seule lecture des heuristiques par coup, avant le tri.
                        int tactique = Priorite(position, coup);
                        int priorite = MemeCoup(memorise, coup) ? 1000000 :
                            PrioriteRecherche(position, coup, distance, tactique);
                        coups.Add(new CoupTrie(coup, priorite, tactique == 0));
                    }
                }
            coups.Sort(ComparaisonCoups);
            return coups;
        }

        /// <summary>
        /// Trie les priorités décroissantes puis départage par coordonnées de départ et d’arrivée pour garder un ordre déterministe.
        /// </summary>
        /// <param name="a">Premier élément de la comparaison.</param>
        /// <param name="b">Second élément de la comparaison.</param>
        /// <returns>Ordre signé attendu par le tri.</returns>
        private static int ComparerCoups(CoupTrie a, CoupTrie b)
        {
            // Comparaison de données précalculées : aucun accès au plateau ni aux heuristiques.
            int ordre = b.Priorite.CompareTo(a.Priorite);
            if (ordre != 0) return ordre;
            int depart = (a.Coup.Depart.x * 8 + a.Coup.Depart.y).CompareTo(b.Coup.Depart.x * 8 + b.Coup.Depart.y);
            return depart != 0 ? depart :
                (a.Coup.Arrivee.x * 8 + a.Coup.Arrivee.y).CompareTo(b.Coup.Arrivee.x * 8 + b.Coup.Arrivee.y);
        }

        /// <summary>
        /// Réutilise la priorité tactique ou applique aux coups calmes les killers de la distance puis l’historique du camp au trait.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        /// <param name="tactique">Priorité matérielle déjà calculée, zéro pour un coup calme.</param>
        /// <returns>Priorité de tri à calculer une fois par candidat.</returns>
        private int PrioriteRecherche(PositionEchecs position, Coup coup, int distance, int tactique)
        {
            if (tactique != 0) return tactique;
            if (MemeCoup(coupsKiller[distance, 0], coup)) return 9000;
            if (MemeCoup(coupsKiller[distance, 1], coup)) return 8000;
            return historique[position.TourBlanc ? 0 : 1,
                coup.Depart.y * 8 + coup.Depart.x, coup.Arrivee.y * 8 + coup.Arrivee.x];
        }

        /// <summary>
        /// Enregistre un coup calme qui a provoqué une coupure bêta. Il met à jour les deux killers du niveau et renforce l’historique selon la profondeur.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
        /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
        private void MemoriserCoupureCalme(PositionEchecs position, Coup coup, int profondeur, int distance)
        {
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

        /// <summary>
        /// Compare deux couples départ-arrivée, en traitant explicitement l’absence de coup mémorisé.
        /// </summary>
        /// <param name="a">Premier élément de la comparaison.</param>
        /// <param name="b">Second élément de la comparaison.</param>
        /// <returns>Vrai lorsque le coup optionnel existe et désigne la même paire de cases.</returns>
        private static bool MemeCoup(Coup? a, Coup b) => a.HasValue &&
            a.Value.Depart == b.Depart && a.Value.Arrivee == b.Arrivee;

        /// <summary>
        /// Classe captures et promotions selon les valeurs de la cible et de l’attaquant. La prise en passant se reconnaît malgré une case d’arrivée vide.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <returns>Valeur tactique ou zéro pour un coup calme.</returns>
        private static int Priorite(PositionEchecs position, Coup coup)
        {
            char piece = position.GetPiece(coup.Depart.x, coup.Depart.y);
            char cible = position.GetPiece(coup.Arrivee.x, coup.Arrivee.y);
            bool pion = char.ToUpperInvariant(piece) == 'P';
            int valeurCapture = Evaluation.ValeurPiece(cible);
            if (pion && coup.Arrivee == position.CaseEnPassant && cible == '.') valeurCapture = 100;
            int attaquant = char.ToUpperInvariant(piece) == 'R' ? 0 : Evaluation.ValeurPiece(piece);
            // MVV-LVA : privilégier la victime la plus précieuse et l'attaquant le moins cher.
            int detail = 16 * valeurCapture - attaquant;
            if (pion && (coup.Arrivee.y == 0 || coup.Arrivee.y == 7)) return 300000 + detail;
            if (valeurCapture == 0) return 0;
            // Estimation matérielle simple, pas une analyse complète des échanges (SEE).
            // Les captures présumées défavorables restent explorées, après les coups calmes.
            return (valeurCapture >= attaquant ? 200000 : -200000) + detail;
        }

        /// <summary>
        /// Joue le coup temporairement, met à jour les sommes et appelle le nœud enfant. Le bloc finally restaure position et évaluation même lors d’une interruption.
        /// </summary>
        /// <param name="position">Position logique correspondant à ce calcul.</param>
        /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
        /// <param name="profondeur">Demi-coups normaux restants dans la position fille.</param>
        /// <param name="alpha">Borne inférieure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="beta">Seuil de coupure du point de vue du camp au trait dans le nœud appelé.</param>
        /// <param name="distance">Distance de la position fille depuis la racine.</param>
        /// <returns>Opposé du score de l’enfant, donc score du camp qui vient de jouer.</returns>
        private int EvaluerCoup(PositionEchecs position, Coup coup, int profondeur,
            int alpha, int beta, int distance)
        {
            var evaluationAvant = evaluationIncrementale;
            PositionEchecs.EtatAvantCoup etat = position.JouerTemporairement(
                coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y);
            try
            {
                evaluationIncrementale.Appliquer(position, etat);
                // Les bornes ont déjà été retournées par l’appelant. Le changement de signe ramène le résultat vers le parent.
                return -Explorer(position, profondeur, alpha, beta, distance);
            }
            finally
            {
                position.AnnulerCoup(etat);
                evaluationIncrementale = evaluationAvant;
            }
        }

        
    }
}
