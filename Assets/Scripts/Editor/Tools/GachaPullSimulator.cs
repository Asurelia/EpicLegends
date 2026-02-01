using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Simulateur de pulls Gacha pour tuner les rates et le pity system.
/// Menu: EpicLegends > Tools > Gacha Pull Simulator
/// </summary>
public class GachaPullSimulator : EditorWindow
{
    #region Banner Settings

    [System.Serializable]
    public class BannerSettings
    {
        public string bannerName = "Character Event Banner";
        public BannerType type = BannerType.Character;

        // Base rates (percentage)
        public float rate5Star = 0.6f;
        public float rate4Star = 5.1f;
        // 3-star is the remainder

        // Featured rates (out of the rarity pool)
        public float featured5StarRate = 50f; // 50% of 5-stars are featured
        public float featured4StarRate = 50f;

        // Pity system
        public int softPityStart = 74;
        public int hardPity = 90;
        public float softPityIncrement = 6f; // +6% per pull after soft pity

        // 4-star guarantee
        public int guarantee4Star = 10;

        // 50/50 system
        public bool has5050 = true;
        public bool lost5050 = false; // Track for simulation

        // Spark/Epitomized Path
        public bool hasEpitomizedPath = false;
        public int epitomizedRequirement = 2;
    }

    public enum BannerType
    {
        Character,
        Weapon,
        Standard,
        Beginner
    }

    #endregion

    #region State

    private BannerSettings _banner = new BannerSettings();
    private int _simulationCount = 10000;
    private Vector2 _scrollPos;
    private Vector2 _resultsScrollPos;

    // Simulation results
    private SimulationResults _results;
    private bool _hasResults = false;
    private bool _isSimulating = false;

    // Presets
    private int _selectedPreset = 0;
    private readonly string[] PRESETS = new string[]
    {
        "Genshin Character",
        "Genshin Weapon",
        "Genshin Standard",
        "HSR Character",
        "HSR Light Cone",
        "Custom"
    };

    // Visualization
    private bool _showDistribution = true;
    private bool _showCostAnalysis = true;
    private float _pricePerPull = 1.60f; // Default price per pull in $

    #endregion

    [System.Serializable]
    private class SimulationResults
    {
        // Basic stats
        public int totalPulls;
        public int total5Stars;
        public int total4Stars;
        public int total3Stars;
        public int featured5Stars;
        public int standard5Stars;

        // Pity analysis
        public List<int> pullsTo5Star = new List<int>();
        public float avg5StarPity;
        public int min5StarPity;
        public int max5StarPity;
        public float median5StarPity;

        // 50/50 stats
        public int wins5050;
        public int losses5050;

        // Cost analysis
        public float avgCostPerFeatured;
        public float minCostPerFeatured;
        public float maxCostPerFeatured;

        // Distribution
        public int[] pityDistribution = new int[91]; // 0-90

        // Extreme cases
        public int worstCaseStreak; // Max pulls without 5-star
        public int bestCaseStreak;

        // C6/R5 analysis
        public int pullsForC0;
        public int pullsForC6;
        public int pullsForR1;
        public int pullsForR5;
    }

    [MenuItem("EpicLegends/Tools/Gacha Pull Simulator")]
    public static void ShowWindow()
    {
        var window = GetWindow<GachaPullSimulator>("Gacha Simulator");
        window.minSize = new Vector2(700, 600);
    }

