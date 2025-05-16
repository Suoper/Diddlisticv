using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using System.IO;
using System.Text;
using System.Collections;

public class GameCodeAnalyzer : MonoBehaviour
{
    [Header("Analysis Configuration")]
    public bool analyzeOnStartup = true;
    public bool generateTypeMap = true;
    public bool analyzePublicAPI = true;
    public bool monitorRuntime = true;

    // Database of discovered types and relationships
    private Dictionary<string, TypeInfo> discoveredTypes = new Dictionary<string, TypeInfo>();
    private Dictionary<string, MethodInfo> discoveredMethods = new Dictionary<string, MethodInfo>();
    private Dictionary<string, List<string>> typeHierarchy = new Dictionary<string, List<string>>();
    private Dictionary<string, List<string>> methodUsagePatterns = new Dictionary<string, List<string>>();

    // Runtime behavior tracking
    private Dictionary<string, int> methodCallFrequency = new Dictionary<string, int>();
    private Dictionary<string, List<object>> methodParameterValues = new Dictionary<string, List<object>>();
    private Dictionary<string, HashSet<string>> componentRelationships = new Dictionary<string, HashSet<string>>();

    private void Start()
    {
        if (analyzeOnStartup)
        {
            StartCoroutine(AnalyzeGameAssemblies());
        }
    }

    public IEnumerator AnalyzeGameAssemblies()
    {
        Debug.Log("Starting game code analysis...");

        // First analyze ThunderRoad assembly (main Blade & Sorcery code)
        AnalyzeAssembly(typeof(Player).Assembly);

        yield return null; // Prevent freezing by allowing a frame to process

        // Analyze Unity engine types used by the game
        AnalyzeAssembly(typeof(MonoBehaviour).Assembly, "UnityEngine");

        yield return null;

        // Generate relationships and hierarchy
        BuildTypeHierarchy();

        yield return null;

        // Generate AI-friendly documentation
        GenerateAPIDocumentation();

        Debug.Log($"Game code analysis complete. Discovered {discoveredTypes.Count} types and {discoveredMethods.Count} methods.");
    }

    private void AnalyzeAssembly(Assembly assembly, string filter = "ThunderRoad")
    {
        try
        {
            Type[] types = assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.Contains(filter))
                .ToArray();

            Debug.Log($"Analyzing {types.Length} types from {assembly.GetName().Name}");

            foreach (Type type in types)
            {
                // Skip compiler-generated types
                if (type.Name.Contains("<") || type.Name.Contains(">"))
                    continue;

                // Record type information
                string typeName = type.FullName;
                discoveredTypes[typeName] = new TypeInfo
                {
                    Name = type.Name,
                    FullName = typeName,
                    IsClass = type.IsClass,
                    IsEnum = type.IsEnum,
                    IsInterface = type.IsInterface,
                    IsAbstract = type.IsAbstract,
                    BaseType = type.BaseType?.FullName,
                    Interfaces = type.GetInterfaces().Select(i => i.FullName).ToList()
                };

                // Record fields
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    discoveredTypes[typeName].Fields.Add(new FieldData
                    {
                        Name = field.Name,
                        TypeName = field.FieldType.Name,
                        IsPublic = field.IsPublic,
                        IsStatic = field.IsStatic
                    });
                }

                // Record properties
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    discoveredTypes[typeName].Properties.Add(new PropertyData
                    {
                        Name = prop.Name,
                        TypeName = prop.PropertyType.Name,
                        CanRead = prop.CanRead,
                        CanWrite = prop.CanWrite
                    });
                }

                // Record methods
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    // Skip auto-generated property methods and compiler-generated methods
                    if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") || method.Name.Contains("<"))
                        continue;

                    string methodKey = $"{typeName}.{method.Name}";

                    // Record method parameters
                    var parameters = method.GetParameters();
                    List<ParameterData> paramList = new List<ParameterData>();

                    foreach (var param in parameters)
                    {
                        paramList.Add(new ParameterData
                        {
                            Name = param.Name,
                            TypeName = param.ParameterType.Name,
                            IsOptional = param.IsOptional,
                            DefaultValue = param.IsOptional ? param.DefaultValue?.ToString() : null
                        });
                    }

                    discoveredTypes[typeName].Methods.Add(new MethodData
                    {
                        Name = method.Name,
                        ReturnTypeName = method.ReturnType.Name,
                        Parameters = paramList,
                        IsPublic = method.IsPublic,
                        IsStatic = method.IsStatic,
                        IsVirtual = method.IsVirtual
                    });

