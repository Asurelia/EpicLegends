using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Simulateur d'économie de jeu pour équilibrer les récompenses et dépenses
    /// Modélise le flux de ressources sur le long terme
    /// </summary>
    public class EconomyBalancer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Economy Balancer")]
        public static void ShowWindow()
        {
            var window = GetWindow<EconomyBalancer>("Economy Balancer");
            window.minSize = new Vector2(900, 700);
        }

        // Enums
        private enum CurrencyType { Premium, Soft, Stamina, PullCurrency, Resin }
        private enum TimeUnit { Day, Week, Month, Year }
        private enum PlayerType { F2P, LowSpender, Dolphin, Whale }

        // State
        private Vector2 _scrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "💰 Income Sources", "💸 Spending Sinks", "📊 Simulation", "📈 Analysis", "⚙️ Settings" };

        // Currency settings
        private List<Currency> _currencies = new List<Currency>();
        private List<IncomeSource> _incomeSources = new List<IncomeSource>();
        private List<SpendingSink> _spendingSinks = new List<SpendingSink>();

        // Simulation settings
        private int _simulationDays = 90;
        private PlayerType _playerType = PlayerType.F2P;
        private float _monthlySpending = 0f;
        private bool _includeEvents = true;
        private float _eventFrequency = 0.3f; // % of days with events

        // Simulation results
        private SimulationResult _result;
        private bool _hasSimulated;

        // Presets
        private static readonly Dictionary<string, Action<EconomyBalancer>> Presets = new Dictionary<string, Action<EconomyBalancer>>
        {
            { "Genshin Impact Style", CreateGenshinPreset },
            { "HSR Style", CreateHSRPreset },
            { "Mobile RPG Basic", CreateMobileRPGPreset }
        };

        private void OnEnable()
        {
            if (_currencies.Count == 0)
                InitializeDefaultCurrencies();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💰 Economy Balancer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Model and balance your game's economy", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quick Presets:", GUILayout.Width(100));
            foreach (var preset in Presets)
            {
                if (GUILayout.Button(preset.Key, GUILayout.Height(22)))
                {
                    preset.Value(this);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, GUILayout.Height(30));

            EditorGUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawIncomeSources(); break;
                case 1: DrawSpendingSinks(); break;
                case 2: DrawSimulation(); break;
                case 3: DrawAnalysis(); break;
                case 4: DrawSettings(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIncomeSources()
        {
            EditorGUILayout.LabelField("Income Sources", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Define all ways players earn currencies", MessageType.Info);

            EditorGUILayout.Space(5);

            for (int i = 0; i < _incomeSources.Count; i++)
            {
                var source = _incomeSources[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                source.IsExpanded = EditorGUILayout.Foldout(source.IsExpanded, source.Name, true);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _incomeSources.RemoveAt(i);
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (source.IsExpanded)
                {
                    source.Name = EditorGUILayout.TextField("Name", source.Name);
                    source.Currency = (CurrencyType)EditorGUILayout.EnumPopup("Currency", source.Currency);
                    source.Amount = EditorGUILayout.FloatField("Amount", source.Amount);
                    source.Frequency = (TimeUnit)EditorGUILayout.EnumPopup("Frequency", source.Frequency);
                    source.FrequencyCount = EditorGUILayout.IntField("Times per " + source.Frequency, source.FrequencyCount);
                    source.IsGuaranteed = EditorGUILayout.Toggle("Guaranteed", source.IsGuaranteed);
                    if (!source.IsGuaranteed)
                        source.Probability = EditorGUILayout.Slider("Probability", source.Probability, 0f, 1f);
                    source.RequiresEffort = EditorGUILayout.Toggle("Requires Effort", source.RequiresEffort);
                    if (source.RequiresEffort)
                        source.EffortMinutes = EditorGUILayout.FloatField("Minutes Required", source.EffortMinutes);
                    source.IsLimited = EditorGUILayout.Toggle("Limited/One-time", source.IsLimited);
                    source.Notes = EditorGUILayout.TextField("Notes", source.Notes);

                    // Show daily/monthly value
                    float dailyValue = CalculateDailyIncome(source);
                    EditorGUILayout.LabelField($"→ ~{dailyValue:F1} {source.Currency}/day | ~{dailyValue * 30:F0}/month", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("+ Add Income Source", GUILayout.Height(30)))
            {
                _incomeSources.Add(new IncomeSource { Name = "New Source", FrequencyCount = 1, Probability = 1f, IsExpanded = true });
            }

            // Summary
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Summary - Daily Income", EditorStyles.boldLabel);

            foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
            {
                float daily = _incomeSources
                    .Where(s => s.Currency == currency)
                    .Sum(s => CalculateDailyIncome(s));

                if (daily > 0)
                    EditorGUILayout.LabelField($"{currency}: {daily:F1}/day | {daily * 30:F0}/month | {daily * 365:F0}/year");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpendingSinks()
        {
            EditorGUILayout.LabelField("Spending Sinks", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Define all ways players spend currencies", MessageType.Info);

            EditorGUILayout.Space(5);

            for (int i = 0; i < _spendingSinks.Count; i++)
            {
                var sink = _spendingSinks[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                sink.IsExpanded = EditorGUILayout.Foldout(sink.IsExpanded, sink.Name, true);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _spendingSinks.RemoveAt(i);
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (sink.IsExpanded)
                {
                    sink.Name = EditorGUILayout.TextField("Name", sink.Name);
                    sink.Currency = (CurrencyType)EditorGUILayout.EnumPopup("Currency", sink.Currency);
                    sink.CostPerUse = EditorGUILayout.FloatField("Cost per Use", sink.CostPerUse);
                    sink.Category = EditorGUILayout.TextField("Category", sink.Category);
                    sink.IsPriority = EditorGUILayout.Toggle("High Priority", sink.IsPriority);
                    sink.IsRequired = EditorGUILayout.Toggle("Required for Progression", sink.IsRequired);
                    sink.UsesPerDay = EditorGUILayout.FloatField("Avg Uses/Day", sink.UsesPerDay);
                    sink.ValueDescription = EditorGUILayout.TextField("What Player Gets", sink.ValueDescription);

                    // Show daily/monthly cost
                    float dailyCost = sink.CostPerUse * sink.UsesPerDay;
                    EditorGUILayout.LabelField($"→ ~{dailyCost:F1} {sink.Currency}/day | ~{dailyCost * 30:F0}/month", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("+ Add Spending Sink", GUILayout.Height(30)))
            {
                _spendingSinks.Add(new SpendingSink { Name = "New Sink", IsExpanded = true });
            }

            // Summary
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Summary - Daily Spending", EditorStyles.boldLabel);

            foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
            {
                float daily = _spendingSinks
                    .Where(s => s.Currency == currency)
                    .Sum(s => s.CostPerUse * s.UsesPerDay);

                if (daily > 0)
                    EditorGUILayout.LabelField($"{currency}: {daily:F1}/day | {daily * 30:F0}/month");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSimulation()
        {
            EditorGUILayout.LabelField("Economy Simulation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _simulationDays = EditorGUILayout.IntSlider("Simulation Days", _simulationDays, 7, 365);
            _playerType = (PlayerType)EditorGUILayout.EnumPopup("Player Type", _playerType);

            if (_playerType != PlayerType.F2P)
            {
                _monthlySpending = EditorGUILayout.FloatField("Monthly Spending ($)", _monthlySpending);
            }
            else
            {
                _monthlySpending = 0;
            }

            _includeEvents = EditorGUILayout.Toggle("Include Events", _includeEvents);
            if (_includeEvents)
                _eventFrequency = EditorGUILayout.Slider("Event Frequency", _eventFrequency, 0.1f, 0.5f);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🎲 Run Simulation", GUILayout.Height(35)))
            {
                RunSimulation();
            }

            if (_hasSimulated && _result != null)
            {
                EditorGUILayout.Space(10);
                DrawSimulationResults();
            }
        }

        private void DrawSimulationResults()
        {
            EditorGUILayout.LabelField("Simulation Results", EditorStyles.boldLabel);

            // Currency balances over time graph
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Currency Balance Over Time", EditorStyles.miniBoldLabel);

            Rect graphRect = GUILayoutUtility.GetRect(100, 150, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

            if (_result.BalanceHistory.Count > 0)
            {
                // Draw each currency line
                Color[] colors = { Color.yellow, Color.cyan, Color.green, Color.magenta, Color.red };
                int colorIdx = 0;

                foreach (var currency in _result.BalanceHistory.Keys)
                {
                    var history = _result.BalanceHistory[currency];
                    if (history.Count < 2) continue;

                    float maxVal = history.Max();
                    float minVal = history.Min();
                    float range = maxVal - minVal;
                    if (range < 1) range = 1;

                    Color lineColor = colors[colorIdx % colors.Length];
                    colorIdx++;

                    Vector3 prevPoint = Vector3.zero;
                    for (int i = 0; i < history.Count; i++)
                    {
                        float x = graphRect.x + (float)i / history.Count * graphRect.width;
                        float y = graphRect.yMax - ((history[i] - minVal) / range) * graphRect.height;

                        Vector3 point = new Vector3(x, y, 0);

                        if (i > 0)
                        {
                            Handles.color = lineColor;
                            Handles.DrawLine(prevPoint, point);
                        }

                        prevPoint = point;
                    }

                    // Legend
                    EditorGUI.DrawRect(new Rect(graphRect.x + 5 + colorIdx * 80, graphRect.y + 5, 10, 10), lineColor);
                    GUI.Label(new Rect(graphRect.x + 18 + colorIdx * 80, graphRect.y + 3, 70, 15), currency.ToString(), EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Final balances
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Final Balances (Day {_simulationDays})", EditorStyles.boldLabel);

            foreach (var kvp in _result.FinalBalances)
            {
                float initial = _result.InitialBalances.GetValueOrDefault(kvp.Key, 0);
                float change = kvp.Value - initial;
                string changeStr = change >= 0 ? $"+{change:F0}" : $"{change:F0}";
                Color changeColor = change >= 0 ? Color.green : Color.red;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{kvp.Key}:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{kvp.Value:F0}", GUILayout.Width(80));
                var style = new GUIStyle(EditorStyles.label) { normal = { textColor = changeColor } };
                EditorGUILayout.LabelField($"({changeStr})", style, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            // Key metrics
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Key Metrics", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Total Pulls Available: {_result.TotalPulls}");
            EditorGUILayout.LabelField($"Expected 5-Stars: {_result.Expected5Stars:F1}");
            EditorGUILayout.LabelField($"Days to Guarantee 5-Star: {_result.DaysToGuarantee5Star}");
            EditorGUILayout.LabelField($"Months to C6 Featured: {_result.MonthsToC6:F1}");

            if (_monthlySpending > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Total Spent: ${_monthlySpending * _simulationDays / 30:F2}");
                EditorGUILayout.LabelField($"Cost per 5-Star: ${_result.CostPer5Star:F2}");
            }

            EditorGUILayout.EndVertical();

            // Warnings
            if (_result.Warnings.Count > 0)
            {
                EditorGUILayout.Space(5);
                foreach (var warning in _result.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
        }

        private void DrawAnalysis()
        {
            EditorGUILayout.LabelField("Economy Analysis", EditorStyles.boldLabel);

            // Income vs Spending comparison
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Income vs Spending Balance", EditorStyles.boldLabel);

            foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
            {
                float dailyIncome = _incomeSources
                    .Where(s => s.Currency == currency)
                    .Sum(s => CalculateDailyIncome(s));

                float dailySpending = _spendingSinks
                    .Where(s => s.Currency == currency)
                    .Sum(s => s.CostPerUse * s.UsesPerDay);

                if (dailyIncome == 0 && dailySpending == 0) continue;

                float balance = dailyIncome - dailySpending;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{currency}", EditorStyles.boldLabel, GUILayout.Width(100));

                // Visual bar
                Rect barRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                float maxVal = Mathf.Max(dailyIncome, dailySpending);

                // Income bar (green)
                float incomeWidth = (dailyIncome / maxVal) * barRect.width * 0.45f;
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, incomeWidth, 8), Color.green);

                // Spending bar (red)
                float spendWidth = (dailySpending / maxVal) * barRect.width * 0.45f;
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + 10, spendWidth, 8), Color.red);

                Color balanceColor = balance >= 0 ? Color.green : Color.red;
                var style = new GUIStyle(EditorStyles.label) { normal = { textColor = balanceColor } };
                EditorGUILayout.LabelField($"{(balance >= 0 ? "+" : "")}{balance:F1}/day", style, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Gacha metrics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Gacha Economy Analysis", EditorStyles.boldLabel);

            float dailyPulls = _incomeSources
                .Where(s => s.Currency == CurrencyType.PullCurrency)
                .Sum(s => CalculateDailyIncome(s)) / 160f; // Assuming 160 per pull

            EditorGUILayout.LabelField($"Daily Pulls (F2P): {dailyPulls:F2}");
            EditorGUILayout.LabelField($"Monthly Pulls (F2P): {dailyPulls * 30:F1}");
            EditorGUILayout.LabelField($"Yearly Pulls (F2P): {dailyPulls * 365:F0}");

            float avgPityTo5Star = 62.5f; // Soft pity average
            float monthsToGuarantee = 180f / (dailyPulls * 30);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Days to Avg 5-Star: {avgPityTo5Star / dailyPulls:F0}");
            EditorGUILayout.LabelField($"Months to Guarantee (180): {monthsToGuarantee:F1}");
            EditorGUILayout.LabelField($"Yearly Expected 5-Stars: {dailyPulls * 365 / avgPityTo5Star:F1}");

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Player type comparison
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Player Type Comparison (90 days)", EditorStyles.boldLabel);

            string[] playerTypes = { "F2P", "Low Spender ($5)", "Dolphin ($30)", "Whale ($100)" };
            float[] monthlySpends = { 0, 5, 30, 100 };
            float[] bonusPulls = { 0, 5, 30, 80 }; // Monthly bonus pulls from purchases

            foreach (int i in Enumerable.Range(0, 4))
            {
                float totalPulls = (dailyPulls * 90) + (bonusPulls[i] * 3);
                float expected5Stars = totalPulls / avgPityTo5Star;
                float totalSpent = monthlySpends[i] * 3;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(playerTypes[i], GUILayout.Width(120));
                EditorGUILayout.LabelField($"{totalPulls:F0} pulls", GUILayout.Width(80));
                EditorGUILayout.LabelField($"~{expected5Stars:F1} 5★", GUILayout.Width(60));
                if (totalSpent > 0)
                    EditorGUILayout.LabelField($"(${totalSpent})", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Recommendations
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 Recommendations", EditorStyles.boldLabel);

            List<string> recommendations = AnalyzeAndRecommend();
            foreach (var rec in recommendations)
            {
                EditorGUILayout.LabelField($"• {rec}", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Currency Settings", EditorStyles.boldLabel);

            for (int i = 0; i < _currencies.Count; i++)
            {
                var currency = _currencies[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(currency.Type.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                currency.DisplayName = EditorGUILayout.TextField("Display Name", currency.DisplayName);
                currency.PremiumConversion = EditorGUILayout.FloatField("Premium → This Rate", currency.PremiumConversion);
                currency.RealMoneyValue = EditorGUILayout.FloatField("$ per 1 unit", currency.RealMoneyValue);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(20);

            // Export/Import
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Data Management", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export to JSON"))
                ExportToJson();
            if (GUILayout.Button("Import from JSON"))
                ImportFromJson();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset", "Reset all economy data?", "Yes", "No"))
                {
                    InitializeDefaultCurrencies();
                    _incomeSources.Clear();
                    _spendingSinks.Clear();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void RunSimulation()
        {
            _result = new SimulationResult();
            System.Random rng = new System.Random();

            // Initialize balances
            Dictionary<CurrencyType, float> balances = new Dictionary<CurrencyType, float>();
            foreach (CurrencyType ct in Enum.GetValues(typeof(CurrencyType)))
            {
                balances[ct] = 0;
                _result.InitialBalances[ct] = 0;
                _result.BalanceHistory[ct] = new List<float>();
            }

            float totalPulls = 0;
            int pity = 0;

            for (int day = 0; day < _simulationDays; day++)
            {
                // Apply income
                foreach (var source in _incomeSources)
                {
                    if (source.IsLimited && day > 0) continue; // One-time sources only on day 0

                    float dailyAmount = CalculateDailyIncome(source);

                    // Apply probability
                    if (!source.IsGuaranteed && rng.NextDouble() > source.Probability)
                        continue;

                    balances[source.Currency] += dailyAmount;
                }

                // Apply event bonuses
                if (_includeEvents && rng.NextDouble() < _eventFrequency)
                {
                    // Random event bonus
                    balances[CurrencyType.PullCurrency] += rng.Next(100, 400);
                    balances[CurrencyType.Soft] += rng.Next(50000, 200000);
                }

                // Apply monthly spending
                if (day % 30 == 0 && _monthlySpending > 0)
                {
                    // Convert $ to premium currency (assume $1 = 60 premium)
                    float premiumBought = _monthlySpending * 60;
                    balances[CurrencyType.Premium] += premiumBought;

                    // Welkin-style pass (if low spender)
                    if (_playerType == PlayerType.LowSpender || _playerType == PlayerType.Dolphin || _playerType == PlayerType.Whale)
                    {
                        // Already getting daily bonus from Welkin
                    }
                }

                // Apply spending
                foreach (var sink in _spendingSinks)
                {
                    float cost = sink.CostPerUse * sink.UsesPerDay;
                    balances[sink.Currency] -= cost;

                    // Convert pulls to tracking
                    if (sink.Currency == CurrencyType.PullCurrency && sink.Name.Contains("Pull"))
                    {
                        totalPulls += sink.UsesPerDay;
                        pity += (int)sink.UsesPerDay;

                        // Simulate 5-star drops
                        if (pity >= 90 || (pity >= 74 && rng.NextDouble() < 0.32f))
                        {
                            _result.Expected5Stars += 1;
                            pity = 0;
                        }
                    }
                }

                // Record history
                foreach (var ct in balances.Keys)
                {
                    _result.BalanceHistory[ct].Add(balances[ct]);
                }
            }

            // Final results
            foreach (var kvp in balances)
                _result.FinalBalances[kvp.Key] = kvp.Value;

            _result.TotalPulls = (int)(balances[CurrencyType.PullCurrency] / 160f + totalPulls);
            if (_result.Expected5Stars == 0)
                _result.Expected5Stars = _result.TotalPulls / 62.5f;

            float dailyPulls = _result.TotalPulls / (float)_simulationDays;
            _result.DaysToGuarantee5Star = dailyPulls > 0 ? (int)(180 / dailyPulls) : 999;
            _result.MonthsToC6 = dailyPulls > 0 ? 180 * 7 / (dailyPulls * 30) : 999;

            if (_monthlySpending > 0 && _result.Expected5Stars > 0)
                _result.CostPer5Star = (_monthlySpending * _simulationDays / 30) / _result.Expected5Stars;

            // Warnings
            if (balances[CurrencyType.PullCurrency] < 0)
                _result.Warnings.Add("Pull currency goes negative - spending exceeds income!");
            if (_result.DaysToGuarantee5Star > 180)
                _result.Warnings.Add("Very slow progression - consider increasing pull income");
            if (_result.MonthsToC6 > 24)
                _result.Warnings.Add("C6 takes over 2 years - may frustrate dedicated players");

            _hasSimulated = true;
        }

        private float CalculateDailyIncome(IncomeSource source)
        {
            float multiplier = source.Frequency switch
            {
                TimeUnit.Day => 1f,
                TimeUnit.Week => 1f / 7f,
                TimeUnit.Month => 1f / 30f,
                TimeUnit.Year => 1f / 365f,
                _ => 1f
            };

            return source.Amount * source.FrequencyCount * multiplier;
        }

        private List<string> AnalyzeAndRecommend()
        {
            var recommendations = new List<string>();

            float dailyPulls = _incomeSources
                .Where(s => s.Currency == CurrencyType.PullCurrency)
                .Sum(s => CalculateDailyIncome(s)) / 160f;

            if (dailyPulls < 0.5f)
                recommendations.Add("Pull income is low. Consider adding more daily/weekly pull sources.");

            if (dailyPulls > 2f)
                recommendations.Add("Pull income is very high. This may devalue premium purchases.");

            float dailySoftIncome = _incomeSources
                .Where(s => s.Currency == CurrencyType.Soft)
                .Sum(s => CalculateDailyIncome(s));

            float dailySoftSpending = _spendingSinks
                .Where(s => s.Currency == CurrencyType.Soft)
                .Sum(s => s.CostPerUse * s.UsesPerDay);

            if (dailySoftSpending > dailySoftIncome * 1.5f)
                recommendations.Add("Soft currency spending greatly exceeds income. Players will feel resource-starved.");

            if (dailySoftIncome > dailySoftSpending * 3f)
                recommendations.Add("Soft currency income far exceeds spending. Add more sinks to give value.");

            bool hasEvents = _incomeSources.Any(s => s.Notes.ToLower().Contains("event"));
            if (!hasEvents)
                recommendations.Add("No event income sources defined. Events are important for engagement spikes.");

            if (recommendations.Count == 0)
                recommendations.Add("Economy looks reasonably balanced! Run simulations to verify.");

            return recommendations;
        }

        private void InitializeDefaultCurrencies()
        {
            _currencies.Clear();
            _currencies.Add(new Currency { Type = CurrencyType.Premium, DisplayName = "Crystals", PremiumConversion = 1, RealMoneyValue = 0.0167f });
            _currencies.Add(new Currency { Type = CurrencyType.Soft, DisplayName = "Gold", PremiumConversion = 0, RealMoneyValue = 0 });
            _currencies.Add(new Currency { Type = CurrencyType.PullCurrency, DisplayName = "Primogems", PremiumConversion = 1, RealMoneyValue = 0.0167f });
            _currencies.Add(new Currency { Type = CurrencyType.Stamina, DisplayName = "Resin", PremiumConversion = 0.5f, RealMoneyValue = 0.0083f });
            _currencies.Add(new Currency { Type = CurrencyType.Resin, DisplayName = "Energy", PremiumConversion = 0, RealMoneyValue = 0 });
        }

        private static void CreateGenshinPreset(EconomyBalancer balancer)
        {
            balancer._incomeSources.Clear();
            balancer._spendingSinks.Clear();

            // Income
            balancer._incomeSources.Add(new IncomeSource { Name = "Daily Commissions", Currency = CurrencyType.PullCurrency, Amount = 60, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Welkin Moon", Currency = CurrencyType.PullCurrency, Amount = 90, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true, Notes = "Paid" });
            balancer._incomeSources.Add(new IncomeSource { Name = "Abyss 36 Stars", Currency = CurrencyType.PullCurrency, Amount = 600, Frequency = TimeUnit.Week, FrequencyCount = 1, IsGuaranteed = false, Probability = 0.3f });
            balancer._incomeSources.Add(new IncomeSource { Name = "Events", Currency = CurrencyType.PullCurrency, Amount = 420, Frequency = TimeUnit.Week, FrequencyCount = 1, IsGuaranteed = false, Probability = 0.7f, Notes = "event" });
            balancer._incomeSources.Add(new IncomeSource { Name = "Exploration", Currency = CurrencyType.PullCurrency, Amount = 200, Frequency = TimeUnit.Week, FrequencyCount = 1, IsGuaranteed = false, Probability = 0.5f });
            balancer._incomeSources.Add(new IncomeSource { Name = "Maintenance", Currency = CurrencyType.PullCurrency, Amount = 300, Frequency = TimeUnit.Month, FrequencyCount = 1, IsGuaranteed = true });

            balancer._incomeSources.Add(new IncomeSource { Name = "Daily Resin", Currency = CurrencyType.Stamina, Amount = 180, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Ley Lines/Domains", Currency = CurrencyType.Soft, Amount = 60000, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });

            // Spending
            balancer._spendingSinks.Add(new SpendingSink { Name = "Limited Banner Pull", Currency = CurrencyType.PullCurrency, CostPerUse = 160, Category = "Gacha", IsPriority = true, UsesPerDay = 0.4f });
            balancer._spendingSinks.Add(new SpendingSink { Name = "Artifact Farming", Currency = CurrencyType.Stamina, CostPerUse = 20, Category = "Progression", IsRequired = true, UsesPerDay = 5 });
            balancer._spendingSinks.Add(new SpendingSink { Name = "Character Ascension", Currency = CurrencyType.Soft, CostPerUse = 100000, Category = "Progression", UsesPerDay = 0.1f });
            balancer._spendingSinks.Add(new SpendingSink { Name = "Talent Upgrade", Currency = CurrencyType.Soft, CostPerUse = 200000, Category = "Progression", UsesPerDay = 0.05f });
        }

        private static void CreateHSRPreset(EconomyBalancer balancer)
        {
            balancer._incomeSources.Clear();
            balancer._spendingSinks.Clear();

            balancer._incomeSources.Add(new IncomeSource { Name = "Daily Training", Currency = CurrencyType.PullCurrency, Amount = 60, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Express Supply Pass", Currency = CurrencyType.PullCurrency, Amount = 90, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true, Notes = "Paid" });
            balancer._incomeSources.Add(new IncomeSource { Name = "MoC Full Clear", Currency = CurrencyType.PullCurrency, Amount = 800, Frequency = TimeUnit.Month, FrequencyCount = 1, IsGuaranteed = false, Probability = 0.4f });
            balancer._incomeSources.Add(new IncomeSource { Name = "Events", Currency = CurrencyType.PullCurrency, Amount = 1000, Frequency = TimeUnit.Month, FrequencyCount = 1, IsGuaranteed = true, Notes = "event" });
            balancer._incomeSources.Add(new IncomeSource { Name = "Simulated Universe", Currency = CurrencyType.PullCurrency, Amount = 200, Frequency = TimeUnit.Week, FrequencyCount = 1, IsGuaranteed = true });

            balancer._incomeSources.Add(new IncomeSource { Name = "Trailblaze Power", Currency = CurrencyType.Stamina, Amount = 240, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Credit Farming", Currency = CurrencyType.Soft, Amount = 120000, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });

            balancer._spendingSinks.Add(new SpendingSink { Name = "Character Warp", Currency = CurrencyType.PullCurrency, CostPerUse = 160, Category = "Gacha", IsPriority = true, UsesPerDay = 0.4f });
            balancer._spendingSinks.Add(new SpendingSink { Name = "Relic Farming", Currency = CurrencyType.Stamina, CostPerUse = 40, Category = "Progression", IsRequired = true, UsesPerDay = 4 });
        }

        private static void CreateMobileRPGPreset(EconomyBalancer balancer)
        {
            balancer._incomeSources.Clear();
            balancer._spendingSinks.Clear();

            balancer._incomeSources.Add(new IncomeSource { Name = "Daily Login", Currency = CurrencyType.PullCurrency, Amount = 50, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Daily Quests", Currency = CurrencyType.PullCurrency, Amount = 30, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Weekly Boss", Currency = CurrencyType.PullCurrency, Amount = 100, Frequency = TimeUnit.Week, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Monthly Reset", Currency = CurrencyType.PullCurrency, Amount = 500, Frequency = TimeUnit.Month, FrequencyCount = 1, IsGuaranteed = true });

            balancer._incomeSources.Add(new IncomeSource { Name = "Stamina Regen", Currency = CurrencyType.Stamina, Amount = 288, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });
            balancer._incomeSources.Add(new IncomeSource { Name = "Gold Dungeon", Currency = CurrencyType.Soft, Amount = 100000, Frequency = TimeUnit.Day, FrequencyCount = 1, IsGuaranteed = true });

            balancer._spendingSinks.Add(new SpendingSink { Name = "Gacha Pull", Currency = CurrencyType.PullCurrency, CostPerUse = 150, Category = "Gacha", IsPriority = true, UsesPerDay = 0.5f });
            balancer._spendingSinks.Add(new SpendingSink { Name = "Equipment Enhance", Currency = CurrencyType.Soft, CostPerUse = 50000, Category = "Progression", UsesPerDay = 0.5f });
        }

        private void ExportToJson()
        {
            string path = EditorUtility.SaveFilePanel("Export Economy", "", "economy_data", "json");
            if (string.IsNullOrEmpty(path)) return;

            var data = new EconomyExportData
            {
                currencies = _currencies,
                incomeSources = _incomeSources,
                spendingSinks = _spendingSinks
            };

            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Exported economy to {path}");
        }

        private void ImportFromJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Economy", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<EconomyExportData>(json);

            _currencies = data.currencies;
            _incomeSources = data.incomeSources;
            _spendingSinks = data.spendingSinks;

            Debug.Log("Imported economy data");
        }

        // Data classes
        [Serializable]
        private class Currency
        {
            public CurrencyType Type;
            public string DisplayName;
            public float PremiumConversion;
            public float RealMoneyValue;
        }

        [Serializable]
        private class IncomeSource
        {
            public string Name;
            public CurrencyType Currency;
            public float Amount;
            public TimeUnit Frequency;
            public int FrequencyCount = 1;
            public bool IsGuaranteed = true;
            public float Probability = 1f;
            public bool RequiresEffort;
            public float EffortMinutes;
            public bool IsLimited;
            public string Notes = "";
            public bool IsExpanded;
        }

        [Serializable]
        private class SpendingSink
        {
            public string Name;
            public CurrencyType Currency;
            public float CostPerUse;
            public string Category;
            public bool IsPriority;
            public bool IsRequired;
            public float UsesPerDay;
            public string ValueDescription;
            public bool IsExpanded;
        }

        private class SimulationResult
        {
            public Dictionary<CurrencyType, float> InitialBalances = new Dictionary<CurrencyType, float>();
            public Dictionary<CurrencyType, float> FinalBalances = new Dictionary<CurrencyType, float>();
            public Dictionary<CurrencyType, List<float>> BalanceHistory = new Dictionary<CurrencyType, List<float>>();
            public int TotalPulls;
            public float Expected5Stars;
            public int DaysToGuarantee5Star;
            public float MonthsToC6;
            public float CostPer5Star;
            public List<string> Warnings = new List<string>();
        }

        [Serializable]
        private class EconomyExportData
        {
            public List<Currency> currencies;
            public List<IncomeSource> incomeSources;
            public List<SpendingSink> spendingSinks;
        }
    }
}
