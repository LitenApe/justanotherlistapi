using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using Api.Http;
using Checklist.Domain;

namespace Api.Tests;

/// <summary>
/// Pins the dependency rules in specifications/architecture.md, so breaking one fails a test
/// instead of waiting for a review to notice.
/// </summary>
public sealed class ArchitectureTests
{
    private const BindingFlags Declared =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Static
        | BindingFlags.Instance
        | BindingFlags.DeclaredOnly;

    private static readonly Assembly inside = typeof(Caller).Assembly;
    private static readonly Assembly outside = typeof(CallerExtensions).Assembly;
    private static readonly string[] drivingAdapters =
    [
        "Api.Http",
        "Api.Realtime",
        "Api.Background",
    ];

    // ".NET" means the assemblies that ship with the runtime itself, not just names that start
    // with "System.": code coverage in CI rewrites Checklist.dll and adds a reference to the
    // runtime's netstandard facade. A package, a project or ASP.NET Core still fails this.
    [Fact]
    public void Checklist_ReferencesNothingButDotNet()
    {
        string runtime = RuntimeEnvironment.GetRuntimeDirectory();

        string[] others =
        [
            .. inside
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? "")
                .Where(name => !File.Exists(Path.Combine(runtime, name + ".dll"))),
        ];

        Assert.Empty(others);
    }

    [Fact]
    public void UseCases_TakeOnlyPortsAndTheClock()
    {
        string[] others =
        [
            .. InNamespace(inside, "Checklist.UseCases")
                .Where(type => !type.IsNested)
                .SelectMany(type => type.GetConstructors())
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter =>
                    parameter.ParameterType != typeof(TimeProvider)
                    && !(
                        parameter.ParameterType.IsInterface
                        && IsIn(parameter.ParameterType, "Checklist.Ports")
                    )
                )
                .Select(Describe),
        ];

        Assert.Empty(others);
    }

    // The endpoints, the hub and the cleanup job run use cases. Taking a port, a SQL adapter or a
    // connection instead would skip the membership check and the rules the use cases apply.
    [Fact]
    public void DrivingAdapters_ReachTheInsideOnlyThroughUseCases()
    {
        string[] shortcuts =
        [
            .. drivingAdapters
                .SelectMany(ns => InNamespace(outside, ns))
                .SelectMany(type =>
                    type.GetMethods(Declared)
                        .Cast<MethodBase>()
                        .Concat(type.GetConstructors(Declared))
                )
                .SelectMany(method => method.GetParameters())
                .Where(parameter =>
                    typeof(IDbConnection).IsAssignableFrom(parameter.ParameterType)
                    || IsIn(parameter.ParameterType, "Checklist.Ports")
                    || IsIn(parameter.ParameterType, "Api.Sql")
                )
                .Select(Describe),
        ];

        Assert.Empty(shortcuts);
    }

    private static IEnumerable<Type> InNamespace(Assembly assembly, string ns) =>
        assembly.GetTypes().Where(type => IsIn(type, ns));

    private static bool IsIn(Type type, string ns) =>
        string.Equals(type.Namespace, ns, StringComparison.Ordinal);

    private static string Describe(ParameterInfo parameter) =>
        $"{parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}({parameter.ParameterType.Name} {parameter.Name})";
}
