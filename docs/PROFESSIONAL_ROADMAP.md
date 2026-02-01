# EpicLegends - Feuille de Route Professionnelle

## Basé sur les Standards de l'Industrie du Jeu Vidéo

Ce document compile les méthodologies utilisées par miHoYo (Genshin Impact), FromSoftware (Dark Souls), CD Projekt Red (Witcher 3), et les best practices Unity.

---

## 1. ÉTAT ACTUEL DU PROJET

### Audit de Complétion (Janvier 2026)

| Phase | Système | Complétion | Priorité |
|-------|---------|------------|----------|
| **PHASE 1** | Player Controller | 95% | ✅ |
| | Player Stats | 90% | ✅ |
| | Camera Controller | 90% | ✅ |
| | GameManager | 85% | ✅ |
| **PHASE 2** | Combat Controller | 80% | ⚠️ |
| | Weapon System | 75% | ⚠️ |
| | Damage System | 85% | ✅ |
| | Skills System | 75% | 🔴 |
| | Enemy AI | 70% | ⚠️ |
| **PHASE 3** | Inventory | 95% | ✅ |
| | Items/Equipment | 90% | ✅ |
| | Quest System | 60% | 🔴 |
| | Loot/Drops | 0% | 🔴 |
| | NPC/Dialogue | 30% | 🔴 |
| **PHASE 4** | Level/XP | 85% | ✅ |
| | Class System | 85% | ✅ |
| | Talent Tree | 20% | ⚠️ |
| | Crafting | 0% | ⚠️ |
| **PHASE 5** | Multiplayer | 0% | ❌ Déprioritisé |
| | Creatures/Mounts | 50% | ⚠️ |
| **PHASE 6** | Save/Load | 90% | ✅ |
| | UI System | 80% | ⚠️ |
| | Audio | 30% | ⚠️ |
| | VFX | 40% | ⚠️ |

**Complétion Globale Estimée: 65-70%**

---

## 2. CORE GAMEPLAY LOOP (FONDATION)

### Le Loop Définitif d'EpicLegends

```
┌─────────────────────────────────────────────────────────────────┐
│                    MOMENT-TO-MOMENT (Secondes)                   │
│  Observer → Cibler → Attaquer/Skill → Esquiver → Récompense     │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    MINUTE-TO-MINUTE (Minutes)                    │
│  Explorer Zone → Combattre Groupes → Looter → Gérer Inventaire  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    HOUR-TO-HOUR (Heures)                         │
│  Compléter Quêtes → Vaincre Boss → Level Up → Upgrade Equipment │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    DAY-TO-DAY (Sessions)                         │
│  Progresser Histoire → Débloquer Zones → Maîtriser Classe       │
└─────────────────────────────────────────────────────────────────┘
```

### Critères de Succès du Core Loop

- [ ] Le combat est **satisfaisant** dès la première attaque
- [ ] Le joueur comprend **immédiatement** ce qu'il doit faire
- [ ] Chaque action a un **feedback** clair (visuel + audio)
- [ ] La progression est **ressentie** à chaque session
- [ ] Le joueur veut **rejouer** après chaque session

---

## 3. DEFINITION OF DONE (DoD)

### Checklist Obligatoire pour CHAQUE Feature

```
CODE QUALITY
[ ] Code compile sans erreurs
[ ] Code compile sans warnings
[ ] Suit les conventions de nommage (_camelCase, PascalCase)
[ ] Pas de GetComponent() dans Update/FixedUpdate
[ ] Scripts < 200 lignes (sinon split)

TESTING
[ ] Unit tests écrits (60%+ coverage pour logique)
[ ] Tests passent
[ ] Test manuel selon acceptance criteria
[ ] Pas de bugs critiques

PERFORMANCE
[ ] 60 FPS stable
[ ] Pas de memory leaks
[ ] Pas de frame drops notables

INTEGRATION
[ ] Fonctionne avec systèmes existants
[ ] Events/callbacks connectés
[ ] UI mis à jour si nécessaire

DOCUMENTATION
[ ] Méthodes publiques documentées (XML comments)
[ ] README mis à jour si nouvelle feature majeure
```

---

## 4. ROADMAP DÉTAILLÉE

### MILESTONE 1: VERTICAL SLICE (8 semaines)

**Objectif**: 1 zone complète et polie démontrant le core gameplay

#### Sprint 1-2: Combat Polish (Semaines 1-4)

**User Stories**:

```
US-001: Exécution des Skills
As a player,
I want to cast skills with hotkeys (1-4),
So that I can use special abilities in combat

Acceptance Criteria:
- GIVEN j'ai un skill équipé sur slot 1
- WHEN je presse la touche 1
- THEN le skill s'exécute avec animation
- AND les dégâts sont appliqués aux ennemis dans la zone
- AND le cooldown démarre
- AND le mana est consommé
- AND les VFX jouent
```

