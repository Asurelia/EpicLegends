using UnityEngine;
using UnityEditor;

/// <summary>
/// Guide de demarrage rapide pour EpicLegends.
/// Affiche automatiquement les instructions au premier lancement.
/// </summary>
[InitializeOnLoad]
public class QuickStart : EditorWindow
{
    private static bool _hasShown = false;
    private const string PREF_KEY = "EpicLegends_QuickStart_Shown";

    static QuickStart()
    {
        EditorApplication.delayCall += ShowOnFirstLaunch;
    }

    private static void ShowOnFirstLaunch()
    {
        if (_hasShown) return;
        _hasShown = true;

        // Verifier si deja affiche
        if (EditorPrefs.GetBool(PREF_KEY, false)) return;

        ShowWindow();
    }

    [MenuItem("EpicLegends/Quick Start Guide")]
    public static void ShowWindow()
    {
        var window = GetWindow<QuickStart>("EpicLegends - Quick Start");
        window.minSize = new Vector2(500, 600);
        window.Show();
    }

    private Vector2 _scrollPosition;

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter
        };

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16
        };

        GUIStyle infoStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            padding = new RectOffset(10, 10, 10, 10)
        };

        // Titre
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("🎮 EpicLegends RPG v1.0.0", titleStyle);
        EditorGUILayout.Space(20);

        // Etape 1
        EditorGUILayout.LabelField("📋 Etape 1: Creer les donnees de base", headerStyle);
        EditorGUILayout.HelpBox(
            "Cliquez sur le bouton ci-dessous pour creer automatiquement:\n" +
            "• 5 Armes (Epee, Claymore, Arc, Baton, Lance)\n" +
            "• 5 Items (Potions, Materiaux, Objet de quete)\n" +
            "• 5 Ennemis (Gobelin, Squelette, Loup, Chevalier, Dragon)\n" +
            "• 5 Skills (Frappe, Boule de feu, Eclair, Soin, Bouclier)",
            MessageType.Info);

        if (GUILayout.Button("Creer toutes les donnees de base", GUILayout.Height(30)))
        {
            TestDataCreator.CreateAllTestData();
        }

        EditorGUILayout.Space(15);

        // Etape 2
        EditorGUILayout.LabelField("🎬 Etape 2: Creer la scene de test", headerStyle);
        EditorGUILayout.HelpBox(
            "Cliquez pour creer une scene de gameplay avec:\n" +
            "• Joueur avec tous les composants\n" +
            "• 3 Ennemis de test\n" +
            "• Sol avec NavMesh\n" +
            "• Camera qui suit le joueur\n" +
            "• Obstacles et collectibles",
            MessageType.Info);

        if (GUILayout.Button("Creer la scene de test", GUILayout.Height(30)))
        {
            SceneSetup.CreateTestScene();
        }

        EditorGUILayout.Space(15);

        // Etape 3
        EditorGUILayout.LabelField("🧱 Etape 3: Creer les prefabs", headerStyle);
        EditorGUILayout.HelpBox(
            "Cree les prefabs de base:\n" +
            "• Player prefab\n" +
            "• Enemy prefab\n" +
            "• Projectile prefab\n" +
            "• VFX Impact prefab",
            MessageType.Info);

        if (GUILayout.Button("Creer tous les prefabs", GUILayout.Height(30)))
        {
            SceneSetup.CreateAllPrefabs();
        }

        EditorGUILayout.Space(15);

        // Etape 4
        EditorGUILayout.LabelField("🗺️ Etape 4: Bake le NavMesh", headerStyle);
        EditorGUILayout.HelpBox(
            "Apres avoir cree la scene, le NavMesh doit etre genere pour que les ennemis puissent se deplacer.\n\n" +
            "Alternative: Window > AI > Navigation > Bake",
            MessageType.Info);

        if (GUILayout.Button("Bake NavMesh", GUILayout.Height(30)))
        {
            SceneSetup.BakeNavMesh();
        }

        EditorGUILayout.Space(20);

        // Controles
        EditorGUILayout.LabelField("🎮 Controles (en mode Play)", headerStyle);
        EditorGUILayout.HelpBox(
            "Deplacement: WASD ou fleches\n" +
            "Camera: Souris\n" +
            "Attaque: Clic gauche\n" +
            "Dash: Espace\n" +
            "Sprint: Shift gauche",
            MessageType.None);

        EditorGUILayout.Space(20);

        // Assets gratuits
        EditorGUILayout.LabelField("📦 Assets gratuits recommandes", headerStyle);
        EditorGUILayout.HelpBox(
            "Pour ameliorer les visuels, telecharge ces assets gratuits:\n\n" +
            "1. Synty POLYGON Starter Pack (Unity Asset Store)\n" +
            "2. Mixamo (mixamo.com) - Personnages + Animations\n" +
            "3. Kenney Assets (kenney.nl) - UI, Particules\n" +
            "4. Quaternius (quaternius.com) - Modeles 3D",
            MessageType.None);

        if (GUILayout.Button("Ouvrir Unity Asset Store"))
        {
            Application.OpenURL("https://assetstore.unity.com/packages/essentials/tutorial-projects/polygon-starter-pack-156819");
        }

        if (GUILayout.Button("Ouvrir Mixamo"))
        {
            Application.OpenURL("https://www.mixamo.com/");
        }

        if (GUILayout.Button("Ouvrir Kenney Assets"))
        {
            Application.OpenURL("https://kenney.nl/assets");
        }

        EditorGUILayout.Space(20);

        // Ne plus afficher
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ne plus afficher au demarrage"))
        {
            EditorPrefs.SetBool(PREF_KEY, true);
            Close();
        }
        if (GUILayout.Button("Fermer"))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }
}
