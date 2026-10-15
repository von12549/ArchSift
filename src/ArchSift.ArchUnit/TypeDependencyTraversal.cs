using ArchUnitNET.Domain;

namespace ArchSift.ArchUnit;

internal static class TypeDependencyTraversal
{
    internal static IEnumerable<IType> Targets(IType target, IEnumerable<GenericArgument> genericArguments) =>
        new[] { target }.Concat(genericArguments.SelectMany(GenericTypes));

    private static IEnumerable<IType> GenericTypes(GenericArgument argument)
    {
        var seen = new HashSet<GenericArgument>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<GenericArgument>();
        pending.Push(argument);
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item)) continue;
            yield return item.Type;
            foreach (var child in item.GenericArguments) pending.Push(child);
        }
    }
}
