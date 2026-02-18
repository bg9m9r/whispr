using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("UserRoles")]
public sealed class UserRoleEntity
{
    [MaxLength(36)]
    public string UserId { get; set; } = null!;

    [MaxLength(64)]
    public string RoleId { get; set; } = null!;
}
