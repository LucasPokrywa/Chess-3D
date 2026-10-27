# Positions FEN

La scène démarre toujours sur la position initiale, sans lire ni importer de FEN.

Pendant le jeu, le menu contextuel du composant (trois points dans l'Inspector)
propose **FEN > Importer le FEN saisi** et **FEN > Copier la position actuelle**.
Pour importer, coller un FEN dans **Fen A Importer**, puis déclencher la commande.
Modifier le champ seul ne charge rien ; un champ vide est refusé à l'import.
Le chargement est refusé pendant une animation. Un FEN invalide ne remplace pas
la partie courante ; l'erreur apparaît dans la console. Un modèle absent est
remplacé par une forme provisoire, avec un avertissement indiquant son type.

```csharp
// Position indépendante de la scène, utilisable par une IA.
PositionEchecs position = PositionEchecs.DepuisFen(PositionEchecs.FenInitial);
string fen = position.ExporterFen();

// Partie 3D : charge aussi les modèles et actualise le statut.
bool charge = controleur.ChargerFen(fen);
string positionActuelle = controleur.ExporterFen();
```

Les six champs sont conservés : pièces, trait, droits de roque, case de prise
en passant, demi-coups sans prise ni mouvement de pion, numéro du coup.
L'import convertit les lettres anglaises FEN vers les codes français internes.
La cible de prise en passant est exportée après chaque double pas de pion,
même sans prise disponible, conformément à la
[spécification FEN, section 16.1](https://www.oocities.org/marochess/pgn/pgn_spec/pgn_1616.htm).

L'import vérifie la structure, la présence d'un roi de chaque couleur et la
cohérence des droits de roque et de prise en passant. Il ne reconstitue pas
l'historique ni ne garantit qu'une position a pu être atteinte légalement.
Les compteurs sont conservés ; les règles de nulle par répétition ou nombre
de coups ne sont pas ajoutées par ce changement.

Contrôles : menu Unity **Halloween Chess > Verifier le FEN**
(également inclus dans **Verifier les regles**).
