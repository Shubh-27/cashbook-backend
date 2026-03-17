using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.common.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.common.Extensions
{
    public static class QueryExtensions
    {
        public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, List<FilterRequestModel> filters)
        {
            if (filters == null || filters.Count == 0)
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? combinedExpression = null;

            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Key) || string.IsNullOrEmpty(filter.Condition))
                    continue;

                var property = GetPropertyExpression(parameter, filter.Key);
                if (property == null) continue;

                var expression = BuildFilterExpression(property, filter);
                if (expression == null) continue;

                if (combinedExpression == null)
                    combinedExpression = expression;
                else
                    combinedExpression = Expression.AndAlso(combinedExpression, expression);
            }

            if (combinedExpression == null)
                return query;

            var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
            return query.Where(lambda);
        }

        public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string? sortBy, string? sortOrder)
        {
            if (string.IsNullOrEmpty(sortBy))
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = GetPropertyExpression(parameter, sortBy);
            if (property == null) return query;

            var lambda = Expression.Lambda(property, parameter);
            var methodName = (sortOrder?.ToLower() == "desc") ? "OrderByDescending" : "OrderBy";

            var resultExpression = Expression.Call(typeof(Queryable), methodName,
                new Type[] { typeof(T), property.Type },
                query.Expression, Expression.Quote(lambda));

            return query.Provider.CreateQuery<T>(resultExpression);
        }

        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page, int pageSize)
        {
            var totalCount = await query.CountAsync();
            
            if (pageSize == -1)
            {
                return new PagedResult<T>
                {
                    Data = await query.ToListAsync(),
                    TotalCount = totalCount,
                    Page = 1,
                    PageSize = totalCount
                };
            }

            var p = page > 0 ? page : 1;
            var ps = pageSize > 0 ? pageSize : 50;

            var data = await query.Skip((p - 1) * ps).Take(ps).ToListAsync();

            return new PagedResult<T>
            {
                Data = data,
                TotalCount = totalCount,
                Page = p,
                PageSize = ps
            };
        }

        private static Expression? GetPropertyExpression(Expression parameter, string propertyName)
        {
            Expression property = parameter;
            foreach (var member in propertyName.Split('.'))
            {
                var type = property.Type;
                // First try direct match (PascalCase or IgnoreCase)
                var propInfo = type.GetProperty(member, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                
                // If not found, try matching by JsonPropertyName attribute (for snake_case mapping)
                if (propInfo == null)
                {
                    propInfo = type.GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == member);
                }

                if (propInfo == null) return null;
                property = Expression.Property(property, propInfo);
            }
            return property;
        }

        private static Expression? BuildFilterExpression(Expression property, FilterRequestModel filter)
        {
            var condition = filter.Condition?.ToLower();
            var type = property.Type;
            var isString = type == typeof(string);

            switch (condition)
            {
                case "equals":
                    return Expression.Equal(property, Expression.Constant(ConvertValue(filter.Value, type)));

                case "contains":
                    if (!isString) return null;
                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                    if (containsMethod == null) return null;
                    return Expression.Call(property, containsMethod, Expression.Constant(filter.Value?.ToString()));

                case "greater_than":
                    if (isString)
                    {
                        var compareMethod = typeof(string).GetMethod("CompareTo", new[] { typeof(string) });
                        if (compareMethod == null) return null;
                        return Expression.GreaterThan(Expression.Call(property, compareMethod, Expression.Constant(filter.Value?.ToString())), Expression.Constant(0));
                    }
                    return Expression.GreaterThan(property, Expression.Constant(ConvertValue(filter.Value, type)));

                case "less_than":
                    if (isString)
                    {
                        var compareMethod = typeof(string).GetMethod("CompareTo", new[] { typeof(string) });
                        if (compareMethod == null) return null;
                        return Expression.LessThan(Expression.Call(property, compareMethod, Expression.Constant(filter.Value?.ToString())), Expression.Constant(0));
                    }
                    return Expression.LessThan(property, Expression.Constant(ConvertValue(filter.Value, type)));

                case "between":
                    var fromVal = ConvertValue(filter.From, type);
                    var toVal = ConvertValue(filter.To, type);
                    if (fromVal == null || toVal == null) return null;

                    if (isString)
                    {
                        var compareMethod = typeof(string).GetMethod("CompareTo", new[] { typeof(string) });
                        if (compareMethod == null) return null;
                        var leftStr = Expression.GreaterThanOrEqual(Expression.Call(property, compareMethod, Expression.Constant(fromVal.ToString())), Expression.Constant(0));
                        var rightStr = Expression.LessThanOrEqual(Expression.Call(property, compareMethod, Expression.Constant(toVal.ToString())), Expression.Constant(0));
                        return Expression.AndAlso(leftStr, rightStr);
                    }

                    var left = Expression.GreaterThanOrEqual(property, Expression.Constant(fromVal));
                    var right = Expression.LessThanOrEqual(property, Expression.Constant(toVal));
                    return Expression.AndAlso(left, right);

                default:
                    return null;
            }
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                            return element.GetDateTime();
                        return element.GetString();
                    case JsonValueKind.Number:
                        if (targetType == typeof(int) || targetType == typeof(int?))
                            return element.GetInt32();
                        if (targetType == typeof(double) || targetType == typeof(double?))
                            return element.GetDouble();
                        if (targetType == typeof(long) || targetType == typeof(long?))
                            return element.GetInt64();
                        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                            return element.GetDecimal();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return element.GetBoolean();
                }
            }

            try
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                return Convert.ChangeType(value.ToString(), underlyingType);
            }
            catch
            {
                return null;
            }
        }
    }
}
