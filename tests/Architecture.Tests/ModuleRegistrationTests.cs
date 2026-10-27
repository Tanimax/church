using System.Reflection;
using ChurchAttendance.Infrastructure;
using ChurchAttendance.SharedKernel;

namespace ChurchAttendance.Architecture.Tests;

// Modules are listed by hand in ModulesSetup.All (see its comment for why). This catches the
// one mistake that list invites: adding a module project and forgetting to list it, which
// would silently leave its services, endpoints and pages out of the app.
public class ModuleRegistrationTests
{
    [Fact]
    public void EveryModuleImplementation_IsListedInModulesSetup()
    {
        var listed = ModulesSetup.All.Select(m => m.GetType()).ToHashSet();

        var unlisted = LoadAppAssemblies()
            .SelectMany(LoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(t))
            .Where(t => !listed.Contains(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(unlisted.Count == 0, "Not listed in ModulesSetup.All: " + string.Join(", ", unlisted));
    }

    [Fact]
    public void ModulesSetup_ListsEachModuleOnce()
    {
        var types = ModulesSetup.All.Select(m => m.GetType()).ToList();

        Assert.Equal(types.Count, types.Distinct().Count());
    }

    // Every assembly in the test output that references SharedKernel: the host and, once
    // they exist, the module projects it references.
    private static IEnumerable<Assembly> LoadAppAssemblies()
    {
        var sharedKernelName = typeof(IModule).Assembly.GetName().Name;

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(AssemblyName.GetAssemblyName(path));
            }
            catch (Exception e) when (e is BadImageFormatException or FileLoadException)
            {
                continue; // native library, or a dependency this test never needs to load
            }

            if (assembly.GetReferencedAssemblies().Any(r => r.Name == sharedKernelName))
            {
                yield return assembly;
            }
        }
    }

    // A type that fails to load could not be listed in ModulesSetup either, so skip it.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }
}
