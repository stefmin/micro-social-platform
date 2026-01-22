using MicroSocialPlatform.Models;
using System.ComponentModel.DataAnnotations;

public class GroupMember
{
    // composite primary key
    public int GroupId { get; set; }
    public virtual Group Group { get; set; }
    public string UserId { get; set; }
    public virtual ApplicationUser User { get; set; }

    [Required]
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;

    [Required]
    public MemberStatus Status { get; set; }
}

public enum MemberStatus
{
    Pending = 0,
    Accepted = 1
}
