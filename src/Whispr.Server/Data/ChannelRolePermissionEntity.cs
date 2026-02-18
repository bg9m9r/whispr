using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("ChannelRolePermissions")]
public sealed class ChannelRolePermissionEntity
{
    public string ChannelId { get; set; } = null!;
    public string RoleId { get; set; } = null!;
    public string PermissionId { get; set; } = null!;
    public int State { get; set; }
}