```
US-002: Loot Drops
As a player,
I want enemies to drop items when killed,
So that I can collect rewards

Acceptance Criteria:
- GIVEN un ennemi meurt
- WHEN sa vie atteint 0
- THEN des items apparaissent au sol (basé sur loot table)
- AND les items ont un VFX de brillance
- AND je peux les ramasser en m'approchant
- AND ils s'ajoutent à mon inventaire
```

```
US-003: Combat Feedback
As a player,
I want clear feedback when I hit enemies,
So that combat feels satisfying

Acceptance Criteria:
- GIVEN je touche un ennemi
- WHEN mon hitbox entre en collision
- THEN des damage numbers apparaissent
- AND un VFX d'impact joue
- AND un SFX d'impact joue
- AND l'ennemi a un hitstun visuel
- AND la caméra fait un léger shake
```

#### Sprint 3-4: Quest & NPC (Semaines 5-8)

```
US-004: NPC Dialogue
As a player,
I want to talk to NPCs,
So that I can receive quests and information

Acceptance Criteria:
- GIVEN je suis proche d'un NPC
- WHEN je presse E (interact)
- THEN le dialogue UI s'ouvre
- AND le texte s'affiche progressivement
- AND je peux choisir des réponses si applicable
- AND le jeu est en pause pendant le dialogue
```

```
US-005: Quest Tracking
As a player,
I want to track my active quests,
So that I know what to do next

Acceptance Criteria:
- GIVEN j'ai une quête active
- WHEN je joue
- THEN les objectifs s'affichent dans le HUD
- AND les objectifs se mettent à jour en temps réel
- AND je reçois une notification quand un objectif est complété
- AND je peux ouvrir un journal de quêtes détaillé
```

---

### MILESTONE 2: CORE SYSTEMS COMPLETE (12 semaines)

#### Sprint 5-6: Loot & Economy

```
US-006: Loot Tables
As a designer,
I want configurable loot tables,
So that I can balance drop rates

Implementation:
- LootTable ScriptableObject
- Drop chance par item
- Rarity weights
- Level scaling
```

```
US-007: Vendor System
As a player,
I want to buy/sell items at vendors,
So that I can acquire equipment

Acceptance Criteria:
- GIVEN je parle à un marchand
- WHEN j'ouvre son shop
- THEN je vois ses items disponibles
- AND je vois mes items vendables
- AND les prix sont affichés
- AND je peux acheter si j'ai assez d'or
- AND je peux vendre mes items
```

#### Sprint 7-8: Talent Tree

```
US-008: Talent Tree UI
As a player,
I want a visual talent tree,
So that I can plan my character build

Acceptance Criteria:
- GIVEN j'ouvre le menu talents
- WHEN l'UI s'affiche
- THEN je vois l'arbre avec tous les talents
- AND les talents débloqués sont en couleur
- AND les talents verrouillés sont grisés
- AND les connexions entre talents sont visibles
- AND je vois mes points disponibles
```

#### Sprint 9-10: Crafting System

```
US-009: Basic Crafting
As a player,
I want to craft items from materials,
So that I can create equipment

Acceptance Criteria:
- GIVEN j'ai les matériaux requis
- WHEN je sélectionne une recette
- THEN je vois les ingrédients nécessaires
- AND je peux crafter si j'ai tout
- AND l'item est ajouté à mon inventaire
- AND les matériaux sont consommés
```

#### Sprint 11-12: Polish Pass

- Audio implementation (SFX + Music)
- VFX polish
- UI animations
- Bug fixing
- Performance optimization

---

### MILESTONE 3: CONTENT EXPANSION (16 semaines)

#### Zones à Créer

| Zone | Niveau | Ennemis | Boss | Quêtes |
|------|--------|---------|------|--------|
| Village de départ | 1-5 | 3 types | - | 5 |
| Forêt des Murmures | 5-10 | 4 types | Treant | 8 |
| Donjon des Ombres | 10-15 | 5 types | Lich | 6 |
| Montagnes de Givre | 15-20 | 4 types | Dragon | 7 |
| Château Maudit | 20-25 | 6 types | Final Boss | 10 |

#### Content Pipeline

1. **Zone Design** (Layout, landmarks)
2. **Enemy Placement** (Spawn points, patrols)
3. **Quest Integration** (NPCs, objectifs)
4. **Loot Distribution** (Coffres, drops)
5. **Polish** (Lighting, ambiance, audio)

---

### MILESTONE 4: ALPHA (4 semaines)

**Critères Alpha**:
- [ ] Toutes les features implémentées
- [ ] Jouable du début à la fin
- [ ] 6-8h de gameplay
- [ ] Feature freeze déclaré

### MILESTONE 5: BETA (6 semaines)

**Focus**:
- Bug fixing intensif
- Balancing (spreadsheet economy)
- Optimization
- Playtesting externe (10+ testeurs)

### MILESTONE 6: GOLD (4 semaines)

