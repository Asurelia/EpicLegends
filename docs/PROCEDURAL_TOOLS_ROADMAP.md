# EpicLegends - Feuille de Route des Outils de Création

> **Document complet pour transformer EpicLegends en projet commercialisable**
>
> Comparaison avec les workflows des grands studios et plan d'implémentation des outils manquants.

**Version**: 1.0
**Date**: 1 Février 2026
**Objectif**: Créer les outils nécessaires pour un développement professionnel

---

## 1. ANALYSE: GRANDS STUDIOS VS NOTRE PROJET

### 1.1 Workflow des Grands Studios

| Studio | Jeu | Outils Utilisés |
|--------|-----|-----------------|
| **miHoYo** | Genshin Impact | World Editor custom, Terrain tools, NPC Placer, Quest Designer |
| **FromSoftware** | Elden Ring | Level Editor, Enemy Placer, Boss Arena Designer |
| **CD Projekt Red** | Witcher 3 | REDkit, World Builder, Quest Editor, Dialogue Tree Editor |
| **Bethesda** | Skyrim | Creation Kit (World, NPC, Quest, Dialogue tout-en-un) |
| **Ubisoft** | Assassin's Creed | Anvil Engine avec Node-based editors |

### 1.2 Comparaison avec EpicLegends

| Fonctionnalité | Grands Studios | EpicLegends | Status |
|----------------|----------------|-------------|--------|
| Terrain procedural | ✅ Outils custom | ❌ Aucun | **CRITIQUE** |
| Biome system | ✅ Avec previews | ❌ Aucun | **CRITIQUE** |
| Dungeon generator | ✅ Node-based | ❌ Aucun | **HAUTE** |
| Object placer | ✅ Brush tools | ❌ Manuel | **HAUTE** |
| NPC/Enemy placer | ✅ Visual tools | ❌ Manuel | **MOYENNE** |
| Quest designer | ✅ Visual graph | ⚠️ ScriptableObjects | **MOYENNE** |
| Texture blending | ✅ Splat maps | ❌ Matériaux manuels | **HAUTE** |
| LOD generator | ✅ Automatique | ❌ Aucun | **MOYENNE** |

---

## 2. OUTILS À CRÉER (PAR PRIORITÉ)

### PRIORITÉ 1: CRITIQUE (Sans ça, pas de jeu commercialisable)

#### 2.1 ProceduralTerrainGenerator.cs
**But**: Générer des terrains avec heightmap et biomes

```
Menu: EpicLegends > Tools > Terrain Generator
Fonctionnalités:
- Taille du terrain (256x256 à 4096x4096)
- Seed pour reproductibilité
- Octaves noise (1-8)
- Hauteur min/max
- Preview en temps réel
- Export vers Unity Terrain
```

**Algorithmes (déjà dans BIBLE)**:
- Perlin Noise multi-octaves (fBm)
- Falloff map pour îles
- Erosion hydraulique (optionnel)

#### 2.2 BiomeSystem.cs
**But**: Définir et placer des biomes automatiquement

```
Biomes à supporter:
- Forêt (arbres, buissons, champignons)
- Plaine (herbe haute, fleurs, rochers)
- Montagne (neige, rochers, glace)
- Désert (sable, cactus, oasis)
- Marécage (eau stagnante, brouillard, végétation morte)
- Donjon intérieur (torches, coffres, pièges)

Fonctionnalités:
- Définition par température/humidité
- Transitions douces entre biomes
- Placement automatique de végétation
- Spawners d'ennemis par biome
```

#### 2.3 DungeonGenerator.cs
**But**: Créer des donjons procéduraux jouables

```
Menu: EpicLegends > Tools > Dungeon Generator
Algorithmes:
- BSP (Binary Space Partitioning) - déjà dans BIBLE
- Cellular Automata pour caves - déjà dans BIBLE
- Wave Function Collapse (optionnel)

Fonctionnalités:
- Nombre de salles
- Taille min/max des salles
- Pourcentage de couloirs
- Placement automatique: portes, coffres, ennemis, boss
- Preview 2D avant génération 3D
- Export en prefab
```

---

### PRIORITÉ 2: HAUTE (Accélère énormément le développement)

#### 2.4 ObjectPlacer.cs (Brush Tool)
**But**: Placer des objets avec un brush comme dans les éditeurs pro

```
Menu: EpicLegends > Tools > Object Placer
Fonctionnalités:
- Brush radius ajustable
- Densité (objets par m²)
- Rotation aléatoire
- Scale aléatoire (min/max)
- Align to surface normal
- Catégories: Arbres, Rochers, Props, Ennemis
- Undo/Redo support
- Erase mode
```

#### 2.5 TextureBlender.cs (Splat Map)
**But**: Peindre des textures sur le terrain

