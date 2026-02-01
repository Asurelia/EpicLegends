# EpicLegends - Game Development Bible

> **Document complet de référence pour le développement de jeux vidéo Unity**
>
> Ce document compile les meilleures pratiques, patterns de code, formules et techniques pour minimiser les erreurs et accélérer le développement.

**Version**: 1.0
**Dernière mise à jour**: 2026-01-30
**Projet**: EpicLegends - Action RPG 3D

---

## Table des Matières

1. [Systèmes RPG (Combat, Inventaire, Progression, Quêtes)](#1-systèmes-rpg)
2. [Performance Unity (CPU, GPU, Mémoire, Physique)](#2-performance-unity)
3. [Génération Procédurale (Terrain, Donjons, Streaming)](#3-génération-procédurale)
4. [IA et Comportements (Pathfinding, Behavior Trees, Combat)](#4-ia-et-comportements)
5. [UI/UX (HUD, Menus, Feedback, Accessibilité)](#5-uiux)
6. [Audio et Polish (Sound Design, Game Feel, Juice)](#6-audio-et-polish)
7. [Multijoueur et Réseau (Sync, Lobby, Sécurité)](#7-multijoueur-et-réseau)
8. [Testing et Debugging (Unit Tests, Profiling, CI/CD)](#8-testing-et-debugging)
9. [Checklists et Anti-Patterns](#9-checklists-et-anti-patterns)

---

# 1. SYSTÈMES RPG

## 1.1 Système de Combat

### Architecture Hitbox/Hurtbox

```csharp
public enum HitboxType { Damage, Block, Parry }

public class Hitbox : MonoBehaviour
{
    [SerializeField] private HitboxType _type;
    [SerializeField] private float _damage;
    [SerializeField] private float _knockbackForce;

    private Collider _collider;
    private HashSet<Collider> _hitTargets = new HashSet<Collider>();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false; // Désactivé par défaut
    }

    // Appelé via Animation Event
    public void EnableHitbox()
    {
        _hitTargets.Clear();
        _collider.enabled = true;
    }

    public void DisableHitbox() => _collider.enabled = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hitTargets.Contains(other)) return;

        if (other.TryGetComponent<Hurtbox>(out var hurtbox))
        {
            _hitTargets.Add(other);
            hurtbox.TakeDamage(_damage, _knockbackForce, transform.position);
        }
    }
}
```

### Formule de Dégâts (Percentage Reduction)

```csharp
public static class CombatFormulas
{
    private const float DEFENSE_CONSTANT = 100f;

    /// <summary>
    /// Calcul principal des dégâts avec réduction par pourcentage
    /// </summary>
    public static float CalculateDamage(float attack, float defense)
    {
        // Formule: damage = attack * (K / (K + defense))
        // Où K est la constante de défense (100)
        return attack * (DEFENSE_CONSTANT / (DEFENSE_CONSTANT + defense));
    }

    /// <summary>
    /// Calcul avec différence de niveau
    /// </summary>
    public static float CalculateDamageWithLevel(
        float attack,
        float defense,
        int attackerLevel,
        int defenderLevel)
    {
        int levelDiff = attackerLevel - defenderLevel;
        float levelMultiplier = 1f + (levelDiff * 0.05f); // ±5% par niveau
        levelMultiplier = Mathf.Clamp(levelMultiplier, 0.5f, 2f);

        return CalculateDamage(attack, defense) * levelMultiplier;
    }

    /// <summary>
    /// Calcul complet avec critique et éléments
    /// </summary>
    public static float CalculateFinalDamage(
        float attack,
        float defense,
        int attackerLevel,
        int defenderLevel,
        float critChance,
        float critMultiplier,
        float elementalResistance)
    {
        float damage = CalculateDamageWithLevel(attack, defense, attackerLevel, defenderLevel);

        // Critique
        if (Random.value < critChance)
            damage *= critMultiplier;

        // Résistance élémentaire (0.0 = normal, 0.5 = 50% résistance)
        damage *= (1f - elementalResistance);

        return Mathf.Max(1f, damage); // Minimum 1 dégât
    }
}
```

### Système de Combo avec State Machine

```csharp
public enum ComboState { Idle, Attack1, Attack2, Attack3, Finisher }

public class ComboSystem : MonoBehaviour
{
    [SerializeField] private float _comboWindow = 0.5f;
    [SerializeField] private float _inputBufferWindow = 0.1f; // 6 frames à 60fps

    private ComboState _currentState = ComboState.Idle;
    private float _lastAttackTime;
    private bool _inputBuffered;

    public void TryAttack()
    {
        float timeSinceLastAttack = Time.time - _lastAttackTime;

        // Input buffering
        if (timeSinceLastAttack < _inputBufferWindow)
        {
            _inputBuffered = true;
            return;
        }

        // Combo window check
        if (timeSinceLastAttack > _comboWindow)
        {
            _currentState = ComboState.Idle;
        }

        // Progression du combo
        _currentState = _currentState switch
        {
            ComboState.Idle => ComboState.Attack1,
            ComboState.Attack1 => ComboState.Attack2,
            ComboState.Attack2 => ComboState.Attack3,
            ComboState.Attack3 => ComboState.Finisher,
            ComboState.Finisher => ComboState.Attack1,
            _ => ComboState.Idle
        };

        ExecuteAttack(_currentState);
        _lastAttackTime = Time.time;
    }

    private void ExecuteAttack(ComboState state)
    {
        // Jouer l'animation correspondante
        _animator.SetTrigger(state.ToString());
    }
}
```

### Constantes de Combat Recommandées

```csharp
public static class CombatConstants
{
    // I-Frames
    public const float PLAYER_IFRAMES_DURATION = 0.5f; // 30 frames à 60fps
    public const float KNOCKBACK_DURATION = 0.3f;

    // Combo
    public const float COMBO_WINDOW = 0.5f;
    public const float INPUT_BUFFER_WINDOW = 0.1f; // 6 frames

    // Critiques
    public const float BASE_CRIT_CHANCE = 0.05f; // 5%
    public const float CRIT_CHANCE_PER_AGILITY = 0.002f; // 0.2%
    public const float BASE_CRIT_MULTIPLIER = 1.5f; // 150%

    // Stats par attribut
    public const int HEALTH_PER_VITALITY = 10;
    public const int ATTACK_PER_STRENGTH = 2;
    public const int DEFENSE_PER_VITALITY = 1;
}
```

---

## 1.2 Système d'Inventaire

### Architecture Grid-Based

```csharp
[CreateAssetMenu(menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    public enum ItemType { Weapon, Armor, Consumable, Material, Quest }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    [Header("Basic Info")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public ItemType type;
    public Rarity rarity;

    [Header("Stacking")]
    public bool isStackable;
    public int maxStackSize = 99;

    [Header("Value")]
    public int buyPrice;
    public int SellPrice => Mathf.RoundToInt(buyPrice * 0.25f);

    [Header("Equipment Stats")]
    public int attackBonus;
    public int defenseBonus;
    public int healthBonus;
}

public class Inventory : MonoBehaviour
{
    [SerializeField] private int _maxSlots = 30;
    private List<InventorySlot> _slots = new List<InventorySlot>();

    public int Gold { get; set; }

    public event Action<int> OnSlotChanged;
    public event Action OnInventoryFull;

    public bool TryAddItem(ItemData item, int quantity = 1)
    {
        // Essayer de stacker d'abord
        if (item.isStackable)
        {
            var existingSlot = _slots.FirstOrDefault(s =>
                s.Item == item && s.Quantity < item.maxStackSize);

            if (existingSlot != null)
            {
                int spaceInSlot = item.maxStackSize - existingSlot.Quantity;
                int toAdd = Mathf.Min(quantity, spaceInSlot);
                existingSlot.Quantity += toAdd;
                quantity -= toAdd;
                OnSlotChanged?.Invoke(_slots.IndexOf(existingSlot));

                if (quantity <= 0) return true;
            }
        }

        // Ajouter dans nouveau slot
        if (_slots.Count >= _maxSlots)
        {
            OnInventoryFull?.Invoke();
            return false;
        }

        _slots.Add(new InventorySlot { Item = item, Quantity = quantity });
        OnSlotChanged?.Invoke(_slots.Count - 1);
        return true;
    }

    public bool RemoveItem(ItemData item, int quantity = 1)
    {
        var slot = _slots.FirstOrDefault(s => s.Item == item && s.Quantity >= quantity);
        if (slot == null) return false;

        slot.Quantity -= quantity;
        if (slot.Quantity <= 0)
        {
            int index = _slots.IndexOf(slot);
            _slots.Remove(slot);
            OnSlotChanged?.Invoke(index);
        }

        return true;
    }
}

[System.Serializable]
public class InventorySlot
{
    public ItemData Item;
    public int Quantity;
}
```

### Système de Loot avec Weight-Based Probabilities

```csharp
[CreateAssetMenu(menuName = "Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public class LootEntry
    {
        public ItemData item;
        public int weight = 100;
        public float dropChance = 1f; // 0-1
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }

    public List<LootEntry> entries = new List<LootEntry>();

    // Poids recommandés par rareté
    public static class RarityWeights
    {
        public const int COMMON = 1000;
        public const int UNCOMMON = 200;
        public const int RARE = 80;
        public const int EPIC = 15;
        public const int LEGENDARY = 5;
    }

    public List<ItemData> RollLoot(int rolls = 1)
    {
        List<ItemData> results = new List<ItemData>();
        int totalWeight = entries.Sum(e => e.weight);

        for (int i = 0; i < rolls; i++)
        {
            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (var entry in entries)
            {
                cumulative += entry.weight;
                if (roll < cumulative)
                {
                    if (Random.value <= entry.dropChance)
                    {
                        int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                        for (int q = 0; q < qty; q++)
                            results.Add(entry.item);
                    }
                    break;
                }
            }
        }

        return results;
    }
}
```

---

## 1.3 Système de Progression

### Courbes d'XP

```csharp
public static class ProgressionFormulas
{
    /// <summary>
    /// Courbe exponentielle (recommandée pour RPG)
    /// XP(n) = Base * Multiplier^(n-1)
    /// </summary>
    public static int GetXPForLevelExponential(int level, int baseXP = 100, float multiplier = 1.15f)
    {
        return Mathf.RoundToInt(baseXP * Mathf.Pow(multiplier, level - 1));
    }

    /// <summary>
    /// Courbe quadratique (progression plus douce)
    /// XP(n) = Base * n^2
    /// </summary>
    public static int GetXPForLevelQuadratic(int level, int baseXP = 100)
    {
        return baseXP * level * level;
    }

    /// <summary>
    /// XP total nécessaire jusqu'au niveau donné
    /// </summary>
    public static int GetTotalXPToLevel(int level)
    {
        int total = 0;
        for (int i = 1; i < level; i++)
            total += GetXPForLevelExponential(i);
        return total;
    }
}

// Exemple de table d'XP (courbe exponentielle, base=100, mult=1.15)
// Level | XP Needed | Total XP  | Time to Level
// ------+-----------+-----------+--------------
// 1     | 0         | 0         | -
// 2     | 100       | 100       | 5 min
// 5     | 152       | 519       | 8 min
// 10    | 352       | 1929      | 18 min
// 20    | 1369      | 13129     | 69 min
// 50    | 108366    | 1293849   | 5416 min
```

### Système de Skill Tree

```csharp
[CreateAssetMenu(menuName = "Progression/Skill Node")]
public class SkillNode : ScriptableObject
{
    public enum NodeType { Passive, Active, Attribute }

    [Header("Info")]
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;
    public NodeType nodeType;

    [Header("Requirements")]
    public int requiredLevel;
    public int skillPointCost = 1;
    public SkillNode[] prerequisites;

    [Header("Passive Effects")]
    public int attackBonus;
    public int defenseBonus;
    public float healthBonus;

    [Header("Active Skill")]
    public GameObject skillPrefab;
    public float manaCost;
    public float cooldown;

    public bool CanUnlock(int playerLevel, int skillPoints, HashSet<SkillNode> unlockedNodes)
    {
        if (playerLevel < requiredLevel) return false;
        if (skillPoints < skillPointCost) return false;

        foreach (var prereq in prerequisites)
            if (!unlockedNodes.Contains(prereq))
                return false;

        return true;
    }
}
```

---

## 1.4 Système de Quêtes

### Quest State Machine

```csharp
[CreateAssetMenu(menuName = "Quests/Quest Data")]
public class QuestData : ScriptableObject
{
    public enum QuestState { NotStarted, InProgress, Completed, Failed }

    [Header("Info")]
    public string questName;
    [TextArea] public string description;
    public int recommendedLevel;

    [Header("Requirements")]
    public QuestData[] prerequisiteQuests;

    [Header("Objectives")]
    public QuestObjective[] objectives;

    [Header("Rewards")]
    public int goldReward;
    public int xpReward;
    public ItemData[] itemRewards;
}

[System.Serializable]
public class QuestObjective
{
    public enum ObjectiveType { KillEnemies, CollectItems, TalkToNPC, ReachLocation }

    public string description;
    public ObjectiveType type;
    public string targetID; // ID de l'ennemi, item, NPC, ou location
    public int requiredAmount;

    [HideInInspector] public int currentAmount;

    public bool IsComplete => currentAmount >= requiredAmount;

    public void UpdateProgress(string id, int amount = 1)
    {
        if (id == targetID)
            currentAmount = Mathf.Min(currentAmount + amount, requiredAmount);
    }
}
```

### Quest Manager

```csharp
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private List<Quest> _activeQuests = new List<Quest>();
    private HashSet<QuestData> _completedQuests = new HashSet<QuestData>();

    public event Action<Quest> OnQuestStarted;
    public event Action<Quest> OnQuestCompleted;
    public event Action<Quest, QuestObjective> OnObjectiveUpdated;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartQuest(QuestData questData)
    {
        if (_completedQuests.Contains(questData)) return;
        if (_activeQuests.Any(q => q.Data == questData)) return;

        var quest = new Quest(questData);
        _activeQuests.Add(quest);
        OnQuestStarted?.Invoke(quest);
    }

    /// <summary>
    /// Appelé par les autres systèmes (combat, collecte, dialogue)
    /// </summary>
    public void UpdateProgress(string targetID, int amount = 1)
    {
        foreach (var quest in _activeQuests.ToList())
        {
            foreach (var objective in quest.Data.objectives)
            {
                if (!objective.IsComplete)
                {
                    objective.UpdateProgress(targetID, amount);
                    OnObjectiveUpdated?.Invoke(quest, objective);
                }
            }

            if (quest.CheckCompletion())
                CompleteQuest(quest);
        }
    }

    private void CompleteQuest(Quest quest)
    {
        _activeQuests.Remove(quest);
        _completedQuests.Add(quest.Data);

        // Distribuer récompenses
        var player = FindFirstObjectByType<PlayerStats>();
        player.GainXP(quest.Data.xpReward);

        var inventory = FindFirstObjectByType<Inventory>();
        inventory.Gold += quest.Data.goldReward;

        foreach (var item in quest.Data.itemRewards)
            inventory.TryAddItem(item, 1);

        OnQuestCompleted?.Invoke(quest);
    }
}
```

---

# 2. PERFORMANCE UNITY

## 2.1 Optimisation CPU

### Object Pooling (Built-in Unity)

```csharp
using UnityEngine.Pool;

public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile _prefab;

    private ObjectPool<Projectile> _pool;

    private void Awake()
    {
        _pool = new ObjectPool<Projectile>(
            createFunc: () => Instantiate(_prefab),
            actionOnGet: (p) => p.gameObject.SetActive(true),
            actionOnRelease: (p) => p.gameObject.SetActive(false),
            actionOnDestroy: (p) => Destroy(p.gameObject),
            collectionCheck: false,
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public Projectile Get() => _pool.Get();

    public void Return(Projectile projectile) => _pool.Release(projectile);
}

// Utilisation dans projectile
public class Projectile : MonoBehaviour
{
    private ProjectilePool _pool;

    public void Initialize(ProjectilePool pool) => _pool = pool;

    private void OnCollisionEnter(Collision collision)
    {
        // Logique de collision...
        _pool.Return(this);
    }
}
```

### Caching de Components (OBLIGATOIRE)

```csharp
// ❌ MAUVAIS - GetComponent chaque frame
void Update()
{
    GetComponent<Rigidbody>().AddForce(Vector3.up);
    GetComponent<Animator>().SetFloat("Speed", speed);
}

// ✅ BON - Cache dans Awake
private Rigidbody _rb;
private Animator _animator;

void Awake()
{
    _rb = GetComponent<Rigidbody>();
    _animator = GetComponent<Animator>();
}

void Update()
{
    _rb.AddForce(Vector3.up);
    _animator.SetFloat("Speed", speed);
}
```

### Optimisation Garbage Collection

```csharp
// ❌ MAUVAIS - Génère des allocations
void Update()
{
    string status = "Player HP: " + health;  // String concat = allocation
    var enemies = FindObjectsOfType<Enemy>();  // Array allocation
}

// ✅ BON - Zéro allocation
private StringBuilder _sb = new StringBuilder();
private List<Enemy> _enemyCache = new List<Enemy>();
private RaycastHit[] _hitResults = new RaycastHit[10];

void Update()
{
    _sb.Clear();
    _sb.Append("Player HP: ").Append(health);

    // Utiliser NonAlloc versions
    int hitCount = Physics.RaycastNonAlloc(ray, _hitResults);
}
```

### Job System avec Burst

```csharp
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct EnemyUpdateJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> PlayerPositions;
    public NativeArray<Vector3> EnemyPositions;
    public NativeArray<float> EnemySpeeds;
    public float DeltaTime;

    public void Execute(int index)
    {
        Vector3 direction = (PlayerPositions[0] - EnemyPositions[index]).normalized;
        EnemyPositions[index] += direction * EnemySpeeds[index] * DeltaTime;
    }
}

// Utilisation
public class EnemyManager : MonoBehaviour
{
    private NativeArray<Vector3> _positions;
    private JobHandle _jobHandle;

    void Update()
    {
        var job = new EnemyUpdateJob
        {
            PlayerPositions = _playerPositions,
            EnemyPositions = _positions,
            EnemySpeeds = _speeds,
            DeltaTime = Time.deltaTime
        };

        _jobHandle = job.Schedule(_positions.Length, 64);
    }

    void LateUpdate()
    {
        _jobHandle.Complete();
        // Appliquer les positions
    }

    void OnDestroy()
    {
        _positions.Dispose();
    }
}
```

---

## 2.2 Optimisation GPU

### Batching et Instancing

```csharp
// GPU Instancing pour objets identiques
public class GPUInstancingExample : MonoBehaviour
{
    [SerializeField] private Mesh _mesh;
    [SerializeField] private Material _material; // GPU Instancing enabled

    private Matrix4x4[] _matrices;
    private MaterialPropertyBlock _propBlock;

    void Start()
    {
        _propBlock = new MaterialPropertyBlock();
        _matrices = new Matrix4x4[1000];

        for (int i = 0; i < 1000; i++)
        {
            _matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 50f,
                Quaternion.identity,
                Vector3.one
            );
        }
    }

    void Update()
    {
        Graphics.DrawMeshInstanced(_mesh, 0, _material, _matrices, 1000, _propBlock);
    }
}
```

### LOD Configuration

```csharp
void SetupLOD()
{
    LODGroup lodGroup = gameObject.AddComponent<LODGroup>();

    LOD[] lods = new LOD[3];

    // LOD 0 - Full detail (60% à 100% screen height)
    lods[0] = new LOD(0.6f, new Renderer[] { highDetailRenderer });

    // LOD 1 - Medium (30% à 60%)
    lods[1] = new LOD(0.3f, new Renderer[] { mediumDetailRenderer });

    // LOD 2 - Low (10% à 30%)
    lods[2] = new LOD(0.1f, new Renderer[] { lowDetailRenderer });

    lodGroup.SetLODs(lods);
    lodGroup.RecalculateBounds();
}
```

---

## 2.3 Optimisation Physique

### Layer Collision Matrix

```csharp
// Configurer les layers de collision
void ConfigurePhysicsLayers()
{
    int playerLayer = LayerMask.NameToLayer("Player");
    int enemyLayer = LayerMask.NameToLayer("Enemy");
    int bulletLayer = LayerMask.NameToLayer("Bullet");
    int pickupLayer = LayerMask.NameToLayer("Pickup");

    // Désactiver collisions inutiles
    Physics.IgnoreLayerCollision(bulletLayer, bulletLayer, true);
    Physics.IgnoreLayerCollision(pickupLayer, pickupLayer, true);
    Physics.IgnoreLayerCollision(pickupLayer, enemyLayer, true);
}

// Layers recommandés:
// Layer 8: Player
// Layer 9: Enemy
// Layer 10: Projectile
// Layer 11: Environment
// Layer 12: Trigger
// Layer 13: Pickup
```

### Rigidbody Sleeping

```csharp
void ConfigureRigidbody(Rigidbody rb)
{
    // Augmenter le seuil de sleep pour meilleure performance
    rb.sleepThreshold = 0.1f; // Default: 0.005

    // Réduire les iterations du solver
    rb.solverIterations = 4; // Default: 6
    rb.solverVelocityIterations = 2; // Default: 4

    // Forcer le sleep quand stationnaire
    if (rb.velocity.magnitude < 0.1f)
        rb.Sleep();
}
```

---

## 2.4 Optimisation Mémoire

### Addressables

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesLoader : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> _handle;

    async void LoadAsset()
    {
        _handle = Addressables.LoadAssetAsync<GameObject>("EnemyPrefab");
        await _handle.Task;

        if (_handle.Status == AsyncOperationStatus.Succeeded)
        {
            Instantiate(_handle.Result);
        }
    }

    void OnDestroy()
    {
        // CRITIQUE: Toujours libérer
        if (_handle.IsValid())
            Addressables.Release(_handle);
    }
}
```

### Compression Textures

| Plateforme | Format | Qualité | Usage |
|------------|--------|---------|-------|
| PC | DXT5/BC3 | High | Textures avec alpha |
| PC | DXT1/BC1 | High | Textures sans alpha |
| Mobile | ASTC 6x6 | Medium | Balance qualité/taille |
| Mobile | ASTC 4x4 | High | Haute qualité |

### Compression Audio

| Type | Load Type | Format | Usage |
|------|-----------|--------|-------|
| Music | Streaming | Vorbis | Musique de fond |
| SFX Court | Decompress on Load | ADPCM | Footsteps, UI |
| SFX Long | Compressed in Memory | Vorbis | Combat sounds |
| Dialogue | Compressed in Memory | Vorbis | Voix |

---

# 3. GÉNÉRATION PROCÉDURALE

## 3.1 Noise Functions

### Perlin Noise Multi-Octaves (fBm)

```csharp
public static class NoiseGenerator
{
    /// <summary>
    /// Fractal Brownian Motion - Noise multi-couches
    /// </summary>
    public static float FBM(
        float x, float y,
        int octaves = 4,
        float persistence = 0.5f,  // Réduction amplitude par octave
        float lacunarity = 2.0f)   // Augmentation fréquence par octave
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxValue; // Normaliser 0-1
    }

    /// <summary>
    /// Génère une heightmap pour terrain
    /// </summary>
    public static float[,] GenerateHeightmap(
        int width, int height,
        float scale,
        int seed,
        int octaves = 4)
    {
        float[,] heightmap = new float[width, height];

        System.Random prng = new System.Random(seed);
        float offsetX = prng.Next(-100000, 100000);
        float offsetY = prng.Next(-100000, 100000);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sampleX = (x + offsetX) / scale;
                float sampleY = (y + offsetY) / scale;

                heightmap[x, y] = FBM(sampleX, sampleY, octaves);
            }
        }

        return heightmap;
    }
}
```

## 3.2 Génération de Donjons

### Binary Space Partitioning (BSP)

```csharp
public class BSPDungeon
{
    public class Room
    {
        public RectInt Bounds;
        public Room Left, Right;
        public bool IsLeaf => Left == null && Right == null;
    }

    private const int MIN_ROOM_SIZE = 6;

    public Room Generate(int width, int height, int iterations)
    {
        Room root = new Room { Bounds = new RectInt(0, 0, width, height) };
        SplitRecursive(root, iterations);
        return root;
    }

    private void SplitRecursive(Room room, int iterations)
    {
        if (iterations <= 0) return;
        if (room.Bounds.width < MIN_ROOM_SIZE * 2 &&
            room.Bounds.height < MIN_ROOM_SIZE * 2) return;

        // Décider direction de split
        bool splitHorizontal = Random.value > 0.5f;
        if (room.Bounds.width > room.Bounds.height * 1.5f)
            splitHorizontal = false;
        else if (room.Bounds.height > room.Bounds.width * 1.5f)
            splitHorizontal = true;

        // Split
        int splitPos;
        if (splitHorizontal)
        {
            splitPos = Random.Range(MIN_ROOM_SIZE, room.Bounds.height - MIN_ROOM_SIZE);
            room.Left = new Room { Bounds = new RectInt(
                room.Bounds.x, room.Bounds.y,
                room.Bounds.width, splitPos) };
            room.Right = new Room { Bounds = new RectInt(
                room.Bounds.x, room.Bounds.y + splitPos,
                room.Bounds.width, room.Bounds.height - splitPos) };
        }
        else
        {
            splitPos = Random.Range(MIN_ROOM_SIZE, room.Bounds.width - MIN_ROOM_SIZE);
            room.Left = new Room { Bounds = new RectInt(
                room.Bounds.x, room.Bounds.y,
                splitPos, room.Bounds.height) };
            room.Right = new Room { Bounds = new RectInt(
                room.Bounds.x + splitPos, room.Bounds.y,
                room.Bounds.width - splitPos, room.Bounds.height) };
        }

        SplitRecursive(room.Left, iterations - 1);
        SplitRecursive(room.Right, iterations - 1);
    }
}
```

### Cellular Automata (Caves)

```csharp
public class CaveGenerator
{
    private int[,] _map;
    private int _width, _height;

    public int[,] Generate(int width, int height, float fillProbability = 0.45f, int smoothIterations = 5)
    {
        _width = width;
        _height = height;
        _map = new int[width, height];

        // Initialisation aléatoire
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    _map[x, y] = 1; // Murs sur les bords
                else
                    _map[x, y] = Random.value < fillProbability ? 1 : 0;
            }
        }

        // Lissage avec règle 4-5
        for (int i = 0; i < smoothIterations; i++)
            SmoothMap();

        return _map;
    }

    private void SmoothMap()
    {
        int[,] newMap = new int[_width, _height];

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                int neighbors = CountNeighbors(x, y);

                // Règle 4-5: Devient mur si 5+ voisins sont murs
                if (neighbors >= 5)
                    newMap[x, y] = 1;
                else if (neighbors <= 3)
                    newMap[x, y] = 0;
                else
                    newMap[x, y] = _map[x, y];
            }
        }

        _map = newMap;
    }

    private int CountNeighbors(int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx < 0 || nx >= _width || ny < 0 || ny >= _height)
                    count++; // Hors limites = mur
                else
                    count += _map[nx, ny];
            }
        }
        return count;
    }
}
```

## 3.3 Chunk Streaming

```csharp
public class ChunkManager : MonoBehaviour
{
    [SerializeField] private int _chunkSize = 16;
    [SerializeField] private int _viewDistance = 3;
    [SerializeField] private Transform _player;

