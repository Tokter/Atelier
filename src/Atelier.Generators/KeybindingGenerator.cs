using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Atelier.Generators;

[Generator(LanguageNames.CSharp)]
public class KeybindingGenerator : IIncrementalGenerator
{
    private const string KeybindingAttributeMetadataName = "Atelier.Core.Keybinding.KeybindingAttribute";

    private static readonly DiagnosticDescriptor NotImplementingICommandRule = new(
        id: "CMD001",
        title: "Type must implement ICommand",
        messageFormat: "The type '{0}' is marked with [Keybinding] but does not implement System.Windows.Input.ICommand",
        category: "Atelier.Keybinding",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CannotBeAbstractRule = new(
        id: "CMD002",
        title: "Keybinding type cannot be abstract",
        messageFormat: "The keybinding type '{0}' is marked with [Keybinding] and cannot be abstract",
        category: "Atelier.Keybinding",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MustHaveParameterlessCtorRule = new(
        id: "CMD003",
        title: "Keybinding type must have a parameterless constructor",
        messageFormat: "The keybinding type '{0}' is marked with [Keybinding] and must have an accessible parameterless constructor to be automatically registered",
        category: "Atelier.Keybinding",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classKeybindings = context.SyntaxProvider.ForAttributeWithMetadataName(
            KeybindingAttributeMetadataName,
            predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, ct) => GetKeybindingClassModel(ctx, ct)
        ).Where(static m => m is not null);

        var propertyKeybindings = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsCandidateMember(node),
            transform: static (ctx, ct) => GetKeybindingPropertyModel(ctx, ct)
        ).Where(static m => m is not null);

        var classesCollected = classKeybindings.Collect();
        var propertiesCollected = propertyKeybindings.Collect();

        var combined = context.CompilationProvider
            .Combine(classesCollected)
            .Combine(propertiesCollected);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((compilation, classes), properties) = source;
            Execute(spc, compilation, classes!, properties!);
        });
    }

    private static bool IsCandidateMember(SyntaxNode node)
    {
        if (node is MethodDeclarationSyntax methodDecl)
        {
            return HasKeybindingAttribute(methodDecl.AttributeLists);
        }
        if (node is PropertyDeclarationSyntax propDecl)
        {
            return HasKeybindingAttribute(propDecl.AttributeLists);
        }
        return false;
    }

    private static bool HasKeybindingAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                string name = attr.Name.ToString();
                int dot = name.LastIndexOf('.');
                if (dot >= 0)
                    name = name.Substring(dot + 1);

                if (name is "Keybinding" or "KeybindingAttribute" or "KeybindingProperty" or "KeybindingPropertyAttribute")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static KeybindingClassModel? GetKeybindingClassModel(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var diagnostics = new List<DiagnosticInfo>();

        // 1. Check if type implements System.Windows.Input.ICommand
        bool implementsICommand = false;
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.ToDisplayString() == "System.Windows.Input.ICommand")
            {
                implementsICommand = true;
                break;
            }
        }

        var location = context.TargetNode.GetLocation();

        if (!implementsICommand)
        {
            diagnostics.Add(new DiagnosticInfo(NotImplementingICommandRule, location, typeSymbol.Name));
        }

        // 2. Check if type is abstract
        if (typeSymbol.IsAbstract)
        {
            diagnostics.Add(new DiagnosticInfo(CannotBeAbstractRule, location, typeSymbol.Name));
        }

        // 3. Check for parameterless constructor
        bool hasParameterlessCtor = typeSymbol.InstanceConstructors.Any(c =>
            c.Parameters.IsEmpty &&
            (c.DeclaredAccessibility == Accessibility.Public || c.DeclaredAccessibility == Accessibility.Internal));

        if (!hasParameterlessCtor)
        {
            diagnostics.Add(new DiagnosticInfo(MustHaveParameterlessCtorRule, location, typeSymbol.Name));
        }

        // 4. Extract [Keybinding] attributes
        var keybindings = new List<KeybindingItemModel>();
        foreach (var attr in context.Attributes)
        {
            if (attr.AttributeClass?.ToDisplayString() != KeybindingAttributeMetadataName)
                continue;

            string name = string.Empty;
            string group = string.Empty;
            string keybinding = string.Empty;

            if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string n)
                name = n;
            if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Value is string g)
                group = g;
            if (attr.ConstructorArguments.Length >= 3 && attr.ConstructorArguments[2].Value is string k)
                keybinding = k;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "Name" && named.Value.Value is string nVal) name = nVal;
                if (named.Key == "Group" && named.Value.Value is string gVal) group = gVal;
                if (named.Key == "DefaultKeybinding" && named.Value.Value is string kVal) keybinding = kVal;
            }

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(group))
            {
                string constantName = $"{ToSafeIdentifier(group)}{ToSafeIdentifier(name)}Keybinding";
                string keyValue = $"{group}_{name}";
                keybindings.Add(new KeybindingItemModel(name, group, keybinding, constantName, keyValue));
            }
        }

        string fullyQualifiedTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new KeybindingClassModel(
            fullyQualifiedTypeName,
            typeSymbol.Name,
            new EquatableArray<KeybindingItemModel>(keybindings.ToArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static KeybindingPropertyMemberModel? GetKeybindingPropertyModel(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is MethodDeclarationSyntax methodDecl)
        {
            var methodSymbol = ctx.SemanticModel.GetDeclaredSymbol(methodDecl, ct) as IMethodSymbol;
            if (methodSymbol == null) return null;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null) return null;

            var items = new List<KeybindingItemModel>();
            foreach (var attrList in methodDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    string name = attr.Name.ToString();
                    int dot = name.LastIndexOf('.');
                    if (dot >= 0) name = name.Substring(dot + 1);

                    if (name is not ("Keybinding" or "KeybindingAttribute" or "KeybindingProperty" or "KeybindingPropertyAttribute"))
                        continue;

                    var item = ExtractKeybindingItem(ctx.SemanticModel, attr, ct);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            if (items.Count == 0) return null;

            string methodName = methodSymbol.Name;
            string propertyName;
            if (methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > 5)
            {
                propertyName = methodName.Substring(0, methodName.Length - 5) + "Command";
            }
            else
            {
                propertyName = methodName + "Command";
            }

            string containingTypeFullName = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string containingTypeName = containingType.Name;
            string containingTypeNamespace = containingType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : containingType.ContainingNamespace.ToDisplayString();

            return new KeybindingPropertyMemberModel(
                containingTypeFullName,
                containingTypeName,
                containingTypeNamespace,
                propertyName,
                new EquatableArray<KeybindingItemModel>(items.ToArray()));
        }
        else if (ctx.Node is PropertyDeclarationSyntax propDecl)
        {
            var propSymbol = ctx.SemanticModel.GetDeclaredSymbol(propDecl, ct) as IPropertySymbol;
            if (propSymbol == null) return null;

            var containingType = propSymbol.ContainingType;
            if (containingType == null) return null;

            var items = new List<KeybindingItemModel>();
            foreach (var attrList in propDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    string name = attr.Name.ToString();
                    int dot = name.LastIndexOf('.');
                    if (dot >= 0) name = name.Substring(dot + 1);

                    if (name is not ("Keybinding" or "KeybindingAttribute" or "KeybindingProperty" or "KeybindingPropertyAttribute"))
                        continue;

                    var item = ExtractKeybindingItem(ctx.SemanticModel, attr, ct);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            if (items.Count == 0) return null;

            string propertyName = propSymbol.Name;
            string containingTypeFullName = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string containingTypeName = containingType.Name;
            string containingTypeNamespace = containingType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : containingType.ContainingNamespace.ToDisplayString();

            return new KeybindingPropertyMemberModel(
                containingTypeFullName,
                containingTypeName,
                containingTypeNamespace,
                propertyName,
                new EquatableArray<KeybindingItemModel>(items.ToArray()));
        }

        return null;
    }

    private static KeybindingItemModel? ExtractKeybindingItem(SemanticModel semanticModel, AttributeSyntax attr, CancellationToken ct)
    {
        if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
            return null;

        string name = string.Empty;
        string group = string.Empty;
        string keybinding = string.Empty;

        int positionalIndex = 0;
        foreach (var arg in attr.ArgumentList.Arguments)
        {
            if (arg.NameEquals != null)
            {
                string named = arg.NameEquals.Name.Identifier.Text;
                string? val = GetStringConstant(semanticModel, arg.Expression, ct);
                if (named == "Name" && val != null) name = val;
                if (named == "Group" && val != null) group = val;
                if (named == "DefaultKeybinding" && val != null) keybinding = val;
            }
            else
            {
                string? val = GetStringConstant(semanticModel, arg.Expression, ct);
                if (val != null)
                {
                    if (positionalIndex == 0) name = val;
                    else if (positionalIndex == 1) group = val;
                    else if (positionalIndex == 2) keybinding = val;
                }
                positionalIndex++;
            }
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(group))
            return null;

        string constantName = $"{ToSafeIdentifier(group)}{ToSafeIdentifier(name)}Keybinding";
        string keyValue = $"{group}_{name}";
        return new KeybindingItemModel(name, group, keybinding, constantName, keyValue);
    }

    private static string? GetStringConstant(SemanticModel semanticModel, ExpressionSyntax expr, CancellationToken ct)
    {
        var constant = semanticModel.GetConstantValue(expr, ct);
        if (constant.HasValue && constant.Value is string s)
            return s;

        if (expr is LiteralExpressionSyntax lit)
            return lit.Token.ValueText;

        return null;
    }

    private static void Execute(
        SourceProductionContext spc,
        Compilation compilation,
        ImmutableArray<KeybindingClassModel> classModels,
        ImmutableArray<KeybindingPropertyMemberModel> propertyModels)
    {
        // 1. Report diagnostics
        foreach (var model in classModels)
        {
            foreach (var diag in model.Diagnostics)
            {
                spc.ReportDiagnostic(diag.ToDiagnostic());
            }
        }

        // Check whether KeybindingManager is in current compilation
        var keybindingManagerSymbol = compilation.GetTypeByMetadataName("Atelier.Core.Keybinding.KeybindingManager");
        bool isKeybindingManagerInCurrentCompilation = keybindingManagerSymbol != null &&
            SymbolEqualityComparer.Default.Equals(keybindingManagerSymbol.ContainingAssembly, compilation.Assembly);

        var validClassKeybindings = new List<(KeybindingItemModel Item, string TypeFullName)>();
        foreach (var model in classModels)
        {
            if (model.HasErrors) continue;
            foreach (var cmd in model.Keybindings)
            {
                validClassKeybindings.Add((cmd, model.FullyQualifiedTypeName));
            }
        }

        var validPropertyKeybindings = new List<(KeybindingItemModel Item, KeybindingPropertyMemberModel Member)>();
        foreach (var member in propertyModels)
        {
            foreach (var item in member.Keybindings)
            {
                validPropertyKeybindings.Add((item, member));
            }
        }

        if (isKeybindingManagerInCurrentCompilation)
        {
            string source = GenerateKeybindingManagerSource(validClassKeybindings, validPropertyKeybindings);
            spc.AddSource("KeybindingManager.Generated.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        else if (validClassKeybindings.Count > 0 || validPropertyKeybindings.Count > 0)
        {
            string source = GenerateExternalAssemblySource(compilation.AssemblyName ?? "AtelierGenerated", validClassKeybindings, validPropertyKeybindings);
            spc.AddSource("GeneratedKeybindings.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateKeybindingManagerSource(
        List<(KeybindingItemModel Item, string TypeFullName)> classKeybindings,
        List<(KeybindingItemModel Item, KeybindingPropertyMemberModel Member)> propertyKeybindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Atelier.Core.Keybinding");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class KeybindingManager");
        sb.AppendLine("    {");

        // Static readonly string constants
        sb.AppendLine("        #region Generated Keybinding Constants");
        sb.AppendLine();
        var seenConstants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, _) in classKeybindings)
        {
            if (seenConstants.Add(item.ConstantName))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Keybinding identifier for '{EscapeString(item.Name)}' in group '{EscapeString(item.Group)}'.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static readonly string {item.ConstantName} = \"{EscapeString(item.KeyValue)}\";");
                sb.AppendLine();
            }
        }
        foreach (var (item, _) in propertyKeybindings)
        {
            if (seenConstants.Add(item.ConstantName))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Keybinding identifier for '{EscapeString(item.Name)}' in group '{EscapeString(item.Group)}'.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static readonly string {item.ConstantName} = \"{EscapeString(item.KeyValue)}\";");
                sb.AppendLine();
            }
        }
        sb.AppendLine("        #endregion");
        sb.AppendLine();

        // Static constructor
        sb.AppendLine("        static KeybindingManager()");
        sb.AppendLine("        {");
        sb.AppendLine("            Initialize();");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Initialize method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes and registers all compile-time discovered Keybindings into <see cref=\"RegisteredKeybindings\"/>.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, typeFullName) in classKeybindings)
        {
            if (seenKeys.Add(item.KeyValue))
            {
                sb.AppendLine($"            RegisterOrUpdateKeybinding(new KeybindingDescriptor(\"{EscapeString(item.Name)}\", \"{EscapeString(item.Group)}\", \"{EscapeString(item.Keybinding)}\", new {typeFullName}()));");
            }
        }
        foreach (var (item, member) in propertyKeybindings)
        {
            if (seenKeys.Add(item.KeyValue))
            {
                sb.AppendLine($"            RegisterOrUpdateKeybinding(new KeybindingDescriptor(\"{EscapeString(item.Name)}\", \"{EscapeString(item.Group)}\", \"{EscapeString(item.Keybinding)}\", new global::Atelier.Core.Keybinding.PropertyKeybindingCommand<{member.ContainingTypeFullName}>(\"{EscapeString(item.Name)}\", static x => x.{member.PropertyName})));");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        GenerateExtensionMethods(sb, propertyKeybindings);

        return sb.ToString();
    }

    private static string GenerateExternalAssemblySource(
        string assemblyName,
        List<(KeybindingItemModel Item, string TypeFullName)> classKeybindings,
        List<(KeybindingItemModel Item, KeybindingPropertyMemberModel Member)> propertyKeybindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using Atelier.Core.Keybinding;");
        sb.AppendLine();
        sb.AppendLine("namespace Atelier.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    public static class GeneratedKeybindings");
        sb.AppendLine("    {");

        sb.AppendLine("        #region Generated Keybinding Constants");
        sb.AppendLine();
        var seenConstants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, _) in classKeybindings)
        {
            if (seenConstants.Add(item.ConstantName))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Keybinding identifier for '{EscapeString(item.Name)}' in group '{EscapeString(item.Group)}'.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static readonly string {item.ConstantName} = \"{EscapeString(item.KeyValue)}\";");
                sb.AppendLine();
            }
        }
        foreach (var (item, _) in propertyKeybindings)
        {
            if (seenConstants.Add(item.ConstantName))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Keybinding identifier for '{EscapeString(item.Name)}' in group '{EscapeString(item.Group)}'.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static readonly string {item.ConstantName} = \"{EscapeString(item.KeyValue)}\";");
                sb.AppendLine();
            }
        }
        sb.AppendLine("        #endregion");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all compile-time discovered Keybindings into <see cref=\"global::Atelier.Core.Keybinding.KeybindingManager\"/>.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void RegisterKeybindings()");
        sb.AppendLine("        {");

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, typeFullName) in classKeybindings)
        {
            if (seenKeys.Add(item.KeyValue))
            {
                sb.AppendLine($"            global::Atelier.Core.Keybinding.KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor(\"{EscapeString(item.Name)}\", \"{EscapeString(item.Group)}\", \"{EscapeString(item.Keybinding)}\", new {typeFullName}()));");
            }
        }
        foreach (var (item, member) in propertyKeybindings)
        {
            if (seenKeys.Add(item.KeyValue))
            {
                sb.AppendLine($"            global::Atelier.Core.Keybinding.KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor(\"{EscapeString(item.Name)}\", \"{EscapeString(item.Group)}\", \"{EscapeString(item.Keybinding)}\", new global::Atelier.Core.Keybinding.PropertyKeybindingCommand<{member.ContainingTypeFullName}>(\"{EscapeString(item.Name)}\", static x => x.{member.PropertyName})));");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        GenerateExtensionMethods(sb, propertyKeybindings);

        return sb.ToString();
    }

    private static void GenerateExtensionMethods(
        StringBuilder sb,
        List<(KeybindingItemModel Item, KeybindingPropertyMemberModel Member)> propertyKeybindings)
    {
        if (propertyKeybindings.Count == 0) return;

        var groupedByType = propertyKeybindings
            .GroupBy(x => x.Member.ContainingTypeFullName)
            .ToList();

        foreach (var group in groupedByType)
        {
            var first = group.First().Member;
            string ns = first.ContainingTypeNamespace;
            string typeFullName = first.ContainingTypeFullName;
            string typeName = first.ContainingTypeName;
            string safeTypeName = ToSafeIdentifier(typeName);
            string safeNs = ToSafeIdentifier(ns);
            string extensionClassName = $"{safeNs}{safeTypeName}KeybindingExtensions";

            sb.AppendLine();
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(ns) ? "" : "    ";
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// Extension methods for keybinding registration on <see cref=\"{typeFullName}\"/>.");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public static class {extensionClassName}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Registers the keybindings defined on this <see cref=\"{typeFullName}\"/> instance with <see cref=\"global::Atelier.Core.Keybinding.KeybindingManager\"/>.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public static void RegisterKeybindings(this {typeFullName} target)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (target == null) throw new global::System.ArgumentNullException(nameof(target));");

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (item, member) in group)
            {
                if (seenKeys.Add(item.KeyValue))
                {
                    sb.AppendLine($"{indent}        global::Atelier.Core.Keybinding.KeybindingManager.RegisterOrUpdateKeybinding(new global::Atelier.Core.Keybinding.KeybindingDescriptor(\"{EscapeString(item.Name)}\", \"{EscapeString(item.Group)}\", \"{EscapeString(item.Keybinding)}\", target.{member.PropertyName}));");
                }
            }

            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");

            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine("}");
            }
        }
    }

    private static string ToSafeIdentifier(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }
        string result = sb.ToString();
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }
        return result;
    }

    private static string EscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}

