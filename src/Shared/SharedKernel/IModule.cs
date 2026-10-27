using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.SharedKernel;

// A self-contained slice of the app (Members, Attendance, ...). The host lists its modules
// explicitly in ModulesSetup and calls these two methods at startup; a module never wires
// itself in. A module's Razor pages are routed through its assembly, which the host adds
// to the Blazor router.
public interface IModule
{
    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder app);
}
