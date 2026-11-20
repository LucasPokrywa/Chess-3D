"""Produit le rapport final uniquement après vérification des 60 parties."""
import csv
import json
from pathlib import Path

root = Path(__file__).resolve().parent
resume = json.loads((root / "verification.json").read_text(encoding="utf-8"))
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
assert resume["parties_verifiees"] == 60 and resume["paires_verifiees"] == 30
assert sum(resume["bilan_paires"].values()) == 30
with (root / "paires.csv").open(encoding="utf-8", newline="") as f:
    paires = list(csv.DictReader(f))
avec = resume["bilan_nouveau"]
bp = resume["bilan_paires"]
points = resume["points_nouveau"]
terminees = 60 - avec["indeterminees"]
ancien_points = terminees - points
lettres = {"victoires": "V", "nulles": "N", "defaites": "D", "indeterminees": "*"}
resultats_paire = {"gagnees": "Gagnée", "egalite": "Égalité", "perdues": "Perdue", "indeterminees": "Indéterminée"}
f = lambda n: str(n).replace(".", ",")
rapport = [
    "# Tournoi de 30 nouvelles positions — bonus de pion passé", "",
    f"Comparaison native Unity 6000.3.11f1 à 500 ms par coup. **60 parties, 30 paires**, et {resume['coups_verifies']} demi-coups vérifiés indépendamment avec python-chess {resume['python_chess']}.", "",
    "## Résultat global", "",
    f"Version avec bonus : **{avec['victoires']} victoires, {avec['nulles']} nulles, {avec['defaites']} défaites**, {avec['indeterminees']} parties indéterminées.", "",
    f"Score : **{f(points)}/{terminees} points**, soit **{f(round(points * 100 / terminees, 1))} %**, contre {f(ancien_points)}/{terminees} pour la version sans bonus.", "",
    f"Paires : **{bp['gagnees']} gagnées, {bp['egalite']} à égalité, {bp['perdues']} perdues**, {bp['indeterminees']} indéterminées.", "",
    "Une paire est gagnée au-delà de 1 point sur 2, à égalité à 1 point sur 2 et perdue en dessous. Une partie indéterminée laisse sa paire indéterminée ; elle n'est jamais assimilée à une nulle.", "",
    "| Catégorie | Parties | Points avec bonus | Paires gagnées | Égalités | Paires perdues |",
    "|---|---:|---:|---:|---:|---:|",
]
for nom, c in resume["categories"].items():
    rapport.append(f"| {nom} | {c['paires'] * 2} | {f(c['points_nouveau'])}/20 | {c['gagnees']} | {c['egalite']} | {c['perdues']} |")
