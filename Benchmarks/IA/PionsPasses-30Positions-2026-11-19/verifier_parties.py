"""Vérification indépendante des CSV/PGN : python verifier_parties.py --deps DOSSIER_PYTHON_CHESS."""
import argparse
import csv
import hashlib
import json
import statistics
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--deps", type=Path)
args = parser.parse_args()
if args.deps:
    sys.path.insert(0, str(args.deps.resolve()))
import chess
import chess.pgn

root = Path(__file__).resolve().parent
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
limite = manifest["LimiteDemiCoups"]
assert hashlib.sha256((root / "positions.tsv").read_bytes()).hexdigest() == manifest["PositionsSha256"].lower()
assert hashlib.sha256((root / "positions.csv").read_bytes()).hexdigest() == manifest["PositionsCsvSha256"].lower()
with (root / "parties.csv").open(encoding="utf-8-sig", newline="") as f:
    parties = list(csv.DictReader(f))
with (root / "coups.csv").open(encoding="utf-8-sig", newline="") as f:
    tous_les_coups = list(csv.DictReader(f))
with (root / "parties.pgn").open(encoding="utf-8-sig") as f:
    pgn = []
    while (game := chess.pgn.read_game(f)) is not None:
        assert not game.errors, game.errors
        pgn.append(game)
assert len(pgn) == len(parties), "CSV/PGN incomplets : relancer après la fin d'une partie."

bilan = {"victoires": 0, "nulles": 0, "defaites": 0, "indeterminees": 0}
details = []
stats = {"nouveau": [], "ancien": []}
for partie, game in zip(parties, pgn):
    identifiant = partie["id"]
    coups = [c for c in tous_les_coups if c["partie"] == identifiant]
    board = game.board()
    assert board.is_valid(), f"FEN initial invalide, partie {identifiant}"
    assert game.headers["Round"] == identifiant
    assert game.headers["Result"] == partie["resultat"]
    assert len(coups) == int(partie["demi_coups"])
    pgn_moves = list(game.mainline_moves())
    assert len(pgn_moves) == len(coups)
    nouveau_blanc = partie["nouveau_blanc"] == "True"
    for index, (coup, pgn_move) in enumerate(zip(coups, pgn_moves), 1):
        assert int(coup["demi_coup"]) == index
        assert board.fen(en_passant="fen") == coup["fen_avant"], (identifiant, index, "FEN avant")
        move = chess.Move.from_uci(coup["coup_uci"])
        assert move in board.legal_moves, (identifiant, index, "coup illégal")
        assert move == pgn_move, (identifiant, index, "PGN différent")
        assert board.san(move) == coup["san"], (identifiant, index, "SAN différent")
        assert (board.turn == nouveau_blanc) == (coup["version"] == "nouveau")
        assert int(coup["principal"]) + int(coup["quietude"]) == int(coup["total"])
        assert 0 <= int(coup["profondeur"]) <= 10
        board.push(move)
        assert board.fen(en_passant="fen") == coup["fen_apres"], (identifiant, index, "FEN après")
        stats[coup["version"]].append(coup)
    assert board.fen(en_passant="fen") == partie["fen_final"]
    motif = partie["motif"]
    if motif == "mat":
        assert board.is_checkmate()
        assert partie["resultat"] == ("0-1" if board.turn else "1-0")
    elif motif == "pat":
        assert board.is_stalemate()
    elif motif == "triple repetition":
        assert board.is_repetition(3)
    elif motif == "50 coups":
        assert board.is_fifty_moves()
    elif motif == "materiel insuffisant":
        assert board.is_insufficient_material()
    elif motif == f"plafond {limite} demi-coups":
        assert len(coups) == limite and partie["resultat"] == "*"
        assert not board.is_game_over(claim_draw=False)
    else:
        raise AssertionError(f"Motif inconnu : {motif}")
    if partie["resultat"] == "*":
        resultat_nouveau = "indeterminees"
    elif partie["resultat"] == "1/2-1/2":
        resultat_nouveau = "nulles"
    else:
        resultat_nouveau = "victoires" if (partie["resultat"] == "1-0") == nouveau_blanc else "defaites"
    bilan[resultat_nouveau] += 1
    details.append(dict(partie, resultat_nouveau=resultat_nouveau))