    private Dictionary<Vector2Int, Chunk> _loadedChunks = new Dictionary<Vector2Int, Chunk>();
    private Vector2Int _lastPlayerChunk;

    void Update()
    {
        Vector2Int currentChunk = WorldToChunkCoord(_player.position);

        if (currentChunk != _lastPlayerChunk)
        {
            UpdateChunks(currentChunk);
            _lastPlayerChunk = currentChunk;
        }
    }

    void UpdateChunks(Vector2Int centerChunk)
    {
        HashSet<Vector2Int> chunksToKeep = new HashSet<Vector2Int>();

        // Charger chunks dans le view distance
        for (int x = -_viewDistance; x <= _viewDistance; x++)
        {
            for (int z = -_viewDistance; z <= _viewDistance; z++)
            {
                Vector2Int coord = centerChunk + new Vector2Int(x, z);
                chunksToKeep.Add(coord);

                if (!_loadedChunks.ContainsKey(coord))
                    LoadChunkAsync(coord);
            }
        }

        // Décharger chunks hors view distance
        foreach (var coord in _loadedChunks.Keys.ToList())
        {
            if (!chunksToKeep.Contains(coord))
                UnloadChunk(coord);
        }
    }

    async void LoadChunkAsync(Vector2Int coord)
    {
        // Générer chunk en background
        Chunk chunk = await Task.Run(() => GenerateChunk(coord));

        // Instantier sur main thread
        _loadedChunks[coord] = chunk;
        chunk.Instantiate();
    }

    Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / _chunkSize),
            Mathf.FloorToInt(worldPos.z / _chunkSize)
        );
    }
}
```

## 3.4 Seeds Déterministes

```csharp
public class DeterministicWorld
{
    public int MasterSeed { get; private set; }

