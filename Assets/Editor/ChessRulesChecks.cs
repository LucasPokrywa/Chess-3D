using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Contrôles sans scène et sans GameObjects, exécutables depuis le menu Unity.
public static class ChessRulesChecks
{
    [MenuItem("Halloween Chess/Verifier les regles")]
    public static void Run()
    {
        RunFen();
        VerifierMemoireRois();
        VerifierGenerationTactique();
        var start = new PositionEchecs();
        Check(Perft(start, 1) == 20, "20 coups initiaux");
        Check(Perft(start, 2) == 400, "Perft 2 = 400");
        Check(Perft(start, 3) == 8902, "Perft 3 = 8902");
        var copy = start.Copier();
        Play(copy, 4, 1, 4, 3);
        Check(start.TourBlanc && start.GetPiece(4, 1) == 'p' && start.CaseEnPassant.x == -1,
            "Les simulations préservent la position originale");
        Check(!new ReglesEchecs(copy).JouerCoup(3, 1, 3, 3), "Respect du tour");

        var ep = new PositionEchecs();
        Play(ep, 4, 1, 4, 3); Play(ep, 0, 6, 0, 5);
        Play(ep, 4, 3, 4, 4); Play(ep, 3, 6, 3, 4);
        var expired = ep.Copier();
        Play(ep, 4, 4, 3, 5);
        Check(ep.GetPiece(3, 4) == '.' && ep.GetPiece(3, 5) == 'p', "Prise en passant");
        Play(expired, 6, 0, 5, 2); Play(expired, 0, 5, 0, 4);
        Check(!Moves(expired, 4, 4, 3, 5), "Expiration de la prise en passant");

        var pinned = Empty();
        pinned.SetPiece(4, 0, '.'); pinned.SetPiece(7, 4, 'r');
        pinned.SetPiece(6, 4, 'p'); pinned.SetPiece(0, 4, 'T');
        pinned.SetPiece(5, 6, 'P'); pinned.SetPiece(1, 1, 'p');
        Play(pinned, 1, 1, 1, 2); Play(pinned, 5, 6, 5, 4);
        Check(!Moves(pinned, 6, 4, 5, 5), "Prise en passant interdite si le roi est exposé");

        var castle = Empty();
        castle.SetPiece(4, 0, 'r', false); castle.SetPiece(7, 0, 't', false);
        Play(castle, 4, 0, 6, 0);
        Check(castle.GetPiece(5, 0) == 't' && castle.GetPiece(7, 0) == '.', "Roque et déplacement de la tour");
        var moved = Empty();
        moved.SetPiece(4, 0, 'r', false); moved.SetPiece(7, 0, 't', false);
        Play(moved, 7, 0, 7, 1); Play(moved, 4, 7, 3, 7);
        Play(moved, 7, 1, 7, 0); Play(moved, 3, 7, 4, 7);
        Check(!Moves(moved, 4, 0, 6, 0), "Droit de roque perdu après déplacement de la tour");
        var attacked = Empty();
        attacked.SetPiece(4, 0, 'r', false); attacked.SetPiece(7, 0, 't', false);
        attacked.SetPiece(5, 7, 'T');
        Check(!Moves(attacked, 4, 0, 6, 0), "Roque interdit à travers une attaque");

        var promotion = Empty();
        promotion.SetPiece(0, 6, 'p'); Play(promotion, 0, 6, 0, 7);
        Check(promotion.GetPiece(0, 7) == 'd', "Promotion automatique en dame");
        var mate = new PositionEchecs();
        Play(mate, 5, 1, 5, 2); Play(mate, 4, 6, 4, 4);
        Play(mate, 6, 1, 6, 3); Play(mate, 3, 7, 7, 3);
        var rules = new ReglesEchecs(mate);
        Check(rules.IsInCheck(true) && !rules.HasAnyLegalMove(true), "Mat du sot");
        var pat = new PositionEchecs(false);
        pat.SetPiece(0, 0, 'r'); pat.SetPiece(2, 1, 'R'); pat.SetPiece(1, 2, 'D');
        rules = new ReglesEchecs(pat);
        Check(!rules.IsInCheck(true) && !rules.HasAnyLegalMove(true), "Pat");
        Debug.Log("Échecs : tous les contrôles logiques ont réussi.");
    }

