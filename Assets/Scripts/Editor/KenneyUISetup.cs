using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool to configure Kenney RPG UI assets for EpicLegends HUD.
/// </summary>
public class KenneyUISetup : EditorWindow
{
    private const string KENNEY_RPG_PATH = "Assets/Art/UI/Kenney/RPG-Expansion/PNG/";
    
    [MenuItem("EpicLegends/UI/Setup Kenney UI Assets")]
    public static void SetupKenneyUI()
    {
        // First, configure all texture imports as sprites
        ConfigureTextureImports();
        
        // Then assign sprites to UI elements
        AssignSpritesToUI();
        
        Debug.Log("[KenneyUISetup] UI setup complete!");
    }
    
    [MenuItem("EpicLegends/UI/Configure Texture Imports")]
    public static void ConfigureTextureImports()
    {
        string[] texturePaths = new string[]
        {
            // Bar backgrounds
            "barBack_horizontalLeft.png",
            "barBack_horizontalMid.png",
            "barBack_horizontalRight.png",
            // Red bars (health)
            "barRed_horizontalLeft.png",
            "barRed_horizontalMid.png",
            "barRed_horizontalRight.png",
            // Blue bars (mana)
            "barBlue_horizontalLeft.png",
            "barBlue_horizontalBlue.png",
            "barBlue_horizontalRight.png",
            // Green bars (stamina)
            "barGreen_horizontalLeft.png",
            "barGreen_horizontalMid.png",
            "barGreen_horizontalRight.png",
            // Yellow bars (exp)
            "barYellow_horizontalLeft.png",
            "barYellow_horizontalMid.png",
            "barYellow_horizontalRight.png",
            // Panels
            "panel_beige.png",
            "panel_brown.png",
            "panelInset_beige.png",
            "panelInset_brown.png"
        };
        
        int configuredCount = 0;
        
        foreach (string textureName in texturePaths)
        {
            string fullPath = KENNEY_RPG_PATH + textureName;

            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[KenneyUISetup] Texture importer not found: {fullPath}");
                continue;
            }

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                needsReimport = true;
            }

            // Configure for UI - no compression, point filtering for pixel art
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                configuredCount++;
            }
        }
        
        AssetDatabase.Refresh();
        Debug.Log($"[KenneyUISetup] Configured {configuredCount} texture imports as Sprites.");
    }
    
    [MenuItem("EpicLegends/UI/Assign Sprites to UI")]
    public static void AssignSpritesToUI()
    {
        // Find UI Canvas
        GameObject canvas = GameObject.Find("=== UI CANVAS ===");
        if (canvas == null)
        {
            Debug.LogError("[KenneyUISetup] UI Canvas not found!");
            return;
        }
        
        // Setup Health Bar
        SetupBar(canvas, "HUD/HealthBar", "barBack_horizontalMid.png", "barRed_horizontalMid.png");
        
        // Setup Mana Bar
        SetupBar(canvas, "HUD/ManaBar", "barBack_horizontalMid.png", "barBlue_horizontalBlue.png");
        
        // Setup Stamina Bar
        SetupBar(canvas, "HUD/StaminaBar", "barBack_horizontalMid.png", "barGreen_horizontalMid.png");
        
        // Setup Exp Bar
        SetupBar(canvas, "HUD/ExpBar", "barBack_horizontalMid.png", "barYellow_horizontalMid.png");
        
        // Setup Inventory Panel
        SetupPanel(canvas, "InventoryPanel", "panel_beige.png");
        
        // Setup other panels
        SetupPanel(canvas, "PauseMenu", "panel_brown.png");
        SetupPanel(canvas, "DialoguePanel", "panelInset_beige.png");
        
        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        Debug.Log("[KenneyUISetup] Sprites assigned to UI elements.");
    }
    
    private static void SetupBar(GameObject canvas, string barPath, string backgroundSprite, string fillSprite)
    {
        Transform bar = canvas.transform.Find(barPath);
        if (bar == null)
        {
            Debug.LogWarning($"[KenneyUISetup] Bar not found: {barPath}");
            return;
        }
        
        // Setup Background
        Transform background = bar.Find("Background");
        if (background != null)
        {
            Image bgImage = background.GetComponent<Image>();
            if (bgImage != null)
            {
                Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KENNEY_RPG_PATH + backgroundSprite);
                if (bgSprite != null)
                {
                    bgImage.sprite = bgSprite;
                    bgImage.type = Image.Type.Tiled;
                    bgImage.color = Color.white;
                    Debug.Log($"[KenneyUISetup] Set background sprite for {barPath}");
                }
                else
                {
                    Debug.LogWarning($"[KenneyUISetup] Could not load sprite: {backgroundSprite}");
                }
            }
        }
        
        // Setup Fill
        Transform fill = bar.Find("Fill");
        if (fill != null)
        {
            Image fillImage = fill.GetComponent<Image>();
            if (fillImage != null)
            {
                Sprite fSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KENNEY_RPG_PATH + fillSprite);
                if (fSprite != null)
                {
                    fillImage.sprite = fSprite;
                    fillImage.type = Image.Type.Filled;
                    fillImage.fillMethod = Image.FillMethod.Horizontal;
                    fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    fillImage.color = Color.white;
                    Debug.Log($"[KenneyUISetup] Set fill sprite for {barPath}");
                }
                else
                {
                    Debug.LogWarning($"[KenneyUISetup] Could not load sprite: {fillSprite}");
                }
            }
        }
    }
    
    private static void SetupPanel(GameObject canvas, string panelPath, string panelSprite)
    {
        Transform panel = canvas.transform.Find(panelPath);
        if (panel == null)
        {
            Debug.LogWarning($"[KenneyUISetup] Panel not found: {panelPath}");
            return;
        }
        
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            Sprite pSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KENNEY_RPG_PATH + panelSprite);
            if (pSprite != null)
            {
                panelImage.sprite = pSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white;
                Debug.Log($"[KenneyUISetup] Set panel sprite for {panelPath}");
            }
            else
            {
                Debug.LogWarning($"[KenneyUISetup] Could not load sprite: {panelSprite}");
            }
        }
    }
}
