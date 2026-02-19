using System.Collections.ObjectModel;
using System.ComponentModel;
using Whispr.Core.Protocol;

namespace Whispr.Client.Models;

/// <summary>
/// Node in the server tree: Server (root), Channel, or User.
/// </summary>
public sealed class ServerTreeNode : INotifyPropertyChanged
{
    public required string DisplayName { get; init; }
    public required NodeKind Kind { get; init; }
    public Guid? ChannelId { get; init; }
    public Guid? UserId { get; init; }
    public uint ClientId { get; set; }
    public bool IsMe { get; init; }
    public bool IsCurrentChannel { get; set; }
    /// <summary>"voice" or "text" for channel nodes.</summary>
    public string? ChannelType { get; init; }

    private bool _isAdmin;
    public bool IsAdmin
    {
        get => _isAdmin;
        set { if (_isAdmin != value) { _isAdmin = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAdmin))); } }
    }

    private bool _isSpeaking;
    public bool IsSpeaking
    {
        get => _isSpeaking;
        set { if (_isSpeaking != value) { _isSpeaking = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSpeaking))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ServerTreeNode> Children { get; } = new();
}

public enum NodeKind
{
    Server,
    Channel,
    User
}
