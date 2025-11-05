using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoF11.Core;

namespace AutoF11.UI;

/// <summary>
/// Window for managing per-app rules.
/// </summary>
public partial class RulesWindow : Window
{
    private readonly Settings _settings;
    private readonly RuleEngine _ruleEngine;
    private readonly ObservableCollection<AppRuleViewModel> _rules;

    public RulesWindow(Settings settings, RuleEngine ruleEngine)
    {
        InitializeComponent();
        _settings = settings;
        _ruleEngine = ruleEngine;
        _rules = new ObservableCollection<AppRuleViewModel>();

        LoadRules();
        RulesDataGrid.ItemsSource = _rules;
        RulesDataGrid.PreparingCellForEdit += OnPreparingCellForEdit;
    }

    private void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.Column.Header?.ToString() == "Strategy" && e.EditingElement is System.Windows.Controls.ComboBox comboBox)
        {
            comboBox.ItemsSource = KeyStrategyValues.Values;
        }
    }

    private void LoadRules()
    {
        _rules.Clear();
        foreach (var rule in _settings.Rules)
        {
            _rules.Add(new AppRuleViewModel
            {
                ProcessName = rule.ProcessName,
                WindowTitleContains = rule.WindowTitleContains ?? string.Empty,
                Strategy = rule.Strategy,
                DelayMs = rule.DelayMs,
                Enabled = rule.Enabled,
                OnlyOncePerSession = rule.OnlyOncePerSession
            });
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        // Validate and save rules
        _settings.Rules.Clear();
        foreach (var viewModel in _rules)
        {
            if (string.IsNullOrWhiteSpace(viewModel.ProcessName))
                continue; // Skip empty rows

            _settings.Rules.Add(new AppRule
            {
                ProcessName = viewModel.ProcessName.Trim(),
                WindowTitleContains = string.IsNullOrWhiteSpace(viewModel.WindowTitleContains) 
                    ? null 
                    : viewModel.WindowTitleContains.Trim(),
                Strategy = viewModel.Strategy,
                DelayMs = viewModel.DelayMs,
                Enabled = viewModel.Enabled,
                OnlyOncePerSession = viewModel.OnlyOncePerSession
            });
        }

        _settings.Save();
        _ruleEngine.ClearSession(); // Clear session tracking when rules change

        System.Windows.MessageBox.Show("Rules saved successfully!", "AutoF11", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// View model for AppRule in the DataGrid.
/// </summary>
public class AppRuleViewModel
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitleContains { get; set; } = string.Empty;
    public KeyStrategy Strategy { get; set; } = KeyStrategy.F11;
    public int DelayMs { get; set; } = 150;
    public bool Enabled { get; set; } = true;
    public bool OnlyOncePerSession { get; set; } = false;
}

