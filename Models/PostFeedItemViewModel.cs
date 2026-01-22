using MicroSocialPlatform.Models;

namespace MicroSocialPlatform.ViewModels
{
    public class PostFeedItemViewModel
    {
        public Post Post { get; set; } = null!;
        public bool CanComment { get; set; }

        // Post heart UI
        public int PostLikeCount { get; set; }
        public bool IsPostLikedByMe { get; set; }

        // Total comments count (because we only load first page of comments)
        public int TotalCommentCount { get; set; }

        // Loaded comments page for display
        public List<Comment> Comments { get; set; } = new();

        // Comment heart UI (for currently loaded comments)
        public ISet<int> LikedCommentIds { get; set; } = new HashSet<int>();
        public IReadOnlyDictionary<int, int> CommentLikeCounts { get; set; } = new Dictionary<int, int>();

        // Paging info for "load more comments"
        public bool CommentsHasMore { get; set; }
        public long? CommentsNextCursorTicks { get; set; }
        public int? CommentsNextCursorId { get; set; }
        public int CommentsPageSize { get; set; } = 5;
    }
}
