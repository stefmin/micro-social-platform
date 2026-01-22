namespace MicroSocialPlatform.Services
{
    /// <summary>
    /// Cursor-based paging request. Cursor is (Ticks, Id) from the last item already loaded.
    /// </summary>
    public class CursorPageRequest
    {
        public int PageSize { get; set; } = 15;

        public long? CursorTicks { get; set; }
        public int? CursorId { get; set; }
    }
}
