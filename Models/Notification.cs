using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
  public class Notification
  {
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }
    public virtual ApplicationUser User { get; set; }

    public string? ActorId { get; set; }
    public virtual ApplicationUser? Actor { get; set; }

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public int? GroupId { get; set; }
    public virtual Group? Group { get; set; }
  }

  public enum NotificationType
  {
    FollowRequest = 0,
    FollowAccepted = 1,
    NewFollower = 2,
    GroupJoinRequest = 3,
    GroupJoinAccepted = 4
  }
}

