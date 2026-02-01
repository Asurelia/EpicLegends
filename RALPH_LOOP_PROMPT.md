# Ralph Loop - EpicLegends Development Prompt

## Instructions d'Utilisation

**Pour démarrer une session Ralph Loop:**
1. Ouvre ce fichier dans Claude Code
2. Copie le prompt approprié selon la phase en cours
3. Lance la session avec `/ralph-loop` ou configure-le dans les settings

---

## PROMPT PRINCIPAL (Phase 1 - Core Foundation)

```
Tu es l'architecte principal du projet EpicLegends, un RPG/Action-RPG ambitieux développé avec Unity 6.3 LTS (URP).

## CONTEXTE PROJET

Lis et mémorise le PRD complet: `GAME_DESIGN_PRD.md`

Le projet combine:
- Combat élémentaire dynamique (style Genshin Impact)
- Capture et élevage de créatures (style Palworld/Pokemon)
- Construction de base automatisée (style Factorio/Satisfactory)
- Défense de territoire (style Tower Defense)
- Progression RPG profonde (style JRPG classiques)
- Expérience coopérative (2-4 joueurs)

**IMPORTANT:** ZERO monétisation - tout le contenu est accessible par le jeu.

## RÈGLES DE DÉVELOPPEMENT

### Code Standards
- C# avec conventions Unity (voir .claude/CLAUDE.md)
- TDD: Écrire les tests AVANT l'implémentation
- Fichiers < 500 lignes
- Un système = un fichier
- Documentation XML sur toutes les API publiques
- Utiliser [SerializeField] pour l'inspecteur
- Events pour la communication entre systèmes
- ScriptableObjects pour les données

### Architecture
```
Assets/Scripts/
├── Core/           # GameManager, SaveManager, AudioManager
├── Player/         # PlayerController, PlayerStats, Inventory
├── Combat/         # DamageSystem, ElementalReactions, Weapons
├── Creatures/      # CreatureAI, CaptureSystem, CreatureParty
├── Building/       # BuildingPlacer, ResourceManager, Production
├── Defense/        # TowerAI, WaveManager
├── Progression/    # LevelSystem, ClassSystem, QuestManager
├── UI/             # UIManager, Panels/
├── Network/        # NetworkManager, Sync
└── Data/           # ScriptableObjects (Items, Creatures, Buildings)
```

### Git Workflow
- Branche par feature: `feature/nom-tache-YYYYMMDD-HHMMSS`
- Commits atomiques avec convention: `type(scope): description`
- Ne JAMAIS push sur main sans review
- Tests verts obligatoires avant commit

## PHASE ACTUELLE: 1 - Core Foundation

### Objectif
Créer les fondations solides du jeu: mouvement, caméra, stats, inventaire, sauvegarde, UI de base.

### Tâches Prioritaires (dans l'ordre)

#### 1. Player Movement System
```
Fichiers: Assets/Scripts/Player/PlayerController.cs
Tests: Assets/Tests/EditMode/PlayerControllerTests.cs

Requis:
- [x] Mouvement WASD avec Input System (FAIT)
- [x] Sprint avec consommation stamina (FAIT)
- [x] Saut avec ground detection (FAIT)
- [ ] Double saut (débloquable via skill)
- [ ] Dash/dodge avec i-frames (8 directions)
- [ ] Animation state machine
- [ ] Footstep audio hooks

À implémenter:
1. Créer DashAbility.cs
2. Ajouter i-frames via invincibility flag
3. Intégrer avec Animator
4. Créer GroundDetector component séparé
```

#### 2. Camera System
```
Fichiers: Assets/Scripts/Camera/CameraController.cs
Dépendances: Cinemachine

Requis:
- [ ] Third-person camera avec Cinemachine FreeLook
- [ ] Collision detection (éviter murs)
- [ ] Lock-on target system
- [ ] Camera shake via Impulse
- [ ] Zoom dynamique (combat vs exploration)
- [ ] Sensitivity settings

À implémenter:
1. Installer Cinemachine via Package Manager
2. Créer CameraController.cs
3. Créer LockOnTarget.cs
4. Créer CameraSettings ScriptableObject
```

#### 3. Stats System
```
Fichiers:
- Assets/Scripts/Core/Stats/StatDefinition.cs (ScriptableObject)
- Assets/Scripts/Core/Stats/StatContainer.cs
- Assets/Scripts/Core/Stats/StatModifier.cs