internal sealed class KeybindingClassModel : IEquatable<KeybindingClassModel>
{
    public string FullyQualifiedTypeName { get; }
    public string ClassName { get; }
    public EquatableArray<KeybindingItemModel> Keybindings { get; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public KeybindingClassModel(
        string fullyQualifiedTypeName,
        string className,
        EquatableArray<KeybindingItemModel> keybindings,
        EquatableArray<DiagnosticInfo> diagnostics)
    {
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        ClassName = className;
        Keybindings = keybindings;
        Diagnostics = diagnostics;
    }

    public bool Equals(KeybindingClassModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FullyQualifiedTypeName == other.FullyQualifiedTypeName
            && ClassName == other.ClassName
            && Keybindings.Equals(other.Keybindings)
            && Diagnostics.Equals(other.Diagnostics);
    }

    public override bool Equals(object? obj) => obj is KeybindingClassModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (FullyQualifiedTypeName != null ? FullyQualifiedTypeName.GetHashCode() : 0);
            hash = (hash * 397) ^ (ClassName != null ? ClassName.GetHashCode() : 0);
            hash = (hash * 397) ^ Keybindings.GetHashCode();
            hash = (hash * 397) ^ Diagnostics.GetHashCode();
            return hash;
        }
    }
}

internal sealed class KeybindingPropertyMemberModel : IEquatable<KeybindingPropertyMemberModel>
{
    public string ContainingTypeFullName { get; }
    public string ContainingTypeName { get; }
    public string ContainingTypeNamespace { get; }
    public string PropertyName { get; }
    public EquatableArray<KeybindingItemModel> Keybindings { get; }