    private Dictionary<string, System.Random> _rngs = new Dictionary<string, System.Random>();

    public DeterministicWorld(int seed)
    {
        MasterSeed = seed;

        // RNGs isolés par système
        _rngs["terrain"] = new System.Random(HashSeed(seed, "terrain"));
        _rngs["biomes"] = new System.Random(HashSeed(seed, "biomes"));
        _rngs["loot"] = new System.Random(HashSeed(seed, "loot"));
        _rngs["enemies"] = new System.Random(HashSeed(seed, "enemies"));
    }

    public System.Random GetRNG(string category) => _rngs[category];

    /// <summary>
    /// Seed unique par chunk (toujours même résultat pour même coord)
    /// </summary>
    public int GetChunkSeed(Vector2Int coord)
    {
        unchecked
        {
            int hash = MasterSeed;
            hash = hash * 397 ^ coord.x;
            hash = hash * 397 ^ coord.y;
            return hash;
        }
    }

    private int HashSeed(int seed, string category)
    {
        return seed.GetHashCode() ^ category.GetHashCode();
    }
}
```

---

# 4. IA ET COMPORTEMENTS

## 4.1 Pathfinding

### NavMesh avec Path Smoothing

```csharp
using UnityEngine.AI;

public class AINavigation : MonoBehaviour
{
    private NavMeshAgent _agent;
    private NavMeshPath _path;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
    }

    public void MoveTo(Vector3 destination)
    {
        if (NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, _path))
        {
            if (_path.status == NavMeshPathStatus.PathComplete)
            {
                _agent.SetPath(_path);
            }
        }
    }

    // Funnel Algorithm pour path smoothing
    public Vector3[] SmoothPath(Vector3[] corners)
    {
        if (corners.Length <= 2) return corners;

        List<Vector3> smoothed = new List<Vector3> { corners[0] };

        for (int i = 1; i < corners.Length - 1; i++)
        {
            // Vérifier ligne de vue directe
            if (!HasLineOfSight(smoothed[smoothed.Count - 1], corners[i + 1]))
            {
                smoothed.Add(corners[i]);
            }
        }

        smoothed.Add(corners[corners.Length - 1]);
        return smoothed.ToArray();
    }

    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        return !Physics.Linecast(from, to, LayerMask.GetMask("Environment"));
    }
}
```

## 4.2 Finite State Machine (FSM)

```csharp
public interface IState
{
    void Enter();
    void Execute();
    void Exit();
}