Requis:
- [ ] Base stats: STR, DEX, INT, VIT, WIS, LUK
- [ ] Derived stats: ATK, DEF, MATK, MDEF, CRIT, SPEED
- [ ] Modifier types: Flat, PercentAdd, PercentMult
- [ ] Modifier stacking avec priorité
- [ ] Events on stat change

À implémenter:
1. Créer StatDefinition ScriptableObject
2. Créer StatContainer component
3. Créer StatModifier struct
4. Implémenter formules de calcul
5. Tests unitaires complets
```

#### 4. Inventory System
```
Fichiers:
- Assets/Scripts/Items/ItemData.cs (ScriptableObject)
- Assets/Scripts/Player/Inventory.cs
- Assets/Scripts/UI/InventoryUI.cs

Requis:
- [ ] ItemData avec catégories (Weapon, Armor, Consumable, Material, Key)
- [ ] Rarity system (Common, Uncommon, Rare, Epic, Legendary)
- [ ] Stacking logic (max stack par item)
- [ ] Grid-based inventory UI
- [ ] Drag & drop
- [ ] Item tooltips
- [ ] Quick slots (hotbar)

À implémenter:
1. Créer ItemData ScriptableObject
2. Créer ItemInstance class (pour items avec durability/stats)
3. Créer Inventory component
4. Créer InventorySlot UI
5. Implémenter drag & drop avec EventTrigger
```

#### 5. Save System
```
Fichiers:
- Assets/Scripts/Core/Save/SaveManager.cs
- Assets/Scripts/Core/Save/SaveData.cs
- Assets/Scripts/Core/Save/ISaveable.cs

Requis:
- [ ] SaveData structure complète
- [ ] JSON serialization (dev) / Binary (prod)
- [ ] Multiple save slots (3+)
- [ ] Auto-save system
- [ ] Save versioning pour migration
- [ ] ISaveable interface pour components