```
Textures par défaut:
- Herbe (plusieurs variantes)
- Terre/Boue
- Pierre/Roche
- Sable
- Neige
- Chemin (dirt path)

Fonctionnalités:
- Brush pour peindre
- Blend automatique entre textures
- Règles par altitude (neige en haut, sable en bas)
- Règles par pente (roche sur pentes raides)
- Import textures custom
```

#### 2.6 PrefabLibrary.cs
**But**: Bibliothèque organisée de tous les prefabs

```
Catégories:
├── Environment/
│   ├── Trees/
│   ├── Rocks/
│   ├── Vegetation/
│   └── Props/
├── Buildings/
│   ├── Houses/
│   ├── Structures/
│   └── Dungeon Pieces/
├── Characters/
│   ├── Player/
│   ├── NPCs/
│   └── Enemies/
├── Items/
│   ├── Weapons/
│   ├── Armor/
│   └── Consumables/
└── VFX/
    ├── Combat/
    ├── Environment/
    └── UI/

Fonctionnalités:
- Recherche par nom/tag
- Preview 3D
- Drag & drop dans la scène
- Favoris
- Création rapide de variantes
```

---

### PRIORITÉ 3: MOYENNE (Qualité de vie)

#### 2.7 EnemyWaveDesigner.cs
**But**: Designer les vagues d'ennemis visuellement

```
Fonctionnalités:
- Timeline des vagues
- Types d'ennemis par vague
- Spawn points sur la map
- Difficulté progressive
- Preview de la vague
- Export/Import de configurations
```

#### 2.8 QuestGraphEditor.cs
**But**: Éditeur visuel de quêtes (node-based)

```
Nodes disponibles:
- Start Quest
- Objective (Kill, Collect, Talk, Reach)
- Branch (condition)
- Reward
- End Quest

Fonctionnalités:
- Drag & drop nodes
- Connexions visuelles
- Preview du flow
- Validation automatique
- Export vers QuestData
```

#### 2.9 DialogueTreeEditor.cs
**But**: Créer des dialogues complexes visuellement

```
Fonctionnalités:
- Nodes de dialogue
- Branches de choix
- Conditions (a item, completed quest, etc.)
- Preview du dialogue
- Localisation support
- Voice-over markers
```

---

### PRIORITÉ 4: NICE TO HAVE (Pour polir le jeu)

#### 2.10 LODGenerator.cs
**But**: Générer automatiquement les niveaux de détail

```
Fonctionnalités:
- Analyse du mesh
- Génération LOD1, LOD2, LOD3
- Preview des transitions
- Batch processing
```

#### 2.11 LightingPresetManager.cs
**But**: Gérer les présets d'éclairage par zone

```
Présets:
- Jour ensoleillé
- Jour nuageux
- Crépuscule
- Nuit étoilée
- Donjon sombre
- Boss arena (dramatique)

Fonctionnalités:
- Transition smooth entre présets
- Cycle jour/nuit automatique
- Override par zone
```

#### 2.12 AudioZoneManager.cs
**But**: Définir les zones audio visuellement

```
Fonctionnalités:
- Zones 2D sur la carte
- Musique par zone
- Ambiance par zone
- Transitions crossfade
- Preview dans l'éditeur
```

---

## 3. PLAN D'IMPLÉMENTATION

### Phase 1: Fondations (2 semaines)

```
Semaine 1:
□ ProceduralTerrainGenerator (base)
□ NoiseGenerator utilities
□ Editor window template

Semaine 2:
□ BiomeData ScriptableObject
□ BiomeSystem (définition seulement)
□ Integration avec terrain
```

### Phase 2: Génération Procédurale (3 semaines)

```
Semaine 3:
□ DungeonGenerator avec BSP
□ Cave generator avec Cellular Automata
□ Room/Corridor connection

Semaine 4:
□ Dungeon prefab placement
□ Door/chest spawning
□ Enemy spawn points

Semaine 5:
□ Preview system
□ Export/Save dungeons
□ Random seed system
```

### Phase 3: Outils de Placement (2 semaines)

```
Semaine 6:
□ ObjectPlacer brush tool
□ Scene view integration
□ Categories & filters

Semaine 7:
□ TextureBlender
□ Splat map painting
□ Automatic rules
```

### Phase 4: Éditeurs Visuels (3 semaines)

```
Semaine 8:
□ PrefabLibrary window
□ Search & preview
□ Drag & drop

Semaine 9:
□ QuestGraphEditor (base)
□ Node system
□ Connections

Semaine 10:
□ DialogueTreeEditor
□ Export to ScriptableObjects
□ Integration with game
```

### Phase 5: Polish (2 semaines)

```
Semaine 11:
□ LODGenerator
□ LightingPresetManager
□ AudioZoneManager

Semaine 12:
□ Documentation
□ Tutorials
□ Bug fixes
```

---

## 4. ARCHITECTURE TECHNIQUE

### 4.1 Structure des Fichiers

