namespace MicroSocialPlatform.ViewModels
{
    /// <summary>
    /// A cursor-paged slice of the feed.
    /// Cursor is (CreatedAtTicks, Id) from the last post in the page.
    /// </summary>
    public class FeedPageViewModel
    {
        public List<PostFeedItemViewModel> Items { get; set; } = new();

        public long? NextCursorTicks { get; set; }
        public int? NextCursorId { get; set; }
        public bool HasMore { get; set; }

        public int PageSize { get; set; } = 15;
    }
}
