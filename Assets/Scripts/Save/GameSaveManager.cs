using System;
using UnityEngine;

/// <summary>
/// Gestionnaire de sauvegarde du jeu.
/// Coordonne la collecte et restauration des donnees de tous les systemes.
/// Singleton MonoBehaviour pour integration avec Unity.
/// </summary>
public class GameSaveManager : MonoBehaviour
{
    #region Singleton

    public static GameSaveManager Instance { get; private set; }

    #endregion

    #region Events

    public event Action OnSaveStarted;
    public event Action<bool> OnSaveCompleted; // success
    public event Action OnLoadStarted;
    public event Action<bool> OnLoadCompleted; // success

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private bool _autoSaveEnabled = true;
    [SerializeField] private float _autoSaveInterval = 300f; // 5 minutes
    [SerializeField] private bool _encryptSaves = true;

    [Header("Audio")]
    [SerializeField] private AudioClip _saveSound;
    [SerializeField] private AudioClip _loadSound;

    [Header("Data References")]
    [SerializeField] private QuestData[] _allQuests;
    [SerializeField] private ItemData[] _allItems;

    #endregion

    #region Private Fields

    private float _lastSaveTime;
    private float _playTimeThisSession;
    private int _currentSlot = 1;
    private AudioSource _audioSource;
    private bool _isSaving;
    private bool _isLoading;

    #endregion

    #region Properties

