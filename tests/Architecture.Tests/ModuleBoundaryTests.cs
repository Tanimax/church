using System.Reflection;
using NetArchTest.Rules;

namespace ChurchAttendance.Architecture.Tests;

// The rules every extracted module follows:
// - X.Core holds the domain and knows nothing about EF Core or its own Infrastructure.
// - X.Contracts is all anyone outside the module may use.
// Add each new module to Modules below.
public class ModuleBoundaryTests
{
    public sealed record Module(string Name, Assembly Contracts, Assembly Core, Assembly Infrastructure)
    {
        public string CoreNamespace => Core.GetName().Name!;
        public string InfrastructureNamespace => Infrastructure.GetName().Name!;
        public override string ToString() => Name;
    }

    private static readonly Module[] Modules =
    [
        new("Identity",
            typeof(Identity.Contracts.UserRole).Assembly,
            typeof(Identity.Core.User).Assembly,
            typeof(Identity.Infrastructure.IdentityModule).Assembly),
    ];

    // The host's feature code that hasn't been extracted yet. The host's composition code
    // (Infrastructure: ModulesSetup; Components: the layout) may reference a module's
    // Infrastructure to wire it in; feature code must not.
    private static readonly string[] HostFeatureNamespaces =
    [
        "ChurchAttendance.Common",
        "ChurchAttendance.Data",
        "ChurchAttendance.Features",
        "ChurchAttendance.Models",
    ];

    public static TheoryData<Module> AllModules => new(Modules);

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Core_DoesNotDependOnEntityFrameworkOrItsInfrastructure(Module module)
    {
        var result = Types.InAssembly(module.Core)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", module.InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Contracts_DependOnNothingElseInTheModule(Module module)
    {
        var result = Types.InAssembly(module.Contracts)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", module.CoreNamespace, module.InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void HostFeatures_UseOnlyTheModulesContracts(Module module)
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespaceMatching($"^({string.Join("|", HostFeatureNamespaces.Select(n => n.Replace(".", "\\.")))})(\\..*)?$")
            .ShouldNot().HaveDependencyOnAny(module.CoreNamespace, module.InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void OtherModules_UseOnlyTheModulesContracts(Module module)
    {
        foreach (var other in Modules.Where(m => m != module))
        {
            var result = Types.InAssemblies([other.Contracts, other.Core, other.Infrastructure])
                .ShouldNot().HaveDependencyOnAny(module.CoreNamespace, module.InfrastructureNamespace)
                .GetResult();

            Assert.True(result.IsSuccessful, $"{other.Name} → {module.Name}: {Failures(result)}");
        }
    }

    private static string Failures(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