                    // Record method separately for quick reference
                    discoveredMethods[methodKey] = method;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error analyzing assembly: {ex.Message}");
        }
    }

    private void BuildTypeHierarchy()
    {
        foreach (var typeInfo in discoveredTypes.Values)
        {
            if (string.IsNullOrEmpty(typeInfo.BaseType))
                continue;

            if (!typeHierarchy.ContainsKey(typeInfo.BaseType))
            {
                typeHierarchy[typeInfo.BaseType] = new List<string>();
            }

            typeHierarchy[typeInfo.BaseType].Add(typeInfo.FullName);
        }

        Debug.Log($"Built type hierarchy with {typeHierarchy.Count} parent types");
    }

    public void GenerateAPIDocumentation()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# BLADE & SORCERY ANALYZED API REFERENCE");
        sb.AppendLine("## GENERATED BY GAME CODE ANALYZER");
        sb.AppendLine($"## DATE: {DateTime.Now.ToString()}");
        sb.AppendLine();

        // Add core types first
        AddTypeDocumentation(sb, "ThunderRoad.Player");
        AddTypeDocumentation(sb, "ThunderRoad.Creature");
        AddTypeDocumentation(sb, "ThunderRoad.Item");
        AddTypeDocumentation(sb, "ThunderRoad.SpellCaster");
        AddTypeDocumentation(sb, "ThunderRoad.CollisionInstance");
        AddTypeDocumentation(sb, "ThunderRoad.ThunderBehaviour");
        AddTypeDocumentation(sb, "ThunderRoad.ItemModule");
        AddTypeDocumentation(sb, "ThunderRoad.LevelModule");

        // Save the API documentation to file
        string docsPath = Path.Combine(Application.persistentDataPath, "AnalyzedAPI.md");
        File.WriteAllText(docsPath, sb.ToString());

        // Create an AI-friendly version with specific patterns
        GenerateAIFriendlyPatterns();

        Debug.Log($"API documentation saved to {docsPath}");
    }

    private void AddTypeDocumentation(StringBuilder sb, string typeName)
    {
        if (!discoveredTypes.ContainsKey(typeName))
            return;

        TypeInfo type = discoveredTypes[typeName];

        sb.AppendLine($"## {type.Name.ToUpper()} CLASS");
        sb.AppendLine($"Base class: {type.BaseType ?? "None"}");
        sb.AppendLine();

        if (type.Properties.Count > 0)
        {
            sb.AppendLine("### Properties");
            foreach (var prop in type.Properties)
            {
                sb.AppendLine($"- {prop.Name} ({prop.TypeName}): {(prop.CanRead ? "Readable" : "")} {(prop.CanWrite ? "Writable" : "")}");
            }
            sb.AppendLine();
        }

        if (type.Fields.Count > 0)
        {
            sb.AppendLine("### Fields");
            foreach (var field in type.Fields)
            {
                sb.AppendLine($"- {field.Name} ({field.TypeName}): {(field.IsPublic ? "Public" : "Protected")} {(field.IsStatic ? "Static" : "")}");
            }
            sb.AppendLine();
        }

        if (type.Methods.Count > 0)
        {
            sb.AppendLine("### Methods");
            foreach (var method in type.Methods)
            {
                string parameters = string.Join(", ", method.Parameters.Select(p => $"{p.TypeName} {p.Name}"));
                sb.AppendLine($"- {method.Name}({parameters}) -> {method.ReturnTypeName}");
            }
            sb.AppendLine();
        }

        // Add derived types if any
        if (typeHierarchy.ContainsKey(typeName))
        {
            sb.AppendLine("### Derived Types");
            foreach (string derived in typeHierarchy[typeName])
            {
                sb.AppendLine($"- {derived.Substring(derived.LastIndexOf('.') + 1)}");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private void GenerateAIFriendlyPatterns()
    {
        Dictionary<string, string> patterns = new Dictionary<string, string>();

        // Extract common patterns from discovered types
        foreach (var type in discoveredTypes.Values)
        {
            // Identify event subscription patterns
            foreach (var methodName in type.Methods.Select(m => m.Name))
            {
                if (methodName.EndsWith("Event") || methodName.StartsWith("On"))
                {
                    string className = type.Name;
                    string patternName = $"{className}{methodName}Subscription";
                    string pattern = GenerateEventSubscriptionPattern(type.FullName, methodName);
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        patterns[patternName] = pattern;
                    }
                }
            }
        }

        // Save patterns to JSON
        string patternsJson = JsonUtility.ToJson(patterns, true);
        string patternsPath = Path.Combine(Application.persistentDataPath, "AnalyzedPatterns.json");
        File.WriteAllText(patternsPath, patternsJson);

        Debug.Log($"Generated {patterns.Count} AI-friendly patterns");
    }

    private string GenerateEventSubscriptionPattern(string typeName, string eventName)
    {
        // Generate sample code for event subscription based on type and event name
        if (!discoveredTypes.ContainsKey(typeName))
            return null;

        TypeInfo type = discoveredTypes[typeName];

        // Simple example pattern generation
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"// Event subscription for {type.Name}.{eventName}");
        sb.AppendLine($"void Subscribe{eventName}({type.Name} target)");
        sb.AppendLine("{");
        sb.AppendLine($"    target.{eventName} += On{eventName}Handler;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"void Unsubscribe{eventName}({type.Name} target)");
        sb.AppendLine("{");
        sb.AppendLine($"    target.{eventName} -= On{eventName}Handler;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"void On{eventName}Handler(/* Add appropriate parameters based on event */)");
        sb.AppendLine("{");
        sb.AppendLine("    // Handle the event");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // Data structures to store type information
    [Serializable]
    public class TypeInfo
    {
        public string Name;
        public string FullName;
        public bool IsClass;
        public bool IsEnum;
        public bool IsInterface;
        public bool IsAbstract;
        public string BaseType;
        public List<string> Interfaces = new List<string>();
        public List<FieldData> Fields = new List<FieldData>();
        public List<PropertyData> Properties = new List<PropertyData>();
        public List<MethodData> Methods = new List<MethodData>();
    }

    [Serializable]
    public class FieldData
    {
        public string Name;
        public string TypeName;
        public bool IsPublic;
        public bool IsStatic;
    }

    [Serializable]
    public class PropertyData
    {
        public string Name;
        public string TypeName;
        public bool CanRead;
        public bool CanWrite;
    }

    [Serializable]
    public class MethodData
    {
        public string Name;
        public string ReturnTypeName;
        public List<ParameterData> Parameters = new List<ParameterData>();
        public bool IsPublic;
        public bool IsStatic;
        public bool IsVirtual;
    }

    [Serializable]
    public class ParameterData
    {
        public string Name;
        public string TypeName;
        public bool IsOptional;
        public string DefaultValue;
    }
}