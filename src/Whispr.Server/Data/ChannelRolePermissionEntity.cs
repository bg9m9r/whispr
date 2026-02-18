using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("ChannelRolePermissions")]
public sealed class ChannelRolePermissionEntity
{
    [MaxLength(256)]
    public string ChannelId { get; set; } = null!;
    [MaxLength(256)]
    public string RoleId { get; set; } = null!;
    [MaxLength(256)]
    public string PermissionId { get; set; } = null!;
    public int State { get; set; }
}
