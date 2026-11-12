using Mono.Cecil;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace Backend.Tests.Architect;

/// <summary>
/// Helper for resolving type dependencies from compiled assemblies using Mono.Cecil.
/// Used by architecture tests to analyze cross-feature references.
/// </summary>
public static class ArchitectHelper
{
    /// <summary>
    /// Recursively resolves all type dependencies (inheritance, fields, properties, methods, parameters, and IL instructions) for a given type.
    /// </summary>
    public static IReadOnlyList<TypeDefinition> GetDependencies(TypeDefinition type)
    {
        var dependencies = new HashSet<TypeDefinition>();

        // Recursive local function to process types and their generic arguments.
        void AddDependenciesFrom(TypeReference? typeRef)
        {
            if (typeRef == null) return;

            // 1. Resolve the main type and add it as a dependency.
            var resolvedType = typeRef.Resolve();
            if (resolvedType != null)
            {
                dependencies.Add(resolvedType);
            }

            // 2. If it's a generic type, recursively process its arguments.
            if (typeRef is GenericInstanceType genericInstance)
            {
                foreach (var genericArg in genericInstance.GenericArguments)
                {
                    AddDependenciesFrom(genericArg);
                }
            }
        }

        // --- Traverse the inheritance hierarchy ---
        var currentType = type;
        while (currentType != null && currentType.FullName != "System.Object")
        {
            // Interfaces
            if (currentType.HasInterfaces)
            {
                foreach (var i in currentType.Interfaces)
                {
                    AddDependenciesFrom(i.InterfaceType);
                }
            }

            // Fields
            if (currentType.HasFields)
            {
                foreach (var field in currentType.Fields)
                {
                    AddDependenciesFrom(field.FieldType);
                }
            }

            // Properties
            if (currentType.HasProperties)
            {
                foreach (var property in currentType.Properties)
                {
                    AddDependenciesFrom(property.PropertyType);
                }
            }

            // Methods (including constructors)
            if (currentType.HasMethods)
            {
                foreach (var method in currentType.Methods)
                {
                    // We only care about methods declared in the current type of the hierarchy
                    if (method.DeclaringType != currentType) continue;

                    // Return type
                    AddDependenciesFrom(method.ReturnType);

                    // Parameters
                    foreach (var parameter in method.Parameters)
                    {
                        AddDependenciesFrom(parameter.ParameterType);
                    }

                    // Method body locals and instructions
                    if (method.HasBody)
                    {
                        foreach (var variable in method.Body.Variables)
                        {
                            AddDependenciesFrom(variable.VariableType);
                        }

                        foreach (var instruction in method.Body.Instructions)
                        {
                            switch (instruction.Operand)
                            {
                                case MethodReference methodReference:
                                    AddDependenciesFrom(methodReference.DeclaringType);
                                    // Also check generic arguments of the method call itself
                                    if (methodReference is GenericInstanceMethod genericMethod)
                                    {
                                        foreach (var arg in genericMethod.GenericArguments)
                                        {
                                            AddDependenciesFrom(arg);
                                        }
                                    }
                                    break;
                                case FieldReference fieldReference:
                                    AddDependenciesFrom(fieldReference.DeclaringType);
                                    break;
                                case TypeReference typeReference:
                                    AddDependenciesFrom(typeReference);
                                    break;
                            }
                        }
                    }
                }
            }

            // Move up to the base class for the next iteration.
            currentType = currentType.BaseType?.Resolve();
        }

        // Filter out the type itself and return the final list.
        return [.. dependencies.Where(d => d != type)];
    }
}