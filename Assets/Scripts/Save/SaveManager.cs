using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Gestionnaire statique pour la sauvegarde et le chargement des donnees.
/// </summary>
public static class SaveManager
{
    /// <summary>
    /// Dossier de sauvegarde par defaut.
    /// </summary>
    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    /// <summary>
    /// Extension des fichiers de sauvegarde.
    /// </summary>
    public const string SAVE_EXTENSION = ".sav";

    /// <summary>
    /// Extension des fichiers JSON (non cryptes).
    /// </summary>
    public const string JSON_EXTENSION = ".json";

    /// <summary>
    /// Cle de cryptage (en production, utiliser une cle plus securisee).
    /// </summary>
    private static readonly byte[] _encryptionKey = Encoding.UTF8.GetBytes("EpicLegends2024!");
    private static readonly byte[] _encryptionIV = Encoding.UTF8.GetBytes("RPGSaveIV123456!");

    /// <summary>
    /// Nombre maximum de slots de sauvegarde.
    /// </summary>
    public const int MAX_SAVE_SLOTS = 3;

    #region Serialization

    /// <summary>
    /// Serialise les donnees de sauvegarde en JSON.
    /// </summary>
    public static string SerializeToJson(SaveData saveData)
    {
        if (saveData == null) return null;
        return JsonUtility.ToJson(saveData, true);
    }

