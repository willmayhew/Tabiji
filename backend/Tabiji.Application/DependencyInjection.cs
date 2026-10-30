using Microsoft.Extensions.DependencyInjection;
using Tabiji.Application.Membership;
using Tabiji.Application.Trips;

namespace Tabiji.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TripService>();
        services.AddScoped<SavedPlaceService>();
        services.AddScoped<TripMembershipService>();
        services.AddScoped<InvitationService>();

        return services;
    }
}
