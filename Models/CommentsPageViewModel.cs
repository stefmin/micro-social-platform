using MicroSocialPlatform.Models;

namespace MicroSocialPlatform.ViewModels
{
    public class CommentsPageViewModel
    {
        public int PostId { get; set; }

        public List<Comment> Comments { get; set; } = new();

        public ISet<int> LikedCommentIds { get; set; } = new HashSet<int>();
        public IReadOnlyDictionary<int, int> CommentLikeCounts { get; set; } = new Dictionary<int, int>();

        public bool HasMore { get; set; }
        public long? NextCursorTicks { get; set; }
        public int? NextCursorId { get; set; }

        public int PageSize { get; set; } = 10;
    }
}
