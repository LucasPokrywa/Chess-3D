# HalloweenChess

HalloweenChess est un jeu d’échecs 3D sous Unity qui se joue en solo contre une IA intégrée. Le menu propose deux décors, **Classique** dans un salon et **Halloween** dans une crypte, ainsi que le choix de la difficulté et du camp.

## Prise en main

Le projet utilise **Unity 6.3 LTS (`6000.3.11f1`)**, **C#** et **Universal Render Pipeline (URP)**. La révision de l’éditeur et les dépendances sont enregistrées dans [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) et [manifest.json](Packages/manifest.json).

```bash
git clone https://github.com/LucasPokrywa/Chess-3D.git
```

1. Ajoute dans Unity Hub le dossier cloné qui contient `Assets`, `Packages` et `ProjectSettings`.
2. Ouvre le projet avec Unity `6000.3.11f1` et attends la fin de l’import des ressources.
3. Dans la fenêtre **Project**, ouvre [MainMenu.unity](Assets/Scenes/MainMenu.unity) et lance le mode **Play**.
4. Clique sur **JOUER**, choisis le plateau, la difficulté et ton camp, puis lance la partie.

## Jouer

- **Clic gauche** sur une de tes pièces, puis sur une case indiquée pour la déplacer.
- **Blancs**, **Noirs** ou **Aléatoire** dans le menu. L’IA prend l’autre camp et joue automatiquement à son tour. Le plateau pivote si tu joues les Noirs.
- **Menu** ou **Échap** pour revenir au menu principal pendant une partie.
- **Haut-parleur**, en bas à droite, pour couper ou rétablir la musique. Survole l’icône pour régler le volume. Le *Nocturne op. 9 n° 2* de Chopin joue en boucle et continue pendant les changements de scène. Le réglage est mémorisé, avec un volume initial de **7 %**.

Le jeu gère les captures, le roque, la prise en passant, l’échec, le mat et le pat. En partie, une promotion transforme automatiquement le pion en dame. La partie peut aussi se terminer par nulle après cinq occurrences de la même position, après 75 coups sans prise ni mouvement de pion, ou dans les cas de matériel insuffisant reconnus par le jeu.

### Charger une position FEN

En mode Play, renseigne le champ **Fen A Importer** du composant `ChessGameController`, puis lance **FEN → Importer le FEN saisi** depuis le menu contextuel du composant. La commande **FEN → Copier la position actuelle** exporte les six champs de la position. Le [guide FEN](FEN.md) détaille le format et les conditions d’import.

## Configurer l’IA

Dans chacune des deux scènes de jeu, sélectionne **Board → ChessIA** dans l’Inspector. Chaque configuration de difficulté contient un nom, une profondeur maximale et un budget de réflexion par coup. Le menu de jeu propose les quatre préréglages suivants et le niveau **Personnalisé**.

| Difficulté | Profondeur maximale | Budget par coup |
| --- | ---: | ---: |
| Facile | P5 | 1 000 ms |
| Moyen | P6 | 2 000 ms |
| Difficile | P7 | 5 000 ms |
| Expert | P8 | 25 000 ms |

Les profondeurs sont exprimées en **demi-coups**. La liste **Configurations** et la sélection **Difficulté active** sont modifiables dans l’Inspector. En mode Personnalisé, la profondeur peut aller jusqu’à **P10** et le budget jusqu’à **100 000 ms**. Les valeurs personnalisées enregistrées dans le dépôt sont **P10 et 5 038 ms** en Classique, **P7 et 1 000 ms** en Halloween. Depuis le menu, le niveau choisi est appliqué au lancement de la partie.

Le champ **Mode Moteur** est indépendant de la difficulté.

- **A — Référence** est sélectionné par défaut dans les deux scènes.
- **D — Génération progressive et bitboards** ajoute la génération progressive des coups et une représentation hybride associant le tableau de cases à des masques de bits.

Les deux moteurs partagent les règles et l’évaluation. La recherche séquentielle utilise un approfondissement itératif, Negamax avec alpha-bêta, une PVS interne, une table de transposition et une recherche de quiétude. Le calcul s’exécute en arrière-plan. Quand le budget expire, l’IA rend le meilleur coup de la dernière profondeur entièrement terminée ou un coup légal de secours si aucune itération n’a abouti.

## Structure du projet

| Emplacement | Rôle |
| --- | --- |
| [`Assets/Scenes/`](Assets/Scenes/) | Menu principal et scènes de jeu Classique et Halloween |
| [`Assets/Scripts/Menu/`](Assets/Scripts/Menu/) | Interface du menu, paramètres de lancement et animation des pièces classiques en 3D |
| [`Assets/Scripts/Pieces/`](Assets/Scripts/Pieces/) | Partie, sélection, déplacement animé des pièces et application des coups de l’IA |
| [`Assets/Scripts/Regles/`](Assets/Scripts/Regles/) | Position logique, FEN, génération et validation des coups |
| [`Assets/Scripts/IA/`](Assets/Scripts/IA/) | Difficultés, recherche A/D et évaluation incrémentale |
| [`Assets/Scripts/Board/`](Assets/Scripts/Board/) | Construction des plateaux, décors et effets d’ambiance |
| [`Assets/Sounds/`](Assets/Sounds/) | Musique du jeu, pilotée par [`MusiqueAmbiance`](Assets/Scripts/Audio/MusiqueAmbiance.cs) |
| [`Assets/Model/`](Assets/Model/) et [`Assets/Materials/`](Assets/Materials/) | Modèles 3D et matériaux |
| [`Assets/Editor/`](Assets/Editor/) et [`Benchmarks/`](Benchmarks/) | Commandes de vérification, benchmarks et relevés de performance |

## Vérifications et benchs

Dans l’éditeur, le menu **Halloween Chess** propose notamment les commandes suivantes.

- **Verifier les regles** et **Verifier le FEN** pour la logique du jeu et les positions importées.
- **Verifier le tri des coups**, **Verifier la table de transposition** et **Tester IA - Suites de quietude** pour la recherche.
- **Verifier l'evaluation incrementale** et **Verifier les compteurs IA** pour les évaluations et les statistiques.
- **Benchmark IA** à profondeur fixe P4, **Benchmark IA - 500 ms** et **Benchmark IA - 1000 ms** pour comparer les temps, les profondeurs atteintes et les nœuds visités.

Lance les benchmarks hors mode Play et lis les résultats dans la **Console**. Le [protocole du benchmark](BENCHMARK_IA.md) décrit les positions, les mesures et les marqueurs du Profiler. Les [rapports d’expérimentation](Benchmarks/IA/) conservent les résultats des campagnes précédentes.

## Créer un build

Ouvre **File → Build Profiles**, sélectionne la plateforme cible et vérifie la liste des scènes. Garde **MainMenu** en première position, suivi de **ChessGame_Classique** et **ChessGame_Halloween**. Lance **Build** ou **Build And Run**. La commande **Chess 3D → Vérifier les scènes du build** fournit aussi un contrôle dans l’éditeur. [Documentation Unity sur la liste des scènes](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/build-profile-scene-list)

## Licence

Le dépôt contient la licence [GNU AGPL v3](LICENSE). Les ressources tierces et l’enregistrement musical peuvent être soumis à leurs propres conditions d’utilisation.
