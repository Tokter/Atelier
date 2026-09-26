using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Atelier.Generators;

/// <summary>
/// Generates reflection-free property descriptors (<c>IInspectableObject.GetProperties</c>) for types marked with
/// <c>[Inspectable]</c>, for use by the PropertyGrid and other inspector tools.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class InspectableGenerator : IIncrementalGenerator
{
    private const string InspectableAttributeName = "Atelier.Core.Inspection.InspectableAttribute";
    private const string InspectablePropertyAttributeName = "Atelier.Core.Inspection.InspectablePropertyAttribute";
    private const string InspectableIgnoreAttributeName = "Atelier.Core.Inspection.InspectableIgnoreAttribute";

    private static readonly DiagnosticDescriptor PartialModifierRequiredRule = new(
        id: "ATL001",
        title: "Type must be declared as partial",
        messageFormat: "The type '{0}' is marked with [Inspectable] and must be declared as partial to generate inspection metadata",
        category: "Atelier.Inspection",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inspectableTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            InspectableAttributeName,
            predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, ct) => GetInspectableModel(ctx, ct)
        );

        context.RegisterSourceOutput(inspectableTypes, static (spc, model) =>
        {
            if (model is null) return;

            if (model.Diagnostics.Count > 0)
            {
                foreach (var diag in model.Diagnostics)
                {
                    spc.ReportDiagnostic(diag);
                }
                return;
            }

            string source = GenerateSource(model);
            spc.AddSource(model.HintName, SourceText.From(source, Encoding.UTF8));
        });
    }

    private static InspectableTypeModel? GetInspectableModel(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var diagnostics = new List<Diagnostic>();

        // 1. Verify partial keyword on all declarations
        bool isPartial = true;
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax(ct);
            if (node is TypeDeclarationSyntax typeDecl)
            {
                if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    isPartial = false;
                    diagnostics.Add(Diagnostic.Create(
                        PartialModifierRequiredRule,
                        typeDecl.Identifier.GetLocation(),
                        typeSymbol.Name));
                }
            }
        }

        if (!isPartial)
        {
            return new InspectableTypeModel(
                typeSymbol.Name,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<InspectableOuterType>(),
                Array.Empty<InspectablePropertyModel>(),
                $"{typeSymbol.Name}_Error.g.cs",
                diagnostics);
        }

        // 2. Extract Type Kind (class, struct, record class, record struct)
        string typeKind;
        if (typeSymbol.IsRecord)
        {
            typeKind = typeSymbol.IsValueType ? "record struct" : "record class";
        }
        else
        {
            typeKind = typeSymbol.IsValueType ? "struct" : "class";
        }

        // 3. Extract Namespace
        string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        // 4. Extract Generic Type Parameters and Constraints
        string typeParameters = string.Empty;
        var constraintClauses = new List<string>();
        if (typeSymbol.TypeParameters.Length > 0)
        {
            typeParameters = "<" + string.Join(", ", typeSymbol.TypeParameters.Select(static p => p.Name)) + ">";
            foreach (var tp in typeSymbol.TypeParameters)
            {
                var constraints = new List<string>();
                if (tp.HasReferenceTypeConstraint) constraints.Add("class");
                if (tp.HasValueTypeConstraint) constraints.Add("struct");
                if (tp.HasNotNullConstraint) constraints.Add("notnull");
                if (tp.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
                foreach (var ctType in tp.ConstraintTypes)
                {
                    constraints.Add(ctType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
                if (tp.HasConstructorConstraint) constraints.Add("new()");

                if (constraints.Count > 0)
                {
                    constraintClauses.Add($"where {tp.Name} : {string.Join(", ", constraints)}");
                }
            }
        }

        // 5. Extract Outer / Nesting types
        var outerTypes = new List<InspectableOuterType>();
        var outerSymbol = typeSymbol.ContainingType;
        while (outerSymbol != null)
        {
            string outerKind = outerSymbol.IsRecord
                ? (outerSymbol.IsValueType ? "record struct" : "record class")
                : (outerSymbol.IsValueType ? "struct" : "class");

            string outerTp = outerSymbol.TypeParameters.Length > 0
                ? "<" + string.Join(", ", outerSymbol.TypeParameters.Select(static p => p.Name)) + ">"
                : string.Empty;

            outerTypes.Add(new InspectableOuterType(outerSymbol.Name, outerKind, outerTp));
            outerSymbol = outerSymbol.ContainingType;
        }
        outerTypes.Reverse();

        // 6. Extract Properties
        var properties = new List<InspectablePropertyModel>();
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var currentType = typeSymbol;

        while (currentType != null &&
               currentType.SpecialType != SpecialType.System_Object &&
               currentType.SpecialType != SpecialType.System_ValueType)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;

                if (prop.IsStatic || prop.IsIndexer)
                    continue;

                if (prop.DeclaredAccessibility != Accessibility.Public)
                    continue;

                if (prop.GetMethod == null || prop.GetMethod.DeclaredAccessibility != Accessibility.Public)
                    continue;

                if (!seenPropertyNames.Add(prop.Name))
                    continue; // Shadowed or overridden

                if (IsPropertyIgnored(prop))
                    continue;

                properties.Add(ExtractPropertyModel(prop));
            }

            currentType = currentType.BaseType;
        }

        properties.Sort((a, b) => a.Order.CompareTo(b.Order));

        string targetTypeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string hintName = GenerateHintName(typeSymbol);

        return new InspectableTypeModel(
            typeSymbol.Name,
            typeKind,
            ns,
            targetTypeFullName,
            typeParameters,
            constraintClauses,
            outerTypes,
            properties,
            hintName,
            diagnostics);
    }

    private static bool IsPropertyIgnored(IPropertySymbol prop)
    {
        foreach (var attr in prop.GetAttributes())
        {
            string attrName = attr.AttributeClass?.ToDisplayString() ?? string.Empty;
            if (attrName == InspectableIgnoreAttributeName ||
                attrName == "InspectableIgnoreAttribute" ||
                attrName == "InspectableIgnore")
            {
                return true;
            }

            // Also honor [System.ComponentModel.Browsable(false)]
            if ((attrName == "System.ComponentModel.BrowsableAttribute" || attrName == "BrowsableAttribute") &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is false)
            {
                return true;
            }
        }

        return false;
    }

    private static InspectablePropertyModel ExtractPropertyModel(IPropertySymbol prop)
    {
        string name = prop.Name;
        string displayName = name;
        string category = "General";
        int order = 0;
        bool isReadOnlyExplicit = false;

        // Check attributes
        foreach (var attr in prop.GetAttributes())
        {
            string attrName = attr.AttributeClass?.ToDisplayString() ?? string.Empty;

            if (attrName == InspectablePropertyAttributeName ||
                attrName == "InspectablePropertyAttribute" ||
                attrName == "InspectableProperty")
            {
                if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string dn && !string.IsNullOrEmpty(dn))
                {
                    displayName = dn;
                }
                if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Value is string cat && !string.IsNullOrEmpty(cat))
                {
                    category = cat;
                }

                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "DisplayName" && named.Value.Value is string ndn && !string.IsNullOrEmpty(ndn))
                    {
                        displayName = ndn;
                    }
                    else if (named.Key == "Category" && named.Value.Value is string ncat && !string.IsNullOrEmpty(ncat))
                    {
                        category = ncat;
                    }
                    else if (named.Key == "Order" && named.Value.Value is int ord)
                    {
                        order = ord;
                    }
                    else if (named.Key == "IsReadOnly" && named.Value.Value is bool ro)
                    {
                        isReadOnlyExplicit = ro;
                    }
                }
            }
            else if (attrName == "System.ComponentModel.DisplayNameAttribute" || attrName == "DisplayNameAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string dn && !string.IsNullOrEmpty(dn))
                {
                    displayName = dn;
                }
            }
            else if (attrName == "System.ComponentModel.CategoryAttribute" || attrName == "CategoryAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string cat && !string.IsNullOrEmpty(cat))
                {
                    category = cat;
                }
            }
            else if (attrName == "System.ComponentModel.ReadOnlyAttribute" || attrName == "ReadOnlyAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is bool ro)
                {
                    isReadOnlyExplicit = ro;
                }
            }
            else if (attrName == "System.ComponentModel.DataAnnotations.DisplayAttribute" || attrName == "DisplayAttribute")
            {
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "Name" && named.Value.Value is string dn && !string.IsNullOrEmpty(dn))
                    {
                        displayName = dn;
                    }
                    else if (named.Key == "GroupName" && named.Value.Value is string cat && !string.IsNullOrEmpty(cat))
                    {
                        category = cat;
                    }
                    else if (named.Key == "Order" && named.Value.Value is int ord)
                    {
                        order = ord;
                    }
                }
            }
        }

        bool hasPublicSetter = prop.SetMethod != null &&
                               prop.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                               !prop.SetMethod.IsInitOnly;

        bool isReadOnly = isReadOnlyExplicit || !hasPublicSetter;

        string propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new InspectablePropertyModel(name, displayName, category, order, isReadOnly, propType);
    }

    private static string GenerateHintName(INamedTypeSymbol typeSymbol)
    {
        var sb = new StringBuilder();
        if (!typeSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.Append(typeSymbol.ContainingNamespace.ToDisplayString().Replace('.', '_'));
            sb.Append('_');
        }

        var parents = new List<string>();
        var parent = typeSymbol.ContainingType;
        while (parent != null)
        {
            parents.Add(parent.Name);
            parent = parent.ContainingType;
        }
        parents.Reverse();

        foreach (var p in parents)
        {
            sb.Append(p);
            sb.Append('_');
        }

        sb.Append(typeSymbol.Name);
        if (typeSymbol.TypeParameters.Length > 0)
        {
            sb.Append('_');
            sb.Append(typeSymbol.TypeParameters.Length);
        }
        sb.Append(".g.cs");

        return sb.ToString();
    }

    private static string GenerateSource(InspectableTypeModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        bool hasNamespace = !string.IsNullOrEmpty(model.Namespace);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        string indent = hasNamespace ? "    " : string.Empty;

        // Outer types if nested
        foreach (var outer in model.OuterTypes)
        {
            sb.AppendLine($"{indent}partial {outer.Kind} {outer.Name}{outer.TypeParameters}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        string constraints = model.ConstraintClauses.Count > 0
            ? " " + string.Join(" ", model.ConstraintClauses)
            : string.Empty;

        sb.AppendLine($"{indent}partial {model.TypeKind} {model.TypeName}{model.TypeParameters} : global::Atelier.Core.Inspection.IInspectableObject{constraints}");
        sb.AppendLine($"{indent}{{");

        // Static properties array
        sb.AppendLine($"{indent}    private static readonly global::Atelier.Core.Inspection.IPropertyDescriptor[] s_properties = new global::Atelier.Core.Inspection.IPropertyDescriptor[]");
        sb.AppendLine($"{indent}    {{");

        for (int i = 0; i < model.Properties.Count; i++)
        {
            var p = model.Properties[i];
            string setter = p.IsReadOnly
                ? "null"
                : $"static (target, val) => target.{p.Name} = val";

            string escapedDisplayName = EscapeString(p.DisplayName);
            string escapedCategory = EscapeString(p.Category);

            sb.AppendLine($"{indent}        new global::Atelier.Core.Inspection.PropertyDescriptor<{model.TargetTypeFullName}, {p.PropertyType}>(");
            sb.AppendLine($"{indent}            nameof({p.Name}),");
            sb.AppendLine($"{indent}            \"{escapedDisplayName}\",");
            sb.AppendLine($"{indent}            \"{escapedCategory}\",");
            sb.AppendLine($"{indent}            static target => target.{p.Name},");
            sb.AppendLine($"{indent}            {setter}");
            sb.AppendLine($"{indent}        ){(i < model.Properties.Count - 1 ? "," : string.Empty)}");
        }

        sb.AppendLine($"{indent}    }};");
        sb.AppendLine();

        // GetProperties() implementation
        sb.AppendLine($"{indent}    /// <inheritdoc />");
        sb.AppendLine($"{indent}    public global::System.Collections.Generic.IReadOnlyList<global::Atelier.Core.Inspection.IPropertyDescriptor> GetProperties() => s_properties;");

        sb.AppendLine($"{indent}}}");

        // Close outer types
        for (int i = model.OuterTypes.Count - 1; i >= 0; i--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}

internal sealed class InspectableTypeModel
{
    public string TypeName { get; }
    public string TypeKind { get; }
    public string Namespace { get; }
    public string TargetTypeFullName { get; }
    public string TypeParameters { get; }
    public IReadOnlyList<string> ConstraintClauses { get; }
    public IReadOnlyList<InspectableOuterType> OuterTypes { get; }
    public IReadOnlyList<InspectablePropertyModel> Properties { get; }
    public string HintName { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public InspectableTypeModel(
        string typeName,
        string typeKind,
        string ns,
        string targetTypeFullName,
        string typeParameters,
        IReadOnlyList<string> constraintClauses,
        IReadOnlyList<InspectableOuterType> outerTypes,
        IReadOnlyList<InspectablePropertyModel> properties,
        string hintName,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        TypeName = typeName;
        TypeKind = typeKind;
        Namespace = ns;
        TargetTypeFullName = targetTypeFullName;
        TypeParameters = typeParameters;
        ConstraintClauses = constraintClauses;
        OuterTypes = outerTypes;
        Properties = properties;
        HintName = hintName;
        Diagnostics = diagnostics;
    }
}

internal sealed class InspectableOuterType
{
    public string Name { get; }
    public string Kind { get; }
    public string TypeParameters { get; }

    public InspectableOuterType(string name, string kind, string typeParameters)
    {
        Name = name;
        Kind = kind;
        TypeParameters = typeParameters;
    }
}

internal sealed class InspectablePropertyModel
{
    public string Name { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public int Order { get; }
    public bool IsReadOnly { get; }
    public string PropertyType { get; }

    public InspectablePropertyModel(
        string name,
        string displayName,
        string category,
        int order,
        bool isReadOnly,
        string propertyType)
    {
        Name = name;
        DisplayName = displayName;
        Category = category;
        Order = order;
        IsReadOnly = isReadOnly;
        PropertyType = propertyType;
    }
}
