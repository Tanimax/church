using System.Reflection;
using ChurchAttendance.SharedKernel;
using NetArchTest.Rules;

namespace ChurchAttendance.Architecture.Tests;

// SharedKernel is referenced by the host and by every module, so it must stay at the bottom
// of the dependency graph: anything it pulled in would leak into every module.
public class SharedKernelTests
{
    private static readonly Assembly SharedKernel = typeof(IModule).Assembly;

    // Namespaces of the host project (src/Web).
    private static readonly string[] HostNamespaces =
    [
        "ChurchAttendance.Common",
        "ChurchAttendance.Components",
        "ChurchAttendance.Data",
        "ChurchAttendance.Features",
        "ChurchAttendance.Infrastructure",
        "ChurchAttendance.Models",
    ];

    [Fact]
    public void SharedKernel_DoesNotDependOnTheHost()
    {
        var result = Types.InAssembly(SharedKernel)
            .ShouldNot().HaveDependencyOnAny(HostNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void SharedKernel_DoesNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(SharedKernel)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
