using MicroSocialPlatform.Models;

namespace MicroSocialPlatform.ViewModels
{
    public class GroupsPageViewModel
    {
        public List<Group> Items { get; set; } = new();
        public string? Search { get; set; }

        public long? NextCursorTicks { get; set; }
        public int? NextCursorId { get; set; }
        public bool HasMore { get; set; }

        public int PageSize { get; set; } = 2;
    }
}
