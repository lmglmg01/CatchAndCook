using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>기존 함수의 서명을 바꾸지 않고 관리 구간 한 곳만 연결합니다.</summary>
public static class UIDirectorSource
{
    static readonly Dictionary<string,(string source,object[] nodes)> Parsed = new();
    public sealed class Method
    {
        public MethodInfo info;
        public string path, source, label, key, reason;
        public int open, close;
        public string boolArgument;
        public bool Supported => string.IsNullOrEmpty(reason);
    }

    public static List<Method> Methods(MonoBehaviour view)
    {
        var result = new List<Method>();
        if (view == null) return result;
        var script = MonoScript.FromMonoBehaviour(view);
        string path = script != null ? AssetDatabase.GetAssetPath(script) : "";
        string source = File.Exists(path) ? File.ReadAllText(path) : "";
        var methods = view.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(void) && !m.IsSpecialName && !m.Name.StartsWith("<") &&
                !new[] { "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset" }.Contains(m.Name))
            .OrderBy(m => m.Name).ToArray();
        foreach (var info in methods)
        {
            var method = Locate(info, path, source);
            if (methods.Count(m => m.Name == info.Name) > 1) method.reason = "오버로드된 함수는 자동 연결하지 않습니다. 구분 가능한 View 함수를 사용하세요.";
            result.Add(method);
        }
        return result;
    }

    public static Method Locate(MethodInfo info, string path, string source)
    {
        var parameters = info.GetParameters();
        var item = new Method { info = info, path = path, source = source, key = info.Name,
            label = info.Name + "(" + string.Join(", ", parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")" };
        if (string.IsNullOrEmpty(source) || !path.StartsWith("Assets/", StringComparison.Ordinal)) item.reason = "Assets 안의 C# View 원본을 찾지 못했습니다.";
        else if (info.IsGenericMethod || info.IsAbstract || info.IsStatic ||
            info.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false).Length > 0 ||
            parameters.Any(p => p.ParameterType.IsByRef || p.IsOut || p.ParameterType.IsPointer))
            item.reason = "제네릭·ref/out·추상·정적 함수는 기존 의미를 보존할 수 없어 자동 연결하지 않습니다.";
        else
        {
            try
            {
                var declarations = SyntaxNodes(source,path).Where(node=>node.GetType().Name=="MethodDeclarationSyntax"&&
                    TokenText(Get(node,"Identifier"))==info.Name&&TokenText(Get(Get(node,"Parent"),"Identifier"))==info.DeclaringType.Name).ToArray();
                if(declarations.Length!=1)throw new InvalidOperationException("원본 선언을 하나로 특정할 수 없습니다 (조건부 선언·오버로드 확인).");
                var declaration=declarations[0];
                if(Get(declaration,"ReturnType")?.ToString()!="void")throw new InvalidOperationException("컴파일된 함수와 원본 반환형이 다릅니다. 컴파일 완료 후 다시 읽어 주세요.");
                var sourceParameters=(System.Collections.IEnumerable)Get(Get(declaration,"ParameterList"),"Parameters");
                var names=sourceParameters.Cast<object>().Select(p=>TokenText(Get(p,"Identifier"))).ToArray();
                if(!names.SequenceEqual(parameters.Select(p=>p.Name)))throw new InvalidOperationException("컴파일된 함수와 원본 인자가 다릅니다. 컴파일 완료 후 다시 읽어 주세요.");
                var body=Get(declarations[0],"Body");
                if(body==null||Equals(Get(declarations[0],"ContainsDiagnostics"),true))throw new InvalidOperationException("구문 오류가 없는 블록 형태의 동기 void 함수만 연결할 수 있습니다.");
                if(Equals(Get(body,"ContainsDirectives"),true))throw new InvalidOperationException("조건부 컴파일 본문은 자동 연결하지 않습니다.");
                item.open=(int)Get(Get(body,"OpenBraceToken"),"SpanStart");item.close=(int)Get(Get(body,"CloseBraceToken"),"SpanStart");
                var bools=parameters.Where(p=>p.ParameterType==typeof(bool)).ToArray();if(bools.Length==1)item.boolArgument="@"+bools[0].Name;
            }
            catch(Exception ex){item.reason=ex.GetBaseException().Message;}
        }
        return item;
    }

    static object Get(object value,string property)=>value?.GetType().GetProperty(property)?.GetValue(value);
    static string TokenText(object token)=>Get(token,"ValueText") as string;
    // 편집기에 이미 로드된 Roslyn을 사용하며, 특정 플러그인의 DLL 참조를 런타임에 추가하지 않습니다.
    static IEnumerable<object> SyntaxNodes(string text,string path)
    {
        if(Parsed.TryGetValue(path,out var cached)&&cached.source==text)return cached.nodes;
        var parser=UIEditorPortableParser.GetParser();
        if(parser==null)throw new InvalidOperationException("안전한 C# 구문 파서를 찾지 못했습니다. 이 환경에서는 코드 자동 연결을 보류합니다.");
        var parse=parser.GetMethods().First(m=>m.Name=="ParseText"&&m.GetParameters().Length==5&&m.GetParameters()[0].ParameterType==typeof(string));
        var optionType=parse.GetParameters()[1].ParameterType;
        object options=optionType.GetProperty("Default",BindingFlags.Static|BindingFlags.Public)?.GetValue(null)??optionType.GetField("Default",BindingFlags.Static|BindingFlags.Public)?.GetValue(null);
        var defines=UnityEditor.Compilation.CompilationPipeline.GetAssemblies().FirstOrDefault(a=>a.sourceFiles.Contains(path))?.defines??Array.Empty<string>();
        if(options!=null)options=optionType.GetMethod("WithPreprocessorSymbols",new[]{typeof(string[])})?.Invoke(options,new object[]{defines})??options;
        var tree=parse.Invoke(null,new object[]{text,options,path,null,default(System.Threading.CancellationToken)});
        var root=tree.GetType().GetMethod("GetRoot",new[]{typeof(System.Threading.CancellationToken)}).Invoke(tree,new object[]{default(System.Threading.CancellationToken)});
        var descendants=root.GetType().GetMethods().First(m=>m.Name=="DescendantNodes"&&m.GetParameters().Length==2);
        var nodes=((System.Collections.IEnumerable)descendants.Invoke(root,new object[]{null,false})).Cast<object>().ToArray();
        if(Parsed.Count>12)Parsed.Clear();Parsed[path]=(text,nodes);return nodes;
    }

    // 문자열·문자·주석의 괄호를 제외하고 위치를 유지합니다. 보간 문자열 전체도 가립니다.
    static string Mask(string text)
    {
        var chars = text.ToCharArray();
        int i = 0;
        while (i < chars.Length)
        {
            int start = i;
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            { i += 2; while (i < text.Length && text[i] != '\n') i++; }
            else if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            { i += 2; while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++; i = Math.Min(text.Length, i + 2); }
            else if (text[i] == '"' || text[i] == '\'')
            {
                char quote = text[i++]; bool verbatim = start > 0 && (text[start - 1] == '@' || start > 1 && text[start - 2] == '@');
                while (i < text.Length)
                {
                    if (!verbatim && text[i] == '\\') { i = Math.Min(text.Length, i + 2); continue; }
                    if (text[i++] != quote) continue;
                    if (verbatim && i < text.Length && text[i] == quote) { i++; continue; }
                    break;
                }
            }
            else { i++; continue; }
            for (int j = start; j < i; j++) if (chars[j] != '\n' && chars[j] != '\r') chars[j] = ' ';
        }
        return new string(chars);
    }
    static int MatchEnd(string mask, int start, char open, char close)
    {
        if (start < 0) return -1;
        int depth = 0;
        for (int i = start; i < mask.Length; i++)
        { if (mask[i] == open) depth++; else if (mask[i] == close && --depth == 0) return i; }
        return -1;
    }

    /// <summary>반복 저장은 이미 연결된 구간을 재사용하며, 다른 원본 코드는 그대로 보존합니다.</summary>
    public static string Rewrite(Method method, bool? entering)
    {
        if (!method.Supported) throw new InvalidOperationException(method.reason);
        string body = method.source.Substring(method.open + 1, method.close - method.open - 1);
        string direction = entering.HasValue ? (entering.Value ? "true" : "false") : method.boolArgument;
        if (string.IsNullOrEmpty(direction)) throw new InvalidOperationException("등장과 퇴장이 같은 함수라면 bool 인자가 하나 있어야 합니다.");
        string marker = "// <UIDirector:" + method.key + ">";
        string prefix = null, suffix = null;
        if (body.Contains(marker))
        {
            // 관리 구간 밖의 중복 재생 방지·현재 투명도 포착 등 사용자 코드는 보존합니다.
            int regionStart = body.IndexOf(marker, StringComparison.Ordinal);
            int regionEnd = body.IndexOf("// </UIDirector>", regionStart, StringComparison.Ordinal);
            if (regionEnd < 0) throw new InvalidOperationException("UIDirector 관리 구간의 끝을 찾지 못했습니다.");
            prefix = body.Substring(0, regionStart);
            suffix = body.Substring(regionEnd + "// </UIDirector>".Length);
            var local=SyntaxNodes(method.source,method.path).Where(node=>node.GetType().Name=="LocalFunctionStatementSyntax"&&TokenText(Get(node,"Identifier"))=="__UIDirectorOriginal")
                .FirstOrDefault(node=>{var span=Get(node,"Span");int pos=(int)Get(span,"Start");return pos>method.open&&pos<method.close;});
            var localBody=Get(local,"Body");
            if(localBody==null)throw new InvalidOperationException("기존 UIDirector 관리 구간이 수정되었습니다. 원본을 확인하세요.");
            int open=(int)Get(Get(localBody,"OpenBraceToken"),"SpanStart"),close=(int)Get(Get(localBody,"CloseBraceToken"),"SpanStart");
            body=method.source.Substring(open+1,close-open-1);
        }
        else if (body.Contains("__UIDirectorOriginal") || body.Contains("UIDirectorView.Dispatch"))
            throw new InvalidOperationException("같은 이름의 사용자 코드가 있어 자동 연결하지 않습니다.");
        string nl = method.source.Contains("\r\n") ? "\r\n" : "\n";
        string region = marker + nl +
            "        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다." + nl +
            "        void __UIDirectorOriginal()" + nl + "        {" + body + "}" + nl +
            "        global::UIDirectorView.Dispatch(this, \"" + method.key + "\", " + direction + ", __UIDirectorOriginal);" + nl +
            "        // </UIDirector>";
        string replacement = prefix != null ? prefix + region + suffix : nl + "        " + region + nl + "    ";
        return method.source.Substring(0, method.open + 1) + replacement + method.source.Substring(method.close);
    }

    public static Dictionary<string, string> Prepare(MonoBehaviour owner, Method enter, Method exit, Method loop = null, Method reaction = null)
    {
        var files = new Dictionary<string, string>();
        if (enter != null && exit != null && enter.key == exit.key && string.IsNullOrEmpty(enter.boolArgument))
            throw new InvalidOperationException("같은 함수의 등장·퇴장 분기는 bool 인자 하나가 있어야 합니다.");
        if(new[]{loop,reaction}.Where(m=>m!=null).Any(m=>(enter!=null&&m.key==enter.key)||(exit!=null&&m.key==exit.key)) || (loop!=null&&reaction!=null&&loop.key==reaction.key))
            throw new InvalidOperationException("상시 반복·상호작용 반응은 다른 유형과 구분되는 함수를 지정하세요.");
        foreach (var selected in new[] { enter, exit, loop, reaction }.Where(m => m != null).Distinct())
        {
            string source = files.TryGetValue(selected.path, out var pending) ? pending : File.ReadAllText(selected.path);
            if (!files.ContainsKey(selected.path) && source != selected.source) throw new InvalidOperationException("함수 원본이 변경되었습니다. 다시 읽은 뒤 적용하세요.");
            var current = Locate(selected.info, selected.path, source);
            files[selected.path] = Rewrite(current, enter != null && exit != null && enter.key == exit.key && selected.key == enter.key ? null : selected != exit);
        }
        return files;
    }

    public static void Write(Dictionary<string, string> files)
    {
        string folder = "Library/UIDirector/Backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        Directory.CreateDirectory(folder);
        foreach (var pair in files)
        {
            string previous = File.ReadAllText(pair.Key);
            if (previous == pair.Value) continue;
            // 충돌 시 되돌릴 수 있도록 원본 전체를 먼저 보관합니다. 원래 BOM/줄바꿈도 보존합니다.
            File.Copy(pair.Key, Path.Combine(folder, pair.Key.Replace('/', '_')), false);
            byte[] bytes = File.ReadAllBytes(pair.Key);
            bool bom = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191;
            File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(bom));
        }
    }
}
