# Publier HalloweenChess

Le [workflow de release](../.github/workflows/release.yml) construit deux versions de HalloweenChess avec GameCI, Windows x86_64 et Linux x86_64. Il se lance à l’envoi d’un tag `v*` ou manuellement pour un tag qui existe déjà sur GitHub. Unity et le code sont pris au commit du tag. La version inscrite dans le build est le nom du tag sans son premier `v`.

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

La publication utilise le `GITHUB_TOKEN` fourni par Actions. Les jobs de préparation et de compilation ont accès en lecture au dépôt. Seul le job **Publier la release GitHub** a la permission `contents: write`. Vérifie que la politique Actions du dépôt ou de l’organisation autorise les actions utilisées et la publication avec ce jeton.

## Lancer une release avec un nouveau tag

Après avoir intégré le workflow dans le dépôt, vérifie que `main` pointe localement sur la révision à publier et que le nom du tag est libre. Envoie uniquement le tag voulu.

```bash
git switch main
git pull --ff-only
git tag -a v1.2.3 -m "HalloweenChess v1.2.3"
git push origin refs/tags/v1.2.3
```

Un tag comme `v1.3.0-alpha.1`, `v1.3.0-beta.1` ou `v1.3.0-rc.1` crée une préversion. Le commit pointé par un nouveau tag doit contenir le workflow pour que son envoi déclenche une compilation.

## Lancer une release pour un tag existant

1. Ouvre **Actions → Release HalloweenChess → Run workflow**. Le workflow doit être présent sur la branche par défaut.
2. Sélectionne la branche qui contient le workflow, généralement `main`.
3. Entre le tag publié sur GitHub dans le champ **tag**, par exemple `v1.2.3`, puis lance l’exécution. Le code compilé provient du tag saisi.

Un tag avec une release déjà publiée est ignoré. Les exécutions qui se chevauchent pour le même tag sont regroupées et la plus ancienne est annulée. Si un tag a déjà été publié, utilise un nouveau tag pour une nouvelle version du jeu.

## Suivre la compilation et récupérer les builds

Dans **Actions → Release HalloweenChess**, ouvre l’exécution et déplie le job concerné. L’étape **Exécuter les contrôles dans Unity** montre les contrôles C# lancés et les erreurs éventuelles. L’étape **Compiler avec GameCI** contient les journaux de l’éditeur et de la compilation de chaque plateforme.

Chaque build réussi dépose son ZIP dans **Artifacts**, avec une conservation de **14 jours**. Quand les deux jobs ont terminé, leurs archives apparaissent dans **Releases → [tag] → Assets**. Pour `v1.2.3`, les fichiers sont les suivants.

- `HalloweenChess-v1.2.3-Windows-x64.zip`
- `HalloweenChess-v1.2.3-Linux-x64.zip`

Extrais toute l’archive. Lance `HalloweenChess.exe` sous Windows, ou `./HalloweenChess` sous Linux (`./HalloweenChess.x86_64` si Unity a ajouté l’extension). Le ZIP Linux conserve le bit exécutable. Si un outil d’extraction l’a retiré, restaure-le avec `chmod +x` sur l’exécutable.

GitHub génère les notes de version. Le workflow prépare un brouillon, téléverse les deux ZIP, vérifie leur présence et leur taille, puis publie. Un échec avant la publication bloque la release. En cas d’arrêt forcé ou de panne de l’API pendant cette étape, un brouillon peut subsister dans **Releases**. Vérifie-le et supprime uniquement le brouillon de cette exécution avant de relancer.

## Dépanner une exécution

| Symptôme | Quoi vérifier |
| --- | --- |
| Activation Unity refusée | Vérifie le type de licence, le couple e-mail/mot de passe, le contenu du `.ulf` ou le numéro de série, ainsi que les sièges disponibles. Si un seul build peut activer Unity à la fois, ajoute la variable Actions `UNITY_SERIALIZE_BUILDS=true` pour exécuter Windows et Linux en série. |
| Tag introuvable | Vérifie que le tag existe sur GitHub, commence par `v` et correspond à celui saisi. Si un tag a été déplacé pendant l’exécution, le pipeline arrête la publication. |
| Contrôle en échec | Ouvre le journal du job **Vérifications Unity existantes**. Exécute le contrôle nommé dans le log depuis le menu **Halloween Chess** de Unity, au commit du tag. |
| Image Unity introuvable ou compilation en échec | Vérifie la version affichée dans le job de préparation et la disponibilité des modules Windows et Linux pour cette version dans les images GameCI. Commence par la première erreur Unity du log. |
| Espace disque insuffisant | Vérifie `df` dans **Faire de la place pour l’image Unity**. Le workflow retire le SDK Android préinstallé des runners temporaires Ubuntu pour faire de la place aux images GameCI. Si les images et le cache dépassent encore l’espace disponible, utilise un runner qui dispose de plus de disque. |
| Objet Git LFS introuvable | Vérifie que les objets LFS du tag ont été envoyés sur GitHub et que le quota LFS du dépôt est disponible. |
| Publication refusée | Vérifie la permission `contents: write` du job de publication, les règles du dépôt sur les releases et l’existence d’un brouillon pour le même tag. |

## Cache, délais et quotas

Le cache `Library` est séparé par environnement de runner, version Unity, cible, usage et configuration du projet. La clé tient compte du manifeste, du verrou de packages et des réglages Unity. Un cache approchant peut accélérer une compilation ultérieure, selon la portée de visibilité du cache pour le tag. Le premier import est généralement le plus long. [Règles de visibilité et d’éviction du cache](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching)

Chaque exécution utilise un job Unity pour les contrôles et deux jobs Unity pour les builds. Le parallélisme réduit le délai total, tandis que chaque job consomme son propre temps de runner. Les artefacts Actions, le cache et d’éventuels téléchargements LFS comptent dans des quotas distincts. Le cache a un seuil d’éviction de **10 Go par dépôt** par défaut. Les quotas et coûts d’Actions dépendent du plan et de la visibilité du dépôt. [Limites GitHub Actions](https://docs.github.com/en/actions/reference/limits) · [Facturation GitHub Actions](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