rapport += ["", "## Interprétation du résultat", "",
    "Le score global est pratiquement équilibré, légèrement en faveur de l'ancienne version : **29,5 contre 30,5 points**. Ce tournoi ne confirme donc pas un gain global de force du bonus constant de 30 centipions.", "",
    "Le nombre de paires gagnées est supérieur au nombre de paires perdues, mais les marges diffèrent : **8 paires gagnées à 1,5–0,5**, **4 paires perdues à 0–2**, une perdue à 0,5–1,5 et 17 à égalité. Cela explique le score global inférieur à 50 % malgré 8 paires gagnées contre 5 perdues.", "",
    "Les finales sont légèrement favorables au bonus, avec **10,5/20 points**, mais seule la paire « majorités opposées » départage les versions : une paire gagnée et neuf à égalité. Les milieux de jeu donnent 10/20 et les ouvertures 9/20. Le meilleur repérage des pions passés ne se traduit pas ici par une amélioration globale des résultats.", "",
    "Le poids de 30 centipions reste donc un candidat à calibrer et à valider avant de cumuler d'autres critères d'évaluation. Aucune valeur ni règle du moteur n'a été changée pendant ou après les parties.", "",
    "## Protocole fixé avant les parties", "",
    "- Ancienne version : évaluation du commit `bb62b8c`, sans bonus de pion passé.",
    "- Nouvelle version : évaluation locale avec le seul bonus constant de **30 centipions** pour chaque pion passé. Les empreintes des sources sont dans `manifest.json`.",
    "- Même Minimax, mêmes règles, mêmes positions et mêmes optimisations précédentes. Classes de référence renommées uniquement pour compiler les deux variantes ensemble.",
    "- Même entrée que le jeu : `ChercherAvecBudget`, **500 ms/coup**, profondeur maximale **10**, table de **500 000 entrées neuve à chaque appel**. Une recherche atteignant la profondeur maximale peut finir plus tôt.",
    "- Recherche séquentielle, une seule instance Unity de test, mode batch sans rendu. Deux parties par FEN, couleurs inversées ; la version prenant les Blancs en premier alterne entre les paires.",
    "- Échauffement hors score : une recherche par version sur le premier départ d'ouverture et le premier départ de finale.",
    "- Mat et pat ; réclamation automatique par l'arbitre de la triple répétition ou des 50 coups ; cas simples de matériel insuffisant. Clé de répétition : plateau, trait, roques et prise en passant uniquement si légale.",
    "- Aucun abandon, aucune adjudication à partir du score d'évaluation. Plafond technique de **400 demi-coups depuis chaque FEN**, fixé avant lancement ; résultat `*` si atteint.", "",
    "## Liste et contrôle d'équilibre", "",
    "Les 30 plateaux sont légaux, distincts et nouveaux par rapport au tournoi précédent. La liste est exportée dans `positions.csv` et `positions.tsv` avant le lancement. Le lanceur vérifie son SHA-256 avant toute partie.", "",
    f"Gel UTC : `{manifest['ListeGeleeUtc']}`. SHA-256 du TSV : `{manifest['PositionsSha256']}`.", "",
    "Dix familles d'ouvertures : Espagnole, Italienne, Najdorf, Dragon, Française, Caro-Kann, gambit dame refusé, Slave, Est-indienne et Anglaise symétrique. Dix milieux de jeu proviennent de prolongements de ces familles. Les dix finales comprennent six finales de pions, deux finales de tours, fou contre cavalier et fous de même couleur.", "",
    "Avant toute partie, les positions ont été contrôlées à profondeur 4 avec l'ancienne version. Le seuil retenu pour les milieux de jeu était ±100 centipions : les dix départs finaux sont entre **−48 et +40 centipions pour les Blancs**. Un premier départ Slave, trop déséquilibré (+173 pour le joueur au trait), a été raccourci avant gel. Ce filtre est une estimation du moteur, pas une preuve d'équilibre. Les finales ont un matériel égal et mettent en jeu la création, la course ou le blocage de pions passés.", "",
    "Aucune position n'a été ajoutée, retirée ou modifiée après le premier coup. Les premiers douze matchs de l'essai précédent ne sont pas mélangés avec ces résultats.", "",
    "## Résultats des 30 paires", "",
    "V/N/D sont les résultats de la version avec bonus, avec chaque couleur.", "",
    "| N° | Départ | Bonus avec les Blancs | Bonus avec les Noirs | Points | Paire |",
    "|---:|---|---|---|---:|---|",
]
for p in paires:
    rapport.append(f"| {p['id']} | {p['position']} | {lettres[p['nouveau_blanc']]} | {lettres[p['nouveau_noir']]} | {f(p['points_nouveau'])}/2 | {resultats_paire[p['resultat_paire']]} |")
rapport += ["", "## Temps et profondeur", "",
    "| Version | Coups | Profondeur terminée moyenne | Temps réel médian | Temps maximal |",
    "|---|---:|---:|---:|---:|",
]
for nom, s in resume["statistiques"].items():
    rapport.append(f"| {nom} | {s['coups']} | {f(round(s['profondeur_moyenne'], 3))} | {f(s['temps_median_ms'])} ms | {f(s['temps_max_ms'])} ms |")
rapport += ["", "Les moyennes de profondeur portent sur des positions rencontrées en partie, qui diffèrent entre variantes. Elles ne mesurent pas directement un coût par position identique.", "",
    "## Vérification et limites", "",
    "Le vérificateur indépendant rejoue les PGN et les CSV : légalité et SAN de chaque coup, FEN complets avant/après, couleurs inversées, identité des FEN avec la liste gelée, somme des compteurs et motifs de fin. Bilan : **OK**. Les empreintes de la liste sont contrôlées à nouveau. Les résultats comprennent **29 mats, 30 nulles par triple répétition et une nulle par matériel insuffisant**. Aucune partie n'atteint le plafond de 400 demi-coups.", "",
    "Le bilan décrit ce lot et ce budget. Les ouvertures et milieux sont issus de dix familles communes ; les 30 départs ne sont donc pas 30 échantillons totalement indépendants. Le temps limité rend la profondeur et les décisions sensibles aux variations de charge. Aucun Elo n'est déduit de ce tournoi.", "",
    "## Fichiers conservés", "",
    "- `parties.pgn`, `parties.csv`, `coups.csv` : toutes les parties et mesures.",
    "- `paires.csv`, `verification.json` : score global, regroupement par paire et vérification.",
    "- `positions.csv`, `positions.tsv`, `positions.sha256`, `manifest.json`, `protocole.txt` : liste figée et configuration.",
    "- `equilibre_avant.csv` : contrôle initial à profondeur 4.",
    "- Sources des lanceurs temporaires et scripts de préparation/vérification : reproductibilité.",
    "", "Le moteur du projet n'a pas été modifié pour ce tournoi.",
]
(root / "rapport.md").write_text("\n".join(rapport) + "\n", encoding="utf-8")
print(f"Rapport final : {root / 'rapport.md'}")
