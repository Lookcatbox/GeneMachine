using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ChemistryReactionEditor;

public class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void Raise([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ChemistryConfigFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("substances")] public ObservableCollection<SubstanceModel> Substances { get; set; } = new();
    [JsonPropertyName("reactions")] public ObservableCollection<ReactionModel> Reactions { get; set; } = new();
}

public class SubstanceModel : ObservableObject
{
    string id = "";
    string displayName = "";
    string phase = "Solid";
    string color = "#FFFFFF";
    float overlayMax = 1f;
    float baselineLand;
    float baselineWater;

    [JsonPropertyName("id")] public string Id { get => id; set => Set(ref id, value); }
    [JsonPropertyName("displayName")] public string DisplayName { get => displayName; set => Set(ref displayName, value); }
    [JsonPropertyName("phase")] public string Phase { get => phase; set => Set(ref phase, value); }
    [JsonPropertyName("color")] public string Color { get => color; set => Set(ref color, value); }
    [JsonPropertyName("overlayMax")] public float OverlayMax { get => overlayMax; set => Set(ref overlayMax, value); }
    [JsonPropertyName("baselineLand")] public float BaselineLand { get => baselineLand; set => Set(ref baselineLand, value); }
    [JsonPropertyName("baselineWater")] public float BaselineWater { get => baselineWater; set => Set(ref baselineWater, value); }
}

public class ReactionModel : ObservableObject
{
    string id = "";
    string name = "";
    bool enabled = true;
    int priority;
    string condition = "true";
    string rateExpression = "0.05 * pow(limiting, 0.75)";

    [JsonPropertyName("id")] public string Id { get => id; set => Set(ref id, value); }
    [JsonPropertyName("name")] public string Name { get => name; set => Set(ref name, value); }
    [JsonPropertyName("enabled")] public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    [JsonPropertyName("priority")] public int Priority { get => priority; set => Set(ref priority, value); }
    [JsonPropertyName("reactants")] public ObservableCollection<ReactionTermModel> Reactants { get; set; } = new();
    [JsonPropertyName("products")] public ObservableCollection<ReactionTermModel> Products { get; set; } = new();
    [JsonPropertyName("condition")] public string Condition { get => condition; set => Set(ref condition, value); }
    [JsonPropertyName("rateExpression")] public string RateExpression { get => rateExpression; set => Set(ref rateExpression, value); }

    [JsonIgnore]
    public string Title => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    public void RefreshTitle()
    {
        Raise(nameof(Title));
    }
}

public class ReactionTermModel : ObservableObject
{
    string substanceId = "";
    float coeff = 1f;

    [JsonPropertyName("substanceId")] public string SubstanceId { get => substanceId; set => Set(ref substanceId, value); }
    [JsonPropertyName("coeff")] public float Coeff { get => coeff; set => Set(ref coeff, value); }
}
