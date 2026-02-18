using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("UserPermissions")]
public sealed class UserPermissionEntity
{
    [MaxLength(36)]
    public string UserId { get; set; } = null!;

    [MaxLength(64)]
    public string PermissionId { get; set; } = null!;

    public int State { get; set; }
}
