using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemoryLab.Models;

public sealed class AddressEntry : INotifyPropertyChanged
{
    private bool _isFrozen;
    private string _description = string.Empty;
    private string _group = "Default";
    private string _value = string.Empty;
    private string _frozenValue = string.Empty;
    private string _addressKind = "Absolute";
    private string _addressExpression = string.Empty;

    public nuint Address { get; set; }

    public string AddressText => $"0x{Address:X16}";

    public string ValueType { get; set; } = "Int32";

    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            if (_isFrozen == value) return;
            _isFrozen = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value) return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public string Group
    {
        get => _group;
        set
        {
            if (_group == value) return;
            _group = value;
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public string FrozenValue
    {
        get => _frozenValue;
        set
        {
            if (_frozenValue == value) return;
            _frozenValue = value;
            OnPropertyChanged();
        }
    }

    public string AddressKind
    {
        get => _addressKind;
        set
        {
            if (_addressKind == value) return;
            _addressKind = value;
            OnPropertyChanged();
        }
    }

    public string AddressExpression
    {
        get => _addressExpression;
        set
        {
            if (_addressExpression == value) return;
            _addressExpression = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyAddressChanged()
    {
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(AddressText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