public class StateMachine
{
    private IState _currentState;
    private Dictionary<System.Type, List<Transition>> _transitions = new Dictionary<System.Type, List<Transition>>();
    private List<Transition> _anyTransitions = new List<Transition>();

    private class Transition
    {
        public System.Func<bool> Condition;
        public IState To;
    }

    public void AddTransition(IState from, IState to, System.Func<bool> condition)
    {
        var type = from.GetType();
        if (!_transitions.ContainsKey(type))
            _transitions[type] = new List<Transition>();

        _transitions[type].Add(new Transition { Condition = condition, To = to });
    }

    public void AddAnyTransition(IState to, System.Func<bool> condition)
    {
        _anyTransitions.Add(new Transition { Condition = condition, To = to });
    }

    public void SetState(IState state)
    {
        _currentState?.Exit();
        _currentState = state;
        _currentState.Enter();
    }

    public void Tick()
    {
        // Check any transitions first
        foreach (var trans in _anyTransitions)
        {
            if (trans.Condition())
            {
                SetState(trans.To);
                return;
            }
        }

        // Check current state transitions
        if (_transitions.TryGetValue(_currentState.GetType(), out var transitions))
        {
            foreach (var trans in transitions)
            {
                if (trans.Condition())
                {
                    SetState(trans.To);
                    return;
                }
            }
        }

        _currentState.Execute();
    }
}

