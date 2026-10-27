using System.Reflection;
using ChurchAttendance.SharedKernel;

namespace ChurchAttendance.Infrastructure;

// The modules the app is made of, in registration order. Listed by hand rather than found
// by reflection so startup order stays visible and deterministic (each module will later
// apply its own migrations, and Identity must run before Reporting). Empty until the first
// feature is extracted out of Features/ into its own module.
public static class ModulesSetup
{
    public static IReadOnlyList<IModule> All { get; } = [];

    // Declared after All: static initializers run in textual order.
    public static Assembly[] RazorAssemblies { get; } = RazorAssembliesOf(All);

    // Assemblies whose @page components the Blazor router and endpoint routing must scan.
    // The host's own assembly is excluded: it is already the router's AppAssembly, and
    // listing it twice would register every page twice.
    public static Assembly[] RazorAssembliesOf(IEnumerable<IModule> modules) =>
        modules.Select(m => m.GetType().Assembly)
            .Where(a => a != typeof(Program).Assembly)
            .Distinct()
            .ToArray();

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.Register(services, configuration);
        }
        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }
        return app;
    }
}