    [MenuItem("Halloween Chess/Verifier le FEN")]
    public static void RunFen()
    {
        Check(new PositionEchecs().ExporterFen() == PositionEchecs.FenInitial, "Export FEN initial");
        Check(PositionEchecs.DepuisFen(PositionEchecs.FenInitial).ExporterFen() == PositionEchecs.FenInitial,
            "Import/export FEN initial et couleurs");
        var p = new PositionEchecs();
        Play(p, 4, 1, 4, 3);
        const string e4 = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";
        Check(p.ExporterFen() == e4, "Export après e4, même sans prise en passant possible");
        Check(PositionEchecs.DepuisFen(e4).ExporterFen() == e4, "Import de la case e3");
        Play(p, 2, 6, 2, 4); Play(p, 6, 0, 5, 2);
        Check(p.ExporterFen() == "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
            "Compteurs après e4 c5 Cf3");
        Check(p.Copier().ExporterFen() == p.ExporterFen(), "Copie de tous les champs FEN");
        const string castle = "r3k2r/8/8/8/8/8/8/R3K2R w Qk - 12 34";
        var roque = PositionEchecs.DepuisFen(castle);
        Check(roque.ExporterFen() == castle, "Droits de roque asymétriques");
        Check(Moves(roque, 4, 0, 2, 0) && !Moves(roque, 4, 0, 6, 0), "Application des droits importés");
        Play(roque, 4, 0, 2, 0);
        Check(roque.ExporterFen() == "r3k2r/8/8/8/8/8/8/2KR3R b k - 13 34", "FEN après grand roque");
        const string ep = "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10";
        var passant = PositionEchecs.DepuisFen(ep);
        Play(passant, 4, 4, 3, 5);
        Check(passant.ExporterFen() == "4k3/8/3P4/8/8/8/8/4K3 b - - 0 10", "FEN après prise en passant");
        var capture = PositionEchecs.DepuisFen("4k3/8/8/8/8/8/n7/R3K3 w - - 15 20");
        Play(capture, 0, 0, 0, 1);
        Check(capture.DemiCoupsSansPriseNiPion == 0, "Capture : compteur remis à zéro");
        RejectFen("8/8/8/8/8/8/8/8 w - - 0 1");
        RejectFen("4k3/8/8/8/8/8/8/4K3 x - - 0 1");
        RejectFen("4k3/8/8/8/8/8/8/4K3 w K - 0 1");
        RejectFen("4k3/8/8/8/8/8/8/4K3 w - e3 0 1");
        RejectFen("4k3/8/8/8/8/8/8/4K3 w - - -1 1");
        RejectFen("4k3/8/8/8/8/8/8/4K3 w - - 0 0");
        RejectFen("4k3/8/8/8/8/8/8/9K w - - 0 1");
        Debug.Log("FEN : tous les contrôles ont réussi.");
    }

