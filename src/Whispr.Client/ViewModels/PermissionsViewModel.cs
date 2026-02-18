using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

public sealed partial class PermissionsViewModel : ObservableObject
{
    private readonly IChannelService _channelService;
    private readonly Guid _userId;

    private IReadOnlyList<PermissionInfo> _permissions = [];
    private IReadOnlyList<RoleInfo> _roles = [];
    private UserPermissionsPayload? _userPerms;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isContentVisible;

    [ObservableProperty]
    private bool _isApplyEnabled;

    public string Title { get; }
    public ObservableCollection<PermissionItemViewModel> PermissionsList { get; } = new();
    public ObservableCollection<RoleItemViewModel> RolesList { get; } = new();

    public PermissionsViewModel(IChannelService channelService, Guid userId, string username)
    {
        _channelService = channelService;
        _userId = userId;
        Title = $"Permissions for {username}";
        _ = LoadAsync();
    }

    private void UpdateDirty()
    {
        var dirty = false;
        foreach (var item in PermissionsList)
        {
            var state = item.SelectedState?.ToLowerInvariant() switch
            {
                "remove" => null,
                "allow" => "allow",
                "deny" => "deny",
                "neutral" => "neutral",
                _ => null
            };
            var current = _userPerms?.Permissions.FirstOrDefault(p => p.PermissionId == item.Id)?.State;
            if (state != current) { dirty = true; break; }
        }
        if (!dirty)
        {
            foreach (var item in RolesList)
            {
                var wasAssigned = _userPerms?.RoleIds.Contains(item.Id) ?? false;
                if (item.IsAssigned != wasAssigned) { dirty = true; break; }
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

        try
        {
            var permsTask = _channelService.RequestPermissionsListAsync();
            var rolesTask = _channelService.RequestRolesListAsync();
            var userPermsTask = _channelService.RequestUserPermissionsAsync(_userId);

            await Task.WhenAll(permsTask, rolesTask, userPermsTask);

            _permissions = (await permsTask)?.Permissions ?? [];
            _roles = (await rolesTask)?.Roles ?? [];
            _userPerms = await userPermsTask;

            BuildPermissionsList();
            BuildRolesList();
            IsLoading = false;
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

    private void BuildPermissionsList()
    {
        PermissionsList.Clear();
        var directStates = _userPerms?.Permissions.ToDictionary(p => p.PermissionId, p => p.State) ?? new Dictionary<string, string>();
        var roleStates = ComputeEffectiveFromRoles();
        foreach (var p in _permissions)
        {
            var current = directStates.GetValueOrDefault(p.Id);
            var desc = p.Description ?? "";
            var (effective, source) = ComputeEffective(p.Id, directStates, roleStates);
            var hasEffective = effective is not null;
            PermissionsList.Add(new PermissionItemViewModel(UpdateDirty)
            {
                Id = p.Id,
                Name = p.Name,
                Description = desc,
                HasDescription = !string.IsNullOrWhiteSpace(desc),
                StateOptions = new[] { "Remove", "Allow", "Deny", "Neutral" },
                SelectedState = string.IsNullOrEmpty(current) ? "Remove" : char.ToUpperInvariant(current[0]) + current[1..],
                EffectiveHint = hasEffective ? $"Effective: {effective} ({source})" : null,
                HasEffectiveHint = hasEffective,
                EffectiveLabel = hasEffective ? $"→ {effective}" : "",
                EffectiveTooltip = hasEffective ? "Computed from direct + roles. Deny overrides Allow." : null
            });
        }
    }

    private Dictionary<string, List<(string State, string Source)>> ComputeEffectiveFromRoles()
    {
        var result = new Dictionary<string, List<(string, string)>>();
        var assignedRoleIds = _userPerms?.RoleIds.ToHashSet() ?? new HashSet<string>();
        foreach (var r in _roles.Where(ro => assignedRoleIds.Contains(ro.Id)))
        {
            foreach (var rp in r.Permissions)
            {
                if (!result.TryGetValue(rp.PermissionId, out var list))
                    result[rp.PermissionId] = list = [];
                list.Add((rp.State.ToLowerInvariant(), r.Name));
            }
        }
        return result;
    }

    private static (string? Effective, string Source) ComputeEffective(string permissionId,
        Dictionary<string, string> directStates,
        Dictionary<string, List<(string State, string Source)>> roleStates)
    {
        var states = new List<(string State, string Source)>();
        if (directStates.TryGetValue(permissionId, out var ds))
            states.Add((ds.ToLowerInvariant(), "direct"));
        if (roleStates.TryGetValue(permissionId, out var rs))
            states.AddRange(rs);
        if (states.Count == 0) return (null, "");
        if (states.Any(s => s.State == "deny")) return ("Deny", "deny overrides");
        if (states.Any(s => s.State == "allow"))
        {
            var src = string.Join(", ", states.Where(s => s.State == "allow").Select(s => s.Source));
            return ("Allow", src);
        }
        return (null, "");
    }

    private void BuildRolesList()
    {
        RolesList.Clear();
        var assignedRoles = _userPerms?.RoleIds.ToHashSet() ?? new HashSet<string>();
        foreach (var r in _roles)
        {
            var perms = r.Permissions.Select(rp => $"{rp.PermissionId}: {rp.State}").ToList();
            var summary = perms.Count > 0 ? "Grants: " + string.Join(", ", perms) : "No permissions";
            RolesList.Add(new RoleItemViewModel(UpdateDirty)
            {
                Id = r.Id,
                Name = r.Name,
                IsAssigned = assignedRoles.Contains(r.Id),
                PermissionSummary = summary,
                DisplayContent = r.Name
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
            foreach (var item in PermissionsList)
            {
                var state = item.SelectedState?.ToLowerInvariant() switch
                {
                    "remove" => null,
                    "allow" => "allow",
                    "deny" => "deny",
                    "neutral" => "neutral",
                    _ => null
                };
                var current = _userPerms?.Permissions.FirstOrDefault(p => p.PermissionId == item.Id)?.State;
                if (state != current)
                    await _channelService.SetUserPermissionAsync(_userId, item.Id, state);
            }

            foreach (var item in RolesList)
            {
                var wasAssigned = _userPerms?.RoleIds.Contains(item.Id) ?? false;
                if (item.IsAssigned != wasAssigned)
                    await _channelService.SetUserRoleAsync(_userId, item.Id, item.IsAssigned);
            }

            _userPerms = await _channelService.RequestUserPermissionsAsync(_userId);
            BuildPermissionsList();
            BuildRolesList();
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

public class PermissionItemViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public PermissionItemViewModel(Action? onChanged = null) => _onChanged = onChanged;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool HasDescription { get; init; }
    public required IReadOnlyList<string> StateOptions { get; init; }
    public string? EffectiveHint { get; init; }
    public bool HasEffectiveHint { get; init; }
    public string EffectiveLabel { get; init; } = "";
    public string? EffectiveTooltip { get; init; }

    private string? _selectedState;
    public string? SelectedState
    {
        get => _selectedState;
        set { _selectedState = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedState))); _onChanged?.Invoke(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class RoleItemViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public RoleItemViewModel(Action? onChanged = null) => _onChanged = onChanged;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? PermissionSummary { get; init; }
    public string DisplayContent { get; init; } = "";

    private bool _isAssigned;
    public bool IsAssigned
    {
        get => _isAssigned;
        set { _isAssigned = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAssigned))); _onChanged?.Invoke(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
