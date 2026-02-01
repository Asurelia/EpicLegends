using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Prévisualiseur d'effets visuels (VFX)
    /// Permet de tester et ajuster les particle systems en temps réel
    /// </summary>
    public class VFXPreviewer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/VFX Previewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<VFXPreviewer>("VFX Previewer");
            window.minSize = new Vector2(800, 600);
        }

        // State
        private Vector2 _scrollPos;
        private GameObject _selectedVFX;
        private List<GameObject> _vfxLibrary = new List<GameObject>();
        private string _searchFilter = "";

        // Preview scene
        private PreviewRenderUtility _preview;
        private GameObject _previewInstance;
        private ParticleSystem[] _particleSystems;
        private Vector2 _previewRotation = new Vector2(15f, -30f);
        private float _previewZoom = 5f;
        private float _previewTime;
        private bool _isPlaying = true;
        private float _timeScale = 1f;

        // Environment
        private enum PreviewEnvironment { None, Grid, Ground, Skybox }
        private PreviewEnvironment _environment = PreviewEnvironment.Grid;
        private Color _backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        private bool _showGround = true;

        // Stats
        private int _totalParticles;
        private float _simulationTime;
        private Dictionary<string, int> _particleCountPerSystem = new Dictionary<string, int>();

        // Recording
        private bool _isRecording;
        private List<Texture2D> _recordedFrames = new List<Texture2D>();
        private float _recordingFPS = 30f;
        private float _recordingDuration = 2f;
        private float _recordingTimer;

        // Comparison
        private bool _showComparison;
        private GameObject _comparisonVFX;
        private GameObject _comparisonInstance;

        private void OnEnable()
        {
            InitializePreview();
            LoadVFXLibrary();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        private void OnEditorUpdate()
        {
            if (_isPlaying && _previewInstance != null)
            {
                _previewTime += Time.deltaTime * _timeScale;
                SimulateParticles(_previewTime);
                Repaint();
            }

            if (_isRecording)
            {
                _recordingTimer += Time.deltaTime;
                if (_recordingTimer >= 1f / _recordingFPS)
                {
                    CaptureFrame();
                    _recordingTimer = 0;
                }

                if (_previewTime >= _recordingDuration)
                {
                    StopRecording();
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("✨ VFX Previewer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Preview and test particle effects in isolation", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Left panel - VFX Library
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawLibraryPanel();
            EditorGUILayout.EndVertical();

            // Center - Preview
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            // Right panel - Controls
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawControlsPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLibraryPanel()
        {
            EditorGUILayout.LabelField("VFX Library", EditorStyles.boldLabel);

            // Search
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);

            EditorGUILayout.Space(5);

            // Drag & drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(dropArea, new Color(0.2f, 0.2f, 0.2f));
            GUI.Label(dropArea, "Drop VFX Prefabs Here", EditorStyles.centeredGreyMiniLabel);

            if (dropArea.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go && go.GetComponentInChildren<ParticleSystem>() != null)
                        {
                            if (!_vfxLibrary.Contains(go))
                                _vfxLibrary.Add(go);
                        }
                    }
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(5);

            // Library list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));

            var filtered = _vfxLibrary.Where(v =>
                string.IsNullOrEmpty(_searchFilter) ||
                v.name.ToLower().Contains(_searchFilter.ToLower())).ToList();

            foreach (var vfx in filtered)
            {
                bool isSelected = vfx == _selectedVFX;

                EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox);

                if (GUILayout.Button(vfx.name, EditorStyles.label))
                {
                    SelectVFX(vfx);
                }

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _vfxLibrary.Remove(vfx);
                    if (_selectedVFX == vfx)
                    {
                        _selectedVFX = null;
                        ClearPreview();
                    }
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Quick actions
            if (GUILayout.Button("📂 Load from Folder"))
                LoadVFXFromFolder();

            if (GUILayout.Button("🔄 Refresh Library"))
                LoadVFXLibrary();
        }

        private void DrawPreviewPanel()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Playback controls
            if (GUILayout.Button(_isPlaying ? "⏸" : "▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
                _isPlaying = !_isPlaying;

            if (GUILayout.Button("⏹", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _isPlaying = false;
                _previewTime = 0;
                if (_previewInstance != null)
                    RestartParticles();
            }

            if (GUILayout.Button("↺", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _previewTime = 0;
                if (_previewInstance != null)
                    RestartParticles();
            }

            EditorGUILayout.LabelField($"Time: {_previewTime:F2}s", GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // Time scale
            EditorGUILayout.LabelField("Speed:", GUILayout.Width(45));
            if (GUILayout.Button("0.25x", EditorStyles.toolbarButton, GUILayout.Width(40))) _timeScale = 0.25f;
            if (GUILayout.Button("0.5x", EditorStyles.toolbarButton, GUILayout.Width(35))) _timeScale = 0.5f;
            if (GUILayout.Button("1x", EditorStyles.toolbarButton, GUILayout.Width(25))) _timeScale = 1f;
            if (GUILayout.Button("2x", EditorStyles.toolbarButton, GUILayout.Width(25))) _timeScale = 2f;

            GUILayout.Space(10);

            // Recording
            GUI.backgroundColor = _isRecording ? Color.red : Color.white;
            if (GUILayout.Button(_isRecording ? "⏺ Recording..." : "⏺ Record", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                if (_isRecording)
                    StopRecording();
                else
                    StartRecording();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Preview area
            Rect previewRect = GUILayoutUtility.GetRect(100, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_preview != null)
            {
                // Handle input
                Event e = Event.current;
                if (previewRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDrag)
                    {
                        if (e.button == 0) // Left drag - rotate
                        {
                            _previewRotation.x += e.delta.y * 0.5f;
                            _previewRotation.y += e.delta.x * 0.5f;
                        }
                        else if (e.button == 1) // Right drag - zoom
                        {
                            _previewZoom += e.delta.y * 0.05f;
                            _previewZoom = Mathf.Clamp(_previewZoom, 1f, 20f);
                        }
                        e.Use();
                        Repaint();
                    }
                    else if (e.type == EventType.ScrollWheel)
                    {
                        _previewZoom += e.delta.y * 0.2f;
                        _previewZoom = Mathf.Clamp(_previewZoom, 1f, 20f);
                        e.Use();
                        Repaint();
                    }
                }

                // Render preview
                _preview.BeginPreview(previewRect, GUIStyle.none);

                // Setup camera
                _preview.camera.transform.position = Quaternion.Euler(_previewRotation.x, _previewRotation.y, 0) * new Vector3(0, 0, -_previewZoom);
                _preview.camera.transform.LookAt(Vector3.zero);
                _preview.camera.backgroundColor = _backgroundColor;
                _preview.camera.clearFlags = CameraClearFlags.SolidColor;

                // Draw grid/ground
                if (_showGround)
                    DrawPreviewGrid();

                _preview.camera.Render();

                var texture = _preview.EndPreview();
                GUI.DrawTexture(previewRect, texture);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, _backgroundColor);
                GUI.Label(previewRect, "Select a VFX to preview", EditorStyles.centeredGreyMiniLabel);
            }

            // Stats bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Particles: {_totalParticles}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Systems: {_particleSystems?.Length ?? 0}", GUILayout.Width(80));
            if (_particleSystems != null && _particleSystems.Length > 0)
            {
                float maxDuration = _particleSystems.Max(ps => ps.main.duration);
                EditorGUILayout.LabelField($"Max Duration: {maxDuration:F2}s", GUILayout.Width(120));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawControlsPanel()
        {
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            // Environment settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Environment", EditorStyles.miniBoldLabel);

            _backgroundColor = EditorGUILayout.ColorField("Background", _backgroundColor);
            _showGround = EditorGUILayout.Toggle("Show Ground", _showGround);
            _environment = (PreviewEnvironment)EditorGUILayout.EnumPopup("Style", _environment);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // VFX Info
            if (_selectedVFX != null && _particleSystems != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("VFX Info", EditorStyles.miniBoldLabel);

                EditorGUILayout.LabelField($"Name: {_selectedVFX.name}");
                EditorGUILayout.LabelField($"Particle Systems: {_particleSystems.Length}");

                EditorGUILayout.Space(5);

                // Per-system breakdown
                foreach (var ps in _particleSystems)
                {
                    if (ps == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(ps.name, GUILayout.Width(120));
                    EditorGUILayout.LabelField($"{ps.particleCount}", GUILayout.Width(50));

                    // Quick controls
                    bool wasEnabled = ps.gameObject.activeSelf;
                    bool isEnabled = EditorGUILayout.Toggle(wasEnabled, GUILayout.Width(20));
                    if (isEnabled != wasEnabled)
                        ps.gameObject.SetActive(isEnabled);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);

                // Quick adjustments
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Quick Adjustments", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("Scale Up (1.5x)"))
                    ScaleVFX(1.5f);
                if (GUILayout.Button("Scale Down (0.75x)"))
                    ScaleVFX(0.75f);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Increase Emission (1.5x)"))
                    AdjustEmission(1.5f);
                if (GUILayout.Button("Decrease Emission (0.75x)"))
                    AdjustEmission(0.75f);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Speed Up (1.5x)"))
                    AdjustSpeed(1.5f);
                if (GUILayout.Button("Slow Down (0.75x)"))
                    AdjustSpeed(0.75f);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Recording settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Recording", EditorStyles.miniBoldLabel);

            _recordingFPS = EditorGUILayout.FloatField("FPS", _recordingFPS);
            _recordingDuration = EditorGUILayout.FloatField("Duration (s)", _recordingDuration);

            if (_recordedFrames.Count > 0)
            {
                EditorGUILayout.LabelField($"Captured: {_recordedFrames.Count} frames");

                if (GUILayout.Button("Export as GIF"))
                    ExportAsGif();

                if (GUILayout.Button("Export as Sprite Sheet"))
                    ExportAsSpriteSheet();

                if (GUILayout.Button("Clear Recording"))
                    ClearRecording();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Comparison mode
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Comparison", EditorStyles.miniBoldLabel);

            _showComparison = EditorGUILayout.Toggle("Enable Split View", _showComparison);

            if (_showComparison)
            {
                _comparisonVFX = (GameObject)EditorGUILayout.ObjectField("Compare With",
                    _comparisonVFX, typeof(GameObject), false);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Actions
            if (_selectedVFX != null)
            {
                if (GUILayout.Button("📍 Select in Project"))
                {
                    Selection.activeObject = _selectedVFX;
                    EditorGUIUtility.PingObject(_selectedVFX);
                }

                if (GUILayout.Button("📋 Duplicate & Edit"))
                {
                    DuplicateVFXForEditing();
                }
            }
        }

        private void InitializePreview()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.farClipPlane = 100f;
            _preview.camera.nearClipPlane = 0.1f;
            _preview.camera.fieldOfView = 30f;
            _preview.camera.backgroundColor = _backgroundColor;

            // Add lights
            _preview.lights[0].transform.rotation = Quaternion.Euler(50, 30, 0);
            _preview.lights[0].intensity = 1f;
        }

        private void CleanupPreview()
        {
            ClearPreview();
            _preview?.Cleanup();
            _preview = null;
            ClearRecording();
        }

        private void ClearPreview()
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
            _particleSystems = null;
            _totalParticles = 0;
        }

        private void SelectVFX(GameObject vfx)
        {
            _selectedVFX = vfx;
            _previewTime = 0;

            ClearPreview();

            if (vfx != null)
            {
                _previewInstance = Instantiate(vfx);
                _previewInstance.hideFlags = HideFlags.HideAndDontSave;

                _particleSystems = _previewInstance.GetComponentsInChildren<ParticleSystem>();

                foreach (var ps in _particleSystems)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main;
                    main.playOnAwake = false;
                    main.simulationSpeed = 1f;
                }

                RestartParticles();
            }
        }

        private void RestartParticles()
        {
            if (_particleSystems == null) return;

            foreach (var ps in _particleSystems)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(true);
                }
            }
        }

        private void SimulateParticles(float time)
        {
            if (_particleSystems == null) return;

            _totalParticles = 0;

            foreach (var ps in _particleSystems)
            {
                if (ps != null && ps.gameObject.activeSelf)
                {
                    ps.Simulate(Time.deltaTime * _timeScale, true, false);
                    _totalParticles += ps.particleCount;
                }
            }
        }

        private void DrawPreviewGrid()
        {
            // Draw a simple grid in the preview
            // This would require custom rendering, simplified here
        }

        private void ScaleVFX(float multiplier)
        {
            if (_previewInstance == null) return;

            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;

                var main = ps.main;
                main.startSizeMultiplier *= multiplier;

                var shape = ps.shape;
                if (shape.enabled)
                {
                    shape.scale *= multiplier;
                }
            }
        }

        private void AdjustEmission(float multiplier)
        {
            if (_previewInstance == null) return;

            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;

                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= multiplier;
            }
        }

        private void AdjustSpeed(float multiplier)
        {
            if (_previewInstance == null) return;

            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;

                var main = ps.main;
                main.simulationSpeed *= multiplier;
            }
        }

        private void StartRecording()
        {
            ClearRecording();
            _isRecording = true;
            _recordingTimer = 0;
            _previewTime = 0;
            RestartParticles();
            _isPlaying = true;
        }

        private void StopRecording()
        {
            _isRecording = false;
            Debug.Log($"Recording stopped: {_recordedFrames.Count} frames captured");
        }

        private void CaptureFrame()
        {
            if (_preview == null) return;

            Rect captureRect = new Rect(0, 0, 512, 512);

            _preview.BeginPreview(captureRect, GUIStyle.none);
            _preview.camera.transform.position = Quaternion.Euler(_previewRotation.x, _previewRotation.y, 0) * new Vector3(0, 0, -_previewZoom);
            _preview.camera.transform.LookAt(Vector3.zero);
            _preview.camera.Render();

            // Capture render texture
            RenderTexture rt = _preview.camera.targetTexture;
            Texture2D frame = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            frame.Apply();
            RenderTexture.active = null;

            _recordedFrames.Add(frame);

            _preview.EndPreview();
        }

        private void ClearRecording()
        {
            foreach (var frame in _recordedFrames)
            {
                if (frame != null)
                    DestroyImmediate(frame);
            }
            _recordedFrames.Clear();
        }

        private void ExportAsGif()
        {
            if (_recordedFrames.Count == 0) return;

            string path = EditorUtility.SaveFilePanel("Export GIF", "", _selectedVFX?.name ?? "vfx", "gif");
            if (string.IsNullOrEmpty(path)) return;

            // Note: Unity doesn't have built-in GIF export
            // This would require a third-party library like NGif
            EditorUtility.DisplayDialog("Export",
                "GIF export requires a third-party library.\n\n" +
                "Consider using:\n" +
                "- NGif (Unity Asset Store)\n" +
                "- FFmpeg (external tool)\n\n" +
                "Sprite sheet export is available as alternative.",
                "OK");
        }

        private void ExportAsSpriteSheet()
        {
            if (_recordedFrames.Count == 0) return;

            string path = EditorUtility.SaveFilePanel("Export Sprite Sheet", "", _selectedVFX?.name ?? "vfx_sheet", "png");
            if (string.IsNullOrEmpty(path)) return;

            int frameWidth = _recordedFrames[0].width;
            int frameHeight = _recordedFrames[0].height;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(_recordedFrames.Count));
            int rows = Mathf.CeilToInt((float)_recordedFrames.Count / columns);

            Texture2D sheet = new Texture2D(frameWidth * columns, frameHeight * rows, TextureFormat.RGBA32, false);

            // Fill with transparent
            Color[] clearColors = new Color[sheet.width * sheet.height];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = Color.clear;
            sheet.SetPixels(clearColors);

            // Copy frames
            for (int i = 0; i < _recordedFrames.Count; i++)
            {
                int col = i % columns;
                int row = rows - 1 - (i / columns); // Flip Y

                sheet.SetPixels(col * frameWidth, row * frameHeight, frameWidth, frameHeight, _recordedFrames[i].GetPixels());
            }

            sheet.Apply();

            byte[] bytes = sheet.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);

            DestroyImmediate(sheet);

            Debug.Log($"Exported sprite sheet: {columns}x{rows} ({_recordedFrames.Count} frames) to {path}");
            EditorUtility.DisplayDialog("Export Complete",
                $"Sprite sheet exported!\n\n" +
                $"Grid: {columns}x{rows}\n" +
                $"Frames: {_recordedFrames.Count}\n" +
                $"Frame Size: {frameWidth}x{frameHeight}",
                "OK");
        }

        private void LoadVFXLibrary()
        {
            _vfxLibrary.Clear();

            // Find all particle system prefabs in project
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

            foreach (string guid in guids.Take(50)) // Limit for performance
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.GetComponentInChildren<ParticleSystem>() != null)
                {
                    _vfxLibrary.Add(prefab);
                }
            }
        }

        private void LoadVFXFromFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select VFX Folder", "Assets", "");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });

            int added = 0;
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab != null && prefab.GetComponentInChildren<ParticleSystem>() != null)
                {
                    if (!_vfxLibrary.Contains(prefab))
                    {
                        _vfxLibrary.Add(prefab);
                        added++;
                    }
                }
            }

            Debug.Log($"Added {added} VFX prefabs from {path}");
        }

        private void DuplicateVFXForEditing()
        {
            if (_selectedVFX == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedVFX);
            string directory = System.IO.Path.GetDirectoryName(sourcePath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string extension = System.IO.Path.GetExtension(sourcePath);

            string newPath = $"{directory}/{fileName}_Edit{extension}";
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            AssetDatabase.CopyAsset(sourcePath, newPath);
            AssetDatabase.Refresh();

            GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
            Selection.activeObject = newPrefab;
            EditorGUIUtility.PingObject(newPrefab);

            Debug.Log($"Created editable copy: {newPath}");
        }
    }
}
