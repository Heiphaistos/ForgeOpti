# ForgeOpti

Optimiseur Windows 10 / 11 tout-en-un, orienté jeux et performances, **entièrement en français** et **100 % réversible**.
Il réunit dans un seul logiciel les réglages que proposent des outils comme Atlas, ReviOS, WinScript ou Fluent Cleaner :
télémétrie, widgets, Copilot, services, tâches planifiées, plans d'alimentation, réseau, GPU, interface, Explorateur,
suppression d'applications (avec réinstallation), Edge/OneDrive, et un module de nettoyage du disque.

## Télécharger

Dernière version : [Releases](https://github.com/Heiphaistos/ForgeOpti/releases/latest)

| Fichier | Usage |
|---|---|
| `ForgeOpti-Setup-X.exe` | Installeur (raccourcis, désinstallation). |
| `ForgeOpti-Portable-X.exe` | **Version portable** : un seul `.exe`, aucune installation, aucun assistant. Elle s'extrait une fois dans votre profil puis se lance. |
| `ForgeOpti-win-x64.zip` | Archive utilisée par la mise à jour automatique. |

Chaque fichier a son empreinte SHA-256 (`.sha256`). Aucune dépendance à installer (.NET et Windows App SDK sont embarqués).

## Sécurité : comment c'est réversible

* Avant le premier changement de la session, ForgeOpti crée un **point de restauration Windows**.
* Chaque interrupteur mémorise la **valeur d'origine** (registre, tâche planifiée) : l'éteindre remet exactement l'état précédent,
  même si plusieurs réglages touchent la même clé.
* Les réglages sont classés **Sûr / Modéré / Avancé**. Les « Avancé » ne sont jamais dans l'optimisation en 1 clic.
* Les interrupteurs **ne changent rien tout de suite** : ils préparent une sélection. Une barre en bas indique les modifications en attente ;
  « Voir et appliquer » affiche la liste exacte, puis chaque changement est relu dans le registre et un compte rendu est affiché.
* Les réglages **restent en place après les mises à jour de Windows** : au lancement et à chaque ouverture de session (tâche planifiée
  `ForgeOpti-Verification`), ForgeOpti remet en place les valeurs qu'une mise à jour aurait défaites. Page **Santé et persistance** pour le faire à la main.
* **Jamais touchés par les profils ni par le 1 clic** : impression, Bluetooth, découverte réseau et partages, mises à jour et protection de Windows.
* Un garde-fou **refuse de modifier un pilote de démarrage ou système** (cela empêcherait Windows de démarrer).
* La suppression d'applications, d'Edge/OneDrive et de fonctionnalités Windows se fait un élément à la fois, jamais en masse.

## Interface

Plusieurs thèmes (Forge, Océan, Minuit, Néon, Papier, Contraste élevé), navigation latérale ou en haut, affichage en cartes,
grille ou liste compacte, recherche globale, profils Gaming / Bureautique / Hybride / Confidentialité / 1 clic (présélection ajustable) et profils personnels enregistrables. Menu **Apparence** en haut à droite.

## Développement

```
dotnet test                                   # moteur + catalogue
scripts/build-release.ps1 -Version 0.6.0      # installeur, portable, zip (Inno Setup requis)
python tools/validate-catalog.py <fichier>    # valider des réglages ajoutés
```

Les réglages vivent dans `src/ForgeOpti.Core/Catalog/*.json`. Le banc d'essai Hyper-V (`tools/vm`) applique tous les réglages
dans une VM, redémarre, vérifie la santé de Windows puis annule tout et compare à l'état d'origine.

## Licence

Les réglages sont réécrits à partir de faits publics (clés de registre, services, tâches) ; aucun code de projets tiers n'est copié.

## Signature des mises à jour

Le manifeste `latest.json` (+ `.sig`, ECDSA P-256) est vérifié avec la clé publique épinglée `UpdateVerifier.PublicKey`
(vide tant que non renseignée : toute mise à jour est alors refusée). Générer le couple avec
`scripts/new-update-key.ps1 -PrivateOut <fichier hors dépôt>`, coller la clé publique dans `UpdateVerifier.cs`,
et mettre la clé privée dans le secret CI `FORGEOPTI_UPDATE_KEY`.