    /// <summary>
    /// Deserialise les donnees de sauvegarde depuis JSON.
    /// </summary>
    public static SaveData DeserializeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur de deserialisation: {e.Message}");
            return null;
        }
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Sauvegarde les donnees dans un fichier.
    /// </summary>
    /// <param name="saveData">Donnees a sauvegarder</param>
    /// <param name="filePath">Chemin du fichier</param>
    /// <param name="encrypt">Crypter le fichier</param>
    public static bool SaveToFile(SaveData saveData, string filePath, bool encrypt = false)
    {
        if (saveData == null || string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[SaveManager] Donnees ou chemin invalide");
            return false;
        }

        try
        {
            // S'assurer que le dossier existe
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Mettre a jour le timestamp
            saveData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string json = SerializeToJson(saveData);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[SaveManager] Echec de la serialisation");
                return false;
            }

            if (encrypt)
            {
                byte[] encryptedData = Encrypt(json);
                File.WriteAllBytes(filePath, encryptedData);
            }
            else
            {
                File.WriteAllText(filePath, json);
            }

            Debug.Log($"[SaveManager] Sauvegarde reussie: {filePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur de sauvegarde: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Charge les donnees depuis un fichier.
    /// </summary>
    /// <param name="filePath">Chemin du fichier</param>
    /// <param name="encrypted">Le fichier est-il crypte</param>
    public static SaveData LoadFromFile(string filePath, bool encrypted = false)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string json;
            if (encrypted)
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);
                json = Decrypt(encryptedData);
            }
            else
            {
                json = File.ReadAllText(filePath);
            }

            var saveData = DeserializeFromJson(json);
            if (saveData != null)
            {
                Debug.Log($"[SaveManager] Chargement reussi: {filePath}");
            }
            return saveData;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur de chargement: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Supprime un fichier de sauvegarde.
    /// </summary>
    public static bool DeleteSave(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            File.Delete(filePath);
            Debug.Log($"[SaveManager] Sauvegarde supprimee: {filePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur de suppression: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verifie si un fichier de sauvegarde existe.
    /// </summary>
    public static bool SaveExists(string filePath)
    {
        return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
    }

    #endregion

    #region Save Slots

    /// <summary>
    /// Obtient le chemin pour un slot de sauvegarde.
    /// </summary>
    public static string GetSaveSlotPath(int slotIndex, bool encrypted = true)
    {
        string extension = encrypted ? SAVE_EXTENSION : JSON_EXTENSION;
        return Path.Combine(SaveDirectory, $"save_slot_{slotIndex}{extension}");
    }

    /// <summary>
    /// Sauvegarde dans un slot specifique.
    /// </summary>
    public static bool SaveToSlot(SaveData saveData, int slotIndex, bool encrypt = true)
    {
        if (slotIndex < 1 || slotIndex > MAX_SAVE_SLOTS)
        {
            Debug.LogError($"[SaveManager] Slot invalide: {slotIndex}");
            return false;
        }

        string path = GetSaveSlotPath(slotIndex, encrypt);
        return SaveToFile(saveData, path, encrypt);
    }

    /// <summary>
    /// Charge depuis un slot specifique.
    /// </summary>
    public static SaveData LoadFromSlot(int slotIndex, bool encrypted = true)
    {
        if (slotIndex < 1 || slotIndex > MAX_SAVE_SLOTS)
        {
            Debug.LogError($"[SaveManager] Slot invalide: {slotIndex}");
            return null;
        }

        string path = GetSaveSlotPath(slotIndex, encrypted);
        return LoadFromFile(path, encrypted);
    }

    /// <summary>
    /// Obtient les infos de tous les slots de sauvegarde.
    /// </summary>
    public static SaveSlotInfo[] GetAllSlotInfos(bool encrypted = true)
    {
        var infos = new SaveSlotInfo[MAX_SAVE_SLOTS];

        for (int i = 0; i < MAX_SAVE_SLOTS; i++)
        {
            int slotIndex = i + 1;
            string path = GetSaveSlotPath(slotIndex, encrypted);

            if (SaveExists(path))
            {
                var saveData = LoadFromFile(path, encrypted);
                if (saveData != null)
                {
                    infos[i] = SaveSlotInfo.FromSaveData(saveData, slotIndex);
                }
                else
                {
                    infos[i] = new SaveSlotInfo { slotIndex = slotIndex };
                }
            }
            else
            {
                infos[i] = new SaveSlotInfo { slotIndex = slotIndex };
            }
        }

        return infos;
    }

    #endregion

    #region Backup

    /// <summary>
    /// Cree une copie de sauvegarde.
    /// </summary>
    public static string CreateBackup(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string directory = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(directory, $"{fileName}.backup_{timestamp}{extension}");

            File.Copy(filePath, backupPath, overwrite: true);
            Debug.Log($"[SaveManager] Backup cree: {backupPath}");
            return backupPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur de backup: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Nettoie les anciennes sauvegardes de backup (garde les N plus recentes).
    /// </summary>
    public static void CleanupOldBackups(int keepCount = 3)
    {
        if (!Directory.Exists(SaveDirectory)) return;

        try
        {
            var backupFiles = Directory.GetFiles(SaveDirectory, "*.backup_*.*");
            if (backupFiles.Length <= keepCount) return;

            // Trier par date de modification (plus recent en premier)
            Array.Sort(backupFiles, (a, b) =>
                File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

            // Supprimer les plus anciens
            for (int i = keepCount; i < backupFiles.Length; i++)
            {
                File.Delete(backupFiles[i]);
                Debug.Log($"[SaveManager] Ancien backup supprime: {backupFiles[i]}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Erreur nettoyage backups: {e.Message}");
        }
    }

    #endregion

    #region Encryption

    /// <summary>
    /// Crypte une chaine en bytes.
    /// </summary>
    private static byte[] Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIV;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Decrypte des bytes en chaine.
    /// </summary>
    private static string Decrypt(byte[] cipherText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIV;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream(cipherText))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// S'assure que le dossier de sauvegarde existe.
    /// </summary>
    public static void EnsureSaveDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    /// <summary>
    /// Obtient la taille totale des sauvegardes en bytes.
    /// </summary>
    public static long GetTotalSaveSize()
    {
        if (!Directory.Exists(SaveDirectory)) return 0;

        long totalSize = 0;
        foreach (var file in Directory.GetFiles(SaveDirectory))
        {
            totalSize += new FileInfo(file).Length;
        }
        return totalSize;
    }

    #endregion
}
