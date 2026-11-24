"""Construit et valide les 30 positions avant toute partie du tournoi."""
import csv
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path("Temp/IA30Match/PythonDeps").resolve()))
import chess

root = Path(__file__).resolve().parent
familles = [
    ("Espagnole", "e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7 Re1 b5 Bb3 d6 c3 O-O",
     "h3 Nb8 d4 Nbd7 Nbd2 Bb7 Bc2 Re8 Nf1 Bf8 Ng3 g6 a4 c5 d5 Bg7"),
    ("Italienne", "e4 e5 Nf3 Nc6 Bc4 Bc5 c3 Nf6 d3 d6 O-O O-O",
     "Re1 a6 Bb3 Ba7 Nbd2 h6 Nf1 Re8 Ng3 Be6 Bc2 d5 exd5 Bxd5 Be3 Bxe3 Rxe3 Qd6"),
    ("Sicilienne Najdorf", "e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6 Be3 e6",
     "f3 b5 Qd2 Nbd7 g4 h6 O-O-O Bb7 h4 b4 Na4 d5 exd5 Nxd5 Bc4 Nxe3 Qxe3 Qc7"),
    ("Sicilienne Dragon", "e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 g6 Be3 Bg7 f3 O-O",
     "Qd2 Nc6 Bc4 Bd7 O-O-O Rc8 Bb3 Ne5 Kb1 Nc4 Bxc4 Rxc4 g4 b5 b3 Rc8"),
    ("Francaise", "e4 e6 d4 d5 Nd2 Nf6 e5 Nfd7 Bd3 c5 c3 Nc6",
     "Ngf3 Be7 O-O O-O Re1 f6 exf6 Bxf6 Nf1 cxd4 cxd4 e5 dxe5 Ndxe5 Nxe5 Nxe5 Be2 Be6"),
    ("Caro-Kann", "e4 c6 d4 d5 Nc3 dxe4 Nxe4 Bf5 Ng3 Bg6 h4 h6",
     "Nf3 Nd7 h5 Bh7 Bd3 Bxd3 Qxd3 e6 Bd2 Ngf6 O-O-O Be7 Kb1 O-O Ne4 Nxe4 Qxe4 Nf6"),
    ("Gambit dame refuse", "d4 d5 c4 e6 Nc3 Nf6 Bg5 Be7 e3 O-O Nf3 Nbd7",
     "Rc1 c6 Bd3 dxc4 Bxc4 Nd5 Bxe7 Qxe7 O-O Nxc3 Rxc3 e5 dxe5 Nxe5 Nxe5 Qxe5 f4 Qe7"),
    ("Slave", "d4 d5 c4 c6 Nf3 Nf6 Nc3 dxc4 a4 Bf5 e3 e6",
     "Bxc4 Bb4 O-O O-O Qe2 Nbd7 e4 Bg6 Bd3 Re8 e5 Nd5 Ne4 Be7"),
    ("Est-indienne", "d4 Nf6 c4 g6 Nc3 Bg7 e4 d6 Nf3 O-O Be2 e5 O-O Nc6",
     "d5 Ne7 Ne1 Nd7 Nd3 f5 Bd2 Nf6 f3 f4 Rc1 g5 c5 Ng6 Nb5 Rf7"),
    ("Anglaise symetrique", "c4 c5 Nc3 Nc6 g3 g6 Bg2 Bg7 Nf3 Nf6 O-O O-O",
     "d4 cxd4 Nxd4 Nxd4 Qxd4 d6 Qd3 Rb8 Be3 b6 Rac1 Bb7 b3 Qd7 Rfd1 Rfc8"),
]
positions = []
for categorie, partie in [("Ouverture", 0), ("Milieu", 1)]:
    for nom, ouverture, suite in familles:
        board = chess.Board()
        sequence = ouverture + (" " + suite if partie else "")
        for san in sequence.split():
            try:
                board.push_san(san)
            except ValueError as e:
                raise ValueError(f"{categorie} {nom}, coup {san}: {board.fen()}") from e
        assert board.is_valid() and not board.is_game_over(claim_draw=True)
        positions.append({"id": len(positions) + 1, "categorie": categorie, "nom": f"{categorie} - {nom}",
                          "fen": board.fen(en_passant="fen"), "origine_san": sequence})

finales = [
    ("Pions - deux ailes verrouillees", "8/6k1/5p1p/p7/P7/5P1P/6K1/8 w - - 0 30"),
    ("Pions - prises sur deux ailes", "8/8/2k5/p5p1/1P5P/5K2/8/8 w - - 0 30"),
    ("Pions - majorite centrale bloquee", "8/8/3k4/1p1p4/1P1P4/5K2/8/8 b - - 0 30"),
    ("Pions - passes eloignes concurrents", "8/5k2/p4p2/2P5/5P2/2K5/8/8 w - - 0 30"),
    ("Pions - quatre contre quatre", "8/5k2/pp3pp1/8/8/PP3PP1/5K2/8 w - - 0 30"),
    ("Pions - majorites opposees", "8/6k1/pp3p2/8/8/1P4PP/6K1/8 b - - 0 30"),
    ("Tours - trois pions contre trois", "7r/5pkp/6p1/8/8/6P1/5PKP/R7 w - - 0 30"),
    ("Tours - creation de passe", "8/4r1k1/1p3p2/8/P7/5P2/2R3K1/8 b - - 0 30"),
    ("Fou contre cavalier - blocage", "8/4n1k1/1p3p2/8/P7/5P2/3B2K1/8 w - - 0 30"),
    ("Fous de meme couleur - blocage", "8/6k1/1p1b1p2/8/P7/5P2/3B2K1/8 b - - 0 30"),
]
for nom, fen in finales:
    board = chess.Board(fen)
    assert board.is_valid() and not board.is_game_over(claim_draw=True), nom
    positions.append({"id": len(positions) + 1, "categorie": "Finale", "nom": nom,
                      "fen": board.fen(en_passant="fen"), "origine_san": "Position construite, materiel egal"})

assert len(positions) == 30
assert len({p["fen"].split(" ")[0] for p in positions}) == 30, "Plateaux dupliques"
anciens = (root.parent / "PionsPasses-2026-11-19/protocole.txt").read_text(encoding="utf-8-sig")
for p in positions:
    assert p["fen"] not in anciens, p["nom"]
with (root / "positions.csv").open("w", newline="", encoding="utf-8") as f:
    writer = csv.DictWriter(f, fieldnames=list(positions[0]))
    writer.writeheader()
    writer.writerows(positions)
(root / "positions.tsv").write_text("".join(p["nom"] + "\t" + p["categorie"] + "\t" + p["fen"] + "\n" for p in positions), encoding="utf-8")
print(json.dumps({"positions": len(positions), "validation": "30 positions legales, nouvelles, plateaux distincts",
                  "sha256_tsv": hashlib.sha256((root / "positions.tsv").read_bytes()).hexdigest()}, ensure_ascii=False))
