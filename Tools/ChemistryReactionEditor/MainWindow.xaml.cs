using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChemistryReactionEditor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    ChemistryConfigFile config = new();
    SubstanceModel? selectedSubstance;
    ReactionModel? selectedReaction;
    string currentPath = "";
    string statusText = "";
    Point dragStartPoint;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChemistryConfigFile Config
    {
        get => config;
        set
        {
            config = value;
            Raise(nameof(Config));
            SelectedReaction = Config.Reactions.FirstOrDefault();
            SelectedSubstance = Config.Substances.FirstOrDefault();
        }
    }

    public SubstanceModel? SelectedSubstance
    {
        get => selectedSubstance;
        set { selectedSubstance = value; Raise(nameof(SelectedSubstance)); }
    }

    public ReactionModel? SelectedReaction
    {
        get => selectedReaction;
        set { selectedReaction = value; Raise(nameof(SelectedReaction)); }
    }

    public string CurrentPath
    {
        get => currentPath;
        set { currentPath = value; Raise(nameof(CurrentPath)); }
    }

    public string StatusText
    {
        get => statusText;
        set { statusText = value; Raise(nameof(StatusText)); }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        string defaultPath = FindDefaultConfigPath();
        if (File.Exists(defaultPath))
            LoadFromPath(defaultPath);
        else
            Config = CreateDefaultConfig();
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Chemistry JSON|*.json|All files|*.*",
            FileName = string.IsNullOrWhiteSpace(CurrentPath) ? "chemistry-reactions.json" : CurrentPath
        };
        if (dialog.ShowDialog(this) == true)
            LoadFromPath(dialog.FileName);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            SaveAs_Click(sender, e);
            return;
        }
        SaveToPath(CurrentPath);
    }

    void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "Chemistry JSON|*.json|All files|*.*",
            FileName = "chemistry-reactions.json"
        };
        if (dialog.ShowDialog(this) == true)
            SaveToPath(dialog.FileName);
    }

    void Validate_Click(object sender, RoutedEventArgs e)
    {
        StatusText = ValidateConfig(out string error) ? "验证通过。" : error;
    }

    void AddSubstance_Click(object sender, RoutedEventArgs e)
    {
        string id = NextUniqueId("substance");
        SubstanceModel item = new()
        {
            Id = id,
            DisplayName = id,
            Phase = "Solid",
            Color = "#FFFFFF",
            OverlayMax = 1f
        };
        Config.Substances.Add(item);
        SelectedSubstance = item;
    }

    void DeleteSubstance_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSubstance == null)
            return;
        string id = SelectedSubstance.Id;
        bool used = Config.Reactions.Any(r =>
            r.Reactants.Any(t => t.SubstanceId == id) ||
            r.Products.Any(t => t.SubstanceId == id));
        if (used && MessageBox.Show(this, "该物质仍被反应引用，确定删除？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        Config.Substances.Remove(SelectedSubstance);
        SelectedSubstance = Config.Substances.FirstOrDefault();
    }

    void AddReaction_Click(object sender, RoutedEventArgs e)
    {
        string id = NextUniqueReactionId("reaction");
        string substanceId = Config.Substances.FirstOrDefault()?.Id ?? "";
        ReactionModel reaction = new()
        {
            Id = id,
            Name = id,
            Enabled = true,
            Condition = "true",
            RateExpression = "0.05 * pow(limiting, 0.75)"
        };
        reaction.Reactants.Add(new ReactionTermModel { SubstanceId = substanceId, Coeff = 1f });
        reaction.Products.Add(new ReactionTermModel { SubstanceId = substanceId, Coeff = 1f });
        Config.Reactions.Add(reaction);
        AssignPrioritiesFromOrder();
        SelectedReaction = reaction;
    }

    void DeleteReaction_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedReaction == null)
            return;
        Config.Reactions.Remove(SelectedReaction);
        AssignPrioritiesFromOrder();
        SelectedReaction = Config.Reactions.FirstOrDefault();
    }

    void ReactionName_TextChanged(object sender, TextChangedEventArgs e)
    {
        SelectedReaction?.RefreshTitle();
    }

    void ReactionList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            dragStartPoint = e.GetPosition(null);
            return;
        }

        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (ReactionList.SelectedItem is ReactionModel reaction)
            DragDrop.DoDragDrop(ReactionList, reaction, DragDropEffects.Move);
    }

    void ReactionList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ReactionModel)))
            return;

        ReactionModel dragged = (ReactionModel)e.Data.GetData(typeof(ReactionModel))!;
        ReactionModel? target = GetListBoxItemAt(e.GetPosition(ReactionList));
        if (target == null || ReferenceEquals(dragged, target))
            return;

        int oldIndex = Config.Reactions.IndexOf(dragged);
        int newIndex = Config.Reactions.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0)
            return;

        Config.Reactions.Move(oldIndex, newIndex);
        AssignPrioritiesFromOrder();
        SelectedReaction = dragged;
    }

    ReactionModel? GetListBoxItemAt(Point point)
    {
        DependencyObject? element = ReactionList.InputHitTest(point) as DependencyObject;
        while (element != null)
        {
            if (element is ListBoxItem item)
                return item.DataContext as ReactionModel;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    void LoadFromPath(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            ChemistryConfigFile? loaded = JsonSerializer.Deserialize<ChemistryConfigFile>(json, JsonOptions);
            Config = loaded ?? CreateDefaultConfig();
            CurrentPath = path;
            StatusText = "已加载: " + path;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "读取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SaveToPath(string path)
    {
        AssignPrioritiesFromOrder();
        if (!ValidateConfig(out string error))
        {
            StatusText = error;
            MessageBox.Show(this, error, "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(path, json);
            CurrentPath = path;
            StatusText = "已保存: " + path;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    bool ValidateConfig(out string error)
    {
        List<string> errors = new();
        HashSet<string> substanceIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (SubstanceModel substance in Config.Substances)
        {
            if (string.IsNullOrWhiteSpace(substance.Id))
                errors.Add("物质 id 不能为空。");
            else if (!substanceIds.Add(substance.Id))
                errors.Add("物质 id 重复: " + substance.Id);
            if (substance.OverlayMax <= 0)
                errors.Add("热力图上限必须大于 0: " + substance.Id);
            if (!substance.Color.StartsWith("#") || (substance.Color.Length != 7 && substance.Color.Length != 9))
                errors.Add("颜色建议使用 #RRGGBB 或 #RRGGBBAA: " + substance.Id);
        }

        HashSet<string> reactionIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReactionModel reaction in Config.Reactions)
        {
            if (string.IsNullOrWhiteSpace(reaction.Id))
                errors.Add("反应 id 不能为空。");
            else if (!reactionIds.Add(reaction.Id))
                errors.Add("反应 id 重复: " + reaction.Id);

            ValidateTerms(reaction.Id, "反应物", reaction.Reactants, substanceIds, errors);
            ValidateTerms(reaction.Id, "生成物", reaction.Products, substanceIds, errors);
            if (string.IsNullOrWhiteSpace(reaction.Condition))
                errors.Add("条件表达式不能为空: " + reaction.Id);
            if (string.IsNullOrWhiteSpace(reaction.RateExpression))
                errors.Add("动力方程不能为空: " + reaction.Id);
        }

        error = errors.Count == 0 ? "" : string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    static void ValidateTerms(string reactionId, string label, ObservableCollection<ReactionTermModel> terms, HashSet<string> substanceIds, List<string> errors)
    {
        if (terms.Count == 0)
            errors.Add(reactionId + " 至少需要 1 个" + label);

        foreach (ReactionTermModel term in terms)
        {
            if (!substanceIds.Contains(term.SubstanceId))
                errors.Add(reactionId + " 的" + label + "引用了不存在的物质: " + term.SubstanceId);
            if (term.Coeff <= 0)
                errors.Add(reactionId + " 的" + label + "系数必须大于 0: " + term.SubstanceId);
        }
    }

    void AssignPrioritiesFromOrder()
    {
        int priority = Config.Reactions.Count;
        foreach (ReactionModel reaction in Config.Reactions)
            reaction.Priority = priority--;
    }

    string NextUniqueId(string prefix)
    {
        int i = Config.Substances.Count + 1;
        while (Config.Substances.Any(s => s.Id == prefix + i))
            i++;
        return prefix + i;
    }

    string NextUniqueReactionId(string prefix)
    {
        int i = Config.Reactions.Count + 1;
        while (Config.Reactions.Any(r => r.Id == prefix + i))
            i++;
        return prefix + i;
    }

    static string FindDefaultConfigPath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "chemistry-reactions.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "StreamingAssets", "chemistry-reactions.json"));
    }

    static ChemistryConfigFile CreateDefaultConfig()
    {
        ChemistryConfigFile config = new();
        config.Substances.Add(new SubstanceModel { Id = "organic", DisplayName = "有机物", Phase = "Solid", Color = "#5A8C38", OverlayMax = 8f, BaselineLand = 2.5f, BaselineWater = 0.4f });
        config.Substances.Add(new SubstanceModel { Id = "co2", DisplayName = "CO2", Phase = "Gas", Color = "#8C8C8C", OverlayMax = 6f, BaselineLand = 1.2f, BaselineWater = 2.0f });
        config.Substances.Add(new SubstanceModel { Id = "h2", DisplayName = "H2", Phase = "Gas", Color = "#33BFDD", OverlayMax = 4f, BaselineLand = 0.3f, BaselineWater = 0.8f });
        config.Substances.Add(new SubstanceModel { Id = "h2s", DisplayName = "H2S", Phase = "Gas", Color = "#E6CC26", OverlayMax = 4f, BaselineLand = 0.2f, BaselineWater = 0.6f });
        config.Substances.Add(new SubstanceModel { Id = "sulfate", DisplayName = "硫酸盐", Phase = "Liquid", Color = "#C0C7F2", OverlayMax = 5f, BaselineLand = 0.5f, BaselineWater = 1.5f });
        return config;
    }

    void Raise(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