// États exemple
public class IdleState : IState
{
    private EnemyAI _ai;
    public IdleState(EnemyAI ai) => _ai = ai;

    public void Enter() => _ai.Animator.Play("Idle");
    public void Execute() { /* Idle logic */ }
    public void Exit() { }
}

public class ChaseState : IState
{
    private EnemyAI _ai;
    public ChaseState(EnemyAI ai) => _ai = ai;

    public void Enter() => _ai.Animator.Play("Run");
    public void Execute() => _ai.MoveToTarget();
    public void Exit() => _ai.StopMoving();
}
```

## 4.3 Behavior Tree

```csharp
public enum NodeStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract NodeStatus Evaluate();
}

public class Selector : BTNode
{
    private List<BTNode> _children;

    public Selector(List<BTNode> children) => _children = children;

    public override NodeStatus Evaluate()
    {
        foreach (var child in _children)
        {
            var status = child.Evaluate();
            if (status != NodeStatus.Failure)
                return status; // Return Success or Running
        }
        return NodeStatus.Failure;
    }
}

public class Sequence : BTNode
{
    private List<BTNode> _children;
    private int _currentChild;

    public Sequence(List<BTNode> children) => _children = children;

    public override NodeStatus Evaluate()
    {
        while (_currentChild < _children.Count)
        {
            var status = _children[_currentChild].Evaluate();
            if (status != NodeStatus.Success)
                return status; // Return Failure or Running
            _currentChild++;
        }
        _currentChild = 0;
        return NodeStatus.Success;
    }
}

// Exemple d'arbre complet
public class EnemyBehaviorTree
{
    public BTNode BuildTree(EnemyAI ai)
    {
        return new Selector(new List<BTNode>
        {
            // Combat sequence
            new Sequence(new List<BTNode>
            {
                new ConditionNode(() => ai.CanSeePlayer()),
                new Selector(new List<BTNode>
                {
                    new Sequence(new List<BTNode>
                    {
                        new ConditionNode(() => ai.IsInAttackRange()),
                        new ActionNode(() => ai.Attack())
                    }),
                    new ActionNode(() => ai.ChasePlayer())
                })
            }),
            // Patrol fallback
            new ActionNode(() => ai.Patrol())
        });
    }
}
```

## 4.4 Système de Perception

```csharp
public class VisionSensor : MonoBehaviour
{
    [SerializeField] private float _visionRange = 10f;
    [SerializeField] private float _visionAngle = 90f;
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private LayerMask _obstructionLayer;

    public bool CanSeeTarget(GameObject target)
    {
        Vector3 direction = (target.transform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, target.transform.position);

        // Check distance
        if (distance > _visionRange) return false;

        // Check angle (field of view)
        float angle = Vector3.Angle(transform.forward, direction);
        if (angle > _visionAngle / 2f) return false;

        // Check line of sight
        if (Physics.Raycast(transform.position, direction, distance, _obstructionLayer))
            return false;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _visionRange);

        Vector3 leftBound = Quaternion.Euler(0, -_visionAngle / 2, 0) * transform.forward * _visionRange;
        Vector3 rightBound = Quaternion.Euler(0, _visionAngle / 2, 0) * transform.forward * _visionRange;

        Gizmos.DrawRay(transform.position, leftBound);
        Gizmos.DrawRay(transform.position, rightBound);
    }
}
```

## 4.5 Threat/Aggro System

```csharp
public class ThreatTable
{
    private Dictionary<GameObject, float> _threats = new Dictionary<GameObject, float>();

    public void AddThreat(GameObject source, float amount)
    {
        if (!_threats.ContainsKey(source))
            _threats[source] = 0f;

        _threats[source] += amount;
    }

    public void DecayThreat(float decayRate = 0.1f)
    {
        foreach (var key in _threats.Keys.ToList())
        {
            _threats[key] -= decayRate * Time.deltaTime;
            if (_threats[key] <= 0)
                _threats.Remove(key);
        }
    }

    public GameObject GetHighestThreat()
    {
        if (_threats.Count == 0) return null;
        return _threats.OrderByDescending(kv => kv.Value).First().Key;
    }
}
```

---

# 5. UI/UX

## 5.1 Health Bar avec Smooth Animation

```csharp
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _delayedFillImage; // Damage preview
    [SerializeField] private float _smoothSpeed = 5f;
    [SerializeField] private float _delayTime = 0.5f;

    private float _targetFill;
    private float _delayedTargetFill;
    private float _delayTimer;

    public void SetValue(float current, float max)
    {
        _targetFill = current / max;
        _delayTimer = _delayTime;
    }

    void Update()
    {
        // Immediate bar
        _fillImage.fillAmount = Mathf.Lerp(
            _fillImage.fillAmount,
            _targetFill,
            Time.deltaTime * _smoothSpeed);

        // Delayed bar (shows damage taken)
        _delayTimer -= Time.deltaTime;
        if (_delayTimer <= 0)
        {
            _delayedFillImage.fillAmount = Mathf.Lerp(
                _delayedFillImage.fillAmount,
                _targetFill,
                Time.deltaTime * _smoothSpeed * 0.5f);
        }
    }
}
```

## 5.2 Damage Numbers

```csharp
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private AnimationCurve _alphaCurve;
    [SerializeField] private float _duration = 1f;
    [SerializeField] private float _riseHeight = 1f;

    private Vector3 _startPos;
    private float _timer;

    public void Initialize(int damage, bool isCritical, Vector3 worldPos)
    {
        _text.text = damage.ToString();
        _text.color = isCritical ? Color.yellow : Color.white;
        _text.fontSize = isCritical ? 36 : 24;

        transform.position = worldPos;
        _startPos = worldPos;
        _timer = 0f;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;

        // Movement
        transform.position = _startPos + Vector3.up * (_riseHeight * t);

        // Scale pop
        transform.localScale = Vector3.one * _scaleCurve.Evaluate(t);

        // Fade out
        _text.alpha = _alphaCurve.Evaluate(t);

        if (t >= 1f)
            DamageNumberPool.Instance.Return(this);
    }
}
```

## 5.3 Tooltip System

```csharp
public class UITooltip : MonoBehaviour
{
    public static UITooltip Instance { get; private set; }

    [SerializeField] private RectTransform _tooltipRect;
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(string header, string body, Vector2 position)
    {
        _headerText.text = header;
        _bodyText.text = body;

        // Position avec offset pour rester dans l'écran
        _tooltipRect.position = position;
        AdjustToScreen();

        _canvasGroup.alpha = 1;
    }

    public void Hide() => _canvasGroup.alpha = 0;

    private void AdjustToScreen()
    {
        Vector3[] corners = new Vector3[4];
        _tooltipRect.GetWorldCorners(corners);

        // Ajuster si hors écran
        float rightOverflow = corners[2].x - Screen.width;
        if (rightOverflow > 0)
            _tooltipRect.position -= Vector3.right * (rightOverflow + 10);

        float topOverflow = corners[1].y - Screen.height;
        if (topOverflow > 0)
            _tooltipRect.position -= Vector3.up * (topOverflow + 10);
    }
}
```

## 5.4 Typewriter Effect (Dialogue)

```csharp
public class DialogueTypewriter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private float _charsPerSecond = 30f;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _typeSound;

    private string _fullText;
    private float _charTimer;
    private int _currentChar;
    private bool _isTyping;

    public event Action OnTypeComplete;

    public void StartTyping(string text)
    {
        _fullText = text;
        _text.text = "";
        _currentChar = 0;
        _charTimer = 0;
        _isTyping = true;
    }

    public void SkipToEnd()
    {
        _text.text = _fullText;
        _isTyping = false;
        OnTypeComplete?.Invoke();
    }

    void Update()
    {
        if (!_isTyping) return;

        _charTimer += Time.deltaTime;
        float charInterval = 1f / _charsPerSecond;

        while (_charTimer >= charInterval && _currentChar < _fullText.Length)
        {
            _charTimer -= charInterval;
            _currentChar++;
            _text.text = _fullText.Substring(0, _currentChar);

            // Son de typewriter
            if (_typeSound != null && _currentChar % 2 == 0)
                _audioSource.PlayOneShot(_typeSound);
        }

        if (_currentChar >= _fullText.Length)
        {
            _isTyping = false;
            OnTypeComplete?.Invoke();
        }
    }
}
```

## 5.5 Accessibilité

### Color Blind Support

```csharp
public enum ColorBlindMode { None, Protanopia, Deuteranopia, Tritanopia }

