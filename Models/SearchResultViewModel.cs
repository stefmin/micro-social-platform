namespace MicroSocialPlatform.ViewModels
{
    public class SearchProfileItemViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
        public string FollowStatus { get; set; } = "None"; // "None", "Pending", "Accepted"
    }

    public class SearchProfilePageViewModel
    {
        public List<SearchProfileItemViewModel> Items { get; set; } = new();
        public string? Query { get; set; }

        // Cursor for paging users (NormalizedUserName|Id)
        public string? NextCursor { get; set; }

        public bool HasMore { get; set; }

        // Recommended default
        public int PageSize { get; set; } = 2;
    }
}
