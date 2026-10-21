using ArchSift.Contracts;
using ArchSift.Core;
using Mono.Cecil;

namespace ArchSift.ArchUnit;

// ArchUnitNET 0.13.4 omits compiler-generated types. Attribute-bound async
// bodies therefore contribute dependencies to their original declaring type.
internal sealed record AsyncStateMachineDependency(string SourceAssembly, string SourceType,
    string TargetAssembly, string TargetNamespace, string TargetType)
{
    internal bool Matches(Selector target) => SelectorMatcher.Matches(target,
        target.Kind == "assembly" ? TargetAssembly : target.Kind == "namespace" ? TargetNamespace : TargetType);
}

internal static class AsyncStateMachineDependencies
{
    internal static AsyncStateMachineDependency[] Read(IEnumerable<string> paths)
    {
        var result = new HashSet<AsyncStateMachineDependency>();
        foreach (var path in paths)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(path);
            foreach (var module in assembly.Modules)
            foreach (var owner in Types(module.Types))
            foreach (var method in owner.Methods)
            foreach (var attribute in method.CustomAttributes.Where(a =>
                         a.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute"))
            {
                if (attribute.ConstructorArguments.Count != 1 || attribute.ConstructorArguments[0].Value is not TypeReference reference)
                    throw new InvalidDataException("Invalid async state-machine attribute.");
                // Resolve only within this input module; never search or execute target code.
                var state = owner.NestedTypes.SingleOrDefault(t => t.FullName == reference.GetElementType().FullName);
                // Local-function state machines can inherit generated provenance from
                // their containing display class without their own Generated attribute.
                if (state is null || !state.Interfaces.Any(i =>
                        i.InterfaceType.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine"))
                    throw new InvalidDataException("Async state machine is not a nested IAsyncStateMachine of its owner.");
                var sourceOwner = owner;
                while (sourceOwner.DeclaringType is not null && sourceOwner.CustomAttributes.Any(a =>
                           a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                    sourceOwner = sourceOwner.DeclaringType;
                foreach (var field in state.Fields) Add(field.FieldType);
                foreach (var body in state.Methods)
                {
                    Add(body.ReturnType);
                    foreach (var parameter in body.Parameters) Add(parameter.ParameterType);
                    if (!body.HasBody) continue;
                    foreach (var variable in body.Body.Variables) Add(variable.VariableType);
                    foreach (var handler in body.Body.ExceptionHandlers) if (handler.CatchType is not null) Add(handler.CatchType);
                    foreach (var instruction in body.Body.Instructions)
                        switch (instruction.Operand)
                        {
                            case TypeReference type: Add(type); break;
                            case MethodReference called:
                                Add(called.DeclaringType); Add(called.ReturnType);
                                foreach (var parameter in called.Parameters) Add(parameter.ParameterType);
                                if (called is GenericInstanceMethod generic)
                                    foreach (var argument in generic.GenericArguments) Add(argument);
                                break;
                            case FieldReference field: Add(field.DeclaringType); Add(field.FieldType); break;
                            case CallSite site:
                                Add(site.ReturnType);
                                foreach (var parameter in site.Parameters) Add(parameter.ParameterType);
                                break;
                        }
                }

                void Add(TypeReference type)
                {
                    if (type is GenericParameter) return;
                    if (type is GenericInstanceType generic)
                        foreach (var argument in generic.GenericArguments) Add(argument);
                    if (type is TypeSpecification specification) { Add(specification.ElementType); return; }
                    var targetAssembly = type.Scope is AssemblyNameReference external ? external.Name : assembly.Name.Name;
                    var outer = type; while (outer.DeclaringType is not null) outer = outer.DeclaringType;
                    result.Add(new(assembly.Name.Name, sourceOwner.FullName.Replace('/', '+'), targetAssembly,
                        outer.Namespace, type.FullName.Replace('/', '+')));
                }
            }
        }
        return result.OrderBy(d => d.SourceAssembly, StringComparer.Ordinal).ThenBy(d => d.SourceType, StringComparer.Ordinal)
            .ThenBy(d => d.TargetAssembly, StringComparer.Ordinal).ThenBy(d => d.TargetType, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in Types(type.NestedTypes)) yield return nested;
        }
    }
}