public class AccessibilitySettings : MonoBehaviour
{
    public static ColorBlindMode CurrentMode { get; private set; }

    // Palettes sûres:
    // ✓ Blue + Orange
    // ✓ Blue + Red
    // ✓ Purple + Yellow
    // ✗ Red + Green (éviter!)

    public static Color GetSafeColor(Color original)
    {
        return CurrentMode switch
        {
            ColorBlindMode.Protanopia => AdjustForProtanopia(original),
            ColorBlindMode.Deuteranopia => AdjustForDeuteranopia(original),
            ColorBlindMode.Tritanopia => AdjustForTritanopia(original),
            _ => original
        };
    }
}
```

### WCAG Contrast Guidelines

- **Niveau AA (minimum)**:
  - Texte normal: ratio 4.5:1
  - Grand texte (18pt+): ratio 3:1

- **Niveau AAA (recommandé)**:
  - Texte normal: ratio 7:1
  - Grand texte: ratio 4.5:1

---

# 6. AUDIO ET POLISH

## 6.1 Audio Manager

```csharp
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixerGroup _musicGroup;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private int _sfxPoolSize = 20;

    private Queue<AudioSource> _sfxPool = new Queue<AudioSource>();
    private AudioSource _musicSource;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = _sfxGroup;
            source.playOnAwake = false;
            _sfxPool.Enqueue(source);
        }
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        var source = GetPooledSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f;
        source.Play();
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        var source = GetPooledSource();
        source.clip = clip;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
        StartCoroutine(ReturnToPool(source, clip.length / source.pitch));
    }

    public void PlaySFX3D(AudioClip clip, Vector3 position, float volume = 1f)
    {
        var source = GetPooledSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.clip = clip;
        source.volume = volume;
        source.Play();
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    private AudioSource GetPooledSource()
    {
        return _sfxPool.Count > 0 ? _sfxPool.Dequeue() : CreateNewSource();
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.spatialBlend = 0f;
        _sfxPool.Enqueue(source);
    }
}
```

## 6.2 Footsteps par Surface

```csharp
[CreateAssetMenu(menuName = "Audio/Surface Type")]
public class SurfaceType : ScriptableObject
{
    public string surfaceName;
    public AudioClip[] footstepSounds;
    public AudioClip[] landingSounds;
    public float volumeMultiplier = 1f;
}

public class FootstepController : MonoBehaviour
{
    [SerializeField] private SurfaceType _defaultSurface;
    [SerializeField] private float _walkVolume = 0.5f;
    [SerializeField] private float _runVolume = 0.8f;

    private SurfaceType _currentSurface;

    // Appelé via Animation Event
    public void PlayFootstep(int isRunning)
    {
        if (_currentSurface == null) _currentSurface = _defaultSurface;

        var clips = _currentSurface.footstepSounds;
        if (clips.Length == 0) return;

        var clip = clips[Random.Range(0, clips.Length)];
        float volume = (isRunning == 1 ? _runVolume : _walkVolume) * _currentSurface.volumeMultiplier;

        AudioManager.Instance.PlaySFXRandomPitch(clip);
    }

    void FixedUpdate()
    {
        // Détecter surface
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
        {
            if (hit.collider.TryGetComponent<SurfaceIdentifier>(out var surface))
                _currentSurface = surface.SurfaceType;
            else
                _currentSurface = _defaultSurface;
        }
    }
}
```

## 6.3 Camera Shake

```csharp
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private AnimationCurve _shakeCurve;

    private Vector3 _originalPosition;
    private Coroutine _shakeCoroutine;

    void Awake()
    {
        Instance = this;
        _originalPosition = transform.localPosition;
    }

    public void Shake(float duration = 0.2f, float magnitude = 0.3f)
    {
        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float strength = _shakeCurve.Evaluate(elapsed / duration) * magnitude;
            transform.localPosition = _originalPosition + Random.insideUnitSphere * strength;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = _originalPosition;
    }
}

// Utilisation
CameraShake.Instance.Shake(0.1f, 0.05f);  // Light shake (footstep)
CameraShake.Instance.Shake(0.15f, 0.2f);  // Medium shake (hit)
CameraShake.Instance.Shake(0.3f, 0.5f);   // Heavy shake (explosion)
```

## 6.4 Hitstop / Freeze Frame

```csharp
public class TimeController : MonoBehaviour
{
    public static TimeController Instance { get; private set; }

    void Awake() => Instance = this;

    public void Hitstop(float duration = 0.05f)
    {
        StartCoroutine(HitstopCoroutine(duration));
    }

    private IEnumerator HitstopCoroutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    public void SlowMotion(float timeScale = 0.3f, float duration = 1f)
    {
        StartCoroutine(SlowMotionCoroutine(timeScale, duration));
    }

    private IEnumerator SlowMotionCoroutine(float timeScale, float duration)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}

// Combat feedback complet
public void OnHitEnemy(bool isCritical)
{
    TimeController.Instance.Hitstop(isCritical ? 0.1f : 0.05f);
    CameraShake.Instance.Shake(isCritical ? 0.2f : 0.1f, isCritical ? 0.3f : 0.15f);
    AudioManager.Instance.PlaySFXRandomPitch(hitSound);
}
```

## 6.5 Dissolve Shader (Death Effect)

```csharp
public class DissolveEffect : MonoBehaviour
{
    [SerializeField] private Material _dissolveMaterial;
    [SerializeField] private float _dissolveSpeed = 2f;

    private static readonly int CutoffHeight = Shader.PropertyToID("_CutoffHeight");

    public void StartDissolve()
    {
        StartCoroutine(DissolveCoroutine());
    }

