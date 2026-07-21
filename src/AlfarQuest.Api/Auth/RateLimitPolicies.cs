using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace AlfarQuest.Api.Auth;

/// <summary>Throttling for the endpoints worth guessing at.
///
/// A strong hash makes an offline attack expensive; it does nothing about someone
/// trying ten thousand passwords through the front door. This is what makes that
/// pointless — and it is also the reason the same hash cost does not become a way
/// to exhaust the server's CPU on demand.</summary>
public static class RateLimitPolicies
{
    public const string Authentication = "auth";

    public static IServiceCollection AddAuthenticationRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(Authentication, http =>
            {
                var options = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    // Partitioned by caller address. The body is still an unread
                    // stream at this point, so the target account is not knowable
                    // here — this caps how fast one source can guess, not how
                    // often one account can be guessed at. Behind a proxy it needs
                    // UseForwardedHeaders, or every caller shares one bucket.
                    partitionKey: $"ip:{http.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.SignInAttemptsPerWindow,
                        Window = options.SignInWindow,
                        QueueLimit = 0,          // refuse immediately; queuing would just delay the answer
                    });
            });
        });
}
