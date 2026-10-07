using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SearchAChurch.Api.Filters;

public static class RateLimitEndpointExtensions
{
    public static RouteHandlerBuilder AddEndpointFilter<TFilter>(
        this RouteHandlerBuilder builder,
        string routeKey) where TFilter : RateLimitFilter
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var filter = ActivatorUtilities.CreateInstance<RateLimitFilter>(
                invocationContext.HttpContext.RequestServices,
                routeKey);
            return await filter.InvokeAsync(invocationContext, next);
        });
    }

    public static RouteGroupBuilder AddEndpointFilter<TFilter>(
        this RouteGroupBuilder builder,
        string routeKey) where TFilter : RateLimitFilter
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var filter = ActivatorUtilities.CreateInstance<RateLimitFilter>(
                invocationContext.HttpContext.RequestServices,
                routeKey);
            return await filter.InvokeAsync(invocationContext, next);
        });
    }

    public static RouteHandlerBuilder WithRateLimit(
        this RouteHandlerBuilder builder,
        string routeKey)
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var filter = ActivatorUtilities.CreateInstance<RateLimitFilter>(
                invocationContext.HttpContext.RequestServices,
                routeKey);
            return await filter.InvokeAsync(invocationContext, next);
        });
    }

    public static RouteHandlerBuilder WithRateLimit(
        this RouteHandlerBuilder builder,
        RateLimitPolicy policy)
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var filter = ActivatorUtilities.CreateInstance<RateLimitFilter>(
                invocationContext.HttpContext.RequestServices,
                policy);
            return await filter.InvokeAsync(invocationContext, next);
        });
    }
}
