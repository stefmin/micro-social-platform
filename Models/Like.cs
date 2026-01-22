using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
    /// <summary>
    /// Instagram-style "heart" like. A Like belongs to exactly one target:
    /// either a Post or a Comment.
    ///
    /// Notes:
    /// - This entity intentionally uses FK scalar properties (UserId/PostId/CommentId)
    ///   to support efficient lookups and DB-level unique constraints.
    /// - Enforce "only like once" with unique indexes:
    ///     (UserId, PostId) where PostId is NOT NULL
    ///     (UserId, CommentId) where CommentId is NOT NULL
    /// </summary>
    public class Like
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Navigation is optional in code (we often set only UserId for speed).
        public ApplicationUser? User { get; set; }

        public int? PostId { get; set; }
        public Post? Post { get; set; }

        public int? CommentId { get; set; }
        public Comment? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
