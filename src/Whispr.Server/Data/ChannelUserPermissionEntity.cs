using System.ComponentModel.DataAnnotations.Schema;

namespace Whispr.Server.Data;

[Table("ChannelUserPermissions")]
public sealed class ChannelUserPermissionEntity
{
    public string ChannelId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string PermissionId { get; set; } = null!;
    public int State { get; set; }
}