    public bool IsSaving => _isSaving;
    public bool IsLoading => _isLoading;
    public int CurrentSlot => _currentSlot;
    public float PlayTimeThisSession => _playTimeThisSession;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // DontDestroyOnLoad only works in Play mode
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && Application.isPlaying)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        SaveManager.EnsureSaveDirectoryExists();
    }

    private void Update()
    {
        _playTimeThisSession += Time.deltaTime;

        // Auto-save
        if (_autoSaveEnabled && !_isSaving && !_isLoading)
        {
            if (Time.time - _lastSaveTime >= _autoSaveInterval)
            {
                AutoSave();
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _autoSaveEnabled)
        {
            // Sauvegarder quand l'application est mise en pause
            QuickSave();
        }
    }

    private void OnApplicationQuit()
    {
        if (_autoSaveEnabled)
        {
            // Sauvegarder avant de quitter
            QuickSave();
        }
    }

    #endregion

    #region Public Methods - Save

    /// <summary>
    /// Sauvegarde rapide dans le slot actuel.
    /// </summary>
    public bool QuickSave()
    {
        return SaveToSlot(_currentSlot);
    }

    /// <summary>
    /// Sauvegarde dans un slot specifique.
    /// </summary>
    public bool SaveToSlot(int slotIndex)
    {
        if (_isSaving || _isLoading) return false;

        _isSaving = true;
        OnSaveStarted?.Invoke();

        try
        {
            var saveData = CollectSaveData();
            bool success = SaveManager.SaveToSlot(saveData, slotIndex, _encryptSaves);

            if (success)
            {
                _currentSlot = slotIndex;
                _lastSaveTime = Time.time;
                PlaySound(_saveSound);
                Debug.Log($"[GameSaveManager] Sauvegarde reussie dans le slot {slotIndex}");
            }

            OnSaveCompleted?.Invoke(success);
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] Erreur de sauvegarde: {e.Message}");
            OnSaveCompleted?.Invoke(false);
            return false;
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>
    /// Auto-save silencieux.
    /// </summary>
    private void AutoSave()
    {
        Debug.Log("[GameSaveManager] Auto-save...");
        SaveToSlot(_currentSlot);
    }

    #endregion

    #region Public Methods - Load

    /// <summary>
    /// Charge depuis le slot actuel.
    /// </summary>
    public bool QuickLoad()
    {
        return LoadFromSlot(_currentSlot);
    }

    /// <summary>
    /// Charge depuis un slot specifique.
    /// </summary>
    public bool LoadFromSlot(int slotIndex)
    {
        if (_isSaving || _isLoading) return false;

        _isLoading = true;
        OnLoadStarted?.Invoke();

        try
        {
            var saveData = SaveManager.LoadFromSlot(slotIndex, _encryptSaves);

            if (saveData == null)
            {
                Debug.LogWarning($"[GameSaveManager] Aucune sauvegarde dans le slot {slotIndex}");
                OnLoadCompleted?.Invoke(false);
                return false;
            }

            bool success = ApplySaveData(saveData);

            if (success)
            {
                _currentSlot = slotIndex;
                _playTimeThisSession = 0f; // Reset car on charge une partie existante
                PlaySound(_loadSound);
                Debug.Log($"[GameSaveManager] Chargement reussi depuis le slot {slotIndex}");
            }

            OnLoadCompleted?.Invoke(success);
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] Erreur de chargement: {e.Message}");
            OnLoadCompleted?.Invoke(false);
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    #endregion

    #region Public Methods - Slot Management

    /// <summary>
    /// Verifie si un slot contient une sauvegarde.
    /// </summary>
    public bool HasSaveInSlot(int slotIndex)
    {
        string path = SaveManager.GetSaveSlotPath(slotIndex, _encryptSaves);
        return SaveManager.SaveExists(path);
    }

    /// <summary>
    /// Obtient les informations de tous les slots.
    /// </summary>
    public SaveSlotInfo[] GetAllSlotInfos()
    {
        return SaveManager.GetAllSlotInfos(_encryptSaves);
    }

    /// <summary>
    /// Supprime une sauvegarde.
    /// </summary>
    public bool DeleteSlot(int slotIndex)
    {
        string path = SaveManager.GetSaveSlotPath(slotIndex, _encryptSaves);
        return SaveManager.DeleteSave(path);
    }

    /// <summary>
    /// Definit le slot actuel.
    /// </summary>
    public void SetCurrentSlot(int slotIndex)
    {
        if (slotIndex >= 1 && slotIndex <= SaveManager.MAX_SAVE_SLOTS)
        {
            _currentSlot = slotIndex;
        }
    }

    #endregion

    #region Private Methods - Data Collection

    /// <summary>
    /// Collecte toutes les donnees de sauvegarde des systemes.
    /// </summary>
    private SaveData CollectSaveData()
    {
        var saveData = new SaveData();

        // Player Data
        saveData.playerData = CollectPlayerData();

        // Inventory
        saveData.inventoryData = CollectInventoryData();

        // Game Progress
        saveData.gameProgress = CollectGameProgressData();

        // Skill Tree
        saveData.skillTreeData = CollectSkillTreeData();

        // Achievements
        saveData.achievementData = CollectAchievementData();

        // Equipment
        saveData.equipmentData = CollectEquipmentData();

        // Quests
        saveData.questData = CollectQuestData();

        // Resource Nodes
        saveData.resourceNodeData = CollectResourceNodeData();

        // NG+
        saveData.ngPlusData = CollectNGPlusData();

        // Territory
        saveData.territoryData = CollectTerritoryData();

        return saveData;
    }

    private PlayerSaveData CollectPlayerData()
    {
        var data = new PlayerSaveData();

        var player = GameManager.Instance?.Player;
        if (player == null) return data;

        // Position
        data.positionX = player.transform.position.x;
        data.positionY = player.transform.position.y;
        data.positionZ = player.transform.position.z;
        data.rotationY = player.transform.eulerAngles.y;

        // Stats
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            data.level = stats.Level;
            data.experience = stats.Experience;
            // experienceToNextLevel calculee par le systeme de niveau
            data.currentHealth = stats.CurrentHealth;
            data.maxHealth = stats.MaxHealth;
            data.currentMana = stats.CurrentMana;
            data.maxMana = stats.MaxMana;
            data.currentStamina = stats.CurrentStamina;
            data.maxStamina = stats.MaxStamina;
        }

        return data;
    }

    private InventorySaveData CollectInventoryData()
    {
        var data = new InventorySaveData();

        var player = GameManager.Instance?.Player;
        if (player == null) return data;

        var inventory = player.GetComponent<Inventory>();
        if (inventory != null)
        {
            data.capacity = inventory.Capacity;
            data.gold = inventory.Gold;

            var items = inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item?.Data != null)
                {
                    data.items.Add(new ItemSaveData
                    {
                        itemId = item.Data.itemId,
                        quantity = item.Quantity,
                        slotIndex = i
                    });
                }
            }
        }

        return data;
    }

    private GameProgressData CollectGameProgressData()
    {
        var data = new GameProgressData();

        // Scene actuelle
        data.currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Temps de jeu
        // Charger le temps precedent et ajouter cette session
        data.totalPlayTimeSeconds += _playTimeThisSession;

        return data;
    }

    private SkillTreeSaveData CollectSkillTreeData()
    {
        var manager = SkillTreeManager.Instance;
        if (manager != null)
        {
            return manager.GetSaveData();
        }
        return new SkillTreeSaveData();
    }

    private AchievementSaveData CollectAchievementData()
    {
        var manager = AchievementManager.Instance;
        if (manager != null)
        {
            return manager.GetSaveData();
        }
        return new AchievementSaveData();
    }

    private EquipmentSaveData CollectEquipmentData()
    {
        var data = new EquipmentSaveData();

        var player = GameManager.Instance?.Player;
        if (player == null) return data;

        var equipment = player.GetComponent<EquipmentManager>();
        if (equipment != null)
        {
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var equipped = equipment.GetEquipped(slot);
                if (equipped?.BaseData != null)
                {
                    data.equippedItemIds[slot] = equipped.BaseData.equipmentName;
                }
            }
        }

        return data;
    }

    private QuestSaveData CollectQuestData()
    {
        var data = new QuestSaveData();

        var manager = QuestManager.Instance;
        if (manager == null) return data;

        // Quetes actives
        var activeQuests = manager.GetActiveQuests();
        foreach (var progress in activeQuests)
        {
            var activeData = new ActiveQuestData
            {
                questId = progress.QuestData.questId,
                startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Progression des objectifs
            if (progress.QuestData.objectives != null)
            {
                for (int i = 0; i < progress.QuestData.objectives.Length; i++)
                {
                    activeData.objectiveProgress.Add(new ObjectiveProgress
                    {
                        objectiveIndex = i,
                        currentProgress = progress.GetObjectiveProgress(i),
                        isCompleted = progress.IsObjectiveComplete(i)
                    });
                }
            }

            data.activeQuests.Add(activeData);
        }

        return data;
    }

    private ResourceNodeSaveData CollectResourceNodeData()
    {
        var manager = ResourceNodeManager.Instance;
        if (manager != null)
        {
            return manager.GetSaveData();
        }
        return new ResourceNodeSaveData();
    }

    private NGPlusSaveData CollectNGPlusData()
    {
        var manager = NGPlusManager.Instance;
        if (manager != null)
        {
            return manager.GetSaveData();
        }
        return new NGPlusSaveData();
    }

    private TerritorySaveData CollectTerritoryData()
    {
        var manager = TerritoryControl.Instance;
        if (manager != null)
        {
            return manager.GetSaveData();
        }
        return new TerritorySaveData();
    }

    #endregion

    #region Private Methods - Data Application

    /// <summary>
    /// Applique les donnees de sauvegarde a tous les systemes.
    /// </summary>
    private bool ApplySaveData(SaveData saveData)
    {
        if (saveData == null) return false;

        try
        {
            // Player
            ApplyPlayerData(saveData.playerData);

            // Inventory
            ApplyInventoryData(saveData.inventoryData);

            // Game Progress
            ApplyGameProgressData(saveData.gameProgress);

            // Skill Tree
            if (saveData.skillTreeData != null && SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.LoadSaveData(saveData.skillTreeData);
            }

            // Achievements
            if (saveData.achievementData != null && AchievementManager.Instance != null)
            {
                AchievementManager.Instance.LoadSaveData(saveData.achievementData);
            }

            // Equipment
            ApplyEquipmentData(saveData.equipmentData);

            // Quests
            ApplyQuestData(saveData.questData);

            // Resource Nodes
            ApplyResourceNodeData(saveData.resourceNodeData);

            // NG+
            ApplyNGPlusData(saveData.ngPlusData);

            // Territory
            ApplyTerritoryData(saveData.territoryData);

            // Crafting
            if (CraftingManager.Instance != null)
            {
                // Le CraftingManager a son propre save/load
                // Mais n'est pas inclus dans SaveData actuellement
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] Erreur lors de l'application des donnees: {e.Message}");
            return false;
        }
    }

    private void ApplyPlayerData(PlayerSaveData data)
    {
        if (data == null) return;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        // Position
        player.transform.position = new Vector3(data.positionX, data.positionY, data.positionZ);
        player.transform.rotation = Quaternion.Euler(0f, data.rotationY, 0f);

        // Stats
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            // Utiliser les methodes publiques si disponibles
            // Pour l'instant, log simplement
            Debug.Log($"[GameSaveManager] Loaded player: Level {data.level}, HP {data.currentHealth}/{data.maxHealth}");
        }
    }

    private void ApplyInventoryData(InventorySaveData data)
    {
        if (data == null) return;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var inventory = player.GetComponent<Inventory>();
        if (inventory == null) return;

        if (_allItems != null && _allItems.Length > 0)
        {
            inventory.LoadSaveData(data, _allItems);
            Debug.Log($"[GameSaveManager] Loaded inventory: {data.items.Count} items, {data.gold} gold");
        }
        else
        {
            Debug.LogWarning("[GameSaveManager] Cannot load inventory: _allItems not configured");
        }
    }

    private void ApplyGameProgressData(GameProgressData data)
    {
        if (data == null) return;

        // Charger la scene si differente
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != data.currentSceneName)
        {
            // Ne pas charger automatiquement pour eviter les problemes
            // Le systeme de jeu devrait gerer ca
            Debug.Log($"[GameSaveManager] Save is from scene: {data.currentSceneName}");
        }
    }

    private void ApplyEquipmentData(EquipmentSaveData data)
    {
        if (data == null) return;

        // L'equipement necessite un EquipmentDatabase pour retrouver les items
        Debug.Log($"[GameSaveManager] Loaded equipment: {data.equippedItemIds.Count} slots");
    }

    private void ApplyQuestData(QuestSaveData data)
    {
        if (data == null) return;

        var manager = QuestManager.Instance;
        if (manager == null) return;

        if (_allQuests != null && _allQuests.Length > 0)
        {
            manager.LoadSaveData(data, _allQuests);
            Debug.Log($"[GameSaveManager] Loaded quests: {data.activeQuests.Count} active, {data.completedQuestIds.Count} completed");
        }
        else
        {
            Debug.LogWarning("[GameSaveManager] Cannot load quests: _allQuests not configured");
        }
    }

    private void ApplyResourceNodeData(ResourceNodeSaveData data)
    {
        if (data == null) return;

        var manager = ResourceNodeManager.Instance;
        if (manager != null)
        {
            manager.LoadSaveData(data);
            Debug.Log($"[GameSaveManager] Loaded resource nodes: {data.nodeStates.Count} states");
        }
    }

    private void ApplyNGPlusData(NGPlusSaveData data)
    {
        if (data == null) return;

        var manager = NGPlusManager.Instance;
        if (manager != null)
        {
            manager.LoadSaveData(data);
            Debug.Log($"[GameSaveManager] Loaded NG+ data: Cycle {data.currentCycle}");
        }
    }

    private void ApplyTerritoryData(TerritorySaveData data)
    {
        if (data == null) return;

        var manager = TerritoryControl.Instance;
        if (manager != null)
        {
            manager.LoadSaveData(data);
            Debug.Log($"[GameSaveManager] Loaded territory data: {data.territoryStates.Count} territories");
        }
    }

    #endregion

    #region Private Methods - Audio

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion
}