    private IEnumerator DissolveCoroutine()
    {
        float height = transform.position.y - 1f;
        float targetHeight = transform.position.y + 2f;

        while (height < targetHeight)
        {
            height += _dissolveSpeed * Time.deltaTime;
            _dissolveMaterial.SetFloat(CutoffHeight, height);
            yield return null;
        }

        Destroy(gameObject);
    }
}
```

---

# 7. MULTIJOUEUR ET RÉSEAU

## 7.1 Unity Netcode Basics

```csharp
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    // Variables synchronisées automatiquement
    private NetworkVariable<int> _health = new NetworkVariable<int>(100);
    private NetworkVariable<Vector3> _position = new NetworkVariable<Vector3>();

    public override void OnNetworkSpawn()
    {
        _health.OnValueChanged += OnHealthChanged;

        if (IsOwner)
        {
            // Configuration spécifique au joueur local
        }
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        // Mise à jour UI, effets visuels, etc.
        Debug.Log($"Health changed: {oldValue} -> {newValue}");
    }

    void Update()
    {
        if (!IsOwner) return;

        // Input uniquement pour le propriétaire
        HandleInput();
    }

    private void HandleInput()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        if (input.magnitude > 0.1f)
            MoveServerRpc(input);
    }

    [Rpc(SendTo.Server)]
    private void MoveServerRpc(Vector3 input)
    {
        // Validation côté serveur
        if (input.magnitude > 1.1f)
        {
            Debug.LogWarning("Invalid input detected");
            return;
        }

        // Appliquer mouvement
        transform.position += input * 5f * Time.deltaTime;
        _position.Value = transform.position;
    }

    [Rpc(SendTo.Server)]
    public void TakeDamageServerRpc(int damage)
    {
        _health.Value -= damage;

        if (_health.Value <= 0)
            DieClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DieClientRpc()
    {
        // Effets de mort visibles par tous
        GetComponent<Animator>().SetTrigger("Death");
    }
}
```

## 7.2 Lobby et Relay

```csharp
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyManager : MonoBehaviour
{
    private Lobby _currentLobby;

    async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task<string> CreateLobby(string lobbyName, int maxPlayers)
    {
        try
        {
            // Créer allocation Relay
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Créer lobby avec join code
            var options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            // Configurer transport
            ConfigureRelay(allocation);
            NetworkManager.Singleton.StartHost();

            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    public async Task JoinLobby(string joinCode)
    {
        try
        {
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            ConfigureRelay(allocation);
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join: {e.Message}");
        }
    }
}
```

## 7.3 Client-Side Prediction

```csharp
public class PredictedMovement : NetworkBehaviour
{
    private struct InputState
    {
        public uint Tick;
        public Vector3 Input;
        public Vector3 Position;
    }

    private Queue<InputState> _pendingInputs = new Queue<InputState>();
    private uint _currentTick;

    void Update()
    {
        if (!IsOwner) return;

        Vector3 input = GetInput();

        // Prédiction locale immédiate
        ApplyMovement(input);

        // Envoyer au serveur
        _pendingInputs.Enqueue(new InputState
        {
            Tick = _currentTick,
            Input = input,
            Position = transform.position
        });

        SendInputServerRpc(_currentTick, input);
        _currentTick++;
    }

    [Rpc(SendTo.Server)]
    void SendInputServerRpc(uint tick, Vector3 input)
    {
        // Serveur applique le mouvement
        ApplyMovement(input);

        // Envoyer position autoritaire
        ConfirmPositionClientRpc(tick, transform.position);
    }

    [Rpc(SendTo.Owner)]
    void ConfirmPositionClientRpc(uint tick, Vector3 serverPosition)
    {
        // Retirer inputs confirmés
        while (_pendingInputs.Count > 0 && _pendingInputs.Peek().Tick <= tick)
            _pendingInputs.Dequeue();

        // Réconciliation si désynchronisé
        if (Vector3.Distance(transform.position, serverPosition) > 0.1f)
        {
            transform.position = serverPosition;

            // Rejouer inputs non confirmés
            foreach (var input in _pendingInputs)
                ApplyMovement(input.Input);
        }
    }
}
```

## 7.4 Sécurité Serveur

```csharp
public class SecureServerRpc : NetworkBehaviour
{
    private Dictionary<ulong, RateLimitData> _rateLimits = new Dictionary<ulong, RateLimitData>();

    [Rpc(SendTo.Server)]
    public void AttackServerRpc(ulong targetId, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // Rate limiting
        if (!CheckRateLimit(senderId, "attack", 10, 1f))
        {
            Debug.LogWarning($"Rate limit exceeded for {senderId}");
            return;
        }

        // Validation de distance
        var target = NetworkManager.SpawnManager.SpawnedObjects[targetId];
        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (distance > 5f) // Max attack range
        {
            Debug.LogWarning($"Attack out of range from {senderId}");
            return;
        }

        // Validation de cooldown
        if (!CanAttack())
        {
            Debug.LogWarning($"Cooldown violation from {senderId}");
            return;
        }

        // Action valide - appliquer dégâts
        target.GetComponent<NetworkPlayer>().TakeDamageServerRpc(CalculateDamage());
    }

    private bool CheckRateLimit(ulong clientId, string action, int maxRequests, float window)
    {
        string key = $"{clientId}_{action}";

        if (!_rateLimits.ContainsKey(clientId))
            _rateLimits[clientId] = new RateLimitData();

        var data = _rateLimits[clientId];
        float currentTime = Time.time;

        // Nettoyer vieux timestamps
        while (data.Timestamps.Count > 0 && data.Timestamps.Peek() < currentTime - window)
            data.Timestamps.Dequeue();

        if (data.Timestamps.Count >= maxRequests)
            return false;

        data.Timestamps.Enqueue(currentTime);
        return true;
    }
}
```

---

# 8. TESTING ET DEBUGGING

## 8.1 Unit Testing

### Structure de Test

```csharp
using NUnit.Framework;

[TestFixture]
public class CombatFormulaTests
{
    [Test]
    public void CalculateDamage_WithZeroDefense_ReturnsFullAttack()
    {
        // Arrange
        float attack = 100f;
        float defense = 0f;

        // Act
        float damage = CombatFormulas.CalculateDamage(attack, defense);

        // Assert
        Assert.AreEqual(100f, damage);
    }

    [Test]
    public void CalculateDamage_WithEqualDefense_ReturnsHalfDamage()
    {
        // Arrange
        float attack = 100f;
        float defense = 100f; // K = 100

        // Act
        float damage = CombatFormulas.CalculateDamage(attack, defense);

        // Assert
        Assert.AreEqual(50f, damage, 0.01f);
    }

    [TestCase(100, 0, 100)]
    [TestCase(100, 100, 50)]
    [TestCase(100, 200, 33.33f)]
    [TestCase(50, 50, 25)]
    public void CalculateDamage_Parameterized(float attack, float defense, float expected)
    {
        float damage = CombatFormulas.CalculateDamage(attack, defense);
        Assert.AreEqual(expected, damage, 0.01f);
    }
}
```

### Testing MonoBehaviours (Humble Object Pattern)

```csharp
// Extraire la logique
public class PlayerLogic
{
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }

    public PlayerLogic(float maxHealth)
    {
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        Health = Mathf.Max(0, Health - damage);
    }

    public bool IsDead => Health <= 0;
}

// MonoBehaviour mince
public class PlayerController : MonoBehaviour
{
    private PlayerLogic _logic;

    void Awake() => _logic = new PlayerLogic(100f);

    public void TakeDamage(float damage)
    {
        _logic.TakeDamage(damage);
        UpdateUI();

        if (_logic.IsDead)
            Die();
    }
}

// Test de la logique
[Test]
public void PlayerLogic_TakeDamage_ReducesHealth()
{
    var logic = new PlayerLogic(100f);

    logic.TakeDamage(30f);

    Assert.AreEqual(70f, logic.Health);
}

[Test]
public void PlayerLogic_TakeFatalDamage_IsDead()
{
    var logic = new PlayerLogic(100f);

    logic.TakeDamage(150f);

    Assert.IsTrue(logic.IsDead);
    Assert.AreEqual(0f, logic.Health);
}
```

### Mocking avec NSubstitute

```csharp
using NSubstitute;

public interface IWeaponSystem
{
    int GetDamage();
    void Fire(Vector3 direction);
}

[Test]
public void Player_Attack_UseWeaponDamage()
{
    // Arrange
    var weaponMock = Substitute.For<IWeaponSystem>();
    weaponMock.GetDamage().Returns(50);

    var combat = new CombatSystem(weaponMock);

    // Act
    int damage = combat.CalculateAttackDamage();

    // Assert
    Assert.AreEqual(50, damage);
    weaponMock.Received(1).GetDamage();
}
```

## 8.2 Integration Testing

```csharp
using UnityEngine.TestTools;
using System.Collections;

public class CombatIntegrationTests
{
    [UnityTest]
    public IEnumerator Player_AttacksEnemy_EnemyTakesDamage()
    {
        // Arrange
        var player = CreateTestPlayer();
        var enemy = CreateTestEnemy();
        enemy.transform.position = player.transform.position + Vector3.forward * 2f;

        // Act
        player.Attack();
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.Less(enemy.Health, enemy.MaxHealth);
    }

    [UnityTest]
    public IEnumerator Player_PicksUpItem_InventoryUpdates()
    {
        // Arrange
        var player = CreateTestPlayer();
        var item = CreateTestItem();
        item.transform.position = player.transform.position;

        // Act
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.AreEqual(1, player.Inventory.ItemCount);
    }
}
```

## 8.3 Logging Système

```csharp
public static class GameLogger
{
    // Supprimé complètement en build release
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogDebug(string message)
    {
        Debug.Log($"[DEBUG] {message}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[WARNING] {message}");
    }

    // Toujours actif (erreurs critiques)
    public static void LogError(string message)
    {
        Debug.LogError($"[ERROR] {message}");
    }
}

// Catégories de log
public enum LogCategory { Combat, AI, Inventory, Network, UI }

public static class CategoryLogger
{
    private static HashSet<LogCategory> _enabledCategories = new HashSet<LogCategory>
    {
        LogCategory.Combat,
        LogCategory.AI
    };

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Log(LogCategory category, string message)
    {
        if (_enabledCategories.Contains(category))
            Debug.Log($"[{category}] {message}");
    }
}
```

## 8.4 Visual Debugging (Gizmos)

```csharp
public class DebugVisualizer : MonoBehaviour
{
    [SerializeField] private float _detectionRadius = 10f;
    [SerializeField] private Transform _target;

    // Affiché quand l'objet est sélectionné
    void OnDrawGizmosSelected()
    {
        // Rayon de détection
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        // Ligne vers la cible
        if (_target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }

    // Toujours affiché
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position + Vector3.up, Vector3.one * 0.5f);
    }
}

