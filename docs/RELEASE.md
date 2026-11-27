# Publier HalloweenChess

Quatre workflows compilent HalloweenChess avec GameCI. À l’envoi d’un tag `v*`, ils s’enchaînent dans cet ordre.

1. [Release Windows HalloweenChess](../.github/workflows/release.yml) compile le ZIP standard Windows x86_64.
2. [Release Linux HalloweenChess](../.github/workflows/release-linux.yml) compile le ZIP standard Linux x86_64, récupère le ZIP Windows et publie les deux dans la même release.
3. [Development Windows HalloweenChess](../.github/workflows/development-windows.yml) et [Development Linux HalloweenChess](../.github/workflows/development-linux.yml) compilent chacun leur ZIP de débogage et l’ajoutent à la release publiée.

Chaque compilation utilise la version de Unity et le code du commit du tag. La version inscrite dans le build est le nom du tag sans son premier `v`.

## État du projet

- Le projet Unity se trouve à la racine. Sa version est actuellement **6000.3.11f1**, lue dans [`ProjectVersion.txt`](../ProjectSettings/ProjectVersion.txt) à chaque exécution.
- Les packages sont définis par [`manifest.json`](../Packages/manifest.json) et [`packages-lock.json`](../Packages/packages-lock.json). Ils incluent URP 17.3, Input System, uGUI et Unity Test Framework. Le backend Standalone est Mono.
- Les [scènes activées](../ProjectSettings/EditorBuildSettings.asset) sont, dans l’ordre, **MainMenu**, **ChessGame_Classique** et **ChessGame_Halloween**.
- Les contrôles du projet sont des commandes de l’éditeur dans [`Assets/Editor`](../Assets/Editor/). Le job **Vérifications Unity existantes** lance celles du tag pour les règles et le FEN, les pions passés, l’évaluation incrémentale, le tri, la table de transposition, les compteurs et la quiétude. Il n’y a actuellement aucune suite NUnit ou Unity Test Framework déclarée par le projet. Une exception ou une erreur signalée par un contrôle bloque les builds.
- Git LFS est utilisable, mais aucun fichier du dépôt n’y est actuellement suivi. Les checkouts qui chargent les ressources Unity activent LFS.

## Configurer la licence Unity

