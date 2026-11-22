using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class MoveOrderingChecks
{
    // Quatre demi-coups tactiques, puis une unique évasion supplémentaire si encore en échec.
    private const int ExtensionQuietudeReference = 4;
    private const string FenReprise = "6k1/8/4p3/3p4/8/8/8/3Q2K1 w - - 0 1";
    private const string FenEvasion = "R6k/8/5K2/8/8/8/8/8 b - - 17 42";
    private const string FenMat = "7k/6Q1/5K2/8/8/8/8/8 b - - 0 1";
    private const string FenPat = "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1";
    private const string FenMatApresEvasion = "7k/5Qr1/6K1/8/8/8/8/8 w - - 0 1";
    private const string FenContreEchec = "8/7k/5Qr1/6K1/8/8/8/8 w - - 0 1";

    /// <summary>
    /// Contrôle les classes de priorité, l’oracle sans élagage, les horizons de reprise et les positions terminales.
    /// </summary>
    [MenuItem("Halloween Chess/Verifier le tri des coups")]
    public static void Run()
    {
        const string fen = "4k3/P7/8/3p4/3Q1q2/8/8/1N2K3 w - - 0 1";
        PositionEchecs p = PositionEchecs.DepuisFen(fen);
        Type rechercheType = typeof(Minimax).GetNestedType("Recherche", BindingFlags.NonPublic);
        object recherche = Activator.CreateInstance(rechercheType, BindingFlags.Instance | BindingFlags.Public,
            null, new object[] { CancellationToken.None, true, 128 }, null);
        MethodInfo generer = rechercheType.GetMethod("GenererCoups", BindingFlags.NonPublic | BindingFlags.Instance);
        var killers = (Minimax.Coup?[,])rechercheType.GetField("coupsKiller", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(recherche);
        var historique = (int[,,])rechercheType.GetField("historique", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(recherche);

        var promotion = Coup(0, 6, 0, 7);
        var bonneCapture = Coup(3, 3, 5, 3);
        var captureDefavorable = Coup(3, 3, 3, 4);
        var killer1 = Coup(1, 0, 2, 2);
        var killer2 = Coup(1, 0, 0, 2);
        var coupHistorique = Coup(3, 3, 2, 3);
        var autreCoup = Coup(1, 0, 3, 1);
        killers[0, 0] = killer1;
        killers[0, 1] = killer2;
        historique[0, 27, 26] = 500;

        List<Minimax.Coup> ordre = LireOrdre(generer, recherche, p, null);
        Avant(ordre, promotion, bonneCapture, "Promotion avant capture favorable");
        Avant(ordre, bonneCapture, killer1, "Capture favorable avant killer");
        Avant(ordre, killer1, killer2, "Premier killer avant second killer");
        Avant(ordre, killer2, coupHistorique, "Killer avant historique");
        Avant(ordre, coupHistorique, autreCoup, "Historique avant coup sans bonus");
        Avant(ordre, autreCoup, captureDefavorable, "Capture défavorable après coup calme");
        List<Minimax.Coup> avecTable = LireOrdre(generer, recherche, p, captureDefavorable);
        Check(avecTable[0].ToString() == captureDefavorable.ToString(), "Coup de table en premier, quelle que soit sa catégorie");
        Check(avecTable.Count == ordre.Count, "Aucun coup éliminé par le tri");
        var uniques = new HashSet<string>();
        foreach (Minimax.Coup coup in avecTable) Check(uniques.Add(coup.ToString()), "Aucun doublon");
        List<Minimax.Coup> repetes = LireOrdre(generer, recherche, p, null);
        for (int i = 0; i < ordre.Count; i++)
            Check(ordre[i].ToString() == repetes[i].ToString(), "Ordre déterministe après réutilisation du tampon");
        Check(p.ExporterFen() == fen, "Le tri ne modifie pas la position");

        Check((int)rechercheType.GetField("ExtensionMaxQuietude", BindingFlags.NonPublic | BindingFlags.Static)
            .GetRawConstantValue() == ExtensionQuietudeReference, "Limite de quiétude du moteur : quatre demi-coups");
        VerifierDifferenceHorizon();

        string[,] cas =
        {
            { "Ouverture", PositionEchecs.FenInitial },
            { "Captures et tri", fen },
            { "Reprise à l'horizon", FenReprise },
            { "Prise en passant", "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10" },
            { "Promotion sans capture", "4k3/P7/8/8/8/8/8/4K3 w - - 12 25" },
            { "Promotion avec capture", "1r2k3/P7/8/8/8/8/8/4K3 w - - 0 1" },
            { "Promotion noire", "4k3/8/8/8/8/8/p7/4K3 b - - 17 25" },
            { "Évasion calme obligatoire", FenEvasion },
            { "Mat après évasion", FenMatApresEvasion },
            { "Contre-échec borné", FenContreEchec },
            { "Mat en un", "7k/5Q2/6K1/8/8/8/8/8 w - - 0 1" },
            { "Déjà mat", FenMat },
            { "Pat", FenPat }
        };
        for (int i = 0; i < cas.GetLength(0); i++)
        {
            for (int profondeur = 1; profondeur <= 2; profondeur++)
                ComparerScore(cas[i, 0], cas[i, 1], profondeur);
            // Fenêtres ouvertes : comparaison directe du contrat de quiétude, y compris sa limite.
            foreach (int limite in new[] { 0, 1, ExtensionQuietudeReference })
                ComparerQuietude(rechercheType, cas[i, 0], cas[i, 1], limite);
        }
        VerifierLimiteEtTerminaux();
        Debug.Log($"Tri des coups : tous les contrôles ont réussi. {cas.GetLength(0)} FEN, " +
            "profondeurs 1 et 2 avec/sans TT et PVS ; quiétude indépendante aux limites 0, 1 et 4.");
    }

    /// <summary>
    /// Inspecte la génération réelle par réflexion et en copie l’ordre observable pour éviter la réutilisation du tampon.
    /// </summary>
    /// <param name="generer">Méthode réelle de génération obtenue par réflexion.</param>
    /// <param name="recherche">Instance ou type de recherche instrumenté par le contrôle.</param>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="memorise">Coup optionnel auquel donner la priorité maximale.</param>
    /// <returns>Liste des coups ordonnés par le moteur.</returns>
    private static List<Minimax.Coup> LireOrdre(MethodInfo generer, object recherche,
        PositionEchecs p, Minimax.Coup? memorise)
    {
        // Reflection.Invoke ne complète pas automatiquement les paramètres optionnels.
        var liste = (IEnumerable)generer.Invoke(recherche,
            new object[] { p, new ReglesEchecs(p), 0, memorise, false }); // tactiquesSeulement: false
        var coups = new List<Minimax.Coup>();
        foreach (object candidat in liste)
            coups.Add((Minimax.Coup)candidat.GetType().GetField("Coup").GetValue(candidat));
        return coups;
    }

    /// <summary>
    /// Construit un coup de test à partir de coordonnées séparées.
    /// </summary>
    /// <param name="x">Indice de colonne de 0 (a) à 7 (h).</param>
    /// <param name="z">Indice de rangée de 0 (1) à 7 (8).</param>
    /// <param name="tx">Colonne logique de destination.</param>
    /// <param name="tz">Rangée logique de destination.</param>
    /// <returns>Coup sans validation de légalité.</returns>
    private static Minimax.Coup Coup(int x, int z, int tx, int tz)
        => new Minimax.Coup(new Vector2Int(x, z), new Vector2Int(tx, tz));

    /// <summary>
    /// Exige que deux coups présents dans la liste respectent l’ordre attendu.
    /// </summary>
    /// <param name="coups">Liste des coups qui doivent être consultés dans l’ordre fourni.</param>
    /// <param name="a">Premier élément de la comparaison.</param>
    /// <param name="b">Second élément de la comparaison.</param>
    /// <param name="message">Contexte de diagnostic en cas d’échec de l’assertion.</param>
    private static void Avant(List<Minimax.Coup> coups, Minimax.Coup a, Minimax.Coup b, string message)
    {
        int ia = coups.FindIndex(c => c.ToString() == a.ToString());
        int ib = coups.FindIndex(c => c.ToString() == b.ToString());
        Check(ia >= 0 && ib > ia, message);
    }

    /// <summary>
    /// Compare le moteur et l’oracle exhaustif, avec et sans table, puis vérifie le coup rendu et le FEN de la position.
    /// </summary>
    /// <param name="nom">Libellé qui identifie le cas dans le rapport ou une assertion.</param>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
    private static void ComparerScore(string nom, string fen, int profondeur)
    {
        var p = PositionEchecs.DepuisFen(fen);
        string avant = p.ExporterFen();
        int reference = Exhaustif(p, profondeur, 0);
        Check(p.ExporterFen() == avant, nom + " : FEN complet intact après l'oracle");
        foreach (bool utiliserTable in new[] { true, false })
        foreach (bool utiliserPvs in new[] { true, false })
        {
            Minimax.Resultat resultat = Minimax.Chercher(p, profondeur, utiliserTable: utiliserTable, utiliserPvs: utiliserPvs);
            string contexte = $"{nom}, profondeur {profondeur}, TT={utiliserTable}, PVS={utiliserPvs}";
            Check(p.ExporterFen() == avant, contexte + " : FEN complet intact après recherche");
            Check(resultat.Profondeur == profondeur, contexte + " : profondeur atteinte");
            Check(resultat.Score == reference,
                $"{contexte} : score moteur={resultat.Score}, référence={reference}. FEN={fen}");
            List<Minimax.Coup> legaux = CoupsLegaux(p);
            if (legaux.Count == 0)
                Check(!resultat.MeilleurCoup.HasValue, contexte + " : aucun coup dans une position terminale");
            else
            {
                Check(resultat.MeilleurCoup.HasValue, contexte + " : meilleur coup présent");
                Minimax.Coup meilleur = resultat.MeilleurCoup.Value;
                Check(legaux.Exists(c => c.Depart == meilleur.Depart && c.Arrivee == meilleur.Arrivee),
                    contexte + " : coup retourné légal");
                int valeurCoup = -Exhaustif(JouerSurCopie(p, meilleur), profondeur - 1, 1);
                // Les coups ex aequo sont acceptés : vérifier leur valeur, pas leur identité.
                Check(valeurCoup == reference, contexte + " : le coup retourné atteint le score optimal");
            }
            Check(p.ExporterFen() == avant, contexte + " : FEN complet intact après validation du coup");
        }
        Debug.Log($"[Tri des coups] OK | {nom} | profondeur {profondeur} | score {reference} | TT activée/désactivée");
    }

    /// <summary>
    /// Collecte indépendamment les coups de toutes les pièces au trait pour l’oracle.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <returns>Liste des couples départ-arrivée légaux.</returns>
    private static List<Minimax.Coup> CoupsLegaux(PositionEchecs p)
    {
        var regles = new ReglesEchecs(p);
        var coups = new List<Minimax.Coup>();
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = p.GetPiece(x, z);
                if (piece == '.' || char.IsLower(piece) != p.TourBlanc) continue;
                foreach (Vector2Int arrivee in regles.GetLegalMoves(x, z)) coups.Add(Coup(x, z, arrivee.x, arrivee.y));
            }
        return coups;
    }

    // Oracle : toutes les branches, copies de positions, ni tri, ni TT, ni alpha-bêta.
    /// <summary>
    /// Calcule la valeur Negamax sans coupure alpha-bêta et prolonge éventuellement les feuilles en quiétude.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="profondeur">Nombre de demi-coups de recherche normale encore autorisés.</param>
    /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
    /// <param name="avecQuietude">Vrai pour prolonger les feuilles avec l’oracle de quiétude.</param>
    /// <returns>Score exact selon le contrat de référence utilisé par le test.</returns>
    private static int Exhaustif(PositionEchecs p, int profondeur, int distance, bool avecQuietude = true)
    {
        if (profondeur == 0 && avecQuietude)
            return QuietudeReference(p, distance, ExtensionQuietudeReference);
        List<Minimax.Coup> coups = CoupsLegaux(p);
        if (coups.Count == 0) return ScoreTerminalReference(p, distance);
        if (profondeur == 0) return Evaluation.Evaluer(p, p.TourBlanc);
        int meilleur = int.MinValue;
        foreach (Minimax.Coup coup in coups)
            meilleur = Math.Max(meilleur,
                -Exhaustif(JouerSurCopie(p, coup), profondeur - 1, distance + 1, avecQuietude));
        return meilleur;
    }

    /// <summary>
    /// Énumère les continuations tactiques ou les évasions sans élagage. Modélise aussi le cas terminal et l’extension d’évasion à la limite.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
    /// <param name="restants">Demi-coups de quiétude encore disponibles dans l’oracle.</param>
    /// <returns>Score de référence du camp au trait.</returns>
    private static int QuietudeReference(PositionEchecs p, int distance, int restants)
    {
        // Générer tous les coups légaux garantit mat/pat même sans coup tactique.
        List<Minimax.Coup> coups = CoupsLegaux(p);
        if (coups.Count == 0) return ScoreTerminalReference(p, distance);
        bool enEchec = new ReglesEchecs(p).IsInCheck(p.TourBlanc);
        if (restants == 0 && !enEchec) return Evaluation.Evaluer(p, p.TourBlanc);
        int meilleur = enEchec ? int.MinValue : Evaluation.Evaluer(p, p.TourBlanc);
        foreach (Minimax.Coup coup in coups)
        {
            if (!enEchec && !EstTactiqueReference(p, coup)) continue;
            PositionEchecs copie = JouerSurCopie(p, coup);
            // À la limite, évaluer l'évasion sans prolonger récursivement, même en contre-échec.
            int score = restants == 0
                ? -TerminalOuStatiqueReference(copie, distance + 1)
                : -QuietudeReference(copie, distance + 1, restants - 1);
            meilleur = Math.Max(meilleur, score);
        }
        return meilleur;
    }

    /// <summary>
    /// Évalue le résultat d’une réponse à la limite de quiétude selon que la partie continue ou se termine.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
    /// <returns>Score terminal ou évaluation statique orientée pour le trait.</returns>
    private static int TerminalOuStatiqueReference(PositionEchecs p, int distance)
        => CoupsLegaux(p).Count == 0 ? ScoreTerminalReference(p, distance) : Evaluation.Evaluer(p, p.TourBlanc);

    /// <summary>
    /// Identifie les captures ordinaires, en passant et promotions selon les données du plateau.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
    /// <returns>Vrai si le coup appartient au sous-ensemble tactique.</returns>
    private static bool EstTactiqueReference(PositionEchecs p, Minimax.Coup coup)
    {
        if (p.GetPiece(coup.Arrivee.x, coup.Arrivee.y) != '.') return true;
        bool pion = char.ToUpperInvariant(p.GetPiece(coup.Depart.x, coup.Depart.y)) == 'P';
        return pion && (coup.Arrivee.y == 0 || coup.Arrivee.y == 7 ||
            (coup.Depart.x != coup.Arrivee.x && coup.Arrivee == p.CaseEnPassant));
    }

    /// <summary>
    /// Crée une vraie position fille pour l’oracle en protégeant entièrement le plateau parent.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="coup">Couple de cases de départ et d’arrivée à examiner.</param>
    /// <returns>Copie après le coup dont la légalité doit être vérifiée.</returns>
    private static PositionEchecs JouerSurCopie(PositionEchecs p, Minimax.Coup coup)
    {
        PositionEchecs copie = p.Copier();
        Check(new ReglesEchecs(copie).JouerCoup(coup.Depart.x, coup.Depart.y, coup.Arrivee.x, coup.Arrivee.y),
            "Coup de référence légal : " + coup);
        return copie;
    }

    /// <summary>
    /// Applique l’échelle de mat et de pat de l’oracle après constat d’absence de coups légaux.
    /// </summary>
    /// <param name="p">Position logique soumise au contrôle.</param>
    /// <param name="distance">Nombre de demi-coups déjà joués depuis la racine.</param>
    /// <returns>Score terminal signé pour le trait.</returns>
    private static int ScoreTerminalReference(PositionEchecs p, int distance)
        => new ReglesEchecs(p).IsInCheck(p.TourBlanc) ? -Evaluation.ScoreMat + distance : 0;

    /// <summary>
    /// Exige qu’au moins une position de reprise sépare les scores statiques et prolongés.
    /// </summary>
    private static void VerifierDifferenceHorizon()
    {
        var p = PositionEchecs.DepuisFen(FenReprise);
        string avant = p.ExporterFen();
        int statique = Exhaustif(p, 1, 0, avecQuietude: false);
        int prolonge = Exhaustif(p, 1, 0);
        Check(statique != prolonge, "La FEN de reprise distingue feuille statique et quiétude");
        PositionEchecs apresPrise = JouerSurCopie(p, Coup(3, 0, 3, 4)); // Dxd5? ...exd5
        int priseStatique = -Evaluation.Evaluer(apresPrise, apresPrise.TourBlanc);
        int priseProlongee = -QuietudeReference(apresPrise, 1, ExtensionQuietudeReference);
        Check(priseProlongee < priseStatique, "La quiétude voit la reprise de la dame sur d5");
        Check(p.ExporterFen() == avant, "FEN complet intact après le test d'horizon");
        Debug.Log($"[Tri des coups] Horizon confirmé | statique={statique}, quiétude={prolonge} | " +
            $"branche Dxd5 : statique={priseStatique}, quiétude={priseProlongee}");
    }

    /// <summary>
    /// Invoque le moteur en pleine fenêtre à un horizon donné et compare son résultat et sa restauration à l’oracle.
    /// </summary>
    /// <param name="rechercheType">Type privé de recherche résolu par réflexion.</param>
    /// <param name="nom">Libellé qui identifie le cas dans le rapport ou une assertion.</param>
    /// <param name="fen">Position au format FEN à six champs.</param>
    /// <param name="limite">Nombre de demi-coups de quiétude accordés au test.</param>
    private static void ComparerQuietude(Type rechercheType, string nom, string fen, int limite)
    {
        var p = PositionEchecs.DepuisFen(fen);
        string avant = p.ExporterFen();
        const int distance = 3;
        int reference = QuietudeReference(p, distance, limite);
        Check(p.ExporterFen() == avant, nom + " : oracle de quiétude sans mutation");
        object recherche = Activator.CreateInstance(rechercheType, BindingFlags.Instance | BindingFlags.Public,
            null, new object[] { CancellationToken.None, false, 128 }, null);
        rechercheType.GetField("regles", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(recherche, new ReglesEchecs(p));
        rechercheType.GetMethod("InitialiserEvaluation", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(recherche, new object[] { p });
        MethodInfo quietude = rechercheType.GetMethod("ChercherQuietude", BindingFlags.NonPublic | BindingFlags.Instance);
        int infini = Evaluation.ScoreMat + 10000;
        int obtenu = (int)quietude.Invoke(recherche, new object[] { p, -infini, infini, distance, limite });
        Check(p.ExporterFen() == avant, nom + " : moteur de quiétude sans mutation");
        Check(obtenu == reference,
            $"{nom} : quiétude limite {limite}, moteur={obtenu}, référence={reference}. FEN={fen}");
    }

    /// <summary>
    /// Teste directement les positions terminales, les évasions et la limite de quiétude.
    /// </summary>
    private static void VerifierLimiteEtTerminaux()
    {
        var echec = PositionEchecs.DepuisFen(FenEvasion);
        List<Minimax.Coup> evasions = CoupsLegaux(echec);
        Check(new ReglesEchecs(echec).IsInCheck(false) && evasions.Count == 1 && evasions[0].ToString() == "h8h7",
            "Une seule évasion légale, sans capture : h8h7");
        Check(!EstTactiqueReference(echec, evasions[0]), "L'évasion est calme");
        int aLaLimite = QuietudeReference(echec, 3, 0);
        int apresEvasion = QuietudeReference(echec, 3, 1);
        int attendu = -TerminalOuStatiqueReference(JouerSurCopie(echec, evasions[0]), 4);
        Check(aLaLimite == attendu && apresEvasion == attendu,
            "À la limite comme avant : l'évasion calme est obligatoirement explorée");
        Check(aLaLimite != Evaluation.Evaluer(echec, false),
            "La limite en échec ne retourne plus l'évaluation avant évasion");
        Check(QuietudeReference(PositionEchecs.DepuisFen(FenMat), 3, 0) == -Evaluation.ScoreMat + 3,
            "Mat détecté avant l'évaluation statique à la limite");
        Check(QuietudeReference(PositionEchecs.DepuisFen(FenPat), 3, 0) == 0,
            "Pat détecté avant l'évaluation statique à la limite");

        Check(QuietudeReference(PositionEchecs.DepuisFen(FenMatApresEvasion), 3, 0) == Evaluation.ScoreMat - 4,
            "Mat après Qxg7 : terminal détecté après l'unique demi-coup supplémentaire");
        var contreEchec = PositionEchecs.DepuisFen(FenContreEchec);
        var apresContreEchec = JouerSurCopie(contreEchec, Coup(5, 5, 6, 5)); // Qxg6+
        var reglesContreEchec = new ReglesEchecs(apresContreEchec);
        Check(reglesContreEchec.IsInCheck(false) && reglesContreEchec.HasAnyLegalMove(false),
            "Le contre-échec n'est pas un mat");
        int statiqueApresContreEchec = -Evaluation.Evaluer(apresContreEchec, false);
        Check(QuietudeReference(contreEchec, 3, 0) == statiqueApresContreEchec,
            "Le contre-échec est évalué statiquement après une seule réponse");
        Check(statiqueApresContreEchec != -QuietudeReference(apresContreEchec, 4, 0),
            "Le test distingue une réponse unique d'une nouvelle extension récursive");
    }

    /// <summary>
    /// Transforme un écart avec le résultat de référence en échec explicite du contrôle.
    /// </summary>
    /// <param name="condition">Invariant attendu par le cas de test.</param>
    /// <param name="message">Contexte de diagnostic en cas d’échec de l’assertion.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Tri des coups : " + message);
    }
}
