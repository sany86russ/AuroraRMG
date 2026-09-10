using System.ComponentModel;

namespace Olden_Era___Template_Editor.Models;

/// <summary>Selection survives filtering and virtualized item-container recycling.</summary>
public sealed class SelectableSid(string id) : INotifyPropertyChanged
{
    public string DisplayName => Olden_Era___Template_Editor.Services.Localization.GameLabels.Name(Id);
    public void RefreshLanguage() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    public string Id { get; } = id;
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