    public KeybindingPropertyMemberModel(
        string containingTypeFullName,
        string containingTypeName,
        string containingTypeNamespace,
        string propertyName,
        EquatableArray<KeybindingItemModel> keybindings)
    {
        ContainingTypeFullName = containingTypeFullName;
        ContainingTypeName = containingTypeName;
        ContainingTypeNamespace = containingTypeNamespace;
        PropertyName = propertyName;
        Keybindings = keybindings;
    }

    public bool Equals(KeybindingPropertyMemberModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ContainingTypeFullName == other.ContainingTypeFullName
            && ContainingTypeName == other.ContainingTypeName
            && ContainingTypeNamespace == other.ContainingTypeNamespace
            && PropertyName == other.PropertyName
            && Keybindings.Equals(other.Keybindings);
    }

    public override bool Equals(object? obj) => obj is KeybindingPropertyMemberModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (ContainingTypeFullName != null ? ContainingTypeFullName.GetHashCode() : 0);
            hash = (hash * 397) ^ (ContainingTypeName != null ? ContainingTypeName.GetHashCode() : 0);
            hash = (hash * 397) ^ (ContainingTypeNamespace != null ? ContainingTypeNamespace.GetHashCode() : 0);
            hash = (hash * 397) ^ (PropertyName != null ? PropertyName.GetHashCode() : 0);
            hash = (hash * 397) ^ Keybindings.GetHashCode();
            return hash;
        }
    }
}

