namespace MicroSocialPlatform.Services
{
    /// <summary>
    /// Cursor-based page result. Next cursor is the last returned item (Ticks, Id).
    /// </summary>
    public class CursorPageResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        public long? NextCursorTicks { get; set; }
        public int? NextCursorId { get; set; }

        public bool HasMore { get; set; }
        public int PageSize { get; set; }
    }
}