```
Assets/
├── Scripts/
│   └── Editor/
│       ├── Tools/
│       │   ├── ProceduralTerrainGenerator.cs
│       │   ├── BiomeSystemEditor.cs
│       │   ├── DungeonGenerator.cs
│       │   ├── ObjectPlacer.cs
│       │   ├── TextureBlender.cs
│       │   └── PrefabLibrary.cs
│       ├── Windows/
│       │   ├── QuestGraphEditor.cs
│       │   ├── DialogueTreeEditor.cs
│       │   └── EnemyWaveDesigner.cs
│       └── Utilities/
│           ├── EditorIcons.cs
│           ├── EditorStyles.cs
│           └── UndoHelper.cs
├── Editor Resources/
│   ├── Icons/
│   ├── Styles/
│   └── Templates/
└── ScriptableObjects/
    ├── Biomes/
    ├── Dungeons/
    └── Presets/
```

### 4.2 Dépendances

```
Required Packages:
- Unity Editor Coroutines (pour async dans editor)
- SerializedDictionary (pour save/load)

Optionnel:
- GraphView API (pour node editors)
- UIToolkit (pour UI modernes)
```

---

## 5. EXEMPLES DE CODE (Templates)

### 5.1 Template Editor Window

```csharp
using UnityEditor;
using UnityEngine;

public class ToolTemplate : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Tool Name")]
    public static void ShowWindow()
    {
        var window = GetWindow<ToolTemplate>("Tool Name");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        // Initialisation
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tool Name", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Contenu de l'outil

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Execute", GUILayout.Height(30)))
        {
            Execute();
        }
    }

    private void Execute()
    {
        Undo.RecordObject(target, "Tool Action");
        // Action
    }
}
```

### 5.2 Template Scene View Tool

```csharp
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class SceneViewTool
{
    static bool _isActive;

    static SceneViewTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        if (!_isActive) return;

        Event e = Event.current;

        // Handle mouse events
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Place object at hit point
                PlaceObject(hit.point, hit.normal);
                e.Use();
            }
        }

        // Draw preview
        Handles.color = Color.green;
        Handles.DrawWireDisc(brushPosition, Vector3.up, brushRadius);
    }
}
```

---

## 6. RESSOURCES GRATUITES POUR ASSETS

### 6.1 Modèles 3D (Gratuits)

| Source | Type | Qualité | URL |
|--------|------|---------|-----|
| Kenney | Low-poly props | Excellente | kenney.nl |
| Quaternius | Characters, props | Très bonne | quaternius.com |
| Synty (starter) | Polygon style | Excellente | syntystore.com |
| Mixamo | Animations | Excellente | mixamo.com |
| Sketchfab | Varié | Variable | sketchfab.com |

### 6.2 Textures (Gratuites)

| Source | Type | URL |
|--------|------|-----|
| ambientCG | PBR textures | ambientcg.com |
| Poly Haven | HDRI, textures | polyhaven.com |
| Texture.ninja | Seamless | texture.ninja |
| 3D Textures | Terrain | 3dtextures.me |

### 6.3 Audio (Gratuit)

| Source | Type | URL |
|--------|------|-----|
| Freesound | SFX | freesound.org |
| OpenGameArt | Music + SFX | opengameart.org |
| Mixkit | Music | mixkit.co |
| Sonniss | GDC bundles | sonniss.com |

---

## 7. PROCHAINES ÉTAPES IMMÉDIATES

### Cette semaine:

1. **Créer ProceduralTerrainGenerator.cs**
   - Utiliser les algorithmes fBm du BIBLE
   - Interface avec preview
   - Export vers Unity Terrain

2. **Créer BiomeData.cs**
   - ScriptableObject pour définir un biome
   - Liste de prefabs par biome
   - Règles de placement

3. **Créer ObjectPlacer.cs**
   - Brush basique
   - Placement sur terrain
   - Rotation/scale aléatoire

### Semaine prochaine:

4. **Créer DungeonGenerator.cs**
   - Implémenter BSP du BIBLE
   - Générer mesh simple
   - Placer portes et coffres

5. **Améliorer FullWorldGenerator.cs**
   - Utiliser les nouveaux outils
   - Générer monde plus varié
   - Ajouter biomes

---

## 8. CONCLUSION

Le projet EpicLegends a une base solide de code gameplay, mais manque crucialement d'outils de création. Les grands studios consacrent 30-40% de leur développement à créer ces outils car ils accélèrent exponentiellement la création de contenu.

**Investissement recommandé**: 10-12 semaines pour les outils critiques

**Retour sur investissement**:
- Création de contenu 10x plus rapide
- Itération facile
- Moins d'erreurs manuelles
- Projet maintenable

---

**Document créé**: 1 Février 2026
**Auteur**: Claude (Assistant IA)
**Pour**: Sylvain - EpicLegends
