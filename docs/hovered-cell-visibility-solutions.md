# Visibilité de la case survolée à l’intérieur des bâtiments

## Problème observé
Quand la case survolée est dans un bâtiment (murs, planchers, étage supérieur, mobilier), l’indicateur visuel peut disparaître car les géométries intermédiaires sont rendues avant/après avec un test de profondeur qui masque le marqueur.

## Solutions proposées

### 1) "X-Ray" local autour de la case survolée (recommandé)
- Détecter les obstacles entre caméra et centre de la case survolée avec un rayon (ou un petit cône de rayons).
- Appliquer une transparence temporaire uniquement aux meshes bloquants:
  - murs,
  - planchers/plafonds d’étages au-dessus,
  - gros meubles.
- Conserver un contour lumineux de la case en premier plan.

**Avantages**
- Très lisible pour le joueur.
- Préserve la lecture globale de la scène.

**Inconvénients**
- Nécessite une logique d’occlusion par type d’objet (pas seulement les murs).

---

### 2) Rendu du marqueur en "always on top" (depth override)
- Dessiner l’outline/halo de la case survolée dans une passe dédiée:
  - soit avec `DepthStencilState.None`,
  - soit avec un bias et un test depth moins strict,
  - soit via un second pass écran (post-process) projeté depuis la case.
- Ajouter un anneau + icône verticale (petit pilier lumineux) pour éviter les ambiguïtés.

**Avantages**
- Implémentation rapide.
- Garantit que la case est toujours visible.

**Inconvénients**
- Peut casser la perception 3D (le marqueur traverse visuellement les murs).

---

### 3) Coupe dynamique des étages/planchers au-dessus de l’étage visé
- Si la case survolée est à l’étage `N`, masquer/atténuer les planchers et objets des étages `> N` dans une zone locale.
- Appliquer une transition douce (fade 150–250 ms) pour éviter le "pop".

**Avantages**
- Très naturel dans un jeu tactique multi-étages.
- Réduit aussi le bruit visuel pour le déplacement et le tir.

**Inconvénients**
- Plus coûteux côté architecture de rendu (gestion par étages + zones locales).

---

### 4) "Ghost camera" contextuelle (micro-ajustement)
- Lorsqu’une case survolée est fortement occultée:
  - remonter légèrement la caméra,
  - ou faire un petit orbit automatique,
  - ou activer un angle alternatif temporaire.
- Revenir à la position initiale quand le survol change.

**Avantages**
- Pas besoin de masquer d’objets.
- Conserve le réalisme du décor.

**Inconvénients**
- Peut gêner certains joueurs (caméra qui bouge trop).

---

### 5) Assistance UX complémentaire (faible coût)
- Afficher en HUD:
  - coordonnées de la case survolée,
  - étage ciblé,
  - distance/AP estimés.
- Ajouter un trait de liaison écran -> monde (leader line).

**Avantages**
- Très peu risqué techniquement.
- Utile même si l’occlusion persiste.

**Inconvénients**
- Ne résout pas à lui seul la lisibilité 3D.

## Recommandation de rollout
1. **Court terme**: solution 2 + solution 5 (quick win).
2. **Moyen terme**: solution 1 avec occlusion multi-objets.
3. **Long terme**: solution 3 (coupe d’étage locale) avec une couche caméra minimale issue de la solution 4, activée uniquement en dernier recours.

### Détail proposé pour l’étape long terme (3)
- **Déclenchement principal**: appliquer d’abord la coupe locale des étages/planchers au-dessus de l’étage cible (rayon fixe ou dépendant du zoom).
- **Fallback caméra très discret** (seulement si la case reste occultée):
  - translation verticale limitée (ex: +0.5 à +1.5 case max),
  - micro-orbite bornée (ex: ±6–10° max),
  - interpolation douce (150–250 ms) et retour immédiat quand l’occlusion disparaît.
- **Garde-fous UX**:
  - ne jamais enchaîner plusieurs mouvements caméra successifs sans temporisation,
  - désactiver l’ajustement si le joueur est en rotation manuelle active,
  - exposer une option d’accessibilité: `Ajustement caméra anti-occlusion: Off / Discret / Standard`.

## Critères d’acceptation (QA)
- La case survolée reste identifiable en < 300 ms dans 95% des cas d’occlusion lourde.
- Aucun "flash" visuel agressif lors du changement de case.
- Impact perf < 1 ms/frame en moyenne sur la scène de test urbaine.
