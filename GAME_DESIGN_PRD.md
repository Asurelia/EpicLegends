# EpicLegends - Product Requirements Document (PRD)
## Document de Conception pour Ralph Loop

**Version:** 1.0.0
**Date:** 2026-01-29
**Moteur:** Unity 6.3 LTS (URP)
**Type:** RPG/Action-RPG avec Base Building et Capture de Créatures

---

## Table des Matières

1. [Vision du Projet](#vision-du-projet)
2. [Architecture Modulaire](#architecture-modulaire)
3. [Phase 1 - Core Foundation](#phase-1---core-foundation)
4. [Phase 2 - Combat & Créatures](#phase-2---combat--créatures)
5. [Phase 3 - Base Building](#phase-3---base-building)
6. [Phase 4 - Progression & Contenu](#phase-4---progression--contenu)
7. [Phase 5 - Multijoueur Co-op](#phase-5---multijoueur-co-op)
8. [Phase 6 - Polish & Optimisation](#phase-6---polish--optimisation)
9. [Spécifications Techniques](#spécifications-techniques)
10. [Métriques de Succès](#métriques-de-succès)
11. [Direction Artistique & Assets](#direction-artistique--assets)

---

## Vision du Projet

### Concept Central
Un RPG d'action en monde ouvert combinant:
- **Combat élémentaire dynamique** (inspiré Genshin Impact)
- **Capture et élevage de créatures** (inspiré Palworld/Pokemon)
- **Construction de base automatisée** (inspiré Factorio/Satisfactory)
- **Défense de territoire** (inspiré Tower Defense)
- **Progression RPG profonde** (inspiré JRPG classiques)
- **Expérience coopérative** (2-4 joueurs)

### Principes de Design
1. **ZERO Monétisation** - Tout le contenu accessible par le jeu
2. **Respect du joueur** - Pas de grind artificiel, progression satisfaisante
3. **Profondeur sans complexité** - Systèmes interconnectés mais intuitifs
4. **Rejouabilité** - Builds variés, contenu procédural, NG+

### Public Cible
- Joueurs de RPG/JRPG cherchant profondeur
- Fans de jeux de capture de créatures
- Amateurs de base building et automation
- Groupes d'amis cherchant une expérience co-op longue durée

---

## Architecture Modulaire

### Priorités de Développement

```
CRITIQUE (MVP)     IMPORTANT           ÉTENDU              BONUS
═══════════════    ═══════════════     ═══════════════     ═══════════════
Player Controller  Elemental System    Factory Automation  Weather System
Basic Combat       Creature Capture    Tower Defense       Day/Night Cycle
Inventory          Creature AI         Co-op Multiplayer   Photo Mode
Save System        Base Building       New Game+           Achievements
Camera System      Quest System        Procedural Dungeons Mod Support
Health/Stats       Crafting Basic      Boss Raids
UI Framework       Equipment System    Guild System
```

### Dépendances des Modules

```
                    ┌─────────────────┐
                    │  CORE ENGINE    │
                    │  (Phase 1)      │
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
         ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ COMBAT SYSTEM   │ │ CREATURE SYSTEM │ │ BASE BUILDING   │
│ (Phase 2a)      │ │ (Phase 2b)      │ │ (Phase 3)       │
└────────┬────────┘ └────────┬────────┘ └────────┬────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   PROGRESSION   │
                    │   (Phase 4)     │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   MULTIPLAYER   │
                    │   (Phase 5)     │
                    └─────────────────┘
```

---

## Phase 1 - Core Foundation

### Module 1.1: Player Controller
**Priorité:** CRITIQUE
**Estimation:** ~50 tâches

#### 1.1.1 Movement System
```
Tâches:
├── [P1-001] Créer PlayerController base avec Rigidbody
├── [P1-002] Implémenter mouvement WASD avec Input System
├── [P1-003] Ajouter sprint avec stamina consumption
├── [P1-004] Implémenter saut avec ground detection
├── [P1-005] Ajouter double saut (débloquable)
├── [P1-006] Implémenter dash/dodge avec i-frames
├── [P1-007] Ajouter wall running (débloquable)
├── [P1-008] Implémenter swimming mechanics
├── [P1-009] Ajouter climbing system (surfaces marquées)
├── [P1-010] Implémenter gliding (débloquable)
├── [P1-011] Créer animation state machine
├── [P1-012] Ajouter root motion blending
├── [P1-013] Implémenter terrain adaptation (slopes)
├── [P1-014] Ajouter footstep audio system
└── [P1-015] Créer particle effects (dust, water splash)
```

#### 1.1.2 Camera System
```
Tâches:
├── [P1-016] Implémenter third-person camera avec Cinemachine
├── [P1-017] Ajouter camera collision detection
├── [P1-018] Implémenter lock-on target system
├── [P1-019] Ajouter camera shake pour impacts
├── [P1-020] Implémenter zoom dynamique (combat/exploration)
├── [P1-021] Créer first-person toggle pour aiming
├── [P1-022] Ajouter cinematic camera pour events
├── [P1-023] Implémenter photo mode camera
└── [P1-024] Créer sensitivity settings
```

#### 1.1.3 Input Management
```
Tâches:
├── [P1-025] Configurer Input System actions
├── [P1-026] Implémenter rebinding UI
├── [P1-027] Ajouter gamepad support complet
├── [P1-028] Implémenter mouse+keyboard layout
├── [P1-029] Ajouter input buffering pour combos
├── [P1-030] Créer context-sensitive inputs
└── [P1-031] Implémenter accessibility options
```

### Module 1.2: Stats & Resources
**Priorité:** CRITIQUE
**Estimation:** ~30 tâches

#### 1.2.1 Stat System
```
Tâches:
├── [P1-032] Créer ScriptableObject StatDefinition
├── [P1-033] Implémenter base stats (STR, DEX, INT, VIT, etc.)
├── [P1-034] Ajouter derived stats (ATK, DEF, CRIT, etc.)
├── [P1-035] Implémenter stat modifiers (flat, percent, multiply)
├── [P1-036] Créer modifier stacking rules
├── [P1-037] Ajouter stat scaling formulas
├── [P1-038] Implémenter level-based stat growth
├── [P1-039] Créer stat preview UI
└── [P1-040] Ajouter stat comparison tool
```

#### 1.2.2 Resource System
```
Tâches:
├── [P1-041] Créer Health component générique
├── [P1-042] Implémenter Mana/Energy resource
├── [P1-043] Ajouter Stamina avec regeneration
├── [P1-044] Créer resource bars UI
├── [P1-045] Implémenter damage numbers popup
├── [P1-046] Ajouter healing effects
├── [P1-047] Créer status effect system base
├── [P1-048] Implémenter buff/debuff icons
└── [P1-049] Ajouter resource regeneration rules
```

### Module 1.3: Inventory System
**Priorité:** CRITIQUE
**Estimation:** ~40 tâches

#### 1.3.1 Item Framework
```
Tâches:
├── [P1-050] Créer ScriptableObject ItemData base
├── [P1-051] Implémenter item categories (Weapon, Armor, Consumable, etc.)
├── [P1-052] Ajouter item rarity system (Common → Legendary)
├── [P1-053] Créer item stacking logic
├── [P1-054] Implémenter item weight system (optionnel)
├── [P1-055] Ajouter item durability (optionnel)
├── [P1-056] Créer item tooltips
├── [P1-057] Implémenter item comparison
└── [P1-058] Ajouter item sorting/filtering
```

#### 1.3.2 Inventory Management
```
Tâches:
├── [P1-059] Créer Inventory component
├── [P1-060] Implémenter grid-based inventory UI
├── [P1-061] Ajouter drag & drop items
├── [P1-062] Créer quick slots (hotbar)
├── [P1-063] Implémenter auto-sort function
├── [P1-064] Ajouter inventory expansion system
├── [P1-065] Créer item dropping/pickup
├── [P1-066] Implémenter item transfer (chests, trade)
└── [P1-067] Ajouter inventory search
```

#### 1.3.3 Storage System
```
Tâches:
├── [P1-068] Créer Storage container component
├── [P1-069] Implémenter chest interaction
├── [P1-070] Ajouter bank/vault system
├── [P1-071] Créer shared storage (base)
└── [P1-072] Implémenter storage categories
```

### Module 1.4: Save System
**Priorité:** CRITIQUE
**Estimation:** ~25 tâches

#### 1.4.1 Serialization
```
Tâches:
├── [P1-073] Créer SaveData structure
├── [P1-074] Implémenter JSON serialization
├── [P1-075] Ajouter binary serialization (production)
├── [P1-076] Créer save file encryption
├── [P1-077] Implémenter save versioning/migration
├── [P1-078] Ajouter auto-save system
├── [P1-079] Créer multiple save slots
├── [P1-080] Implémenter cloud save (Steam/platforms)
└── [P1-081] Ajouter save file backup
```

#### 1.4.2 Save Points
```
Tâches:
├── [P1-082] Créer save point interaction
├── [P1-083] Implémenter checkpoint system
├── [P1-084] Ajouter save anywhere option (configurable)
└── [P1-085] Créer save confirmation UI
```

### Module 1.5: UI Framework
**Priorité:** CRITIQUE
**Estimation:** ~35 tâches

#### 1.5.1 Core UI
```
Tâches:
├── [P1-086] Créer UI Manager singleton
├── [P1-087] Implémenter UI stack system (menus)
├── [P1-088] Ajouter transition animations
├── [P1-089] Créer modal dialog system
├── [P1-090] Implémenter notification system
├── [P1-091] Ajouter tutorial highlight system
├── [P1-092] Créer loading screen
└── [P1-093] Implémenter pause menu
```

#### 1.5.2 HUD
```
Tâches:
├── [P1-094] Créer HUD layout
├── [P1-095] Implémenter health/mana bars
├── [P1-096] Ajouter minimap
├── [P1-097] Créer quest tracker
├── [P1-098] Implémenter compass/waypoint
├── [P1-099] Ajouter damage direction indicator
├── [P1-100] Créer interaction prompts
└── [P1-101] Implémenter crosshair system
```

#### 1.5.3 Menus
```
Tâches:
├── [P1-102] Créer main menu
├── [P1-103] Implémenter settings menu complet
├── [P1-104] Ajouter character menu
├── [P1-105] Créer map screen
├── [P1-106] Implémenter journal/codex
└── [P1-107] Ajouter credits screen
```

---

## Phase 2 - Combat & Créatures

### Module 2.1: Combat System
**Priorité:** CRITIQUE
**Estimation:** ~80 tâches

#### 2.1.1 Basic Combat
```
Tâches:
├── [P2-001] Créer WeaponData ScriptableObject
├── [P2-002] Implémenter attack input handling
├── [P2-003] Ajouter combo system (light/heavy)
├── [P2-004] Créer hitbox/hurtbox system
├── [P2-005] Implémenter damage calculation formula
├── [P2-006] Ajouter knockback system
├── [P2-007] Créer stagger/poise system
├── [P2-008] Implémenter invincibility frames
├── [P2-009] Ajouter perfect dodge reward
├── [P2-010] Créer parry/counter system
├── [P2-011] Implémenter block/guard mechanic
├── [P2-012] Ajouter guard break
├── [P2-013] Créer charged attacks
├── [P2-014] Implémenter aerial combos
└── [P2-015] Ajouter finisher moves
```

#### 2.1.2 Elemental System (Style Genshin)
```
Tâches:
├── [P2-016] Créer ElementType enum (Fire, Water, Ice, Electric, Wind, Earth, Light, Dark)
├── [P2-017] Implémenter elemental damage types
├── [P2-018] Créer ElementalReaction ScriptableObject
├── [P2-019] Implémenter Vaporize (Fire + Water = 2x damage)
├── [P2-020] Ajouter Melt (Fire + Ice = 2x damage)
├── [P2-021] Implémenter Overload (Fire + Electric = AoE explosion)
├── [P2-022] Ajouter Superconduct (Ice + Electric = DEF down)
├── [P2-023] Implémenter Electro-Charged (Water + Electric = chain damage)
├── [P2-024] Ajouter Frozen (Water + Ice = immobilize)
├── [P2-025] Implémenter Swirl (Wind + any = spread element)
├── [P2-026] Ajouter Crystallize (Earth + any = shield)
├── [P2-027] Créer elemental status application
├── [P2-028] Implémenter elemental gauge system
├── [P2-029] Ajouter elemental resistance system
├── [P2-030] Créer visual effects par élément
├── [P2-031] Implémenter environmental interactions (water conducts electricity, etc.)
└── [P2-032] Ajouter elemental puzzles framework
```

#### 2.1.3 Weapon Types
```
Tâches:
├── [P2-033] Implémenter Sword (balanced, combo-focused)
├── [P2-034] Ajouter Greatsword (slow, high damage, stagger)
├── [P2-035] Implémenter Dual Blades (fast, low damage, combo)
├── [P2-036] Ajouter Spear (range, thrust attacks)
├── [P2-037] Implémenter Bow (ranged, charged shots)
├── [P2-038] Ajouter Staff (magic focused, elemental)
├── [P2-039] Implémenter Gauntlets (close range, parry bonus)
├── [P2-040] Ajouter Shield + Sword (defensive)
├── [P2-041] Implémenter Scythe (AoE, lifesteal)
├── [P2-042] Ajouter unique weapon movesets
└── [P2-043] Créer weapon switching mid-combo
```

#### 2.1.4 Skills & Abilities
```
Tâches:
├── [P2-044] Créer SkillData ScriptableObject
├── [P2-045] Implémenter skill tree UI
├── [P2-046] Ajouter active skills (cooldown-based)
├── [P2-047] Créer passive skills (always active)
├── [P2-048] Implémenter ultimate abilities (gauge-based)
├── [P2-049] Ajouter skill leveling system
├── [P2-050] Créer skill inheritance/transfer
├── [P2-051] Implémenter skill combos (chain skills)
├── [P2-052] Ajouter class-specific skills
└── [P2-053] Créer shared utility skills
```

### Module 2.2: Enemy System
**Priorité:** CRITIQUE
**Estimation:** ~50 tâches

#### 2.2.1 Enemy AI
```
Tâches:
├── [P2-054] Créer EnemyData ScriptableObject
├── [P2-055] Implémenter state machine AI (Idle, Patrol, Chase, Attack, Flee)
├── [P2-056] Ajouter NavMesh navigation
├── [P2-057] Créer aggro/threat system
├── [P2-058] Implémenter attack patterns
├── [P2-059] Ajouter telegraphing system (shows incoming attacks)
├── [P2-060] Créer enemy weak points
├── [P2-061] Implémenter elemental weaknesses
├── [P2-062] Ajouter pack behavior
├── [P2-063] Créer boss AI framework
└── [P2-064] Implémenter phase transitions (bosses)
```

#### 2.2.2 Enemy Types
```
Tâches:
├── [P2-065] Créer Small enemies (fodder)
├── [P2-066] Ajouter Medium enemies (standard)
├── [P2-067] Créer Large enemies (mini-boss)
├── [P2-068] Implémenter Elite variants
├── [P2-069] Ajouter Boss enemies
├── [P2-070] Créer World Bosses
├── [P2-071] Implémenter Raid Bosses (co-op)
└── [P2-072] Ajouter Enemy spawner system
```

### Module 2.3: Creature System (Style Palworld/Pokemon)
**Priorité:** IMPORTANT
**Estimation:** ~100 tâches

#### 2.3.1 Creature Framework
```
Tâches:
├── [P2-073] Créer CreatureData ScriptableObject
├── [P2-074] Implémenter creature types (éléments)
├── [P2-075] Ajouter creature rarity tiers
├── [P2-076] Créer creature stats system
├── [P2-077] Implémenter creature abilities (4 slots)
├── [P2-078] Ajouter creature passive traits
├── [P2-079] Créer creature evolution system
├── [P2-080] Implémenter alternate forms
├── [P2-081] Ajouter shiny/variant system
└── [P2-082] Créer creature size categories
```

#### 2.3.2 Capture System
```
Tâches:
├── [P2-083] Créer capture mechanic (throw item)
├── [P2-084] Implémenter capture rate formula
├── [P2-085] Ajouter capture item tiers
├── [P2-086] Créer weakening bonus (low HP = easier)
├── [P2-087] Implémenter status effect bonus
├── [P2-088] Ajouter elemental capture bonus
├── [P2-089] Créer capture animation/sequence
├── [P2-090] Implémenter failed capture handling
├── [P2-091] Ajouter legendary capture mechanics
└── [P2-092] Créer first-catch bonus
```

#### 2.3.3 Creature AI (Companions)
```
Tâches:
├── [P2-093] Implémenter follow behavior
├── [P2-094] Ajouter combat AI (assist player)
├── [P2-095] Créer ability usage AI
├── [P2-096] Implémenter target prioritization
├── [P2-097] Ajouter defensive behavior toggle
├── [P2-098] Créer mount system
├── [P2-099] Implémenter flying mounts
├── [P2-100] Ajouter swimming mounts
├── [P2-101] Créer special traversal abilities
└── [P2-102] Implémenter creature commands (Stay, Follow, Attack, Ability)
```

#### 2.3.4 Creature Management
```
Tâches:
├── [P2-103] Créer party system (max 6 active)
├── [P2-104] Implémenter creature storage (PC box style)
├── [P2-105] Ajouter creature switching (in/out combat)
├── [P2-106] Créer creature naming
├── [P2-107] Implémenter creature release
├── [P2-108] Ajouter creature trading (co-op)
├── [P2-109] Créer creature breeding
├── [P2-110] Implémenter inheritance mechanics
├── [P2-111] Ajouter egg hatching system
└── [P2-112] Créer nursery building
```

#### 2.3.5 Creature Progression
```
Tâches:
├── [P2-113] Implémenter creature XP/leveling
├── [P2-114] Ajouter creature stat growth
├── [P2-115] Créer ability learning (level-based)
├── [P2-116] Implémenter ability teaching (items)
├── [P2-117] Ajouter affection/bond system
├── [P2-118] Créer bond abilities (unlocked via friendship)
├── [P2-119] Implémenter creature accessories
└── [P2-120] Ajouter creature skill trees
```

#### 2.3.6 Work Creatures (Style Palworld)
```
Tâches:
├── [P2-121] Créer work aptitude system
├── [P2-122] Implémenter Mining aptitude
├── [P2-123] Ajouter Logging aptitude
├── [P2-124] Créer Farming aptitude
├── [P2-125] Implémenter Crafting aptitude
├── [P2-126] Ajouter Transport aptitude
├── [P2-127] Créer Combat aptitude
├── [P2-128] Implémenter Guard aptitude
├── [P2-129] Ajouter creature assignment UI
└── [P2-130] Créer work efficiency calculations
```

---

## Phase 3 - Base Building

### Module 3.1: Building System
**Priorité:** IMPORTANT
**Estimation:** ~70 tâches

#### 3.1.1 Placement System
```
Tâches:
├── [P3-001] Créer BuildingData ScriptableObject
├── [P3-002] Implémenter grid-based placement
├── [P3-003] Ajouter placement preview (ghost)
├── [P3-004] Créer placement validation
├── [P3-005] Implémenter terrain snapping
├── [P3-006] Ajouter rotation controls
├── [P3-007] Créer foundation system
├── [P3-008] Implémenter multi-floor building
├── [P3-009] Ajouter blueprint system
├── [P3-010] Créer copy/paste buildings
└── [P3-011] Implémenter undo system
```

#### 3.1.2 Building Types
```
Tâches:
├── [P3-012] Créer Walls/Floors/Roofs
├── [P3-013] Implémenter Doors/Windows
├── [P3-014] Ajouter Stairs/Ramps
├── [P3-015] Créer Workbenches (crafting)
├── [P3-016] Implémenter Furnaces/Smelters
├── [P3-017] Ajouter Storage containers
├── [P3-018] Créer Beds/Rest points
├── [P3-019] Implémenter Farm plots
├── [P3-020] Ajouter Animal pens
├── [P3-021] Créer Defensive walls
├── [P3-022] Implémenter Guard towers
├── [P3-023] Ajouter Turrets (automated)
├── [P3-024] Créer Traps
├── [P3-025] Implémenter Power generators
├── [P3-026] Ajouter Power conduits
└── [P3-027] Créer Decorative items
```

#### 3.1.3 Building Upgrades
```
Tâches:
├── [P3-028] Implémenter tier system (Wood → Stone → Metal → Tech)
├── [P3-029] Ajouter upgrade preview
├── [P3-030] Créer upgrade costs
├── [P3-031] Implémenter gradual upgrade animation
└── [P3-032] Ajouter building health/durability
```

### Module 3.2: Resource Management
**Priorité:** IMPORTANT
**Estimation:** ~50 tâches

#### 3.2.1 Resource Types
```
Tâches:
├── [P3-033] Créer ResourceData ScriptableObject
├── [P3-034] Implémenter raw resources (Wood, Stone, Ore, etc.)
├── [P3-035] Ajouter processed resources (Planks, Bricks, Ingots)
├── [P3-036] Créer advanced materials (Alloys, Circuits)
├── [P3-037] Implémenter energy resource
├── [P3-038] Ajouter fuel types
└── [P3-039] Créer resource UI
```

#### 3.2.2 Gathering
```
Tâches:
├── [P3-040] Implémenter manual gathering (axe, pickaxe)
├── [P3-041] Ajouter gatherable objects (trees, rocks, nodes)
├── [P3-042] Créer gathering animations
├── [P3-043] Implémenter tool durability
├── [P3-044] Ajouter tool upgrades
├── [P3-045] Créer creature-based gathering
└── [P3-046] Implémenter automated extractors
```

#### 3.2.3 Storage & Logistics (Style Factorio Lite)
```
Tâches:
├── [P3-047] Créer container priority system
├── [P3-048] Implémenter auto-sort to containers
├── [P3-049] Ajouter container filters
├── [P3-050] Créer conveyor belts (basic)
├── [P3-051] Implémenter item splitters
├── [P3-052] Ajouter underground belts
├── [P3-053] Créer logistic creatures (transport)
└── [P3-054] Implémenter drone network
```

### Module 3.3: Production Chains
**Priorité:** IMPORTANT
**Estimation:** ~40 tâches

#### 3.3.1 Crafting Stations
```
Tâches:
├── [P3-055] Créer CraftingRecipe ScriptableObject
├── [P3-056] Implémenter basic workbench
├── [P3-057] Ajouter specialized stations (Forge, Alchemy, etc.)
├── [P3-058] Créer crafting queue system
├── [P3-059] Implémenter batch crafting
├── [P3-060] Ajouter auto-crafting toggle
├── [P3-061] Créer recipe unlocking
└── [P3-062] Implémenter crafting skill bonus
```

#### 3.3.2 Production Lines
```
Tâches:
├── [P3-063] Créer input/output slots
├── [P3-064] Implémenter chain detection
├── [P3-065] Ajouter production statistics
├── [P3-066] Créer bottleneck detection UI
├── [P3-067] Implémenter production presets
└── [P3-068] Ajouter creature workers assignment
```

### Module 3.4: Defense System (Tower Defense Elements)
**Priorité:** ÉTENDU
**Estimation:** ~45 tâches

#### 3.4.1 Tower Types
```
Tâches:
├── [P3-069] Créer TowerData ScriptableObject
├── [P3-070] Implémenter Arrow Tower (basic, single target)
├── [P3-071] Ajouter Cannon Tower (AoE, slow)
├── [P3-072] Créer Magic Tower (elemental damage)
├── [P3-073] Implémenter Frost Tower (slow enemies)
├── [P3-074] Ajouter Lightning Tower (chain damage)
├── [P3-075] Créer Support Tower (buff allies)
├── [P3-076] Implémenter Healing Tower (heal creatures)
├── [P3-077] Ajouter Trap Tower (ground AoE)
├── [P3-078] Créer tower targeting AI
├── [P3-079] Implémenter tower range indicators
└── [P3-080] Ajouter tower upgrade paths
```

#### 3.4.2 Wave System
```
Tâches:
├── [P3-081] Créer WaveData ScriptableObject
├── [P3-082] Implémenter wave spawning
├── [P3-083] Ajouter wave scaling (difficulty)
├── [P3-084] Créer wave preview
├── [P3-085] Implémenter between-wave prep time
├── [P3-086] Ajouter boss waves
├── [P3-087] Créer endless mode
├── [P3-088] Implémenter wave rewards
└── [P3-089] Ajouter wave completion stats
```

#### 3.4.3 Territory Control
```
Tâches:
├── [P3-090] Créer territory boundary system
├── [P3-091] Implémenter territory expansion
├── [P3-092] Ajouter territory bonuses
├── [P3-093] Créer raid event system
└── [P3-094] Implémenter territory leaderboards
```

---

## Phase 4 - Progression & Contenu

### Module 4.1: Character Progression
**Priorité:** IMPORTANT
**Estimation:** ~60 tâches

#### 4.1.1 Leveling System
```
Tâches:
├── [P4-001] Créer XP curve formula
├── [P4-002] Implémenter level cap (100)
├── [P4-003] Ajouter XP sources (combat, quests, exploration)
├── [P4-004] Créer level up rewards
├── [P4-005] Implémenter stat points allocation
├── [P4-006] Ajouter skill points
└── [P4-007] Créer level scaling enemies
```

#### 4.1.2 Class/Job System (Style FFV/FFXI)
```
Tâches:
├── [P4-008] Créer ClassData ScriptableObject
├── [P4-009] Implémenter base classes (Warrior, Mage, Rogue, Ranger)
├── [P4-010] Ajouter advanced classes
├── [P4-011] Créer hybrid classes
├── [P4-012] Implémenter class change system
├── [P4-013] Ajouter class level vs character level
├── [P4-014] Créer cross-class abilities
├── [P4-015] Implémenter class quests
├── [P4-016] Ajouter class-specific equipment
└── [P4-017] Créer class mastery bonuses
```

#### 4.1.3 Equipment System
```
Tâches:
├── [P4-018] Créer equipment slots (Head, Body, Hands, Legs, Feet, 2 Accessories)
├── [P4-019] Implémenter weapon slots (Main, Off-hand)
├── [P4-020] Ajouter equipment rarity (Common → Legendary)
├── [P4-021] Créer random stat rolls
├── [P4-022] Implémenter equipment sets (bonuses)
├── [P4-023] Ajouter equipment upgrading
├── [P4-024] Créer enchanting system
├── [P4-025] Implémenter socket/gem system
├── [P4-026] Ajouter transmog/appearance system
├── [P4-027] Créer equipment comparison UI
└── [P4-028] Implémenter equipment loadouts
```

### Module 4.2: Quest System
**Priorité:** IMPORTANT
**Estimation:** ~50 tâches

#### 4.2.1 Quest Framework
```
Tâches:
├── [P4-029] Créer QuestData ScriptableObject
├── [P4-030] Implémenter quest types (Main, Side, Daily, Event)
├── [P4-031] Ajouter quest objectives (Kill, Collect, Escort, etc.)
├── [P4-032] Créer quest tracking
├── [P4-033] Implémenter quest journal UI
├── [P4-034] Ajouter quest markers/waypoints
├── [P4-035] Créer quest rewards system
├── [P4-036] Implémenter quest prerequisites
├── [P4-037] Ajouter branching quests
└── [P4-038] Créer quest replay (NG+)
```

#### 4.2.2 Dialogue System
```
Tâches:
├── [P4-039] Créer DialogueData ScriptableObject
├── [P4-040] Implémenter dialogue UI
├── [P4-041] Ajouter dialogue choices
├── [P4-042] Créer dialogue conditions
├── [P4-043] Implémenter relationship impact
├── [P4-044] Ajouter voiceover support
└── [P4-045] Créer NPC scheduler
```

### Module 4.3: World & Exploration
**Priorité:** IMPORTANT
**Estimation:** ~55 tâches

#### 4.3.1 World Structure
```
Tâches:
├── [P4-046] Créer region system
├── [P4-047] Implémenter zone transitions
├── [P4-048] Ajouter fast travel points
├── [P4-049] Créer waypoint unlock system
├── [P4-050] Implémenter map fog of war
├── [P4-051] Ajouter map markers
├── [P4-052] Créer points of interest
├── [P4-053] Implémenter secret areas
└── [P4-054] Ajouter exploration XP
```

#### 4.3.2 Dungeons
```
Tâches:
├── [P4-055] Créer DungeonData ScriptableObject
├── [P4-056] Implémenter dungeon instances
├── [P4-057] Ajouter dungeon difficulty modes
├── [P4-058] Créer dungeon puzzles
├── [P4-059] Implémenter dungeon bosses
├── [P4-060] Ajouter dungeon loot tables
├── [P4-061] Créer weekly reset dungeons
├── [P4-062] Implémenter procedural dungeon elements
└── [P4-063] Ajouter dungeon leaderboards
```

#### 4.3.3 World Events
```
Tâches:
├── [P4-064] Créer EventData ScriptableObject
├── [P4-065] Implémenter world boss spawns
├── [P4-066] Ajouter resource node events
├── [P4-067] Créer weather events
├── [P4-068] Implémenter invasion events
├── [P4-069] Ajouter seasonal events
└── [P4-070] Créer community challenges
```

### Module 4.4: New Game+ & Endgame
**Priorité:** ÉTENDU
**Estimation:** ~30 tâches

```
Tâches:
├── [P4-071] Implémenter NG+ unlock conditions
├── [P4-072] Ajouter NG+ difficulty scaling
├── [P4-073] Créer carried over items/stats
├── [P4-074] Implémenter new NG+ content
├── [P4-075] Ajouter NG+ exclusive rewards
├── [P4-076] Créer infinite scaling (NG+7, etc.)
├── [P4-077] Implémenter challenge modes
├── [P4-078] Ajouter time attack mode
├── [P4-079] Créer no-damage runs tracking
└── [P4-080] Implémenter achievement system
```

---

## Phase 5 - Multijoueur Co-op

### Module 5.1: Network Foundation
**Priorité:** ÉTENDU
**Estimation:** ~50 tâches

#### 5.1.1 Networking
```
Tâches:
├── [P5-001] Configurer Unity Netcode for GameObjects
├── [P5-002] Implémenter host/client architecture
├── [P5-003] Ajouter dedicated server option
├── [P5-004] Créer lobby system
├── [P5-005] Implémenter matchmaking
├── [P5-006] Ajouter friend invite system
├── [P5-007] Créer connection handling
├── [P5-008] Implémenter reconnection
├── [P5-009] Ajouter lag compensation
└── [P5-010] Créer network statistics UI
```

#### 5.1.2 Synchronization
```
Tâches:
├── [P5-011] Implémenter player sync
├── [P5-012] Ajouter creature sync
├── [P5-013] Créer enemy sync
├── [P5-014] Implémenter projectile sync
├── [P5-015] Ajouter building sync
├── [P5-016] Créer inventory sync
├── [P5-017] Implémenter world state sync
└── [P5-018] Ajouter chat system
```

### Module 5.2: Co-op Features
**Priorité:** ÉTENDU
**Estimation:** ~40 tâches

#### 5.2.1 Party System
```
Tâches:
├── [P5-019] Créer party formation (2-4 players)
├── [P5-020] Implémenter party roles
├── [P5-021] Ajouter party buffs
├── [P5-022] Créer shared XP options
├── [P5-023] Implémenter party finder
└── [P5-024] Ajouter party voice chat
```

#### 5.2.2 Loot Distribution
```
Tâches:
├── [P5-025] Implémenter loot modes (Free for all, Round Robin, Need/Greed)
├── [P5-026] Ajouter personal loot option
├── [P5-027] Créer trade confirmation
├── [P5-028] Implémenter anti-ninja systems
└── [P5-029] Ajouter loot history log
```

#### 5.2.3 Shared Base
```
Tâches:
├── [P5-030] Implémenter shared building permissions
├── [P5-031] Ajouter resource sharing
├── [P5-032] Créer creature sharing (lending)
├── [P5-033] Implémenter co-op defense waves
├── [P5-034] Ajouter base visiting
└── [P5-035] Créer co-op achievements
```

### Module 5.3: Raid Content
**Priorité:** BONUS
**Estimation:** ~25 tâches

```
Tâches:
├── [P5-036] Créer RaidData ScriptableObject
├── [P5-037] Implémenter 4-player raids
├── [P5-038] Ajouter raid mechanics
├── [P5-039] Créer raid boss phases
├── [P5-040] Implémenter raid rewards
├── [P5-041] Ajouter weekly lockouts
├── [P5-042] Créer raid difficulty tiers
├── [P5-043] Implémenter raid finder
└── [P5-044] Ajouter raid leaderboards
```

---

## Phase 6 - Polish & Optimisation

### Module 6.1: Performance
**Priorité:** CRITIQUE
**Estimation:** ~35 tâches

```
Tâches:
├── [P6-001] Implémenter object pooling
├── [P6-002] Ajouter LOD system
├── [P6-003] Créer occlusion culling
├── [P6-004] Implémenter batching optimization
├── [P6-005] Ajouter GPU instancing
├── [P6-006] Créer async loading
├── [P6-007] Implémenter streaming world
├── [P6-008] Ajouter memory management
├── [P6-009] Créer profiling tools
├── [P6-010] Implémenter quality presets
├── [P6-011] Ajouter DLSS/FSR support
├── [P6-012] Créer frame rate targets
└── [P6-013] Implémenter adaptive quality
```

### Module 6.2: Audio
**Priorité:** IMPORTANT
**Estimation:** ~25 tâches

```
Tâches:
├── [P6-014] Créer AudioManager
├── [P6-015] Implémenter music system
├── [P6-016] Ajouter ambient sounds
├── [P6-017] Créer SFX system
├── [P6-018] Implémenter 3D audio
├── [P6-019] Ajouter audio occlusion
├── [P6-020] Créer dynamic mixing
├── [P6-021] Implémenter music transitions
└── [P6-022] Ajouter audio settings
```

### Module 6.3: Visual Polish
**Priorité:** IMPORTANT
**Estimation:** ~30 tâches

```
Tâches:
├── [P6-023] Implémenter post-processing
├── [P6-024] Ajouter particle effects polish
├── [P6-025] Créer screen shake
├── [P6-026] Implémenter hit stop (frame pause)
├── [P6-027] Ajouter motion blur
├── [P6-028] Créer depth of field
├── [P6-029] Implémenter bloom tuning
├── [P6-030] Ajouter color grading
├── [P6-031] Créer weather effects
└── [P6-032] Implémenter day/night cycle
```

### Module 6.4: Accessibility
**Priorité:** IMPORTANT
**Estimation:** ~20 tâches

```
Tâches:
├── [P6-033] Implémenter colorblind modes
├── [P6-034] Ajouter subtitle system
├── [P6-035] Créer text scaling
├── [P6-036] Implémenter controller remapping
├── [P6-037] Ajouter one-handed mode
├── [P6-038] Créer difficulty options
├── [P6-039] Implémenter auto-aim assistance
└── [P6-040] Ajouter screen reader support
```

---

## Spécifications Techniques

### Architecture Code

```
Assets/
├── Scripts/
│   ├── Core/                    # Singletons, managers
│   │   ├── GameManager.cs
│   │   ├── SaveManager.cs
│   │   └── AudioManager.cs
│   ├── Player/                  # Player-specific
│   │   ├── PlayerController.cs
│   │   ├── PlayerStats.cs
│   │   └── PlayerInventory.cs
│   ├── Combat/                  # Combat system
│   │   ├── DamageSystem.cs
│   │   ├── ElementalReactions.cs
│   │   └── WeaponController.cs
│   ├── Creatures/               # Creature system
│   │   ├── CreatureAI.cs
│   │   ├── CaptureSystem.cs
│   │   └── CreatureParty.cs
│   ├── Building/                # Base building
│   │   ├── BuildingPlacer.cs
│   │   ├── ResourceManager.cs
│   │   └── ProductionChain.cs
│   ├── Defense/                 # Tower defense
│   │   ├── TowerAI.cs
│   │   └── WaveManager.cs
│   ├── Progression/             # RPG systems
│   │   ├── LevelSystem.cs
│   │   ├── ClassSystem.cs
│   │   └── QuestManager.cs
│   ├── UI/                      # All UI
│   │   ├── UIManager.cs
│   │   └── Panels/
│   ├── Network/                 # Multiplayer
│   │   └── NetworkManager.cs
│   └── Data/                    # ScriptableObjects
│       ├── Items/
│       ├── Creatures/
│       ├── Buildings/
│       └── Quests/
├── Prefabs/
├── Art/
├── Audio/
└── Tests/
```

### Conventions de Code

```csharp
// Naming
public class PlayerController : MonoBehaviour { }  // PascalCase classes
private float _moveSpeed;                           // _camelCase private fields
public float MoveSpeed => _moveSpeed;              // PascalCase properties
public void CalculateDamage() { }                  // PascalCase methods
const float MAX_HEALTH = 100f;                     // SCREAMING_SNAKE constants

// Component caching
private Rigidbody _rb;
private void Awake() => _rb = GetComponent<Rigidbody>();

// Events
public event Action<float> OnHealthChanged;
public event Action OnDeath;

// Serialization
[SerializeField] private float _speed = 5f;
[Header("Movement")]
[Tooltip("Base movement speed")]
```

### Performance Targets

| Platform | Resolution | FPS Target | Quality |
|----------|------------|------------|---------|
| PC Low   | 1080p      | 60 FPS     | Low     |
| PC Mid   | 1440p      | 60 FPS     | Medium  |
| PC High  | 4K         | 60 FPS     | High    |
| PC Ultra | 4K         | 120 FPS    | Ultra   |

### Memory Budgets

| System          | Budget    |
|-----------------|-----------|
| Textures        | 2 GB      |
| Meshes          | 512 MB    |
| Audio           | 256 MB    |
| Scripts/Data    | 256 MB    |
| Particles       | 128 MB    |
| UI              | 128 MB    |
| **Total**       | **~3.5 GB** |

---

## Métriques de Succès

### Phase 1 (Core) ✅ TERMINÉE
- [x] Player peut se déplacer, sauter, sprinter (PlayerController.cs)
- [x] Dash/Dodge avec i-frames (DashAbility.cs)
- [x] Camera smooth avec Cinemachine (CameraController.cs)
- [x] Système de stats complet (StatContainer.cs, StatModifier.cs)
- [x] Inventory fonctionnel (Inventory.cs, ItemData.cs)
- [x] Save/Load avec encryption (SaveManager.cs, SaveData.cs)
- [x] UI Framework (UIManager.cs, UIPanel.cs, UITooltip.cs)
- [x] Tests unitaires: 6 fichiers de tests

**Fichiers créés Phase 1:** 24 fichiers C#
**Commits:** bf1a1e4 → 08b81b1

### Phase 2 (Combat & Creatures) ✅ TERMINÉE
- [x] Combat System complet (CombatController.cs, DamageCalculator.cs)
- [x] Hitbox/Hurtbox system (Hitbox.cs, Hurtbox.cs)
- [x] 8 éléments + réactions (ElementType.cs, ElementalReactionHandler.cs)
- [x] Système de Knockback/Stagger (KnockbackReceiver.cs, StaggerHandler.cs)
- [x] 5+ types d'armes (WeaponData.cs, WeaponType.cs, WeaponController.cs)
- [x] Skills & Abilities (SkillData.cs, SkillController.cs)
- [x] Enemy AI avancé (EnemyAI.cs, AggroSystem.cs, AttackPattern.cs)
- [x] Creature Framework (CreatureData.cs, CreatureInstance.cs)
- [x] Capture System (CaptureController.cs, CaptureCalculator.cs)
- [x] Creature AI + Mounts (CreatureAI.cs, MountSystem.cs)
- [x] Tests unitaires: 10 fichiers de tests

**Fichiers créés Phase 2:** 37 fichiers C#
**Commits:** a89adfc → a56c1b3

### Phase 3 (Base Building) 🔄 EN COURS (4/8 tâches)
- [x] Building Placement System (BuildingPlacer.cs, BuildingGrid.cs)
- [x] Building Types (Building.cs, BuildingData.cs, DefenseTower.cs, etc.)
- [x] Building Upgrades (BuildingUpgradeManager.cs, TierVisualConfig.cs)
- [x] Resource Management (ResourceManager.cs, ResourceData.cs, ResourceType.cs)
- [ ] Storage & Logistics (convoyeurs, priorités)
- [ ] Production Chains (CraftingRecipeData.cs créé, reste à compléter)
- [ ] Defense System / Tower AI
- [ ] Wave System

**Fichiers créés Phase 3 (en cours):** 15 fichiers C#
**Commits:** 1b465d6 → 206f88e

### Phase 4 (Progression)
- [ ] Système de classes fonctionnel
- [ ] Quêtes main story jouables
- [ ] Équipement avec stats aléatoires
- [ ] Au moins 3 dungeons

### Phase 5 (Multiplayer)
- [ ] Co-op 4 joueurs stable
- [ ] Synchronisation fluide
- [ ] Base partagée fonctionne

### Phase 6 (Polish)
- [ ] 60 FPS stable sur PC moyen
- [ ] Aucun bug bloquant
- [ ] Audio immersif
- [ ] Options d'accessibilité

---

## État Actuel du Projet

**Dernière mise à jour:** 2026-01-29

```
PROGRESSION GLOBALE
═══════════════════════════════════════════════════════════════

Phase 1: Core Foundation      ████████████████████  TERMINÉE ✅
Phase 2: Combat & Creatures   ████████████████████  TERMINÉE ✅
Phase 3: Base Building        ██████████░░░░░░░░░░  EN COURS 50%
Phase 4: Progression          ░░░░░░░░░░░░░░░░░░░░  À faire
Phase 5: Multiplayer          ░░░░░░░░░░░░░░░░░░░░  À faire
Phase 6: Polish               ░░░░░░░░░░░░░░░░░░░░  À faire

STATISTIQUES
├── Fichiers C#: 76
├── Fichiers de tests: 16
├── Commits: 22
└── Branches: feature/dash-system-20260129-184101-475
```

---

## Roadmap Estimée

```
Phase 1: Core Foundation      ████████████████████  TERMINÉE ✅
Phase 2: Combat & Creatures   ████████████████████  TERMINÉE ✅
Phase 3: Base Building        ██████████░░░░░░░░░░  ~50% fait
Phase 4: Progression          ░░░░░░░░░░░░░░░░░░░░  À faire
Phase 5: Multiplayer          ░░░░░░░░░░░░░░░░░░░░  À faire
Phase 6: Polish               ░░░░░░░░░░░░░░░░░░░░  À faire

TOTAL FICHIERS: 76 scripts + 16 tests = 92 fichiers C#
```

---

## Direction Artistique & Assets

### Style Visuel: Genshin Impact / Anime Stylisé

#### Caractéristiques du Style
```
RENDU
├── Cel-shading avec outlines subtiles
├── Couleurs vives et saturées
├── Ombres douces avec gradient
├── Rim lighting (contour lumineux)
└── Post-processing: Bloom, Color Grading

MODÈLES 3D
├── Low-poly stylisé (~3000-8000 tris par personnage)
├── Textures peintes à la main (hand-painted)
├── Proportions anime (grands yeux, corps stylisés)
├── Silhouettes distinctives et lisibles
└── Couleurs unies avec détails peints

ENVIRONNEMENTS
├── Palette de couleurs harmonieuse par biome
├── Végétation stylisée (pas photoréaliste)
├── Architecture fantasy avec influences asiatiques/européennes
├── Skybox colorées et atmosphériques
└── Particules pour ambiance (lucioles, pétales, poussière)

EFFETS VISUELS
├── Particules lumineuses pour la magie
├── Trails colorés sur les attaques
├── Impact effects stylisés
├── Éléments avec couleurs distinctes:
│   ├── Feu: Orange/Rouge
│   ├── Eau: Bleu/Cyan
│   ├── Glace: Bleu clair/Blanc
│   ├── Électro: Violet/Magenta
│   ├── Vent: Turquoise/Vert menthe
│   ├── Terre: Orange/Marron/Jaune
│   ├── Lumière: Blanc/Or
│   └── Ténèbres: Violet foncé/Noir
└── UI avec éléments fantasy ornementés
```

### Sources d'Assets Gratuits

#### Priorité 1: Packs Complets (Style Compatible)

| Source | Type | Licence | Lien |
|--------|------|---------|------|
| **Synty POLYGON Starter** | Low-poly stylisé | Unity Asset Store | [Lien](https://assetstore.unity.com/packages/essentials/tutorial-projects/polygon-starter-pack-156819) |
| **Kenney.nl** | 60,000+ assets 2D/3D | CC0 (libre) | [kenney.nl](https://kenney.nl/assets) |
| **Quaternius** | Modèles 3D riggés | CC0 (libre) | [quaternius.com](https://quaternius.com/) |
| **Mixamo** | Personnages + Animations | Gratuit (Adobe) | [mixamo.com](https://www.mixamo.com/) |

#### Priorité 2: Assets Spécifiques

| Catégorie | Source Recommandée |
|-----------|-------------------|
| **Personnages** | Mixamo + Synty Characters |
| **Animations** | Mixamo (2000+ animations gratuites) |
| **Environnements** | Kenney Nature Kit, Synty |
| **UI/Icons** | Kenney UI Pack, Game-Icons.net |
| **VFX/Particules** | Unity Asset Store (Free Quick Effects) |
| **Audio/SFX** | Kenney, Freesound.org |
| **Musique** | Incompetech, OpenGameArt |

#### Priorité 3: Génération IA (Si Besoin)

| Outil | Usage | Prix |
|-------|-------|------|
| **Meshy AI** | Image → Modèle 3D | 200 crédits/mois gratuits |
| **Luma AI** | Texte → 3D | Gratuit illimité |
| **Leonardo AI** | Concept art, textures | 150/jour gratuit |
| **Cascadeur** | Animation IA | Gratuit non-commercial |
| **Beatoven.ai** | Musique IA | 15 min/mois gratuit |

### Pipeline de Création d'Assets

```
WORKFLOW ASSETS
═══════════════════════════════════════════════════════════════

1. RECHERCHE
   ├── Chercher dans Kenney/Quaternius/Synty d'abord
   ├── Vérifier compatibilité style (low-poly stylisé)
   └── Télécharger et tester dans Unity

2. ADAPTATION (si asset existant)
   ├── Ajuster les materials pour URP
   ├── Configurer le cel-shading
   ├── Ajouter rim lighting
   └── Harmoniser les couleurs

3. GÉNÉRATION IA (si rien trouvé)
   ├── Créer concept avec Leonardo AI
   ├── Générer modèle 3D avec Meshy/Luma
   ├── Rigging automatique avec Mixamo
   ├── Retouches dans Blender si nécessaire
   └── Import Unity avec materials adaptés

4. ANIMATIONS
   ├── Base: Mixamo (idle, walk, run, jump, attack)
   ├── Polish: Cascadeur (ajustements physiques)
   └── Spécifiques: Créer dans Unity/Blender

5. INTÉGRATION
   ├── Nommage: Category_Name_Variant (ex: Char_Knight_Blue)
   ├── Prefabs avec composants configurés
   ├── Materials partagés quand possible
   └── LODs pour performance
```

### Conventions de Nommage des Assets

```
DOSSIERS
Assets/
├── Art/
│   ├── Characters/
│   │   ├── Player/
│   │   ├── NPCs/
│   │   ├── Enemies/
│   │   └── Creatures/
│   ├── Environments/
│   │   ├── Biomes/
│   │   ├── Buildings/
│   │   ├── Props/
│   │   └── Vegetation/
│   ├── UI/
│   │   ├── Icons/
│   │   ├── Frames/
│   │   └── Fonts/
│   ├── VFX/
│   │   ├── Elements/
│   │   ├── Combat/
│   │   └── Ambient/
│   └── Materials/
│       ├── Characters/
│       ├── Environment/
│       └── Shared/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   ├── Ambience/
│   └── Voice/
└── Animations/
    ├── Characters/
    ├── Creatures/
    └── UI/

FICHIERS
Format: [Categorie]_[Nom]_[Variante]_[Détail]

Exemples:
- Char_Knight_Blue_Idle.fbx
- Env_Tree_Oak_01.prefab
- UI_Icon_Sword_Rare.png
- VFX_Fire_Explosion_Large.prefab
- SFX_Combat_Hit_Metal_01.wav
- Mat_Char_Skin_Light.mat
```

### Shader et Materials

#### Configuration URP Toon Shader
```
SHADER SETTINGS (pour style Genshin)
├── Base Color: Texture peinte ou couleur unie
├── Shade Color: 70-80% de la base color, légèrement désaturé
├── Shade Threshold: 0.4-0.6
├── Shade Smoothness: 0.1-0.2 (transitions douces)
├── Rim Light: Blanc/Couleur complémentaire, intensité 0.3-0.5
├── Outline: Noir ou couleur foncée, width 0.001-0.003
└── Emission: Pour effets magiques et brillance
```

#### Materials Standards
```csharp
// Materials à créer pour cohérence
Mat_Toon_Character_Base    // Personnages standard
Mat_Toon_Character_Skin    // Peau (subsurface scattering léger)
Mat_Toon_Character_Hair    // Cheveux (anisotropic highlights)
Mat_Toon_Character_Metal   // Armures métalliques
Mat_Toon_Environment       // Props et bâtiments
Mat_Toon_Vegetation        // Arbres, herbe (two-sided)
Mat_Toon_Water             // Eau stylisée
Mat_Toon_Crystal           // Cristaux et gemmes (emission)
Mat_Unlit_VFX              // Particules et effets
```

### Ressources Recommandées à Télécharger

#### Immédiat (Phase 1-2)
- [ ] Synty POLYGON Starter Pack (Unity Asset Store - Gratuit)
- [ ] Mixamo: 10 personnages de base + 50 animations essentielles
- [ ] Kenney: UI Pack, Particle Pack
- [ ] Unity Asset Store: Free Quick Effects Vol. 1

#### Phase 3 (Base Building)
- [ ] Kenney: Modular Buildings, Castle Kit
- [ ] Quaternius: Medieval Village MegaKit
- [ ] Synty: POLYGON Town Pack (si budget)

#### Phase 4+ (Contenu)
- [ ] Environnements par biome (forêt, désert, neige, etc.)
- [ ] Créatures variées (Quaternius + génération IA)
- [ ] Armes et équipements (Synty Weapons)

### Checklist Style Genshin

Avant de valider un asset, vérifier:
- [ ] Polycount raisonnable (< 10K tris pour personnages)
- [ ] Textures stylisées (pas photoréalistes)
- [ ] Couleurs vives et saturées
- [ ] Silhouette lisible à distance
- [ ] Compatible avec cel-shading
- [ ] Proportions cohérentes avec le reste du jeu

---

## Notes pour Ralph Loop

1. **Toujours commencer par les tests** - TDD pour chaque système
2. **Un système à la fois** - Ne pas paralléliser les phases
3. **Valider chaque module** - Tests jouables avant de passer au suivant
4. **Commits atomiques** - Une fonctionnalité = un commit
5. **Documentation inline** - XML comments sur toutes les API publiques

---

*Document généré pour utilisation avec Claude Code Ralph Loop*
*Version 1.0.0 - 2026-01-29*
