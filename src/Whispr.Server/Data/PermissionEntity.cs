using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("Permissions")]
public sealed class PermissionEntity
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = null!;

    [MaxLength(512)]
    public string? Description { get; set; }
}