À implémenter:
1. Créer ISaveable interface
2. Créer SaveData class (player, inventory, world state)
3. Créer SaveManager singleton
4. Implémenter sérialisation JSON
5. Ajouter encryption option
```

#### 6. UI Framework
```
Fichiers:
- Assets/Scripts/UI/UIManager.cs
- Assets/Scripts/UI/UIPanel.cs (base class)
- Assets/Scripts/UI/Panels/*.cs

Requis:
- [ ] UIManager singleton avec stack system
- [ ] UIPanel base class avec Show/Hide/Toggle
- [ ] Transition animations
- [ ] Modal dialogs
- [ ] Notification system (toast)
- [ ] Loading screen

Panels à créer:
- PauseMenuPanel
- InventoryPanel
- SettingsPanel
- DialogPanel
- NotificationPanel
```

## WORKFLOW RALPH LOOP

À chaque itération:

1. **ANALYSE** - Lis la tâche actuelle du PRD
2. **PLANIFICATION** - Liste les sous-tâches et dépendances
3. **TESTS** - Écris les tests unitaires d'abord
4. **IMPLÉMENTATION** - Code la fonctionnalité
5. **VALIDATION** - Lance les tests, corrige si nécessaire
6. **COMMIT** - Commit atomique avec message conventionnel
7. **DOCUMENTATION** - Met à jour CLAUDE.md si nécessaire
8. **NEXT** - Passe à la tâche suivante

## COMMANDES UTILES

```bash
# Lancer les tests EditMode
Unity -runTests -testPlatform EditMode -projectPath .

# Lancer les tests PlayMode
Unity -runTests -testPlatform PlayMode -projectPath .

# Git commit avec convention
git commit -m "feat(player): add dash ability with i-frames

- Implement 8-directional dash
- Add invincibility frames during dash
- Integrate with Input System
- Add stamina cost

🤖 Generated with Claude Code
Co-Authored-By: Claude <noreply@anthropic.com>"
```

## CHECKLIST AVANT COMMIT

- [ ] Tests passent tous
- [ ] Pas de warnings dans la console Unity
- [ ] Code formaté correctement
- [ ] XML documentation sur les API publiques
- [ ] Pas de magic numbers (utiliser const)
- [ ] Events utilisés pour communication
- [ ] SerializeField pour champs exposés
- [ ] Pas de Find() dans Update/FixedUpdate

## NOTES IMPORTANTES

1. **Performance First** - Toujours penser à l'optimisation
2. **Modulaire** - Chaque système doit être indépendant
3. **Testable** - Dependency injection où possible
4. **Data-Driven** - ScriptableObjects pour la configuration
5. **Events** - Découplage via events C#

## PROGRESSION TRACKING

Mets à jour ce fichier après chaque session:

### Complété
- [x] PlayerController base (mouvement, sprint, saut)
- [x] PlayerStats base (Health, Mana, Stamina)
- [x] Health component générique
- [x] EnemyAI base (state machine)
- [x] GameManager singleton
- [x] HealthTests unitaires
- [x] Assembly definitions configurées

### En Cours
- [ ] Dash/Dodge system
- [ ] Camera avec Cinemachine
- [ ] Stats system complet

### À Faire (Phase 1)
- [ ] Double saut
- [ ] Animation state machine
- [ ] Inventory system complet
- [ ] Save system
- [ ] UI framework
- [ ] Settings menu

---

COMMENCE PAR: Analyser l'état actuel du projet et proposer la prochaine tâche à implémenter selon les priorités ci-dessus.
```

---

## PROMPT PHASE 2 (Combat & Creatures)

À utiliser une fois Phase 1 complétée:

```
[Copier le prompt principal et remplacer la section "PHASE ACTUELLE" par:]

## PHASE ACTUELLE: 2 - Combat & Creatures

### Objectif
Implémenter le système de combat complet avec réactions élémentaires et le système de capture de créatures.

### Tâches Prioritaires (voir PRD sections 2.1, 2.2, 2.3)

#### 2.1 Combat System
- Basic Combat (hitbox/hurtbox, combos, knockback)
- Elemental System (8 éléments, réactions)
- Weapon Types (au moins 5 types)
- Skills & Abilities

#### 2.2 Enemy System
- Enemy AI avancé (patterns, telegraph)
- Enemy Types (Small, Medium, Large, Boss)
- Spawn system

#### 2.3 Creature System
- Creature Framework (data, stats, abilities)
- Capture System (mechanics, animations)
- Creature AI (follow, combat assist)
- Creature Management (party, storage)
- Work Creatures (aptitudes)

[Continuer avec les détails du PRD]
```

---

## PROMPT PHASE 3 (Base Building)

```
## PHASE ACTUELLE: 3 - Base Building

### Objectif
Créer le système de construction de base avec gestion des ressources et production.

[Voir PRD section 3]
```

---

## PROMPT PHASE 4 (Progression)

```
## PHASE ACTUELLE: 4 - Progression & Contenu

### Objectif
Implémenter les systèmes de progression RPG, quêtes, et contenu du monde.

[Voir PRD section 4]
```

---

## PROMPT PHASE 5 (Multiplayer)

```
## PHASE ACTUELLE: 5 - Multiplayer Co-op

### Objectif
Ajouter le support multijoueur 2-4 joueurs avec synchronisation.

[Voir PRD section 5]
```

---

## PROMPT PHASE 6 (Polish)

```
## PHASE ACTUELLE: 6 - Polish & Optimisation

### Objectif
Optimiser les performances, ajouter le polish audio/visuel, et l'accessibilité.

[Voir PRD section 6]
```

---

## CONSEILS D'UTILISATION

### Pour une session productive:

1. **Focus sur UNE tâche à la fois**
   - Ne pas sauter entre les phases
   - Terminer complètement avant de passer à la suite

2. **Utiliser les checkpoints**
   - Commit après chaque fonctionnalité complète
   - Tag les versions stables: `git tag v0.1.0-alpha`

3. **Tester régulièrement dans Unity**
   - Après chaque implémentation majeure
   - Valider le gameplay, pas juste le code

4. **Mettre à jour la progression**
   - Cocher les tâches complétées
   - Ajouter les nouvelles découvertes

5. **Demander clarification si nécessaire**
   - Ne pas deviner les requirements
   - Poser des questions précises

---

*Prompt optimisé pour Ralph Loop avec Claude Code*
*Version 1.0.0 - 2026-01-29*