    private void OnEnable()
    {
        ApplyPreset(0);
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawPresets();
        DrawBannerSettings();
        DrawSimulationSettings();
        DrawActions();

        if (_hasResults)
        {
            DrawResults();
        }

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Gacha Pull Simulator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simulate thousands of gacha pulls to analyze rates, pity systems, and expected costs. " +
            "Use this to balance your game's monetization and ensure fair player experience.",
            MessageType.Info
        );
        EditorGUILayout.Space(10);
    }

    private void DrawPresets()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _selectedPreset = EditorGUILayout.Popup("Banner Preset", _selectedPreset, PRESETS);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyPreset(_selectedPreset);
        }

        if (GUILayout.Button("Apply", GUILayout.Width(60)))
        {
            ApplyPreset(_selectedPreset);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);
    }

    private void DrawBannerSettings()
    {
        EditorGUILayout.LabelField("Banner Configuration", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _banner.bannerName = EditorGUILayout.TextField("Banner Name", _banner.bannerName);
        _banner.type = (BannerType)EditorGUILayout.EnumPopup("Banner Type", _banner.type);

        EditorGUILayout.Space(5);

        // Base rates
        EditorGUILayout.LabelField("Base Rates", EditorStyles.miniBoldLabel);
        _banner.rate5Star = EditorGUILayout.Slider("5-Star Rate (%)", _banner.rate5Star, 0f, 10f);
        _banner.rate4Star = EditorGUILayout.Slider("4-Star Rate (%)", _banner.rate4Star, 0f, 20f);

        float rate3Star = 100f - _banner.rate5Star - _banner.rate4Star;
        EditorGUILayout.LabelField($"3-Star Rate: {rate3Star:F1}%", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        // Featured rates
        EditorGUILayout.LabelField("Featured Rates (within rarity)", EditorStyles.miniBoldLabel);
        _banner.featured5StarRate = EditorGUILayout.Slider("Featured 5-Star (%)", _banner.featured5StarRate, 0f, 100f);
        _banner.featured4StarRate = EditorGUILayout.Slider("Featured 4-Star (%)", _banner.featured4StarRate, 0f, 100f);

        EditorGUILayout.Space(5);

        // Pity
        EditorGUILayout.LabelField("Pity System", EditorStyles.miniBoldLabel);
        _banner.softPityStart = EditorGUILayout.IntSlider("Soft Pity Start", _banner.softPityStart, 1, 100);
        _banner.hardPity = EditorGUILayout.IntSlider("Hard Pity", _banner.hardPity, 1, 100);
        _banner.softPityIncrement = EditorGUILayout.Slider("Soft Pity +% Per Pull", _banner.softPityIncrement, 0f, 20f);
        _banner.guarantee4Star = EditorGUILayout.IntSlider("4-Star Guarantee", _banner.guarantee4Star, 1, 20);

        EditorGUILayout.Space(5);

        // 50/50
        EditorGUILayout.LabelField("50/50 System", EditorStyles.miniBoldLabel);
        _banner.has5050 = EditorGUILayout.Toggle("Enable 50/50", _banner.has5050);

        if (_banner.type == BannerType.Weapon)
        {
            _banner.hasEpitomizedPath = EditorGUILayout.Toggle("Epitomized Path", _banner.hasEpitomizedPath);
            if (_banner.hasEpitomizedPath)
            {
                _banner.epitomizedRequirement = EditorGUILayout.IntSlider("Path Requirement", _banner.epitomizedRequirement, 1, 5);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawSimulationSettings()
    {
        EditorGUILayout.LabelField("Simulation Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _simulationCount = EditorGUILayout.IntField("Simulations", _simulationCount);
        _simulationCount = Mathf.Clamp(_simulationCount, 100, 1000000);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("100")) _simulationCount = 100;
        if (GUILayout.Button("1,000")) _simulationCount = 1000;
        if (GUILayout.Button("10,000")) _simulationCount = 10000;
        if (GUILayout.Button("100,000")) _simulationCount = 100000;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        _showDistribution = EditorGUILayout.Toggle("Show Pity Distribution", _showDistribution);
        _showCostAnalysis = EditorGUILayout.Toggle("Show Cost Analysis", _showCostAnalysis);

        if (_showCostAnalysis)
        {
            _pricePerPull = EditorGUILayout.FloatField("Price per Pull ($)", _pricePerPull);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        EditorGUI.BeginDisabledGroup(_isSimulating);

        if (GUILayout.Button("Run Simulation", GUILayout.Height(35)))
        {
            RunSimulation();
        }

        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Export JSON", GUILayout.Height(35), GUILayout.Width(100)))
        {
            ExportResults();
        }

        if (GUILayout.Button("Reset", GUILayout.Height(35), GUILayout.Width(60)))
        {
            _results = null;
            _hasResults = false;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);
    }

    private void DrawResults()
    {
        EditorGUILayout.LabelField("Simulation Results", EditorStyles.boldLabel);

        _resultsScrollPos = EditorGUILayout.BeginScrollView(_resultsScrollPos, GUILayout.Height(400));

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Summary
        EditorGUILayout.LabelField("Summary", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"Total Simulations: {_simulationCount:N0}");
        EditorGUILayout.LabelField($"Total Pulls Simulated: {_results.totalPulls:N0}");

        EditorGUILayout.Space(5);

        // 5-Star Stats
        DrawSection("5-Star Statistics", () =>
        {
            EditorGUILayout.LabelField($"Total 5-Stars: {_results.total5Stars:N0}");
            EditorGUILayout.LabelField($"Featured 5-Stars: {_results.featured5Stars:N0} ({100f * _results.featured5Stars / Mathf.Max(1, _results.total5Stars):F1}%)");
            EditorGUILayout.LabelField($"Standard 5-Stars: {_results.standard5Stars:N0}");

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField($"Average Pity: {_results.avg5StarPity:F1} pulls");
            EditorGUILayout.LabelField($"Median Pity: {_results.median5StarPity:F1} pulls");
            EditorGUILayout.LabelField($"Min Pity: {_results.min5StarPity} pulls");
            EditorGUILayout.LabelField($"Max Pity: {_results.max5StarPity} pulls");

            if (_banner.has5050)
            {
                EditorGUILayout.Space(3);
                float winRate = 100f * _results.wins5050 / Mathf.Max(1, _results.wins5050 + _results.losses5050);
                EditorGUILayout.LabelField($"50/50 Wins: {_results.wins5050:N0} ({winRate:F1}%)");
                EditorGUILayout.LabelField($"50/50 Losses: {_results.losses5050:N0}");
            }
        });

        // Constellation Analysis
        DrawSection("Constellation/Refinement Analysis", () =>
        {
            EditorGUILayout.LabelField($"Average for C0/R1: {_results.pullsForC0:F0} pulls");
            EditorGUILayout.LabelField($"Average for C6: {_results.pullsForC6:F0} pulls");
            EditorGUILayout.LabelField($"Average for R5: {_results.pullsForR5:F0} pulls");
        });

        // Cost Analysis
        if (_showCostAnalysis)
        {
            DrawSection("Cost Analysis", () =>
            {
                float avgCost = _results.pullsForC0 * _pricePerPull;
                float c6Cost = _results.pullsForC6 * _pricePerPull;
                float r5Cost = _results.pullsForR5 * _pricePerPull;

                EditorGUILayout.LabelField($"Avg Cost for C0: ${avgCost:F2}");
                EditorGUILayout.LabelField($"Avg Cost for C6: ${c6Cost:F2}");
                EditorGUILayout.LabelField($"Avg Cost for R5: ${r5Cost:F2}");

                EditorGUILayout.Space(3);

                EditorGUILayout.LabelField($"Min Cost per Featured: ${_results.minCostPerFeatured:F2}");
                EditorGUILayout.LabelField($"Avg Cost per Featured: ${_results.avgCostPerFeatured:F2}");
                EditorGUILayout.LabelField($"Max Cost per Featured: ${_results.maxCostPerFeatured:F2}");
            });
        }

        // Extremes
        DrawSection("Extreme Cases", () =>
        {
            EditorGUILayout.LabelField($"Worst Streak (max pulls without 5*): {_results.worstCaseStreak}");
            EditorGUILayout.LabelField($"Best Streak (min pulls): {_results.bestCaseStreak}");

            // Probability warnings
            if (_results.worstCaseStreak > _banner.hardPity)
            {
                EditorGUILayout.HelpBox("Warning: Worst case exceeded hard pity! Check your pity logic.", MessageType.Error);
            }
        });

        // Pity Distribution
        if (_showDistribution)
        {
            DrawSection("Pity Distribution", () =>
            {
                DrawPityHistogram();
            });
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawSection(string title, System.Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        content?.Invoke();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawPityHistogram()
    {
        Rect histRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(100));

        // Find max value for scaling
        int maxCount = 1;
        for (int i = 0; i < _results.pityDistribution.Length; i++)
        {
            if (_results.pityDistribution[i] > maxCount)
                maxCount = _results.pityDistribution[i];
        }

        // Draw bars
        float barWidth = histRect.width / 90f;

        for (int i = 1; i <= 90; i++)
        {
            float height = (float)_results.pityDistribution[i] / maxCount * histRect.height;

            Color barColor = Color.green;
            if (i >= _banner.softPityStart)
                barColor = Color.yellow;
            if (i >= _banner.hardPity - 1)
                barColor = Color.red;

            Rect barRect = new Rect(
                histRect.x + (i - 1) * barWidth,
                histRect.yMax - height,
                barWidth - 1,
                height
            );

            EditorGUI.DrawRect(barRect, barColor);
        }

        // Labels
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("1", EditorStyles.miniLabel, GUILayout.Width(20));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"Soft Pity ({_banner.softPityStart})", EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("90", EditorStyles.miniLabel, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Logic

    private void ApplyPreset(int presetIndex)
    {
        switch (presetIndex)
        {
            case 0: // Genshin Character
                _banner = new BannerSettings
                {
                    bannerName = "Character Event Wish",
                    type = BannerType.Character,
                    rate5Star = 0.6f,
                    rate4Star = 5.1f,
                    featured5StarRate = 50f,
                    featured4StarRate = 50f,
                    softPityStart = 74,
                    hardPity = 90,
                    softPityIncrement = 6f,
                    guarantee4Star = 10,
                    has5050 = true
                };
                break;

            case 1: // Genshin Weapon
                _banner = new BannerSettings
                {
                    bannerName = "Weapon Event Wish",
                    type = BannerType.Weapon,
                    rate5Star = 0.7f,
                    rate4Star = 6.0f,
                    featured5StarRate = 75f,
                    featured4StarRate = 75f,
                    softPityStart = 63,
                    hardPity = 77,
                    softPityIncrement = 7f,
                    guarantee4Star = 10,
                    has5050 = true,
                    hasEpitomizedPath = true,
                    epitomizedRequirement = 2
                };
                break;

            case 2: // Genshin Standard
                _banner = new BannerSettings
                {
                    bannerName = "Standard Wish",
                    type = BannerType.Standard,
                    rate5Star = 0.6f,
                    rate4Star = 5.1f,
                    featured5StarRate = 0f,
                    featured4StarRate = 0f,
                    softPityStart = 74,
                    hardPity = 90,
                    softPityIncrement = 6f,
                    guarantee4Star = 10,
                    has5050 = false
                };
                break;

            case 3: // HSR Character
                _banner = new BannerSettings
                {
                    bannerName = "Character Warp",
                    type = BannerType.Character,
                    rate5Star = 0.6f,
                    rate4Star = 5.1f,
                    featured5StarRate = 50f,
                    featured4StarRate = 50f,
                    softPityStart = 74,
                    hardPity = 90,
                    softPityIncrement = 6f,
                    guarantee4Star = 10,
                    has5050 = true
                };
                break;

            case 4: // HSR Light Cone
                _banner = new BannerSettings
                {
                    bannerName = "Light Cone Warp",
                    type = BannerType.Weapon,
                    rate5Star = 0.8f,
                    rate4Star = 6.6f,
                    featured5StarRate = 75f,
                    featured4StarRate = 75f,
                    softPityStart = 65,
                    hardPity = 80,
                    softPityIncrement = 7f,
                    guarantee4Star = 10,
                    has5050 = true,
                    hasEpitomizedPath = false
                };
                break;

            default: // Custom - don't change
                break;
        }
    }

    private void RunSimulation()
    {
        _isSimulating = true;
        _results = new SimulationResults();

        List<int> allPullsToFeatured = new List<int>();
        int totalPulls = 0;
        int total5Stars = 0;
        int featured5Stars = 0;
        int standard5Stars = 0;
        int total4Stars = 0;
        int total3Stars = 0;
        int wins5050 = 0;
        int losses5050 = 0;

        int worstStreak = 0;
        int bestStreak = int.MaxValue;

        // For constellation analysis
        List<int> pullsToGetOne = new List<int>();

        for (int sim = 0; sim < _simulationCount; sim++)
        {
            int pity5 = 0;
            int pity4 = 0;
            bool guaranteedFeatured = false;
            int pullsThisSim = 0;
            int featuredThisSim = 0;

            // Simulate until we get one featured 5-star
            while (featuredThisSim == 0)
            {
                pullsThisSim++;
                pity5++;
                pity4++;
                totalPulls++;

                // Calculate current 5-star rate with soft pity
                float current5Rate = _banner.rate5Star;
                if (pity5 >= _banner.softPityStart)
                {
                    int pullsIntoSoftPity = pity5 - _banner.softPityStart + 1;
                    current5Rate += pullsIntoSoftPity * _banner.softPityIncrement;
                }

                // Hard pity
                if (pity5 >= _banner.hardPity)
                {
                    current5Rate = 100f;
                }

                // Roll for 5-star
                float roll = Random.Range(0f, 100f);

                if (roll < current5Rate)
                {
                    // Got 5-star!
                    total5Stars++;
                    _results.pityDistribution[Mathf.Clamp(pity5, 0, 90)]++;
                    _results.pullsTo5Star.Add(pity5);

                    if (pity5 > worstStreak) worstStreak = pity5;
                    if (pity5 < bestStreak) bestStreak = pity5;

                    // 50/50 check
                    bool gotFeatured = false;
                    if (_banner.has5050)
                    {
                        if (guaranteedFeatured)
                        {
                            gotFeatured = true;
                            guaranteedFeatured = false;
                        }
                        else
                        {
                            if (Random.Range(0f, 100f) < _banner.featured5StarRate)
                            {
                                gotFeatured = true;
                                wins5050++;
                            }
                            else
                            {
                                gotFeatured = false;
                                guaranteedFeatured = true;
                                losses5050++;
                            }
                        }
                    }
                    else
                    {
                        gotFeatured = Random.Range(0f, 100f) < _banner.featured5StarRate;
                    }

                    if (gotFeatured)
                    {
                        featured5Stars++;
                        featuredThisSim++;
                    }
                    else
                    {
                        standard5Stars++;
                    }

                    pity5 = 0;
                }
                else if (pity4 >= _banner.guarantee4Star || roll < current5Rate + _banner.rate4Star)
                {
                    // Got 4-star
                    total4Stars++;
                    pity4 = 0;
                }
                else
                {
                    // Got 3-star
                    total3Stars++;
                }
            }

            pullsToGetOne.Add(pullsThisSim);
        }

        // Calculate stats
        _results.totalPulls = totalPulls;
        _results.total5Stars = total5Stars;
        _results.total4Stars = total4Stars;
        _results.total3Stars = total3Stars;
        _results.featured5Stars = featured5Stars;
        _results.standard5Stars = standard5Stars;
        _results.wins5050 = wins5050;
        _results.losses5050 = losses5050;
        _results.worstCaseStreak = worstStreak;
        _results.bestCaseStreak = bestStreak;

        // Pity stats
        if (_results.pullsTo5Star.Count > 0)
        {
            _results.avg5StarPity = (float)_results.pullsTo5Star.Average();
            _results.min5StarPity = _results.pullsTo5Star.Min();
            _results.max5StarPity = _results.pullsTo5Star.Max();

            var sorted = _results.pullsTo5Star.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            _results.median5StarPity = sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2f
                : sorted[mid];
        }

        // Constellation/cost analysis
        if (pullsToGetOne.Count > 0)
        {
            pullsToGetOne.Sort();

            _results.pullsForC0 = (int)pullsToGetOne.Average();
            _results.pullsForC6 = _results.pullsForC0 * 7; // Rough estimate
            _results.pullsForR1 = _results.pullsForC0;
            _results.pullsForR5 = _results.pullsForC0 * 5;

            _results.minCostPerFeatured = pullsToGetOne.Min() * _pricePerPull;
            _results.avgCostPerFeatured = _results.pullsForC0 * _pricePerPull;
            _results.maxCostPerFeatured = pullsToGetOne.Max() * _pricePerPull;
        }

        _hasResults = true;
        _isSimulating = false;

        Debug.Log($"[GachaPullSimulator] Completed {_simulationCount:N0} simulations");
    }

    private void ExportResults()
    {
        if (!_hasResults)
        {
            EditorUtility.DisplayDialog("No Results", "Run a simulation first before exporting.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel(
            "Export Gacha Simulation",
            "",
            $"gacha_sim_{_banner.bannerName}_{System.DateTime.Now:yyyyMMdd_HHmmss}",
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;

        var export = new
        {
            banner = new
            {
                name = _banner.bannerName,
                type = _banner.type.ToString(),
                rate5Star = _banner.rate5Star,
                rate4Star = _banner.rate4Star,
                softPityStart = _banner.softPityStart,
                hardPity = _banner.hardPity,
                softPityIncrement = _banner.softPityIncrement
            },
            simulation = new
            {
                count = _simulationCount,
                totalPulls = _results.totalPulls
            },
            results = new
            {
                total5Stars = _results.total5Stars,
                featured5Stars = _results.featured5Stars,
                avgPity = _results.avg5StarPity,
                medianPity = _results.median5StarPity,
                minPity = _results.min5StarPity,
                maxPity = _results.max5StarPity,
                wins5050 = _results.wins5050,
                losses5050 = _results.losses5050,
                avgPullsForC0 = _results.pullsForC0,
                avgPullsForC6 = _results.pullsForC6
            },
            cost = new
            {
                pricePerPull = _pricePerPull,
                avgCostC0 = _results.pullsForC0 * _pricePerPull,
                avgCostC6 = _results.pullsForC6 * _pricePerPull
            }
        };

        string json = JsonUtility.ToJson(export, true);
        System.IO.File.WriteAllText(path, json);

        Debug.Log($"[GachaPullSimulator] Exported results to {path}");
        EditorUtility.RevealInFinder(path);
    }

    #endregion
}
