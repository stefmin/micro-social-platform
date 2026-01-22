using MicroSocialPlatform.Models;
using System.ComponentModel.DataAnnotations;

public class UserFollow
{
    // tuple primary key
    public string ObserverId { get; set; }
    public virtual ApplicationUser Observer { get; set; }
    public string TargetId { get; set; }
    public virtual ApplicationUser Target { get; set; }

    [Required]
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    [Required]
    public FollowStatus Status { get; set; }
}

public enum FollowStatus
{
    Pending = 0,
    Accepted = 1
}