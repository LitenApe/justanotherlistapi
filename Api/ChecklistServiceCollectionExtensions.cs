using Api.Checklist;
using Api.Sql;
using Checklist.Ports;
using Checklist.UseCases;

namespace Api;

internal static class ChecklistServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Checklist use cases and plugs an adapter into each of their ports (see
    /// specifications/architecture.md). The SQL adapters are scoped, so they share the request's
    /// connection.
    /// </summary>
    internal static IServiceCollection AddChecklist(this IServiceCollection services)
    {
        // Ports
        services.AddScoped<IMembership, SqlMembership>();
        services.AddSingleton<IChecklistNotifier, ChecklistNotifier>();

        // Use cases
        services.AddScoped<GetMembers>();
        services.AddScoped<AddMember>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<SubscribeToItemGroup>();

        return services;
    }
}
