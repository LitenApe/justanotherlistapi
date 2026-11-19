using System.Data;
using System.Reflection;
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

    [Fact]
    public void Checklist_ReferencesNothingButDotNet()
    {
        string[] references = [.. inside.GetReferencedAssemblies().Select(a => a.Name ?? "")];

        Assert.All(references, name =>
            Assert.StartsWith("System.", name, StringComparison.Ordinal)
        );
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
                        && string.Equals(
                            parameter.ParameterType.Namespace,
                            "Checklist.Ports",
                            StringComparison.Ordinal
                        )
                    )
                )
                .Select(Describe),
        ];

        Assert.Empty(others);
    }

    [Fact]
    public void HttpAdapters_NeverTakeADatabaseConnection()
    {
        string[] connections =
        [
            .. InNamespace(outside, "Api.Http")
                .SelectMany(type =>
                    type.GetMethods(Declared)
                        .Cast<MethodBase>()
                        .Concat(type.GetConstructors(Declared))
                )
                .SelectMany(method => method.GetParameters())
                .Where(parameter => typeof(IDbConnection).IsAssignableFrom(parameter.ParameterType))
                .Select(Describe),
        ];

        Assert.Empty(connections);
    }

    private static IEnumerable<Type> InNamespace(Assembly assembly, string ns) =>
        assembly
            .GetTypes()
            .Where(type => string.Equals(type.Namespace, ns, StringComparison.Ordinal));

    private static string Describe(ParameterInfo parameter) =>
        $"{parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}({parameter.ParameterType.Name} {parameter.Name})";
}
