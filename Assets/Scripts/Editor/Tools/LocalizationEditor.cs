using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Éditeur de localisation avec support multi-langues
    /// Importation/exportation CSV, détection de clés manquantes
    /// </summary>
    public class LocalizationEditor : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Localization Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationEditor>("Localization");
            window.minSize = new Vector2(1000, 600);
        }

        // State
        private Vector2 _scrollPos;
        private Vector2 _entriesScrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "📝 Entries", "🔍 Search", "📊 Statistics", "⚙️ Settings" };

        // Languages
        private List<string> _languages = new List<string> { "en", "fr", "de", "es", "ja", "zh-CN", "ko" };
        private string _baseLanguage = "en";
        private Dictionary<string, bool> _visibleLanguages = new Dictionary<string, bool>();

        // Entries
        private List<LocalizationEntry> _entries = new List<LocalizationEntry>();
        private string _filterText = "";
        private string _filterCategory = "All";
        private bool _showMissingOnly;
        private bool _showLongOnly;
        private int _longThreshold = 100;

        // Editing
        private int _selectedEntryIdx = -1;
        private string _newKey = "";
        private string _newCategory = "UI";

        // Categories
        private List<string> _categories = new List<string> { "UI", "Dialog", "Quest", "Item", "Skill", "Enemy", "System", "Tutorial" };

        // Language display names
        private static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "fr", "Français" },
            { "de", "Deutsch" },
            { "es", "Español" },
            { "ja", "日本語" },
            { "zh-CN", "简体中文" },
            { "zh-TW", "繁體中文" },
            { "ko", "한국어" },
            { "pt-BR", "Português (BR)" },
            { "ru", "Русский" },
            { "it", "Italiano" },
            { "id", "Bahasa Indonesia" },
            { "th", "ไทย" },
            { "vi", "Tiếng Việt" }
        };

        private void OnEnable()
        {
            foreach (var lang in _languages)
            {
                if (!_visibleLanguages.ContainsKey(lang))
                    _visibleLanguages[lang] = true;
            }

            if (_entries.Count == 0)
                CreateSampleEntries();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌐 Localization Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{_entries.Count} entries | {_languages.Count} languages", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, GUILayout.Height(28));

            EditorGUILayout.Space(5);

            switch (_selectedTab)
            {
                case 0: DrawEntriesTab(); break;
                case 1: DrawSearchTab(); break;
                case 2: DrawStatisticsTab(); break;
                case 3: DrawSettingsTab(); break;
            }
        }

        private void DrawEntriesTab()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Filter
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _filterText = EditorGUILayout.TextField(_filterText, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            // Category filter
            var categoryOptions = new List<string> { "All" };
            categoryOptions.AddRange(_categories);
            int catIdx = categoryOptions.IndexOf(_filterCategory);
            catIdx = EditorGUILayout.Popup(catIdx, categoryOptions.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(80));
            _filterCategory = categoryOptions[catIdx];

            _showMissingOnly = GUILayout.Toggle(_showMissingOnly, "Missing Only", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            // Language visibility
            EditorGUILayout.LabelField("Show:", GUILayout.Width(35));
            foreach (var lang in _languages)
            {
                bool visible = _visibleLanguages.GetValueOrDefault(lang, true);
                bool newVisible = GUILayout.Toggle(visible, lang.ToUpper(), EditorStyles.toolbarButton, GUILayout.Width(35));
                _visibleLanguages[lang] = newVisible;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("+ Add Entry", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ShowAddEntryPopup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Cat", EditorStyles.boldLabel, GUILayout.Width(60));

            foreach (var lang in _languages)
            {
                if (!_visibleLanguages.GetValueOrDefault(lang, true)) continue;
                EditorGUILayout.LabelField(lang.ToUpper(), EditorStyles.boldLabel, GUILayout.Width(150));
            }

            EditorGUILayout.LabelField("", GUILayout.Width(50)); // Actions
            EditorGUILayout.EndHorizontal();

            // Entries
            _entriesScrollPos = EditorGUILayout.BeginScrollView(_entriesScrollPos);

            var filteredEntries = GetFilteredEntries();

            for (int i = 0; i < filteredEntries.Count; i++)
            {
                var entry = filteredEntries[i];
                int realIdx = _entries.IndexOf(entry);
                bool isSelected = realIdx == _selectedEntryIdx;

                // Check for missing translations
                bool hasMissing = entry.Translations.Count < _languages.Count ||
                    entry.Translations.Any(t => string.IsNullOrEmpty(t.Value));

                Color bgColor = isSelected ? new Color(0.3f, 0.5f, 0.7f) :
                              hasMissing ? new Color(0.5f, 0.3f, 0.3f) : Color.clear;

                var prevBg = GUI.backgroundColor;
                if (bgColor != Color.clear)
                {
                    GUI.backgroundColor = bgColor;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUI.backgroundColor = prevBg;
                }
                else
                {
                    EditorGUILayout.BeginVertical();
                }

                EditorGUILayout.BeginHorizontal();

                // Key (clickable)
                if (GUILayout.Button(entry.Key, EditorStyles.label, GUILayout.Width(200)))
                    _selectedEntryIdx = realIdx;

                // Category
                EditorGUILayout.LabelField(entry.Category, GUILayout.Width(60));

                // Translations
                foreach (var lang in _languages)
                {
                    if (!_visibleLanguages.GetValueOrDefault(lang, true)) continue;

                    string value = entry.Translations.GetValueOrDefault(lang, "");
                    bool isEmpty = string.IsNullOrEmpty(value);

                    var style = isEmpty ? new GUIStyle(EditorStyles.textField) { normal = { textColor = Color.red } } : EditorStyles.textField;

                    string newValue = EditorGUILayout.TextField(value, style, GUILayout.Width(150));
                    if (newValue != value)
                    {
                        entry.Translations[lang] = newValue;
                    }
                }

                // Actions
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete", $"Delete key '{entry.Key}'?", "Yes", "No"))
                    {
                        _entries.Remove(entry);
                        break;
                    }
                }

                if (GUILayout.Button("⎘", GUILayout.Width(25)))
                {
                    DuplicateEntry(entry);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // Bottom toolbar
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📥 Import CSV", GUILayout.Height(30)))
                ImportCSV();

            if (GUILayout.Button("📤 Export CSV", GUILayout.Height(30)))
                ExportCSV();

            if (GUILayout.Button("📥 Import JSON", GUILayout.Height(30)))
                ImportJSON();

            if (GUILayout.Button("📤 Export JSON", GUILayout.Height(30)))
                ExportJSON();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔄 Auto-Translate Missing", GUILayout.Height(30)))
                AutoTranslateMissing();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchTab()
        {
            EditorGUILayout.LabelField("Advanced Search", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Search options
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search in:", GUILayout.Width(70));
            if (GUILayout.Button("Keys", GUILayout.Width(60))) SearchIn("keys");
            if (GUILayout.Button("Values", GUILayout.Width(60))) SearchIn("values");
            if (GUILayout.Button("All", GUILayout.Width(60))) SearchIn("all");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _filterText = EditorGUILayout.TextField("Search Text", _filterText);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Find & Replace
            EditorGUILayout.LabelField("Find & Replace", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _searchFindText = EditorGUILayout.TextField("Find", _searchFindText);
            _searchReplaceText = EditorGUILayout.TextField("Replace", _searchReplaceText);
            _searchInLanguage = EditorGUILayout.TextField("In Language (empty=all)", _searchInLanguage);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find All"))
            {
                int count = FindAll(_searchFindText);
                EditorUtility.DisplayDialog("Find", $"Found {count} matches", "OK");
            }
            if (GUILayout.Button("Replace All"))
            {
                int count = ReplaceAll(_searchFindText, _searchReplaceText, _searchInLanguage);
                EditorUtility.DisplayDialog("Replace", $"Replaced {count} occurrences", "OK");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Validation
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("🔍 Find Missing Translations"))
            {
                var missing = FindMissingTranslations();
                if (missing.Count == 0)
                    EditorUtility.DisplayDialog("Validation", "All translations complete!", "OK");
                else
                    EditorUtility.DisplayDialog("Validation", $"Found {missing.Count} entries with missing translations", "OK");
            }

            if (GUILayout.Button("🔍 Find Duplicate Keys"))
            {
                var duplicates = FindDuplicateKeys();
                if (duplicates.Count == 0)
                    EditorUtility.DisplayDialog("Validation", "No duplicate keys found!", "OK");
                else
                    EditorUtility.DisplayDialog("Validation", $"Found {duplicates.Count} duplicate keys:\n{string.Join(", ", duplicates)}", "OK");
            }

            if (GUILayout.Button("🔍 Find Unused Keys (in Scripts)"))
            {
                EditorUtility.DisplayDialog("Info", "This would scan all scripts for localization key references", "OK");
            }

            if (GUILayout.Button("📏 Find Long Translations"))
            {
                _showLongOnly = true;
                _selectedTab = 0;
            }

            EditorGUILayout.EndVertical();
        }

        private string _searchFindText = "";
        private string _searchReplaceText = "";
        private string _searchInLanguage = "";

        private void DrawStatisticsTab()
        {
            EditorGUILayout.LabelField("Localization Statistics", EditorStyles.boldLabel);

            // Overview
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Total Entries: {_entries.Count}");
            EditorGUILayout.LabelField($"Languages: {_languages.Count}");
            EditorGUILayout.LabelField($"Total Strings: {_entries.Count * _languages.Count}");

            int totalChars = _entries.Sum(e => e.Translations.Sum(t => t.Value?.Length ?? 0));
            EditorGUILayout.LabelField($"Total Characters: {totalChars:N0}");

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Per language stats
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Per Language", EditorStyles.boldLabel);

            foreach (var lang in _languages)
            {
                int translated = _entries.Count(e => !string.IsNullOrEmpty(e.Translations.GetValueOrDefault(lang, "")));
                float pct = (float)translated / _entries.Count * 100;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{LanguageNames.GetValueOrDefault(lang, lang)}", GUILayout.Width(150));

                // Progress bar
                Rect barRect = EditorGUILayout.GetControlRect(GUILayout.Width(200));
                EditorGUI.ProgressBar(barRect, pct / 100f, $"{translated}/{_entries.Count} ({pct:F1}%)");

                int chars = _entries.Sum(e => e.Translations.GetValueOrDefault(lang, "")?.Length ?? 0);
                EditorGUILayout.LabelField($"{chars:N0} chars", GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Per category stats
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Per Category", EditorStyles.boldLabel);

            foreach (var cat in _categories)
            {
                int count = _entries.Count(e => e.Category == cat);
                if (count == 0) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(cat, GUILayout.Width(100));

                float pct = (float)count / _entries.Count * 100;
                Rect barRect = EditorGUILayout.GetControlRect(GUILayout.Width(200));
                EditorGUI.ProgressBar(barRect, pct / 100f, $"{count} ({pct:F1}%)");

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Longest strings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Longest Strings (may need UI adjustment)", EditorStyles.boldLabel);

            var longest = _entries
                .SelectMany(e => e.Translations.Select(t => new { Entry = e, Lang = t.Key, Length = t.Value?.Length ?? 0 }))
                .OrderByDescending(x => x.Length)
                .Take(5);

            foreach (var item in longest)
            {
                EditorGUILayout.LabelField($"[{item.Lang}] {item.Entry.Key}: {item.Length} chars", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Language Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Base language
            int baseIdx = _languages.IndexOf(_baseLanguage);
            baseIdx = EditorGUILayout.Popup("Base Language", baseIdx, _languages.ToArray());
            if (baseIdx >= 0) _baseLanguage = _languages[baseIdx];

            EditorGUILayout.Space(5);

            // Language list
            EditorGUILayout.LabelField("Active Languages:", EditorStyles.miniBoldLabel);

            for (int i = 0; i < _languages.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _languages[i] = EditorGUILayout.TextField(_languages[i], GUILayout.Width(60));
                EditorGUILayout.LabelField(LanguageNames.GetValueOrDefault(_languages[i], "Unknown"), GUILayout.Width(120));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _languages.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Language"))
            {
                GenericMenu menu = new GenericMenu();
                foreach (var kvp in LanguageNames)
                {
                    if (!_languages.Contains(kvp.Key))
                    {
                        string code = kvp.Key;
                        menu.AddItem(new GUIContent($"{kvp.Value} ({kvp.Key})"), false, () =>
                        {
                            _languages.Add(code);
                            _visibleLanguages[code] = true;
                        });
                    }
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Categories
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _categories.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _categories[i] = EditorGUILayout.TextField(_categories[i]);
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _categories.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Category"))
            {
                _categories.Add("NewCategory");
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Code generation
            EditorGUILayout.LabelField("Code Generation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("Generate C# Keys Class"))
                GenerateKeysClass();

            if (GUILayout.Button("Generate Localization Manager"))
                GenerateLocalizationManager();

            EditorGUILayout.EndVertical();
        }

        private List<LocalizationEntry> GetFilteredEntries()
        {
            return _entries.Where(e =>
            {
                // Text filter
                if (!string.IsNullOrEmpty(_filterText))
                {
                    bool matchKey = e.Key.ToLower().Contains(_filterText.ToLower());
                    bool matchValue = e.Translations.Any(t =>
                        t.Value != null && t.Value.ToLower().Contains(_filterText.ToLower()));

                    if (!matchKey && !matchValue) return false;
                }

                // Category filter
                if (_filterCategory != "All" && e.Category != _filterCategory)
                    return false;

                // Missing only
                if (_showMissingOnly)
                {
                    bool hasMissing = _languages.Any(lang =>
                        string.IsNullOrEmpty(e.Translations.GetValueOrDefault(lang, "")));
                    if (!hasMissing) return false;
                }

                // Long only
                if (_showLongOnly)
                {
                    bool hasLong = e.Translations.Any(t => (t.Value?.Length ?? 0) > _longThreshold);
                    if (!hasLong) return false;
                }

                return true;
            }).ToList();
        }

        private void ShowAddEntryPopup()
        {
            var popup = new AddEntryPopup(this);
            PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), popup);
        }

        public void AddEntry(string key, string category)
        {
            if (_entries.Any(e => e.Key == key))
            {
                EditorUtility.DisplayDialog("Error", $"Key '{key}' already exists", "OK");
                return;
            }

            var entry = new LocalizationEntry
            {
                Key = key,
                Category = category,
                Translations = new Dictionary<string, string>()
            };

            foreach (var lang in _languages)
                entry.Translations[lang] = "";

            _entries.Add(entry);
            _selectedEntryIdx = _entries.Count - 1;
        }

        private void DuplicateEntry(LocalizationEntry source)
        {
            var dup = new LocalizationEntry
            {
                Key = source.Key + "_copy",
                Category = source.Category,
                Translations = new Dictionary<string, string>(source.Translations)
            };

            _entries.Add(dup);
            _selectedEntryIdx = _entries.Count - 1;
        }

        private void SearchIn(string mode)
        {
            // Set search mode and focus
            Debug.Log($"Search mode: {mode}");
        }

        private int FindAll(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            return _entries.Sum(e =>
                e.Translations.Count(t => t.Value?.Contains(text) ?? false));
        }

        private int ReplaceAll(string find, string replace, string language)
        {
            if (string.IsNullOrEmpty(find)) return 0;

            int count = 0;
            foreach (var entry in _entries)
            {
                foreach (var lang in _languages)
                {
                    if (!string.IsNullOrEmpty(language) && lang != language) continue;

                    if (entry.Translations.TryGetValue(lang, out string value) && value != null && value.Contains(find))
                    {
                        entry.Translations[lang] = value.Replace(find, replace);
                        count++;
                    }
                }
            }

            return count;
        }

        private List<LocalizationEntry> FindMissingTranslations()
        {
            return _entries.Where(e =>
                _languages.Any(lang => string.IsNullOrEmpty(e.Translations.GetValueOrDefault(lang, "")))).ToList();
        }

        private List<string> FindDuplicateKeys()
        {
            return _entries.GroupBy(e => e.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 2) return;

                // Parse header
                string[] headers = ParseCSVLine(lines[0]);
                var langIndices = new Dictionary<int, string>();

                for (int i = 2; i < headers.Length; i++)
                {
                    string lang = headers[i].Trim();
                    if (!_languages.Contains(lang))
                        _languages.Add(lang);
                    langIndices[i] = lang;
                }

                // Parse entries
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] cols = ParseCSVLine(lines[i]);
                    if (cols.Length < 2) continue;

                    string key = cols[0].Trim();
                    string category = cols[1].Trim();

                    var entry = _entries.FirstOrDefault(e => e.Key == key);
                    if (entry == null)
                    {
                        entry = new LocalizationEntry
                        {
                            Key = key,
                            Category = category,
                            Translations = new Dictionary<string, string>()
                        };
                        _entries.Add(entry);
                    }

                    foreach (var kvp in langIndices)
                    {
                        if (kvp.Key < cols.Length)
                            entry.Translations[kvp.Value] = cols[kvp.Key].Trim();
                    }
                }

                Debug.Log($"Imported {_entries.Count} entries from CSV");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to import: {e.Message}", "OK");
            }
        }

        private string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export CSV", "", "localization", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();

            // Header
            sb.Append("Key,Category");
            foreach (var lang in _languages)
                sb.Append($",{lang}");
            sb.AppendLine();

            // Entries
            foreach (var entry in _entries)
            {
                sb.Append($"\"{entry.Key}\",\"{entry.Category}\"");
                foreach (var lang in _languages)
                {
                    string value = entry.Translations.GetValueOrDefault(lang, "");
                    value = value?.Replace("\"", "\"\"") ?? "";
                    sb.Append($",\"{value}\"");
                }
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"Exported {_entries.Count} entries to {path}");
        }

        private void ImportJSON()
        {
            string path = EditorUtility.OpenFilePanel("Import JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<LocalizationExportData>(json);

                _languages = data.languages;
                _entries = data.entries;

                Debug.Log($"Imported {_entries.Count} entries from JSON");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to import: {e.Message}", "OK");
            }
        }

        private void ExportJSON()
        {
            string path = EditorUtility.SaveFilePanel("Export JSON", "", "localization", "json");
            if (string.IsNullOrEmpty(path)) return;

            var data = new LocalizationExportData
            {
                languages = _languages,
                entries = _entries
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            Debug.Log($"Exported {_entries.Count} entries to {path}");
        }

        private void AutoTranslateMissing()
        {
            EditorUtility.DisplayDialog("Auto-Translate",
                "This would integrate with a translation API (Google Translate, DeepL) to fill missing translations.\n\n" +
                "For now, this is a placeholder. Implement your preferred translation service here.",
                "OK");
        }

        private void GenerateKeysClass()
        {
            string path = EditorUtility.SaveFilePanel("Save Keys Class", "Assets/Scripts", "LocalizationKeys", "cs");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated Localization Keys");
            sb.AppendLine("// Do not edit manually");
            sb.AppendLine();
            sb.AppendLine("namespace EpicLegends.Localization");
            sb.AppendLine("{");
            sb.AppendLine("    public static class LocalizationKeys");
            sb.AppendLine("    {");

            foreach (var category in _categories)
            {
                var catEntries = _entries.Where(e => e.Category == category).ToList();
                if (catEntries.Count == 0) continue;

                sb.AppendLine($"        public static class {category}");
                sb.AppendLine("        {");

                foreach (var entry in catEntries)
                {
                    string fieldName = entry.Key.Replace(".", "_").Replace("-", "_").ToUpper();
                    sb.AppendLine($"            public const string {fieldName} = \"{entry.Key}\";");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"Generated keys class at {path}");
        }

        private void GenerateLocalizationManager()
        {
            string path = EditorUtility.SaveFilePanel("Save Manager", "Assets/Scripts", "LocalizationManager", "cs");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace EpicLegends.Localization");
            sb.AppendLine("{");
            sb.AppendLine("    public class LocalizationManager : MonoBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine("        public static LocalizationManager Instance { get; private set; }");
            sb.AppendLine("        ");
            sb.AppendLine("        [SerializeField] private string _currentLanguage = \"en\";");
            sb.AppendLine("        private Dictionary<string, Dictionary<string, string>> _translations;");
            sb.AppendLine("        ");
            sb.AppendLine("        public string CurrentLanguage => _currentLanguage;");
            sb.AppendLine("        ");
            sb.AppendLine("        public static event System.Action<string> OnLanguageChanged;");
            sb.AppendLine("        ");
            sb.AppendLine("        private void Awake()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Instance != null) { Destroy(gameObject); return; }");
            sb.AppendLine("            Instance = this;");
            sb.AppendLine("            DontDestroyOnLoad(gameObject);");
            sb.AppendLine("            LoadTranslations();");
            sb.AppendLine("        }");
            sb.AppendLine("        ");
            sb.AppendLine("        private void LoadTranslations()");
            sb.AppendLine("        {");
            sb.AppendLine("            _translations = new Dictionary<string, Dictionary<string, string>>();");
            sb.AppendLine("            // Load from Resources or Addressables");
            sb.AppendLine("        }");
            sb.AppendLine("        ");
            sb.AppendLine("        public void SetLanguage(string languageCode)");
            sb.AppendLine("        {");
            sb.AppendLine("            _currentLanguage = languageCode;");
            sb.AppendLine("            OnLanguageChanged?.Invoke(languageCode);");
            sb.AppendLine("        }");
            sb.AppendLine("        ");
            sb.AppendLine("        public string Get(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_translations.TryGetValue(_currentLanguage, out var langDict))");
            sb.AppendLine("            {");
            sb.AppendLine("                if (langDict.TryGetValue(key, out var value))");
            sb.AppendLine("                    return value;");
            sb.AppendLine("            }");
            sb.AppendLine("            return key; // Fallback to key");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"Generated localization manager at {path}");
        }

        private void CreateSampleEntries()
        {
            AddSampleEntry("ui.menu.play", "UI", "Play", "Jouer", "Spielen", "Jugar", "プレイ", "开始游戏", "플레이");
            AddSampleEntry("ui.menu.settings", "UI", "Settings", "Paramètres", "Einstellungen", "Ajustes", "設定", "设置", "설정");
            AddSampleEntry("ui.menu.quit", "UI", "Quit", "Quitter", "Beenden", "Salir", "終了", "退出", "종료");
            AddSampleEntry("dialog.greeting", "Dialog", "Hello, Traveler!", "Bonjour, Voyageur!", "Hallo, Reisender!", "¡Hola, Viajero!", "こんにちは、旅人さん！", "你好，旅行者！", "안녕하세요, 여행자님!");
            AddSampleEntry("quest.main.intro", "Quest", "Begin your adventure", "Commencez votre aventure", "Beginne dein Abenteuer", "Comienza tu aventura", "冒険を始めよう", "开始你的冒险", "모험을 시작하세요");
            AddSampleEntry("item.sword.desc", "Item", "A trusty blade", "Une lame fiable", "Eine zuverlässige Klinge", "Una espada confiable", "信頼できる刃", "一把可靠的剑", "믿을 수 있는 검");
        }

        private void AddSampleEntry(string key, string category, params string[] values)
        {
            var entry = new LocalizationEntry
            {
                Key = key,
                Category = category,
                Translations = new Dictionary<string, string>()
            };

            for (int i = 0; i < Mathf.Min(values.Length, _languages.Count); i++)
            {
                entry.Translations[_languages[i]] = values[i];
            }

            _entries.Add(entry);
        }

        // Data classes
        [Serializable]
        public class LocalizationEntry
        {
            public string Key;
            public string Category;
            public Dictionary<string, string> Translations = new Dictionary<string, string>();
        }

        [Serializable]
        private class LocalizationExportData
        {
            public List<string> languages;
            public List<LocalizationEntry> entries;
        }

        // Add entry popup
        private class AddEntryPopup : PopupWindowContent
        {
            private LocalizationEditor _editor;
            private string _key = "";
            private int _categoryIdx = 0;

            public AddEntryPopup(LocalizationEditor editor)
            {
                _editor = editor;
            }

            public override Vector2 GetWindowSize() => new Vector2(300, 100);

            public override void OnGUI(Rect rect)
            {
                EditorGUILayout.LabelField("Add New Entry", EditorStyles.boldLabel);
                _key = EditorGUILayout.TextField("Key", _key);
                _categoryIdx = EditorGUILayout.Popup("Category", _categoryIdx, _editor._categories.ToArray());

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(_key))
                    {
                        _editor.AddEntry(_key, _editor._categories[_categoryIdx]);
                        editorWindow.Close();
                    }
                }
                if (GUILayout.Button("Cancel"))
                {
                    editorWindow.Close();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
