using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
    public class Comment
    {
        [Required]
        public int Id { get; set; }
        [Required]
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required]
        public Post Post { get; set; } = null!;
        [Required]
        public ApplicationUser User { get; set; } 
        public ICollection<Like> Likes { get; set; } = new List<Like>();

    }
}
