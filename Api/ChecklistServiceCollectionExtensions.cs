using Api.Accounts;
using Api.AuditLog;
using Api.Email;
using Api.Realtime;
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
        services.AddScoped<IItemStore, SqlItemStore>();
        services.AddScoped<IItemGroupStore, SqlItemGroupStore>();
        services.AddSingleton<IChecklistNotifier, ChecklistNotifier>();
        services.AddSingleton<IAuditTrail, AuditTrail>();
        services.AddAccounts();
        services.AddSingleton<IMailer, SmtpMailer>();

        // Use cases
        services.AddScoped<SignUp>();
        services.AddScoped<SignIn>();
        services.AddScoped<RequestPasswordReset>();
        services.AddScoped<ResetPassword>();
        services.AddScoped<GetItemGroups>();
        services.AddScoped<GetItemGroup>();
        services.AddScoped<CreateItemGroup>();
        services.AddScoped<UpdateItemGroup>();
        services.AddScoped<UpdateItemGroupSettings>();
        services.AddScoped<DeleteItemGroup>();
        services.AddScoped<AutoDeleteItemGroups>();
        services.AddScoped<GetMembers>();
        services.AddScoped<AddMember>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<SubscribeToItemGroup>();
        services.AddScoped<CreateItem>();
        services.AddScoped<UpdateItem>();
        services.AddScoped<DeleteItem>();
        services.AddScoped<ReserveItem>();
        services.AddScoped<UnreserveItem>();

        return services;
    }
}
