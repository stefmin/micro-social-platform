namespace MicroSocialPlatform.ViewModels
{

  public class NotificationPageViewModel
  {
    public List<NotificationItemViewModel> Items { get; set; } = new();

    public long? NextCursorTicks { get; set; }
    public int? NextCursorId { get; set; }
    public bool HasMore { get; set; }

    public int PageSize { get; set; } = 15;
  }

  public class NotificationItemViewModel
  {
    public required MicroSocialPlatform.Models.Notification Notification { get; set; }
  }
}
