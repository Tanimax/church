using ChurchAttendance.Infrastructure;
using ChurchAttendance.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Startup;

public class ModulesSetupTests
{
    private sealed class FakeService;

    private sealed class FakeModule : IModule
    {
        public void Register(IServiceCollection services, IConfiguration configuration) =>
            services.AddSingleton<FakeService>();

        public void MapEndpoints(IEndpointRouteBuilder app) =>
            app.MapGet("/fake-module", () => "ok");
    }

    [Fact]
    public void AddModules_And_MapModuleEndpoints_WireEachModuleIn()
    {
        IModule[] modules = [new FakeModule()];
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddModules(builder.Configuration, modules);
        var app = builder.Build();
        app.MapModuleEndpoints(modules);

        Assert.NotNull(app.Services.GetService<FakeService>());
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText);
        Assert.Contains("/fake-module", routes);
    }

    [Fact]
    public void RazorAssembliesOf_ReturnsModuleAssembliesOnce_AndNeverTheHost()
    {
        IModule[] modules = [new FakeModule(), new FakeModule()];

        var assemblies = ModulesSetup.RazorAssembliesOf(modules);

        Assert.Equal(new[] { typeof(FakeModule).Assembly }, assemblies);
        Assert.DoesNotContain(typeof(Program).Assembly, assemblies);
    }
}