statistiques = {}
for version, coups in stats.items():
    if not coups:
        continue
    temps = [float(c["ms"]) for c in coups]
    statistiques[version] = {
        "coups": len(coups),
        "profondeur_moyenne": statistics.mean(int(c["profondeur"]) for c in coups),
        "temps_median_ms": statistics.median(temps),
        "temps_max_ms": max(temps),
        "iterations_interrompues": sum(c["budget_interrompu"] == "True" for c in coups),
    }
resume = {
    "verification": "OK", "python_chess": chess.__version__,
    "parties_verifiees": len(parties), "coups_verifies": sum(len(c) for c in stats.values()),
    "bilan_nouveau": bilan,
    "points_nouveau": bilan["victoires"] + bilan["nulles"] / 2,
    "statistiques": statistiques, "details": details,
}
(root / "verification.json").write_text(json.dumps(resume, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({k: v for k, v in resume.items() if k != "details"}, ensure_ascii=False, indent=2))

# Agregation par paire, en excluant explicitement tout resultat indetermine.
with (root / "positions.csv").open(encoding="utf-8-sig", newline="") as f:
    positions = list(csv.DictReader(f))
paires = []
bilan_paires = {"gagnees": 0, "egalite": 0, "perdues": 0, "indeterminees": 0}
for position in positions:
    matchs = [p for p in details if p["position"] == position["nom"]]
    if len(matchs) != 2:
        continue
    assert {p["nouveau_blanc"] for p in matchs} == {"True", "False"}
    assert all(pgn[int(p["id"]) - 1].headers["FEN"] == position["fen"] for p in matchs)
    points = sum(1 if p["resultat_nouveau"] == "victoires" else 0.5 if p["resultat_nouveau"] == "nulles" else 0 for p in matchs)
    resultat = "indeterminees" if any(p["resultat_nouveau"] == "indeterminees" for p in matchs) else "gagnees" if points > 1 else "perdues" if points < 1 else "egalite"
    bilan_paires[resultat] += 1
    blanc = next(p["resultat_nouveau"] for p in matchs if p["nouveau_blanc"] == "True")
    noir = next(p["resultat_nouveau"] for p in matchs if p["nouveau_blanc"] == "False")
    paires.append({"id": position["id"], "position": position["nom"], "categorie": position["categorie"], "nouveau_blanc": blanc,
                   "nouveau_noir": noir, "points_nouveau": points, "resultat_paire": resultat})
if paires:
    with (root / "paires.csv").open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(paires[0]))
        writer.writeheader()
        writer.writerows(paires)
resume["paires_verifiees"] = len(paires)
resume["bilan_paires"] = bilan_paires
resume["categories"] = {}
for categorie in ("Ouverture", "Milieu", "Finale"):
    groupe = [p for p in paires if p["categorie"] == categorie]
    resume["categories"][categorie] = {
        "paires": len(groupe), "points_nouveau": sum(p["points_nouveau"] for p in groupe),
        "gagnees": sum(p["resultat_paire"] == "gagnees" for p in groupe),
        "egalite": sum(p["resultat_paire"] == "egalite" for p in groupe),
        "perdues": sum(p["resultat_paire"] == "perdues" for p in groupe),
        "indeterminees": sum(p["resultat_paire"] == "indeterminees" for p in groupe),
    }
(root / "verification.json").write_text(json.dumps(resume, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"paires_verifiees": len(paires), "bilan_paires": bilan_paires, "categories": resume["categories"]}, ensure_ascii=False, indent=2))