internal sealed class KeybindingItemModel : IEquatable<KeybindingItemModel>
{
    public string Name { get; }
    public string Group { get; }
    public string Keybinding { get; }
    public string ConstantName { get; }
    public string KeyValue { get; }

    public KeybindingItemModel(string name, string group, string keybinding, string constantName, string keyValue)
    {
        Name = name;
        Group = group;
        Keybinding = keybinding;
        ConstantName = constantName;
        KeyValue = keyValue;
    }

    public bool Equals(KeybindingItemModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Group == other.Group
            && Keybinding == other.Keybinding
            && ConstantName == other.ConstantName
            && KeyValue == other.KeyValue;
    }

    public override bool Equals(object? obj) => obj is KeybindingItemModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (Name != null ? Name.GetHashCode() : 0);
            hash = (hash * 397) ^ (Group != null ? Group.GetHashCode() : 0);
            hash = (hash * 397) ^ (Keybinding != null ? Keybinding.GetHashCode() : 0);
            hash = (hash * 397) ^ (ConstantName != null ? ConstantName.GetHashCode() : 0);
            hash = (hash * 397) ^ (KeyValue != null ? KeyValue.GetHashCode() : 0);
            return hash;
        }
    }
}

internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticDescriptor Descriptor { get; }
    public Location? Location { get; }
    public string[] MessageArgs { get; }
    public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;

    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
    {
        Descriptor = descriptor;
        Location = location;
        MessageArgs = messageArgs;
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location, (object[])MessageArgs);
    }

    public bool Equals(DiagnosticInfo other)
    {
        return Equals(Descriptor.Id, other.Descriptor.Id)
            && Equals(Location, other.Location);
    }

    public override bool Equals(object? obj) => obj is DiagnosticInfo other && Equals(other);
    public override int GetHashCode() => (Descriptor?.Id.GetHashCode() ?? 0) ^ (Location?.GetHashCode() ?? 0);
}

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[] _array;

    public EquatableArray(T[] array)
    {
        _array = array;
    }

    public int Count => _array?.Length ?? 0;
    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (ReferenceEquals(_array, other._array)) return true;
        int count1 = Count;
        int count2 = other.Count;
        if (count1 != count2) return false;
        for (int i = 0; i < count1; i++)
        {
            if (!_array[i].Equals(other._array[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array == null) return 0;
        int hash = 17;
        for (int i = 0; i < _array.Length; i++)
        {
            hash = hash * 31 + (_array[i]?.GetHashCode() ?? 0);
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
