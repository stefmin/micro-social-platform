using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
    public class Group
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Numele grupului nu poate avea mai mult de 100 de caractere")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Descrierea nu poate depasi 500 de caractere")]
        public string? Description { get; set; }

        [Required]
        public string OwnerId { get; set; }
        public virtual ApplicationUser Owner { get; set; }

        public bool IsPublic { get; set; } = true;

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<GroupMember> Members { get; set; } = [];
        public virtual ICollection<Post> Posts { get; set; } = [];
    }
}
