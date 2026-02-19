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

    /// <summary>Empty for text channels (no audio).</summary>
    [Required]
    public byte[] KeyMaterial { get; set; } = null!;

    public bool IsDefault { get; set; }

    /// <summary>0 = Voice, 1 = Text.</summary>
    public int ChannelType { get; set; }
}
