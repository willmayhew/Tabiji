using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tabiji.Application.Abstractions;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Repositories;

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
        services.AddScoped<ISavedPlaceRepository, SavedPlaceRepository>();

        return services;
    }
}
