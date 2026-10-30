using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tabiji.Application.Abstractions;
using Tabiji.Application.Membership;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Repositories;
using Tabiji.Infrastructure.Security;

namespace Tabiji.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Tabiji";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The '{ConnectionStringName}' connection string is not configured. " +
                "Set it in appsettings, with 'dotnet user-secrets', " +
                "or via the 'ConnectionStrings__Tabiji' environment variable.");
        }

        services.AddDbContext<TabijiDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ITripRepository, TripRepository>();

        // An invitation that lasts a day is a nuisance; one that lasts a year
        // is a second password. The window is small and explicit.
        var expiryDays = configuration.GetValue("Invitations:ExpiryDays", 7);

        if (expiryDays is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "Invitations:ExpiryDays must be between 1 and 30.");
        }

        services.AddSingleton(new InvitationPolicy(TimeSpan.FromDays(expiryDays)));
        services.AddSingleton<IInvitationTokens, InvitationTokens>();

        return services;
    }
}
