using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

/// <summary>구문 분석기를 편집기에서만 로드합니다. Unity Pipeline 등 다른 플러그인은 필요하지 않습니다.</summary>
public static class UIEditorPortableParser
{
    const string DirectoryPath = "Assets/Editor/CanvasEditor/Dependencies/Roslyn";
    static readonly Dictionary<string,Assembly> Loaded = new();
    static Type _parser;

    public static Type GetParser()
    {
        if (_parser != null) return _parser;
        // 이미 정상 동작하는 분석기가 있으면 재사용하여 다른 편집기 플러그인의 로드를 방해하지 않습니다.
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.FullName.Contains("CodeAnalysis.CSharp,")))
        {
            var existing=assembly.GetExportedTypes().FirstOrDefault(t=>t.Name=="CSharpSyntaxTree");
            if(existing!=null)return _parser=existing;
        }
        string[] names = {
            "System.Runtime.CompilerServices.Unsafe", "System.Threading.Tasks.Extensions", "System.Buffers",
            "System.Memory", "System.Text.Encoding.CodePages", "System.Collections.Immutable", "System.Reflection.Metadata",
            "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp"
        };
        AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        foreach (string name in names)
        {
            var platform=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.FullName.StartsWith(name+",",StringComparison.Ordinal));
            if(name.StartsWith("System.")&&name!="System.Collections.Immutable"&&name!="System.Reflection.Metadata"&&platform!=null)
            { Loaded[platform.FullName]=platform;continue; }
            string path = Path.Combine(DirectoryPath, name + ".dll.bytes");
            if (!File.Exists(path)) throw new FileNotFoundException("CanvasEditor 구문 분석기가 누락되었습니다. 패키지의 Editor/CanvasEditor/Dependencies를 함께 가져오세요.", path);
            var assembly = Assembly.LoadFrom(Path.GetFullPath(path));
            Loaded[assembly.FullName] = assembly;
        }
        _parser = Loaded.Values.First(a => a.FullName.StartsWith("Microsoft.CodeAnalysis.CSharp,",StringComparison.Ordinal))
            .GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", true);
        return _parser;
    }
    static Assembly Resolve(object sender, ResolveEventArgs args) => Loaded.TryGetValue(args.Name, out var assembly) ? assembly : null;
}
