"""Adapte le lanceur du premier tournoi sans modifier le moteur."""
from pathlib import Path

root = Path(__file__).resolve().parent
ancien = root.parent / "PionsPasses-2026-11-19"
source = (ancien / "PassedPawnMatchTemp.cs.txt").read_text(encoding="utf-8-sig")
source = source.replace("PassedPawnMatchTemp", "PassedPawnMatch30Temp")
source = source.replace("PionsPasses-2026-11-19", "PionsPasses-30Positions-2026-11-19")
source = source.replace("LimiteDemiCoups = 200", "LimiteDemiCoups = 400")
source = source.replace("200 demi-coups", "400 demi-coups")
source = source.replace("/12", "/60")
source = source.replace("new[] { 0, 4 }", "new[] { 0, 20 }")
debut = source.index("            var positions = new List<Cas>();")
fin = source.index("            using (var protocole", debut)
source = source[:debut] + '''            string fichierPositions = Path.Combine(Sortie, "positions.tsv");
            string empreinteAttendue = File.ReadAllText(Path.Combine(Sortie, "positions.sha256")).Trim();
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                string empreinte = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(fichierPositions))).Replace("-", "").ToLowerInvariant();
                if (empreinte != empreinteAttendue) throw new Exception("La liste gelee a ete modifiee.");
            }
            if (File.Exists(Path.Combine(Sortie, "parties.csv"))) throw new Exception("Resultats deja presents : ne pas ecraser un tournoi.");
            var positions = new List<Cas>();
            foreach (string ligne in File.ReadAllLines(fichierPositions))
            {
                string[] champs = ligne.Split('\\t');
                if (champs.Length != 3) throw new Exception("Ligne de position invalide.");
                positions.Add(new Cas(champs[0], champs[2]));
            }
            if (positions.Count != 30) throw new Exception("30 positions requises.");
''' + source[fin:]
source = source.replace('protocole.WriteLine("Tournoi pions passes | UTC="', 'protocole.WriteLine("Tournoi pions passes 30 positions | UTC="')
source = source.replace('foreach (Cas cas in positions) protocole.WriteLine', 'protocole.WriteLine("SHA256 positions.tsv=" + empreinteAttendue);\n                foreach (Cas cas in positions) protocole.WriteLine')
(root / "PassedPawnMatch30Temp.cs.txt").write_text(source, encoding="utf-8")
destination = Path("Temp/IA30Match/Assets/Editor/PassedPawnMatch30Temp.cs")
destination.write_text(source, encoding="utf-8")

verification = (ancien / "verifier_parties.py").read_text(encoding="utf-8-sig")
verification = verification.replace("import csv\n", "import csv\nimport hashlib\n")
verification = verification.replace('root = Path(__file__).resolve().parent',
    'root = Path(__file__).resolve().parent\nmanifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))\nlimite = manifest["LimiteDemiCoups"]\nassert hashlib.sha256((root / "positions.tsv").read_bytes()).hexdigest() == manifest["PositionsSha256"].lower()\nassert hashlib.sha256((root / "positions.csv").read_bytes()).hexdigest() == manifest["PositionsCsvSha256"].lower()')
verification = verification.replace('elif motif == "plafond 200 demi-coups":', 'elif motif == f"plafond {limite} demi-coups":')
verification = verification.replace('assert len(coups) == 200', 'assert len(coups) == limite')
verification += '''
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
'''
(root / "verifier_parties.py").write_text(verification, encoding="utf-8")
