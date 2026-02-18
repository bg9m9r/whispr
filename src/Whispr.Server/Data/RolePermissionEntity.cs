using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("RolePermissions")]
public sealed class RolePermissionEntity
{
    [MaxLength(64)]
    public string RoleId { get; set; } = null!;

    [MaxLength(64)]
    public string PermissionId { get; set; } = null!;

    public int State { get; set; }
}
