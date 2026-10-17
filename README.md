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
* Un garde-fou **refuse de modifier un pilote de démarrage ou système** (cela empêcherait Windows de démarrer).
* La suppression d'applications, d'Edge/OneDrive et de fonctionnalités Windows se fait un élément à la fois, jamais en masse.

## Interface

Plusieurs thèmes (Forge, Océan, Minuit, Néon, Papier, Contraste élevé), navigation latérale ou en haut, affichage en cartes,
grille ou liste compacte, recherche globale, profils Gaming / Confidentialité / 1 clic. Menu **Apparence** en haut à droite.

## Développement

```
dotnet test                                   # moteur + catalogue
scripts/build-release.ps1 -Version 0.4.1      # installeur, portable, zip (Inno Setup requis)
python tools/validate-catalog.py <fichier>    # valider des réglages ajoutés
```

Les réglages vivent dans `src/ForgeOpti.Core/Catalog/*.json`. Le banc d'essai Hyper-V (`tools/vm`) applique tous les réglages
dans une VM, redémarre, vérifie la santé de Windows puis annule tout et compare à l'état d'origine.

## Licence

Les réglages sont réécrits à partir de faits publics (clés de registre, services, tâches) ; aucun code de projets tiers n'est copié.
