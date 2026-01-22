namespace MicroSocialPlatform.Services
{
    /// <summary>
    /// Provides methods to check user-generated content for safety and policy violations.
    /// </summary>
    public interface IContentModerator
    {
        Task<bool> IsHarmfulAsync(string text);
    }
}