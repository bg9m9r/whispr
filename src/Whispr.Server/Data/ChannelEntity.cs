using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("Channels")]
public sealed class ChannelEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = null!;

    [Required]
    public byte[] KeyMaterial { get; set; } = null!;

    public bool IsDefault { get; set; }
}
