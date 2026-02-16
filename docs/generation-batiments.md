# Génération des bâtiments (Urban)

## Vue d'ensemble
La génération des bâtiments est gérée dans `EdgeWallGenerator` quand le pattern `Urban` est sélectionné.

Pipeline simplifié :
1. Découpage de la carte en blocs réguliers + rues.
2. Placement d'un bâtiment aléatoire dans chaque lot valide.
3. Calcul des étages/sous-sols (étages aléatoires de 1 à 8).
4. Génération des murs extérieurs (avec fenêtres), puis d'une porte.
5. Génération de l'intérieur selon un type de bâtiment.
6. Ajout optionnel de fortifications Hesco entre bâtiments proches.

## Règles clés
- Un bâtiment est au minimum `6x6` cases.
- Les étages suivent la règle métier : **1 étage = 2 cases de hauteur**.
- Le nombre d'étages des bâtiments urbains est fixé aléatoirement entre `1` et `8`.
- Les sous-sols sont plus probables pour les grandes empreintes.

## Types d'intérieurs
Le générateur choisit aléatoirement parmi :
- `SmallHouse`
- `Apartment`
- `Office`
- `Warehouse`

Chaque type possède sa stratégie d'agencement (séparations, couloir, cubicules, rayonnages, etc.).

## Intégration dans la map
`MapGenerator` récupère les bâtiments générés (`LastGeneratedBuildings`) et les convertit en `BuildingFootprintData` avec clamp des étages.

Ces empreintes servent ensuite à créer des escaliers internes sur les bâtiments multi-étages afin d'assurer la circulation verticale.