Dans le dépôt GitHub, ouvre **Settings → Secrets and variables → Actions → New repository secret**. Utilise la méthode correspondant à la licence réellement associée à ton compte. [Activation GitHub Actions de GameCI](https://game.ci/docs/github/activation/)

| Licence | Secrets à renseigner |
| --- | --- |
| **Unity Personal** | `UNITY_EMAIL`, `UNITY_PASSWORD` et `UNITY_LICENSE` |
| **Unity Pro avec numéro de série** | `UNITY_EMAIL`, `UNITY_PASSWORD` et `UNITY_SERIAL` |

Pour **Unity Personal**, connecte-toi dans Unity Hub. Dans **Preferences → Licenses**, utilise **Add** pour activer la licence Personal. Si le Hub a créé le fichier `Unity_lic.ulf`, copie son contenu complet dans `UNITY_LICENSE`. Sous Windows, GameCI indique `C:\ProgramData\Unity\Unity_lic.ulf`. Utilise **Unity Hub** pour cette procédure avec Unity 6. Si le Hub ne génère pas de `.ulf`, suis la section Personal du guide d’activation GameCI avant de lancer le workflow. [Procédure Unity Personal](https://game.ci/docs/github/activation/#personal-license)

Pour **Unity Pro**, renseigne le numéro de série de ton abonnement dans `UNITY_SERIAL` et libère un siège si nécessaire. Configure **un seul** des deux secrets `UNITY_LICENSE` et `UNITY_SERIAL`. Garde le fichier `.ulf`, le numéro de série et les identifiants dans les secrets GitHub. [Procédure Unity Pro](https://game.ci/docs/github/activation/#professional-license)

La publication utilise le `GITHUB_TOKEN` fourni par Actions. Les jobs de préparation et de compilation ont accès en lecture au dépôt. La publication des ZIP nécessite `contents: write`. Les workflows qui récupèrent les références ou ZIP d’une exécution précédente utilisent `actions: read`.

## Lancer une release avec un nouveau tag

Après avoir intégré les quatre workflows à la branche par défaut, vérifie que `main` pointe localement sur la révision à publier et que le nom du tag est libre. Envoie uniquement le tag voulu.

```bash
git switch main
git pull --ff-only
git tag -a v1.2.3 -m "HalloweenChess v1.2.3"
git push origin refs/tags/v1.2.3
```

Un tag comme `v1.3.0-alpha.1`, `v1.3.0-beta.1` ou `v1.3.0-rc.1` crée une préversion. Le commit du tag doit contenir le workflow Release Windows. Les trois workflows suivants doivent être présents sur la branche par défaut pour recevoir les événements de fin. [Déclencheur `workflow_run` de GitHub Actions](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run)

## Lancer une release pour un tag existant

1. Ouvre **Actions → Release Windows HalloweenChess → Run workflow**. Le workflow doit être présent sur la branche par défaut.
2. Sélectionne la branche qui contient le workflow, généralement `main`.
3. Entre le tag publié sur GitHub dans le champ **tag**, par exemple `v1.2.3`, puis lance l’exécution. Le code compilé provient du tag saisi.

Pour relancer uniquement la compilation Linux standard, ouvre **Release Linux HalloweenChess → Run workflow** et saisis l’identifiant numérique d’une exécution Release Windows réussie dont le ZIP est encore disponible. L’identifiant figure à la fin de l’URL de l’exécution.

Pour sortir une nouvelle version du jeu, utilise un nouveau tag. Pour compléter une release existante, lance manuellement le workflow Development de la plateforme voulue.

## Générer les builds Development

Les builds Development démarrent après la réussite du workflow Release Linux, qui publie les deux archives standard. Si un build Development échoue, les fichiers standard et le ZIP Development de l’autre plateforme restent disponibles dès que leurs workflows réussissent.

Pour une release déjà publiée, tu peux ouvrir **Actions → Development Windows HalloweenChess** ou **Development Linux HalloweenChess**, cliquer sur **Run workflow** et renseigner le tag. Chaque ZIP apparaît dans **Releases → [tag] → Assets** après la réussite du workflow de sa plateforme. Il reste aussi disponible dans **Artifacts** pendant 14 jours. [Documentation GameCI sur les artefacts](https://game.ci/docs/github/builder/#storing-the-build)

- `HalloweenChess-v1.2.3-Windows-x64-Development.zip`
- `HalloweenChess-v1.2.3-Linux-x64-Development.zip`

Ces builds comprennent les symboles de débogage, permettent d’attacher un débogueur C# et peuvent être sélectionnés dans le Profiler Unity. Le jeu démarre directement. L’attente du débogueur et la connexion automatique au Profiler sont désactivées. [Options de build Unity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildOptions.html)

Le workflow ajoute les ZIP Development manquants et conserve les ZIP présents ainsi que les notes de version.

## Suivre la compilation et récupérer les builds

Dans **Actions**, ouvre le workflow de la plateforme et du type de build voulus, puis déplie le job concerné. L’étape **Exécuter les contrôles dans Unity** montre les contrôles C# lancés et les erreurs éventuelles. L’étape **Compiler avec GameCI** contient les journaux de l’éditeur et de la compilation.

Chaque build réussi dépose son ZIP dans **Artifacts**, avec une conservation de **14 jours**. Une fois le job de publication terminé, les archives apparaissent dans **Releases → [tag] → Assets**. Pour `v1.2.3`, les deux archives standard portent ces noms.

- `HalloweenChess-v1.2.3-Windows-x64.zip`
- `HalloweenChess-v1.2.3-Linux-x64.zip`

Extrais toute l’archive. Lance `HalloweenChess.exe` sous Windows, ou `./HalloweenChess` sous Linux (`./HalloweenChess.x86_64` si Unity a ajouté l’extension). Le ZIP Linux conserve le bit exécutable. Si un outil d’extraction l’a retiré, restaure-le avec `chmod +x` sur l’exécutable.

Pour une nouvelle release, GitHub génère les notes de version. Le workflow prépare un brouillon, téléverse les ZIP demandés, vérifie leur présence et leur taille, puis publie. Un échec avant la publication bloque la release. En cas d’arrêt forcé ou de panne de l’API pendant cette étape, un brouillon peut subsister dans **Releases**. Vérifie-le et supprime uniquement le brouillon de cette exécution avant de relancer.

## Dépanner une exécution

| Symptôme | Quoi vérifier |
| --- | --- |
| Activation Unity refusée | Vérifie le type de licence, le couple e-mail/mot de passe, le contenu du `.ulf` ou le numéro de série, ainsi que les sièges disponibles. Si une seule activation peut être utilisée, ajoute la variable Actions `UNITY_SERIALIZE_BUILDS=true` pour sérialiser les deux workflows Development issus de la même release. Les builds standard s’enchaînent déjà. |
| Tag introuvable | Vérifie que le tag existe sur GitHub, commence par `v` et correspond à celui saisi. Si un tag a été déplacé pendant l’exécution, le pipeline arrête la publication. |
| Contrôle en échec | Ouvre le journal du job **Vérifications Unity existantes**. Exécute le contrôle nommé dans le log depuis le menu **Halloween Chess** de Unity, au commit du tag. |
| Image Unity introuvable ou compilation en échec | Vérifie la version affichée dans le job de préparation et la disponibilité des modules Windows et Linux pour cette version dans les images GameCI. Commence par la première erreur Unity du log. |
| Espace disque insuffisant | Vérifie `df` dans **Faire de la place pour l’image Unity**. Le workflow retire le SDK Android préinstallé des runners temporaires Ubuntu pour faire de la place aux images GameCI. Si les images et le cache dépassent encore l’espace disponible, utilise un runner qui dispose de plus de disque. |
| Objet Git LFS introuvable | Vérifie que les objets LFS du tag ont été envoyés sur GitHub et que le quota LFS du dépôt est disponible. |
| Publication refusée | Vérifie la permission `contents: write` du job de publication, les règles du dépôt sur les releases et l’existence d’un brouillon pour le même tag. |
| Release immuable | GitHub bloque l’ajout d’archives après publication. Télécharge les ZIP Development depuis **Artifacts**. [Documentation GitHub](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository#editing-a-release) |

## Cache, délais et quotas

Le cache `Library` est séparé par environnement de runner, version Unity, cible, usage et configuration du projet. La clé tient compte du manifeste, du verrou de packages et des réglages Unity. Un cache approchant peut accélérer une compilation ultérieure, selon la portée de visibilité du cache pour le tag. Le premier import est généralement le plus long. [Règles de visibilité et d’éviction du cache](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching)

Chaque workflow utilise un job Unity pour les contrôles puis un job de build. Les builds standard sont successifs. Les deux workflows Development peuvent s’exécuter en parallèle après la publication des builds standard. Les artefacts Actions, le cache et d’éventuels téléchargements LFS comptent dans des quotas distincts. Le cache a un seuil d’éviction de **10 Go par dépôt** par défaut. Les quotas et coûts d’Actions dépendent du plan et de la visibilité du dépôt. [Limites GitHub Actions](https://docs.github.com/en/actions/reference/limits) · [Facturation GitHub Actions](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
