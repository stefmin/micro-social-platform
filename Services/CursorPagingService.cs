using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MicroSocialPlatform.Services
{
    /// <summary>
    /// Generic cursor paginator:
    /// - Order by CreatedAt then Id (asc or desc)
    /// - Apply cursor filter (older/newer depending on sort direction)
    /// - Take PageSize + 1 to compute HasMore
    /// - Return NextCursor from last item
    /// </summary>
    public class CursorPagingService : ICursorPagingService
    {
        private const int DefaultPageSize = 15;
        private const int MaxPageSize = 50;

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0) return DefaultPageSize;
            return Math.Clamp(pageSize, 1, MaxPageSize);
        }

        public async Task<CursorPageResult<T>> PageAsync<T>(
            IQueryable<T> source,
            CursorPageRequest request,
            Expression<Func<T, DateTime>> createdAtSelector,
            Expression<Func<T, int>> idSelector,
            bool descending = true,
            CancellationToken cancellationToken = default)
        {
            var pageSize = NormalizePageSize(request.PageSize);

            // Apply cursor filter if present
            if (request.CursorTicks.HasValue && request.CursorId.HasValue)
            {
                var cursorTime = new DateTime(request.CursorTicks.Value, DateTimeKind.Utc);
                var cursorId = request.CursorId.Value;

                var predicate = BuildCursorPredicate(createdAtSelector, idSelector, cursorTime, cursorId, descending);
                source = source.Where(predicate);
            }

            // Apply ordering
            source = descending
                ? source.OrderByDescending(createdAtSelector).ThenByDescending(idSelector)
                : source.OrderBy(createdAtSelector).ThenBy(idSelector);

            // Take +1 to detect HasMore
            var list = await source.Take(pageSize + 1).ToListAsync(cancellationToken);
            var hasMore = list.Count > pageSize;

            if (hasMore)
                list.RemoveAt(list.Count - 1);

            long? nextTicks = null;
            int? nextId = null;

            if (list.Count > 0)
            {
                // Next cursor = last returned item
                var createdAtFunc = createdAtSelector.Compile();
                var idFunc = idSelector.Compile();

                var last = list[^1];
                nextTicks = createdAtFunc(last).Ticks;
                nextId = idFunc(last);
            }

            return new CursorPageResult<T>
            {
                Items = list,
                HasMore = hasMore,
                NextCursorTicks = nextTicks,
                NextCursorId = nextId,
                PageSize = pageSize
            };
        }

        private static Expression<Func<T, bool>> BuildCursorPredicate<T>(
            Expression<Func<T, DateTime>> createdAtSelector,
            Expression<Func<T, int>> idSelector,
            DateTime cursorTimeUtc,
            int cursorId,
            bool descending)
        {
            // Normalize both selectors to share the same parameter
            var param = Expression.Parameter(typeof(T), "x");

            var createdAt = ReplaceParameter(createdAtSelector, param);
            var id = ReplaceParameter(idSelector, param);

            var cursorTimeConst = Expression.Constant(cursorTimeUtc, typeof(DateTime));
            var cursorIdConst = Expression.Constant(cursorId, typeof(int));

            // For DESC: get items "older" than cursor:
            // CreatedAt < cursorTime OR (CreatedAt == cursorTime AND Id < cursorId)
            //
            // For ASC: get items "newer" than cursor:
            // CreatedAt > cursorTime OR (CreatedAt == cursorTime AND Id > cursorId)

            Expression timeCmp;
            Expression idCmp;

            if (descending)
            {
                timeCmp = Expression.LessThan(createdAt, cursorTimeConst);
                idCmp = Expression.LessThan(id, cursorIdConst);
            }
            else
            {
                timeCmp = Expression.GreaterThan(createdAt, cursorTimeConst);
                idCmp = Expression.GreaterThan(id, cursorIdConst);
            }

            var timeEq = Expression.Equal(createdAt, cursorTimeConst);
            var tieBreak = Expression.AndAlso(timeEq, idCmp);

            var body = Expression.OrElse(timeCmp, tieBreak);
            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        private static Expression ReplaceParameter<T, TProp>(Expression<Func<T, TProp>> expr, ParameterExpression newParam)
        {
            var visitor = new ReplaceParameterVisitor(expr.Parameters[0], newParam);
            return visitor.Visit(expr.Body)!;
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParam;
            private readonly ParameterExpression _newParam;

            public ReplaceParameterVisitor(ParameterExpression oldParam, ParameterExpression newParam)
            {
                _oldParam = oldParam;
                _newParam = newParam;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == _oldParam ? _newParam : base.VisitParameter(node);
        }
    }
}