    private static void VerifierCasesRois(PositionEchecs p)
    {
        var blanc = new Vector2Int(-1, -1);
        var noir = new Vector2Int(-1, -1);
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                if (p.GetPiece(x, z) == 'r') blanc = new Vector2Int(x, z);
                if (p.GetPiece(x, z) == 'R') noir = new Vector2Int(x, z);
            }
        Check(p.CaseRoiBlanc == blanc && p.CaseRoiNoir == noir,
            "Les coordonnées mémorisées correspondent au parcours indépendant du plateau");
    }

    private static void VerifierMemoireRois()
    {
        var vide = new PositionEchecs(false);
        VerifierCasesRois(vide);
        Check(new ReglesEchecs(vide).IsInCheck(true) && new ReglesEchecs(vide).IsInCheck(false),
            "Un roi absent conserve le comportement en échec");
        vide.SetPiece(2, 2, 'r'); vide.SetPiece(6, 6, 'R');
        VerifierCasesRois(vide);
        vide.SetPiece(2, 2, 'p'); vide.SetPiece(6, 6, '.');
        VerifierCasesRois(vide);

        var initiale = new PositionEchecs();
        VerifierCasesRois(initiale);
        var copie = initiale.Copier();
        VerifierCasesRois(copie);
        // Même ordre d'écriture qu'AppliquerCoup : arrivée, puis effacement du départ.
        copie.SetPiece(4, 2, 'r'); copie.SetPiece(4, 0, '.');
        copie.SetPiece(3, 5, 'R'); copie.SetPiece(4, 7, '.');
        VerifierCasesRois(copie); VerifierCasesRois(initiale);
        Check(initiale.CaseRoiBlanc == new Vector2Int(4, 0) && initiale.CaseRoiNoir == new Vector2Int(4, 7),
            "Modifier la copie ne change pas les rois de l'original");

        string[] positions =
        {
            PositionEchecs.FenInitial,
            "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1",
            "r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1",
            "4k3/8/8/8/8/8/3p4/4K3 w - - 0 1",
            "4k3/3P4/8/8/8/8/8/4K3 b - - 0 1",
            "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10",
            "4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 1",
            "1r1r3k/2P5/8/8/8/8/8/4K3 w - - 0 1",
            "4k3/8/8/8/8/8/p7/1R2K3 b - - 0 1",
            "R6k/8/5K2/8/8/8/8/8 b - - 0 1"
        };
        MethodInfo jouer = typeof(PositionEchecs).GetMethod("JouerTemporairement", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo annuler = typeof(PositionEchecs).GetMethod("AnnulerCoup", BindingFlags.Instance | BindingFlags.NonPublic);
        int roques = 0;
        foreach (string fen in positions)
        {
            var p = PositionEchecs.DepuisFen(fen);
            VerifierCasesRois(p); VerifierCasesRois(p.Copier());
            var regles = new ReglesEchecs(p);
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    char piece = p.GetPiece(x, z);
                    if (piece == '.' || char.IsLower(piece) != p.TourBlanc) continue;
                    List<Vector2Int> coups = regles.GetLegalMoves(x, z);
                    VerifierCasesRois(p);
                    foreach (Vector2Int arrivee in coups)
                    {
                        object etat = jouer.Invoke(p, new object[] { x, z, arrivee.x, arrivee.y });
                        try
                        {
                            VerifierCasesRois(p); VerifierCasesRois(p.Copier());
                            if (char.ToUpperInvariant(piece) == 'R')
                            {
                                Check((char.IsLower(piece) ? p.CaseRoiBlanc : p.CaseRoiNoir) == arrivee,
                                    "Le roi déplacé, y compris lors du roque, est mémorisé à l'arrivée");
                                if (Math.Abs(arrivee.x - x) == 2) roques++;
                            }
                            // La génération adverse simule et annule d'autres coups à un niveau imbriqué.
                            new ReglesEchecs(p).HasAnyLegalMove(p.TourBlanc);
                            VerifierCasesRois(p);
                        }
                        finally { annuler.Invoke(p, new[] { etat }); }
                        VerifierCasesRois(p);
                        Check(p.ExporterFen() == fen, "Annulation : FEN et coordonnées des deux rois restaurés");
                    }
                }
        }
        Check(roques == 4, "Petit et grand roque testés pour les deux couleurs");
        Debug.Log("Cases des rois : initialisation, édition, FEN, copies, coups/captures, quatre roques et annulations imbriquées réussis.");
    }

    private static void VerifierGenerationTactique()
    {
        string[] positions =
        {
            PositionEchecs.FenInitial,
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "1r1r3k/2P5/8/8/8/8/8/4K3 w - - 0 1", // Promotion calme puis captures gauche/droite.
            "4k3/8/8/8/8/8/p7/1R2K3 b - - 0 1", // Promotions noires, avec/sans capture.
            "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10",
            "4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 1",
            "4k3/8/8/r4pPK/8/8/8/8 w - f6 0 1", // Prise en passant exposant le roi.
            "4k3/8/1p3n2/2p1p3/3Q4/2N1P3/8/4K3 w - - 0 1",
            "4k3/8/3n4/8/r2R2p1/8/3P4/4K3 w - - 0 1", // Captures au-delà de cases vides, obstacles amis.
            "4k3/8/1p3p2/8/3B4/8/1P3P2/4K3 w - - 0 1",
            "4k3/8/2p1p3/8/3N4/8/2P1P3/4K3 w - - 0 1",
            "7k/8/8/8/8/8/3p4/4K3 w - - 0 1", // Capture du roi.
            "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", // Roques exclus du mode tactique.
            "R6k/8/5K2/8/8/8/8/8 b - - 0 1", // Une évasion calme, aucune tactique.
            "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1" // Pat.
        };
        var tactiques = new List<Vector2Int>();
        MethodInfo generer = typeof(ReglesEchecs).GetMethod("GetLegalMoves", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(int), typeof(int), typeof(List<Vector2Int>), typeof(bool) }, null);
        foreach (string fen in positions)
        {
            var p = PositionEchecs.DepuisFen(fen);
            var regles = new ReglesEchecs(p);
            int nombreLegaux = 0;
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    char piece = p.GetPiece(x, z);
                    if (piece == '.' || char.IsLower(piece) != p.TourBlanc) continue;
                    List<Vector2Int> legaux = regles.GetLegalMoves(x, z);
                    nombreLegaux += legaux.Count;
                    tactiques.Add(new Vector2Int(-1, -1)); // Vérifier aussi la remise à zéro du tampon.
                    generer.Invoke(regles, new object[] { x, z, tactiques, true });
                    int index = 0;
                    foreach (Vector2Int coup in legaux)
                    {
                        bool pion = char.ToUpperInvariant(piece) == 'P';
                        bool tactique = p.GetPiece(coup.x, coup.y) != '.' ||
                            (pion && (coup.y == 0 || coup.y == 7 ||
                                (coup.x != x && coup == p.CaseEnPassant)));
                        if (!tactique) continue;
                        Check(index < tactiques.Count && tactiques[index] == coup,
                            $"Coups tactiques identiques au filtrage complet, dans le même ordre : {fen}, ({x},{z})");
                        index++;
                    }
                    Check(index == tactiques.Count, "Aucun coup calme ni doublon dans la liste tactique");
                    Check(p.ExporterFen() == fen, "Génération tactique : FEN complet restauré");
                }
            Check(regles.HasAnyLegalMove(p.TourBlanc) == (nombreLegaux > 0),
                "L'absence de coups tactiques ne remplace pas la détection de mat/pat");
        }
        Debug.Log($"Génération tactique : {positions.Length} FEN, coups légaux et ordre identiques au filtrage complet.");
    }

    private static void RejectFen(string fen)
    {
        try { PositionEchecs.DepuisFen(fen); }
        catch (FormatException) { return; }
        throw new Exception("Un FEN invalide a été accepté : " + fen);
    }

    private static PositionEchecs Empty()
    {
        var p = new PositionEchecs(false);
        p.SetPiece(4, 0, 'r'); p.SetPiece(4, 7, 'R');
        return p;
    }
    private static bool Moves(PositionEchecs p, int x, int z, int tx, int tz)
        => new ReglesEchecs(p).GetLegalMoves(x, z).Contains(new Vector2Int(tx, tz));
    private static void Play(PositionEchecs p, int x, int z, int tx, int tz)
    {
        Check(new ReglesEchecs(p).JouerCoup(x, z, tx, tz), $"Coup ({x},{z}) vers ({tx},{tz})");
        VerifierCasesRois(p);
    }
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception("Échec du contrôle : " + message);
    }
    private static long Perft(PositionEchecs p, int depth)
    {
        if (depth == 0) return 1;
        long count = 0;
        var rules = new ReglesEchecs(p);
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = p.GetPiece(x, z);
                if (piece == '.' || char.IsLower(piece) != p.TourBlanc) continue;
                foreach (Vector2Int move in rules.GetLegalMoves(x, z))
                {
                    var copy = p.Copier();
                    Play(copy, x, z, move.x, move.y);
                    count += Perft(copy, depth - 1);
                }
            }
        return count;
    }
}
