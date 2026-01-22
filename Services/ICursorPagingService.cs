using System.Linq.Expressions;

namespace MicroSocialPlatform.Services
{
    public interface ICursorPagingService
    {
        Task<CursorPageResult<T>> PageAsync<T>(
            IQueryable<T> source,
            CursorPageRequest request,
            Expression<Func<T, DateTime>> createdAtSelector,
            Expression<Func<T, int>> idSelector,
            bool descending = true,
            CancellationToken cancellationToken = default);
    }
}
