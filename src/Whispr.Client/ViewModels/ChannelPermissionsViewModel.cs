using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

public sealed partial class ChannelPermissionsViewModel : ObservableObject
{
    private readonly IChannelService _channelService;
    private readonly Guid _channelId;

    private IReadOnlyList<RoleInfo> _roles = [];
    private ChannelPermissionsPayload? _channelPerms;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isContentVisible;

    [ObservableProperty]
    private bool _isHintVisible;

    [ObservableProperty]
    private bool _isApplyEnabled;

    public string Title { get; }
    public ObservableCollection<ChannelRoleItemViewModel> RolesList { get; } = new();
    public ObservableCollection<ChannelUserItemViewModel> UsersList { get; } = new();

    public ChannelPermissionsViewModel(IChannelService channelService, Guid channelId, string channelName)
    {
        _channelService = channelService;
        _channelId = channelId;
        Title = $"Channel permissions: {channelName}";
        _ = LoadAsync();
    }

    private void UpdateDirty()
    {
        var dirty = false;
        foreach (var item in RolesList)
        {
            var state = item.SelectedState?.ToLowerInvariant() switch
            {
                "remove" => null,
                "allow" => "allow",
                "deny" => "deny",
                "neutral" => "neutral",
                _ => null
            };
            var current = _channelPerms?.RoleStates?.FirstOrDefault(r => r.RoleId == item.RoleId)?.State;
            var currentNorm = string.IsNullOrEmpty(current) ? null : current.ToLowerInvariant();
            if (state != currentNorm) { dirty = true; break; }
        }
        if (!dirty)
        {
            foreach (var item in UsersList)
            {
                var state = item.SelectedState?.ToLowerInvariant() switch
                {
                    "remove" => null,
                    "allow" => "allow",
                    "deny" => "deny",
                    "neutral" => "neutral",
                    _ => null
                };
                var current = _channelPerms?.UserStates?.FirstOrDefault(u => u.UserId == item.UserId)?.State;
                var currentNorm = string.IsNullOrEmpty(current) ? null : current.ToLowerInvariant();
                if (state != currentNorm) { dirty = true; break; }
            }
        }
        IsApplyEnabled = dirty;
    }

    private async Task LoadAsync()
    {
        IsApplyEnabled = false;
        IsLoading = true;
        HasError = false;
        ErrorMessage = "";
        IsContentVisible = false;
        IsHintVisible = false;

        try
        {
            var rolesTask = _channelService.RequestRolesListAsync();
            var channelPermsTask = _channelService.RequestChannelPermissionsAsync(_channelId);

            await Task.WhenAll(rolesTask, channelPermsTask);

            _roles = (await rolesTask)?.Roles ?? [];
            _channelPerms = await channelPermsTask;

            BuildRolesList();
            BuildUsersList();
            IsLoading = false;
            IsHintVisible = true;
            IsContentVisible = true;
        }
        catch (Exception ex)
        {
            IsLoading = false;
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            UpdateDirty();
        }
    }

    private void BuildRolesList()
    {
        RolesList.Clear();
        var roleStates = _channelPerms?.RoleStates.ToDictionary(r => r.RoleId, r => r.State) ?? new Dictionary<string, string>();
        foreach (var r in _roles)
        {
            var current = roleStates.GetValueOrDefault(r.Id);
            RolesList.Add(new ChannelRoleItemViewModel(UpdateDirty)
            {
                RoleId = r.Id,
                Name = r.Name,
                StateOptions = new[] { "Remove", "Allow", "Deny", "Neutral" },
                SelectedState = string.IsNullOrEmpty(current) ? "Remove" : char.ToUpperInvariant(current[0]) + current[1..]
            });
        }
    }

    private void BuildUsersList()
    {
        UsersList.Clear();
        var userStates = _channelPerms?.UserStates.ToDictionary(u => u.UserId, u => u.State) ?? new Dictionary<Guid, string>();
        var channelMembers = _channelService.GetChannelMembers(_channelId);
        var seen = new HashSet<Guid>();
        foreach (var m in channelMembers)
        {
            seen.Add(m.UserId);
            var state = userStates.GetValueOrDefault(m.UserId);
            UsersList.Add(new ChannelUserItemViewModel(UpdateDirty)
            {
                UserId = m.UserId,
                Username = m.Username,
                StateOptions = new[] { "Remove", "Allow", "Deny", "Neutral" },
                SelectedState = string.IsNullOrEmpty(state) ? "Remove" : char.ToUpperInvariant(state[0]) + state[1..]
            });
        }
        foreach (var kv in userStates.Where(u => !seen.Contains(u.Key)))
        {
            var username = _channelService.GetUsernameForUserId(kv.Key) ?? kv.Key.ToString();
            UsersList.Add(new ChannelUserItemViewModel(UpdateDirty)
            {
                UserId = kv.Key,
                Username = username,
                StateOptions = new[] { "Remove", "Allow", "Deny", "Neutral" },
                SelectedState = string.IsNullOrEmpty(kv.Value) ? "Remove" : char.ToUpperInvariant(kv.Value[0]) + kv.Value[1..]
            });
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        IsApplyEnabled = false;
        HasError = false;

        try
        {
            foreach (var item in RolesList)
            {
                var state = item.SelectedState?.ToLowerInvariant() switch
                {
                    "remove" => null,
                    "allow" => "allow",
                    "deny" => "deny",
                    "neutral" => "neutral",
                    _ => null
                };
                var current = _channelPerms?.RoleStates?.FirstOrDefault(r => r.RoleId == item.RoleId)?.State;
                var currentNorm = string.IsNullOrEmpty(current) ? null : current.ToLowerInvariant();
                if (state != currentNorm)
                    _channelPerms = await _channelService.SetChannelRolePermissionAsync(_channelId, item.RoleId, state);
            }

            foreach (var item in UsersList)
            {
                var state = item.SelectedState?.ToLowerInvariant() switch
                {
                    "remove" => null,
                    "allow" => "allow",
                    "deny" => "deny",
                    "neutral" => "neutral",
                    _ => null
                };
                var current = _channelPerms?.UserStates?.FirstOrDefault(u => u.UserId == item.UserId)?.State;
                var currentNorm = string.IsNullOrEmpty(current) ? null : current.ToLowerInvariant();
                if (state != currentNorm)
                    _channelPerms = await _channelService.SetChannelUserPermissionAsync(_channelId, item.UserId, state);
            }

            _channelPerms = await _channelService.RequestChannelPermissionsAsync(_channelId);
            BuildRolesList();
            BuildUsersList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            UpdateDirty();
        }
    }
}

public class ChannelRoleItemViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public ChannelRoleItemViewModel(Action? onChanged = null) => _onChanged = onChanged;

    public required string RoleId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> StateOptions { get; init; }

    private string? _selectedState;
    public string? SelectedState
    {
        get => _selectedState;
        set { _selectedState = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedState))); _onChanged?.Invoke(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ChannelUserItemViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public ChannelUserItemViewModel(Action? onChanged = null) => _onChanged = onChanged;

    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required IReadOnlyList<string> StateOptions { get; init; }

    private string? _selectedState;
    public string? SelectedState
    {
        get => _selectedState;
        set { _selectedState = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedState))); _onChanged?.Invoke(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
