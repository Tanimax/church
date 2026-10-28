using System.Reflection;
using NetArchTest.Rules;

namespace ChurchAttendance.Architecture.Tests;

// The dependency rule of the layered structure (src/Domain, Application, Infrastructure, Api):
// every layer only depends on the layers inside it. SharedKernel sits outside the circle and
// may be used by any layer, so it must not depend on any of them.
public class LayerTests
{
    private static readonly Assembly Domain = typeof(ChurchAttendance.Domain.Member).Assembly;
    private static readonly Assembly Application = typeof(ChurchAttendance.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(ChurchAttendance.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly SharedKernel = typeof(ChurchAttendance.SharedKernel.ChurchClock).Assembly;

    private const string DomainNamespace = "ChurchAttendance.Domain";
    private const string ApplicationNamespace = "ChurchAttendance.Application";
    private const string InfrastructureNamespace = "ChurchAttendance.Infrastructure";

    // The Api project keeps the ChurchAttendance.* namespaces it had before the split.
    private static readonly string[] ApiNamespaces =
    [
        "ChurchAttendance.Components",
        "ChurchAttendance.Features",
        "ChurchAttendance.Startup",
    ];

    [Fact]
    public void Domain_DependsOnNoOtherLayerNorFramework()
    {
        AssertNoDependency(Domain,
        [
            ApplicationNamespace, InfrastructureNamespace, .. ApiNamespaces,
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
        ]);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        AssertNoDependency(Application, [InfrastructureNamespace, .. ApiNamespaces, "Npgsql", "Microsoft.AspNetCore"]);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        AssertNoDependency(Infrastructure, ApiNamespaces);
    }

    [Fact]
    public void SharedKernel_DependsOnNoLayer()
    {
        AssertNoDependency(SharedKernel,
        [
            DomainNamespace, ApplicationNamespace, InfrastructureNamespace, .. ApiNamespaces,
            "Microsoft.EntityFrameworkCore",
        ]);
    }

    // Pages and endpoints go through Application (use cases, IAppDbContext); only the
    // composition root (Program.cs, Startup/) knows Infrastructure.
    [Fact]
    public void ApiPagesAndEndpoints_DoNotUseInfrastructure()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespaceMatching(@"^ChurchAttendance\.(Components|Features)(\..*)?$")
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static void AssertNoDependency(Assembly assembly, string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
