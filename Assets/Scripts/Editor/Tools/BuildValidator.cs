using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Validateur de build avec vérifications pré-build
    /// Détecte les problèmes courants avant la compilation
    /// </summary>
    public class BuildValidator : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Build Validator")]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildValidator>("Build Validator");
            window.minSize = new Vector2(700, 600);
        }

        // Enums
        private enum ValidationCategory
        {
            Scenes, Assets, Scripts, Settings, Performance, Platform
        }

        private enum IssueSeverity { Info, Warning, Error, Critical }

        // State
        private Vector2 _scrollPos;
        private List<ValidationIssue> _issues = new List<ValidationIssue>();
        private bool _isValidating;
        private float _validationProgress;
        private string _validationStatus;

        // Filter
        private bool _showInfo = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;
        private bool _showCritical = true;
        private ValidationCategory? _filterCategory;

        // Stats
        private int _infoCount;
        private int _warningCount;
        private int _errorCount;
        private int _criticalCount;
        private DateTime _lastValidation;

        // Build settings
        private BuildTarget _targetPlatform = BuildTarget.StandaloneWindows64;
        private bool _developmentBuild;
        private bool _autoConnectProfiler;

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔍 Build Validator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pre-build validation and issue detection", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Build settings
            DrawBuildSettings();

            EditorGUILayout.Space(10);

            // Validation buttons
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_isValidating;
            if (GUILayout.Button("🔍 Run Full Validation", GUILayout.Height(35)))
                RunFullValidation();

            if (GUILayout.Button("⚡ Quick Check", GUILayout.Height(35)))
                RunQuickValidation();

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Progress bar
            if (_isValidating)
            {
                EditorGUILayout.Space(5);
                Rect progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                EditorGUI.ProgressBar(progressRect, _validationProgress, _validationStatus);
            }

            EditorGUILayout.Space(10);

            // Summary
            if (_issues.Count > 0 || _lastValidation != DateTime.MinValue)
            {
                DrawSummary();
            }

            EditorGUILayout.Space(5);

            // Filters
            DrawFilters();

            EditorGUILayout.Space(5);

            // Issues list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawIssuesList();
            EditorGUILayout.EndScrollView();

            // Actions
            EditorGUILayout.Space(10);
            DrawActions();
        }

        private void DrawBuildSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Build Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _targetPlatform = (BuildTarget)EditorGUILayout.EnumPopup("Target Platform", _targetPlatform);

            // Quick platform buttons
            if (GUILayout.Button("Win", GUILayout.Width(40)))
                _targetPlatform = BuildTarget.StandaloneWindows64;
            if (GUILayout.Button("Mac", GUILayout.Width(40)))
                _targetPlatform = BuildTarget.StandaloneOSX;
            if (GUILayout.Button("Web", GUILayout.Width(40)))
                _targetPlatform = BuildTarget.WebGL;
            if (GUILayout.Button("Android", GUILayout.Width(60)))
                _targetPlatform = BuildTarget.Android;
            if (GUILayout.Button("iOS", GUILayout.Width(40)))
                _targetPlatform = BuildTarget.iOS;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _developmentBuild = EditorGUILayout.Toggle("Development Build", _developmentBuild);
            _autoConnectProfiler = EditorGUILayout.Toggle("Auto Connect Profiler", _autoConnectProfiler);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Validation Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Last run: {_lastValidation:HH:mm:ss}", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // Severity counts with colors
            DrawSeverityBadge("Critical", _criticalCount, new Color(0.8f, 0.2f, 0.2f));
            DrawSeverityBadge("Errors", _errorCount, new Color(1f, 0.4f, 0.2f));
            DrawSeverityBadge("Warnings", _warningCount, new Color(1f, 0.8f, 0.2f));
            DrawSeverityBadge("Info", _infoCount, new Color(0.4f, 0.7f, 1f));

            GUILayout.FlexibleSpace();

            // Overall status
            string status;
            Color statusColor;

            if (_criticalCount > 0)
            {
                status = "❌ BLOCKED";
                statusColor = Color.red;
            }
            else if (_errorCount > 0)
            {
                status = "⚠️ HAS ERRORS";
                statusColor = new Color(1f, 0.5f, 0f);
            }
            else if (_warningCount > 0)
            {
                status = "⚡ WARNINGS";
                statusColor = Color.yellow;
            }
            else
            {
                status = "✅ READY";
                statusColor = Color.green;
            }

            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = statusColor } };
            EditorGUILayout.LabelField(status, style, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSeverityBadge(string label, int count, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = count > 0 ? color : Color.gray }
            };
            EditorGUILayout.LabelField($"{label}: {count}", style, GUILayout.Width(80));
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));

            _showCritical = GUILayout.Toggle(_showCritical, "Critical", "Button", GUILayout.Width(60));
            _showErrors = GUILayout.Toggle(_showErrors, "Errors", "Button", GUILayout.Width(60));
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", "Button", GUILayout.Width(70));
            _showInfo = GUILayout.Toggle(_showInfo, "Info", "Button", GUILayout.Width(50));

            GUILayout.Space(20);

            // Category filter
            if (GUILayout.Button(_filterCategory?.ToString() ?? "All Categories", EditorStyles.popup, GUILayout.Width(120)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("All"), _filterCategory == null, () => _filterCategory = null);
                menu.AddSeparator("");
                foreach (ValidationCategory cat in Enum.GetValues(typeof(ValidationCategory)))
                {
                    ValidationCategory c = cat;
                    menu.AddItem(new GUIContent(cat.ToString()), _filterCategory == cat, () => _filterCategory = c);
                }
                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear All", GUILayout.Width(70)))
            {
                _issues.Clear();
                UpdateCounts();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssuesList()
        {
            var filtered = _issues.Where(i =>
                (i.Severity == IssueSeverity.Critical && _showCritical) ||
                (i.Severity == IssueSeverity.Error && _showErrors) ||
                (i.Severity == IssueSeverity.Warning && _showWarnings) ||
                (i.Severity == IssueSeverity.Info && _showInfo)
            ).Where(i =>
                !_filterCategory.HasValue || i.Category == _filterCategory.Value
            ).ToList();

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox(_issues.Count == 0 ?
                    "Run validation to check for issues" :
                    "No issues match current filters",
                    MessageType.Info);
                return;
            }

            foreach (var issue in filtered)
            {
                DrawIssue(issue);
            }
        }

        private void DrawIssue(ValidationIssue issue)
        {
            Color bgColor = issue.Severity switch
            {
                IssueSeverity.Critical => new Color(0.6f, 0.1f, 0.1f),
                IssueSeverity.Error => new Color(0.5f, 0.2f, 0.1f),
                IssueSeverity.Warning => new Color(0.5f, 0.4f, 0.1f),
                IssueSeverity.Info => new Color(0.2f, 0.3f, 0.4f),
                _ => Color.gray
            };

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();

            // Severity icon
            string icon = issue.Severity switch
            {
                IssueSeverity.Critical => "🚫",
                IssueSeverity.Error => "❌",
                IssueSeverity.Warning => "⚠️",
                IssueSeverity.Info => "ℹ️",
                _ => "?"
            };

            EditorGUILayout.LabelField(icon, GUILayout.Width(25));

            // Category
            EditorGUILayout.LabelField($"[{issue.Category}]", EditorStyles.miniLabel, GUILayout.Width(80));

            // Title
            EditorGUILayout.LabelField(issue.Title, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Quick fix button
            if (issue.HasAutoFix && GUILayout.Button("Fix", GUILayout.Width(40)))
            {
                issue.AutoFix?.Invoke();
                _issues.Remove(issue);
                UpdateCounts();
            }

            // Go to button
            if (issue.Asset != null && GUILayout.Button("→", GUILayout.Width(25)))
            {
                Selection.activeObject = issue.Asset;
                EditorGUIUtility.PingObject(issue.Asset);
            }

            EditorGUILayout.EndHorizontal();

            // Description
            EditorGUILayout.LabelField(issue.Description, EditorStyles.wordWrappedMiniLabel);

            // Asset path
            if (!string.IsNullOrEmpty(issue.AssetPath))
            {
                EditorGUILayout.LabelField($"📁 {issue.AssetPath}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📋 Export Report", GUILayout.Height(25)))
                ExportReport();

            if (GUILayout.Button("🔧 Fix All Auto-Fixable", GUILayout.Height(25)))
                FixAllAutoFixable();

            GUILayout.FlexibleSpace();

            GUI.enabled = _criticalCount == 0;
            if (GUILayout.Button("🚀 Build Project", GUILayout.Height(25), GUILayout.Width(120)))
                StartBuild();
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void RunFullValidation()
        {
            _issues.Clear();
            _isValidating = true;
            _validationProgress = 0f;

            EditorApplication.delayCall += () =>
            {
                ValidateScenes();
                _validationProgress = 0.15f;
                _validationStatus = "Validating assets...";
                Repaint();

                EditorApplication.delayCall += () =>
                {
                    ValidateAssets();
                    _validationProgress = 0.35f;
                    _validationStatus = "Validating scripts...";
                    Repaint();

                    EditorApplication.delayCall += () =>
                    {
                        ValidateScripts();
                        _validationProgress = 0.55f;
                        _validationStatus = "Validating settings...";
                        Repaint();

                        EditorApplication.delayCall += () =>
                        {
                            ValidateSettings();
                            _validationProgress = 0.75f;
                            _validationStatus = "Checking performance...";
                            Repaint();

                            EditorApplication.delayCall += () =>
                            {
                                ValidatePerformance();
                                _validationProgress = 0.9f;
                                _validationStatus = "Platform checks...";
                                Repaint();

                                EditorApplication.delayCall += () =>
                                {
                                    ValidatePlatform();
                                    _validationProgress = 1f;
                                    _validationStatus = "Complete!";
                                    _isValidating = false;
                                    _lastValidation = DateTime.Now;
                                    UpdateCounts();
                                    Repaint();
                                };
                            };
                        };
                    };
                };
            };
        }

        private void RunQuickValidation()
        {
            _issues.Clear();
            _isValidating = true;

            ValidateScenes();
            ValidateSettings();
            ValidateScripts();

            _isValidating = false;
            _lastValidation = DateTime.Now;
            UpdateCounts();
        }

        private void ValidateScenes()
        {
            _validationStatus = "Validating scenes...";

            // Check build settings scenes
            var scenes = EditorBuildSettings.scenes;

            if (scenes.Length == 0)
            {
                AddIssue(IssueSeverity.Critical, ValidationCategory.Scenes,
                    "No scenes in build",
                    "Add at least one scene to Build Settings",
                    null, null, null);
            }

            foreach (var scene in scenes)
            {
                if (!scene.enabled) continue;

                if (!File.Exists(scene.path))
                {
                    AddIssue(IssueSeverity.Error, ValidationCategory.Scenes,
                        "Missing scene file",
                        $"Scene '{scene.path}' is in build settings but file doesn't exist",
                        scene.path, null, null);
                }
            }

            // Check for main scene
            if (scenes.Length > 0 && !scenes[0].path.ToLower().Contains("main") && !scenes[0].path.ToLower().Contains("boot"))
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Scenes,
                    "First scene naming",
                    "Consider naming your first scene 'MainMenu', 'Boot', or similar for clarity",
                    scenes[0].path, null, null);
            }
        }

        private void ValidateAssets()
        {
            _validationStatus = "Validating assets...";

            // Find large textures
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            foreach (string guid in textureGuids.Take(100)) // Limit for performance
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null && texture.width > 4096)
                    {
                        AddIssue(IssueSeverity.Warning, ValidationCategory.Assets,
                            "Very large texture",
                            $"Texture is {texture.width}x{texture.height}. Consider reducing size for mobile.",
                            path, texture, null);
                    }

                    // Check compression
                    if (!importer.crunchedCompression && _targetPlatform == BuildTarget.Android)
                    {
                        AddIssue(IssueSeverity.Info, ValidationCategory.Assets,
                            "Texture compression",
                            "Consider enabling Crunch Compression for mobile builds",
                            path, texture, () =>
                            {
                                importer.crunchedCompression = true;
                                importer.SaveAndReimport();
                            });
                    }
                }
            }

            // Find uncompressed audio
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            foreach (string guid in audioGuids.Take(50))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings;
                    if (settings.loadType == AudioClipLoadType.DecompressOnLoad)
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        if (clip != null && clip.length > 10f)
                        {
                            AddIssue(IssueSeverity.Warning, ValidationCategory.Assets,
                                "Large audio decompressed on load",
                                $"'{clip.name}' ({clip.length:F1}s) decompresses on load. Use Streaming for long audio.",
                                path, clip, () =>
                                {
                                    settings.loadType = AudioClipLoadType.Streaming;
                                    importer.defaultSampleSettings = settings;
                                    importer.SaveAndReimport();
                                });
                        }
                    }
                }
            }
        }

        private void ValidateScripts()
        {
            _validationStatus = "Validating scripts...";

            // Check for compilation errors
            if (EditorUtility.scriptCompilationFailed)
            {
                AddIssue(IssueSeverity.Critical, ValidationCategory.Scripts,
                    "Compilation errors",
                    "There are script compilation errors. Fix them before building.",
                    null, null, null);
            }

            // Find scripts with common issues
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" });
            foreach (string guid in scriptGuids.Take(100))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string content = File.ReadAllText(path);

                // Check for Debug.Log in production
                if (!_developmentBuild && content.Contains("Debug.Log"))
                {
                    AddIssue(IssueSeverity.Info, ValidationCategory.Scripts,
                        "Debug.Log in production",
                        "Consider removing Debug.Log calls for release builds",
                        path, AssetDatabase.LoadAssetAtPath<MonoScript>(path), null);
                }

                // Check for TODO comments
                if (content.Contains("// TODO") || content.Contains("//TODO"))
                {
                    AddIssue(IssueSeverity.Info, ValidationCategory.Scripts,
                        "TODO comment found",
                        "There are TODO comments that may need attention",
                        path, AssetDatabase.LoadAssetAtPath<MonoScript>(path), null);
                }

                // Check for GetComponent in Update
                if (content.Contains("void Update") && content.Contains("GetComponent"))
                {
                    AddIssue(IssueSeverity.Warning, ValidationCategory.Scripts,
                        "GetComponent in Update",
                        "Calling GetComponent in Update is expensive. Cache the component.",
                        path, AssetDatabase.LoadAssetAtPath<MonoScript>(path), null);
                }
            }
        }

        private void ValidateSettings()
        {
            _validationStatus = "Validating settings...";

            // Check company/product name
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Settings,
                    "Default company name",
                    "Update the company name in Player Settings",
                    null, null, null);
            }

            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "New Unity Project")
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Settings,
                    "Default product name",
                    "Update the product name in Player Settings",
                    null, null, null);
            }

            // Check bundle identifier
            if (PlayerSettings.applicationIdentifier.Contains("com.Company.ProductName"))
            {
                AddIssue(IssueSeverity.Error, ValidationCategory.Settings,
                    "Default bundle identifier",
                    "Update the bundle identifier before building",
                    null, null, null);
            }

            // Check API compatibility
            if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) == ApiCompatibilityLevel.NET_Standard_2_0)
            {
                AddIssue(IssueSeverity.Info, ValidationCategory.Settings,
                    "API Compatibility",
                    "Using .NET Standard 2.0. Consider .NET 4.x for more features.",
                    null, null, null);
            }

            // Check quality settings
            if (QualitySettings.names.Length < 3)
            {
                AddIssue(IssueSeverity.Info, ValidationCategory.Settings,
                    "Quality settings",
                    "Consider adding multiple quality levels (Low/Medium/High)",
                    null, null, null);
            }
        }

        private void ValidatePerformance()
        {
            _validationStatus = "Checking performance...";

            // Check for too many materials
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            if (matGuids.Length > 500)
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Performance,
                    "Many materials",
                    $"Project has {matGuids.Length} materials. Consider material atlasing.",
                    null, null, null);
            }

            // Check for missing LODs on complex meshes
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh");
            foreach (string guid in meshGuids.Take(50))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (mesh != null && mesh.vertexCount > 50000)
                {
                    AddIssue(IssueSeverity.Warning, ValidationCategory.Performance,
                        "High poly mesh",
                        $"Mesh '{mesh.name}' has {mesh.vertexCount} vertices. Consider LOD.",
                        path, mesh, null);
                }
            }

            // Check physics settings
            if (Physics.defaultSolverIterations > 6)
            {
                AddIssue(IssueSeverity.Info, ValidationCategory.Performance,
                    "High physics iterations",
                    "Physics solver iterations are high. May impact mobile performance.",
                    null, null, null);
            }
        }

        private void ValidatePlatform()
        {
            _validationStatus = "Platform validation...";

            switch (_targetPlatform)
            {
                case BuildTarget.Android:
                    ValidateAndroid();
                    break;
                case BuildTarget.iOS:
                    ValidateiOS();
                    break;
                case BuildTarget.WebGL:
                    ValidateWebGL();
                    break;
            }
        }

        private void ValidateAndroid()
        {
            // Check minimum API level
            if ((int)PlayerSettings.Android.minSdkVersion < 24)
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Platform,
                    "Low minimum SDK",
                    "Consider raising minimum SDK to 24 (Android 7.0) for better features",
                    null, null, null);
            }

            // Check Vulkan support
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (!graphics.Contains(UnityEngine.Rendering.GraphicsDeviceType.Vulkan))
            {
                AddIssue(IssueSeverity.Info, ValidationCategory.Platform,
                    "Vulkan not enabled",
                    "Consider enabling Vulkan for better Android performance",
                    null, null, null);
            }

            // Check keystore
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Platform,
                    "No keystore configured",
                    "Configure a keystore for signed APK/AAB builds",
                    null, null, null);
            }
        }

        private void ValidateiOS()
        {
            // Check minimum iOS version
            if (float.TryParse(PlayerSettings.iOS.targetOSVersionString, out float version) && version < 12f)
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Platform,
                    "Low iOS version target",
                    "Consider targeting iOS 12+ for better API support",
                    null, null, null);
            }
        }

        private void ValidateWebGL()
        {
            // Check compression
            if (PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled)
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Platform,
                    "WebGL compression disabled",
                    "Enable compression for smaller builds",
                    null, null, () =>
                    {
                        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                    });
            }

            // Check memory size
            if (PlayerSettings.WebGL.memorySize > 512)
            {
                AddIssue(IssueSeverity.Warning, ValidationCategory.Platform,
                    "High WebGL memory",
                    $"WebGL memory is {PlayerSettings.WebGL.memorySize}MB. Consider reducing for compatibility.",
                    null, null, null);
            }
        }

        private void AddIssue(IssueSeverity severity, ValidationCategory category, string title, string description, string assetPath, UnityEngine.Object asset, Action autoFix)
        {
            _issues.Add(new ValidationIssue
            {
                Severity = severity,
                Category = category,
                Title = title,
                Description = description,
                AssetPath = assetPath,
                Asset = asset,
                AutoFix = autoFix,
                HasAutoFix = autoFix != null
            });
        }

        private void UpdateCounts()
        {
            _infoCount = _issues.Count(i => i.Severity == IssueSeverity.Info);
            _warningCount = _issues.Count(i => i.Severity == IssueSeverity.Warning);
            _errorCount = _issues.Count(i => i.Severity == IssueSeverity.Error);
            _criticalCount = _issues.Count(i => i.Severity == IssueSeverity.Critical);
        }

        private void FixAllAutoFixable()
        {
            var fixable = _issues.Where(i => i.HasAutoFix).ToList();
            foreach (var issue in fixable)
            {
                issue.AutoFix?.Invoke();
                _issues.Remove(issue);
            }
            UpdateCounts();
            Debug.Log($"Fixed {fixable.Count} issues automatically");
        }

        private void ExportReport()
        {
            string path = EditorUtility.SaveFilePanel("Export Validation Report", "", "build_validation_report", "txt");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BUILD VALIDATION REPORT ===");
            sb.AppendLine($"Generated: {DateTime.Now}");
            sb.AppendLine($"Target Platform: {_targetPlatform}");
            sb.AppendLine($"Development Build: {_developmentBuild}");
            sb.AppendLine();
            sb.AppendLine($"Critical: {_criticalCount}");
            sb.AppendLine($"Errors: {_errorCount}");
            sb.AppendLine($"Warnings: {_warningCount}");
            sb.AppendLine($"Info: {_infoCount}");
            sb.AppendLine();
            sb.AppendLine("=== ISSUES ===");

            foreach (var issue in _issues.OrderByDescending(i => i.Severity))
            {
                sb.AppendLine($"[{issue.Severity}] [{issue.Category}] {issue.Title}");
                sb.AppendLine($"  {issue.Description}");
                if (!string.IsNullOrEmpty(issue.AssetPath))
                    sb.AppendLine($"  Path: {issue.AssetPath}");
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"Report exported to {path}");
        }

        private void StartBuild()
        {
            if (_criticalCount > 0)
            {
                EditorUtility.DisplayDialog("Cannot Build",
                    "There are critical issues that must be fixed before building.", "OK");
                return;
            }

            if (_errorCount > 0)
            {
                if (!EditorUtility.DisplayDialog("Build with Errors?",
                    $"There are {_errorCount} errors. Are you sure you want to build?", "Build Anyway", "Cancel"))
                    return;
            }

            string buildPath = EditorUtility.SaveFolderPanel("Select Build Folder", "", "");
            if (string.IsNullOrEmpty(buildPath)) return;

            string extension = _targetPlatform switch
            {
                BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneOSX => ".app",
                BuildTarget.Android => ".apk",
                _ => ""
            };

            string buildFile = Path.Combine(buildPath, PlayerSettings.productName + extension);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = buildFile,
                target = _targetPlatform,
                options = _developmentBuild ? BuildOptions.Development : BuildOptions.None
            };

            if (_autoConnectProfiler)
                options.options |= BuildOptions.ConnectWithProfiler;

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build Succeeded",
                    $"Build completed successfully!\n\nPath: {buildFile}\nSize: {report.summary.totalSize / (1024 * 1024):F2} MB",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Build Failed",
                    $"Build failed with {report.summary.totalErrors} errors.",
                    "OK");
            }
        }

        // Data classes
        private class ValidationIssue
        {
            public IssueSeverity Severity;
            public ValidationCategory Category;
            public string Title;
            public string Description;
            public string AssetPath;
            public UnityEngine.Object Asset;
            public Action AutoFix;
            public bool HasAutoFix;
        }
    }
}