**Critères Gold**:
- [ ] 0 bugs critiques
- [ ] 0 bugs hauts
- [ ] Performance targets atteints
- [ ] Build release testé

---

## 5. SYSTÈMES À IMPLÉMENTER (Détail)

### 5.1 Skill Execution System (CRITIQUE)

**Fichier**: `Assets/Scripts/Skills/SkillExecutor.cs`

```csharp
// Architecture recommandée
public class SkillExecutor : MonoBehaviour
{
    public void ExecuteSkill(SkillData skill, Transform caster, Vector3 targetPos)
    {
        // 1. Vérifier mana/cooldown
        // 2. Consommer ressources
        // 3. Jouer animation
        // 4. Spawner VFX
        // 5. Trouver targets (OverlapSphere)
        // 6. Appliquer dégâts/effets
        // 7. Jouer SFX
        // 8. Démarrer cooldown
    }
}
```

### 5.2 Loot System

**Fichiers requis**:
- `LootTable.cs` - ScriptableObject définissant drops
- `LootDropper.cs` - Component sur enemies
- `WorldItem.cs` - Item physique dans le monde
- `ItemPickup.cs` - Logic de ramassage

### 5.3 NPC Dialogue System

**Fichiers requis**:
- `DialogueData.cs` - ScriptableObject (existe partiellement)
- `DialogueManager.cs` - Singleton gérant conversations
- `DialogueUI.cs` - Affichage du dialogue
- `NPCInteractable.cs` - Component sur NPCs

### 5.4 Quest Completion Logic

**Manquant dans QuestManager**:
- Tracking des objectifs (kill count, items collected)
- Événements de complétion
- Distribution des récompenses
- Quest log UI

---

## 6. BALANCING SPREADSHEET

### Stats de Base par Niveau

| Level | HP | Mana | Stamina | Base Damage | XP Required |
|-------|-----|------|---------|-------------|-------------|
| 1 | 100 | 50 | 100 | 10 | 0 |
| 5 | 200 | 80 | 120 | 25 | 1000 |
| 10 | 350 | 120 | 150 | 45 | 5000 |
| 15 | 550 | 170 | 180 | 70 | 15000 |
| 20 | 800 | 230 | 220 | 100 | 35000 |
| 25 | 1100 | 300 | 260 | 135 | 70000 |

### Formule XP

```
XP_Required(level) = 100 * (level ^ 2.2)
```

### Formule Dégâts

```
Final_Damage = (Base_Damage + Weapon_Damage) * (1 + Strength/100) * Element_Multiplier * Crit_Multiplier
```

### Drop Rates par Rarity

| Rarity | Base Drop Rate | Gold Value Multiplier |
|--------|----------------|----------------------|
| Common | 60% | 1x |
| Uncommon | 25% | 2.5x |
| Rare | 10% | 6x |
| Epic | 4% | 15x |
| Legendary | 0.9% | 40x |
| Mythic | 0.1% | 100x |

---

## 7. PRIORITÉS IMMÉDIATES

### Cette Semaine

1. **Implémenter SkillExecutor**
   - Permet enfin d'utiliser les 18 skills créés
   - Ouvre le combat complet

2. **Implémenter LootSystem**
   - LootTable ScriptableObject
   - Drops sur mort des ennemis
   - Pickup items

3. **Compléter DialogueSystem**
   - DialogueManager
   - DialogueUI basique
   - NPCInteractable

### Semaine Prochaine

4. **Quest Completion Logic**
   - Objective tracking
   - Reward distribution
   - Quest UI

5. **Combat Feedback**
   - Damage numbers
   - Hit VFX/SFX
   - Camera shake

---

## 8. TIMELINE ESTIMÉE

| Phase | Durée | Date Fin Estimée |
|-------|-------|------------------|
| Vertical Slice | 8 semaines | Mars 2026 |
| Core Systems | 12 semaines | Juin 2026 |
| Content Expansion | 16 semaines | Octobre 2026 |
| Alpha | 4 semaines | Novembre 2026 |
| Beta | 6 semaines | Décembre 2026 |
| Gold | 4 semaines | Janvier 2027 |

**Release Cible: Q1 2027** (MVP complet et poli)

---

## 9. RESSOURCES

### Références de Design
- Genshin Impact (elemental combat, exploration)
- Dark Souls (combat feel, difficulty)
- Diablo 3 (loot, progression)
- Zelda BotW (exploration, freedom)

### Assets Recommandés (gratuits)
- Synty POLYGON Starter Pack
- Mixamo (animations)
- Kenney Assets (UI, audio)
- Quaternius (3D models)

### Documentation
- [Unity Best Practices](https://unity.com/how-to)
- [GDC Vault](https://gdcvault.com)
- [Game Developer](https://gamedeveloper.com)

---

**Document créé**: 30 Janvier 2026
**Basé sur**: Standards industrie AAA, méthodologies Agile/Scrum
**Applicable à**: EpicLegends Action-RPG