// Debug.DrawRay pour visualisation runtime
void Update()
{
    if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit))
    {
        Debug.DrawRay(transform.position, transform.forward * hit.distance, Color.green);
        Debug.DrawLine(hit.point, hit.point + hit.normal, Color.yellow);
    }
    else
    {
        Debug.DrawRay(transform.position, transform.forward * 100f, Color.red);
    }
}
```

## 8.5 CI/CD avec GitHub Actions

```yaml
# .github/workflows/unity-ci.yml
name: Unity CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    name: Run Tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          unityVersion: 2022.3.20f1
          testMode: all

      - uses: actions/upload-artifact@v3
        with:
          name: Test results
          path: artifacts

  build:
    name: Build ${{ matrix.targetPlatform }}
    runs-on: ubuntu-latest
    needs: test
    strategy:
      matrix:
        targetPlatform:
          - StandaloneWindows64
          - WebGL
    steps:
      - uses: actions/checkout@v3

      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          targetPlatform: ${{ matrix.targetPlatform }}

      - uses: actions/upload-artifact@v3
        with:
          name: Build-${{ matrix.targetPlatform }}
          path: build
```

---

# 9. CHECKLISTS ET ANTI-PATTERNS

## 9.1 Checklist Avant Commit

### Code Quality

- [ ] Pas d'erreurs de compilation
- [ ] Pas de warnings dans la Console
- [ ] Tous les `GetComponent()` sont cachés dans `Awake/Start`
- [ ] `[SerializeField]` utilisé au lieu de `public` pour les champs Inspector
- [ ] Variables privées préfixées avec `_underscore`
- [ ] Tests unitaires passent
- [ ] Public methods documentées avec `///`
- [ ] Pas de valeurs hardcodées (utiliser constantes)
- [ ] Script < 200 lignes (split si plus grand)

### Performance

- [ ] Pas de `GetComponent()` dans `Update/FixedUpdate`
- [ ] Pas d'allocations dans les hot paths
- [ ] Object pooling pour objets fréquents
- [ ] Layer Collision Matrix configurée
- [ ] LODs configurés pour objets complexes

### Gameplay

- [ ] Hitboxes désactivées par défaut
- [ ] I-frames implémentées pour le joueur
- [ ] Input buffering implémenté
- [ ] Feedback visuel et audio présents

## 9.2 Anti-Patterns à Éviter

### ❌ GetComponent dans Update

```csharp
// MAUVAIS
void Update()
{
    GetComponent<Rigidbody>().AddForce(Vector3.up);
}

// BON
private Rigidbody _rb;
void Awake() => _rb = GetComponent<Rigidbody>();
void Update() => _rb.AddForce(Vector3.up);
```

### ❌ Find dans Update

```csharp
// MAUVAIS
void Update()
{
    var player = GameObject.FindWithTag("Player");
}

// BON
private Transform _player;
void Start() => _player = GameObject.FindWithTag("Player").transform;
```

### ❌ String Concatenation dans Logs

```csharp
// MAUVAIS
Debug.Log("Player HP: " + health + " at " + position);

// BON
Debug.Log($"Player HP: {health} at {position}");
// Ou mieux: utiliser GameLogger avec Conditional
```

### ❌ Public Fields pour Serialization

```csharp
// MAUVAIS
public float moveSpeed = 5f;

// BON
[SerializeField] private float _moveSpeed = 5f;
```

### ❌ Transform Direct sur Physics Objects

```csharp
// MAUVAIS
void FixedUpdate()
{
    transform.position += Vector3.forward * speed;
}

// BON
void FixedUpdate()
{
    _rb.MovePosition(_rb.position + Vector3.forward * speed * Time.fixedDeltaTime);
}
```

### ❌ Damage Formula Soustraction

```csharp
// MAUVAIS - Défense peut annuler tous les dégâts
float damage = attack - defense;

// BON - Pourcentage reduction (toujours des dégâts)
float damage = attack * (100f / (100f + defense));
```

### ❌ Nested if/else pour Combos

```csharp
// MAUVAIS
if (combo == 0) { Attack1(); combo = 1; }
else if (combo == 1) { Attack2(); combo = 2; }
else if (combo == 2) { Attack3(); combo = 0; }

// BON - State Machine
_currentState = _currentState switch
{
    ComboState.Idle => ComboState.Attack1,
    ComboState.Attack1 => ComboState.Attack2,
    ComboState.Attack2 => ComboState.Attack3,
    _ => ComboState.Idle
};
```

## 9.3 Constantes de Référence

```csharp
public static class GameConstants
{
    // Combat
    public const float PLAYER_IFRAMES = 0.5f;
    public const float KNOCKBACK_DURATION = 0.3f;
    public const float COMBO_WINDOW = 0.5f;
    public const float INPUT_BUFFER = 0.1f;

    // Stats
    public const float BASE_CRIT_CHANCE = 0.05f;
    public const float BASE_CRIT_MULTIPLIER = 1.5f;
    public const int HEALTH_PER_VITALITY = 10;
    public const int ATTACK_PER_STRENGTH = 2;

    // Progression
    public const int SKILL_POINTS_PER_LEVEL = 1;
    public const float XP_MULTIPLIER = 1.15f;
    public const int BASE_XP = 100;

    // Inventory
    public const int DEFAULT_INVENTORY_SIZE = 30;
    public const int MAX_STACK_CONSUMABLES = 99;
    public const int MAX_STACK_MATERIALS = 999;
    public const float SELL_PRICE_RATIO = 0.25f;

    // Loot Weights
    public const int WEIGHT_COMMON = 1000;
    public const int WEIGHT_UNCOMMON = 200;
    public const int WEIGHT_RARE = 80;
    public const int WEIGHT_EPIC = 15;
    public const int WEIGHT_LEGENDARY = 5;

    // Audio
    public const float MASTER_VOLUME_DEFAULT = 0.8f;
    public const float MUSIC_VOLUME_DEFAULT = 0.6f;
    public const float SFX_VOLUME_DEFAULT = 0.8f;

    // Physics Layers
    public const int LAYER_PLAYER = 8;
    public const int LAYER_ENEMY = 9;
    public const int LAYER_PROJECTILE = 10;
    public const int LAYER_ENVIRONMENT = 11;
    public const int LAYER_TRIGGER = 12;
    public const int LAYER_PICKUP = 13;
}
```

---

## Sources et Références

### Combat Systems
- Game Developer - Hitboxes and Hurtboxes
- CritPoints - Frame Data Patterns
- Tung's Word Box - Simplest Non-Problematic Damage Formula

### Performance
- Unity Manual - Object Pooling
- Unity Blog - Job System and Burst Compiler
- Game Dev Beginner - Garbage Collection

### Procedural Generation
- Red Blob Games - A* Introduction
- RogueBasin - BSP Dungeon Generation
- RogueBasin - Cellular Automata Caves

### AI Systems
- Game Programming Patterns - State Pattern
- Game Developer - Behavior Trees for AI
- Game AI Pro - F.E.A.R. GOAP

### UI/UX
- Game UI Database
- Unity Learn - UI Toolkit
- Game Dev Beginner - Typewriter Effect

### Audio
- Unity Manual - Audio System
- Feel Documentation - Screen Shakes

### Networking
- Unity Netcode for GameObjects Documentation
- Gaffer On Games - Snapshot Compression

### Testing
- Unity Manual - Test Framework
- NSubstitute Documentation

---

**Document généré le**: 2026-01-30
**Pour le projet**: EpicLegends
**Moteur**: Unity 6.3 LTS
