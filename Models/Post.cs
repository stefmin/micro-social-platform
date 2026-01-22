using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
    public class Post
    {
        [Required]
        public int Id { get; set; }
        [Required]
        public string Title { get; set; }
        public string? Description { get; set; }
        [Required]
        public string MediaType { get; set; }
        [Required]
        public string MediaKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required]
        public ApplicationUser User { get; set; }
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();

        public int? GroupId { get; set; }
        public virtual Group? Group { get; set; }
    }
}
