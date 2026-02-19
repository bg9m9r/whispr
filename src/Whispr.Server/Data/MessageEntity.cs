using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("Messages")]
public sealed class MessageEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(36)]
    public string ChannelId { get; set; } = null!;

    [Required]
    [MaxLength(36)]
    public string SenderId { get; set; } = null!;

    [Required]
    public string Content { get; set; } = null!;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
