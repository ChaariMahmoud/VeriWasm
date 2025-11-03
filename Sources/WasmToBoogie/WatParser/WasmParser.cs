using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WasmToBoogie.Parser.Ast;
using SharedConfig;

namespace WasmToBoogie.Parser
{
    public class WasmParser
    {
        private readonly string filePath;

        static WasmParser()
        {
            // Load the Binaryen library from the centralized path
            try
            {
                var binaryenPath = ToolPaths.BinaryenLibraryPath;
                if (File.Exists(binaryenPath))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        LoadLibrary(binaryenPath);
                    }
                    Console.WriteLine($"✅ Binaryen library loaded from: {binaryenPath}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Binaryen library not found at: {binaryenPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to load Binaryen library: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        public WasmParser(string filePath)
        {
            this.filePath = filePath;
        }

        // ---------------- helpers for locals scan ----------------

        private static int ComputeMaxLocalIndexInBody(List<WasmNode> body)
        {
            int max = -1;

            int? TryName(WasmNode n)
            {
                return n switch
                {
                    LocalGetNode g => g.Index ?? TryParseAutoName(g.Name),
                    LocalSetNode s => s.Index ?? TryParseAutoName(s.Name),
                    LocalTeeNode t => t.Index ?? TryParseAutoName(t.Name),
                    _ => null
                };
            }

            void Walk(WasmNode n)
            {
                switch (n)
                {
                    case LocalGetNode:
                    case LocalSetNode:
                    case LocalTeeNode:
                        {
                            var k = TryName(n);
                            if (k.HasValue) max = Math.Max(max, k.Value);

                            // Also dive into folded value in LocalSet
                            if (n is LocalSetNode s && s.Value != null) Walk(s.Value);
                            break;
                        }

                    case UnaryOpNode u:
                        if (u.Operand != null) Walk(u.Operand);
                        break;

                    case BinaryOpNode b:
                        Walk(b.Left); Walk(b.Right);
                        break;

                    case IfNode iff:
                        Walk(iff.Condition);
                        foreach (var m in iff.ThenBody) Walk(m);
                        if (iff.ElseBody != null) foreach (var m in iff.ElseBody) Walk(m);
                        break;

                    case BlockNode blk:
                        foreach (var m in blk.Body) Walk(m);
                        break;

                    case LoopNode lp:
                        foreach (var m in lp.Body) Walk(m);
                        break;

                    case BrIfNode brIf:
                        Walk(brIf.Condition);
                        break;

                    case CallNode c:
                        if (c.Args != null) foreach (var a in c.Args) Walk(a);
                        break;

                    case SelectNode s:
                        Walk(s.V1);
                        Walk(s.V2);
                        Walk(s.Cond);
                        break;

                    case UnreachableNode:
                    case ReturnNode:
                    case NopNode:
                    case BrTableNode:
                        // nothing to collect for locals
                        break;

                    case BrNode:
                    case RawInstructionNode:
                    case ConstNode:
                        // nothing
                        break;
                }
            }

            foreach (var n in body) Walk(n);
            return max; // -1 means “no locals referenced”
        }

        // ---------------- main entry ----------------

        public WasmModule Parse()
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"❌ WAT file not found: {filePath}");

            Console.WriteLine("📖 Reading WAT file: " + filePath);
            string wasmPath = ConvertWatToWasm(filePath);

            IntPtr modulePtr = LoadWasmTextFile(wasmPath);
            if (modulePtr == IntPtr.Zero || !ValidateModule(modulePtr))
                throw new Exception("❌ Error reading or validating Binaryen module");

            PrintModuleAST(modulePtr);

            var module = new WasmModule();

            int fnCount = GetFunctionCount(modulePtr);
            Console.WriteLine($"🔢 Number of functions: {fnCount}");

            for (int fi = 0; fi < fnCount; fi++)
            {
                // optional: function name
                string? funcName = null;
                try
                {
                    IntPtr namePtr = GetFunctionNameByIndex(modulePtr, fi);
                    if (namePtr != IntPtr.Zero)
                    {
                        var nm = Marshal.PtrToStringAnsi(namePtr);
                        if (!string.IsNullOrEmpty(nm)) funcName = "$" + nm; // keep '$' to match sanitizer later
                    }
                }
                catch { /* wrapper may lack this symbol */ }

                // body text (temp module)
                IntPtr bodyPtr = GetFunctionBodyText(modulePtr, fi);
                string watBody = bodyPtr != IntPtr.Zero ? (Marshal.PtrToStringAnsi(bodyPtr) ?? "") : "";
                if (bodyPtr != IntPtr.Zero) FreeCString(bodyPtr);

                Console.WriteLine($"\n📄 Extracted body of function #{fi}:\n{watBody}");

                var tokens = Tokenize(watBody);
                Console.WriteLine("🔍 Tokens: " + string.Join(" ", tokens));

                int idx = 0;
                var body = new List<WasmNode>();
while (idx < tokens.Count)
{
    if (tokens[idx] == ")")
    {
        // These are often the closers of the wrapping (module ...) we already consumed.
        // Don't send them to ParseNode (it throws by design).
        Console.WriteLine($"↩️ Skipping stray ')' at index {idx}");
        idx++;
        continue;
    }

    Console.WriteLine($"\n🕽️ ParseNode call at index {idx}");
    body.Add(ParseNode(tokens, ref idx));Console.WriteLine($"\n🕽️ ParseNode call at index {idx}");
}

                var func = new WasmFunction { Body = body, Name = funcName };
int paramCount = 0;
try { paramCount = GetFunctionParamCount(modulePtr, fi); } catch { }
func.ParamCount = Math.Max(0, paramCount);

// 3.2) Compter les résultats via le wrapper  // NEW
int resultCount = 0;
try { resultCount = GetFunctionResultCount(modulePtr, fi); } catch { }  // NEW
func.ResultCount = Math.Max(0, resultCount);                             // NEW
                
                // infer local count by scanning references
                int maxIdx = ComputeMaxLocalIndexInBody(func.Body);
                int total = (maxIdx >= 0 ? (maxIdx + 1) : 0);
               // if (total < func.ParamCount) total = func.ParamCount;
               // func.LocalCount = Math.Max(0, total - func.ParamCount);

                if (func.LocalCount + func.ParamCount < total)
                func.LocalCount = Math.Max(0, total - func.ParamCount);

                // fill $0..$N mapping
                for (int k = 0; k < func.ParamCount + func.LocalCount; k++)
                    func.LocalIndexByName["$" + k] = k;

                // try to parse header for explicit names/slots
                PopulateFunctionSignature(tokens, func);

                Console.WriteLine($"🧭 Signature: name={func.Name ?? "<anonymous>"}, params={func.ParamCount}, locals={func.LocalCount}");

                module.Functions.Add(func);
            }

            Console.WriteLine($"✅ WAT AST generated with {module.Functions.Count} functions.");

            // Verify labels (optional)
            VerifyLabels(module);
            return module;
        }

        // ---------------- signature helpers ----------------

        private void InferSignatureFromBody(WasmFunction func)
        {
            int maxIdx = -1;
            var assigned = new HashSet<int>();

            void Walk(WasmNode n)
            {
                switch (n)
                {
                    case LocalGetNode g:
                        {
                            var k = g.Index ?? TryParseAutoName(g.Name);
                            if (k.HasValue) maxIdx = Math.Max(maxIdx, k.Value);
                            break;
                        }
                    case LocalSetNode s:
                        {
                            var k = s.Index ?? TryParseAutoName(s.Name);
                            if (k.HasValue)
                            {
                                maxIdx = Math.Max(maxIdx, k.Value);
                                assigned.Add(k.Value);
                            }
                            if (s.Value != null) Walk(s.Value);
                            break;
                        }
                    case LocalTeeNode t:
                        {
                            var k = t.Index ?? TryParseAutoName(t.Name);
                            if (k.HasValue)
                            {
                                maxIdx = Math.Max(maxIdx, k.Value);
                                assigned.Add(k.Value);
                            }
                            break;
                        }
                    case UnaryOpNode u:
                        if (u.Operand != null) Walk(u.Operand);
                        break;
                    case BinaryOpNode b:
                        Walk(b.Left); Walk(b.Right);
                        break;
                    case IfNode iff:
                        Walk(iff.Condition);
                        foreach (var m in iff.ThenBody) Walk(m);
                        if (iff.ElseBody != null) foreach (var m in iff.ElseBody) Walk(m);
                        break;
                    case BlockNode blk:
                        foreach (var m in blk.Body) Walk(m);
                        break;
                    case LoopNode lp:
                        foreach (var m in lp.Body) Walk(m);
                        break;
                    case BrIfNode brIf:
                        Walk(brIf.Condition);
                        break;
                    case CallNode c:
                        foreach (var a in c.Args) Walk(a);
                        break;
                }
            }

            foreach (var n in func.Body) Walk(n);

            int total = maxIdx + 1;
            if (total <= 0) return;

            int paramCount = assigned.Count > 0 ? assigned.Min() : total;
            if (paramCount < 0 || paramCount > total) paramCount = total;

            func.ParamCount = paramCount;
            func.LocalCount = total - paramCount;

            for (int k = 0; k < total; k++)
                func.LocalIndexByName["$" + k] = k;
        }

        private static readonly HashSet<string> NumTypes = new(StringComparer.Ordinal)
        { "i32","i64","f32","f64" };

        private static int? TryParseAutoName(string? name)
        {
            if (!string.IsNullOrEmpty(name) && name![0] == '$' &&
                int.TryParse(name.AsSpan(1), out var k)) return k;
            return null;
        }

private void PopulateFunctionSignature(List<string> tokens, WasmFunction func)
{
    for (int i = 0; i + 1 < tokens.Count; i++)
    {
        if (tokens[i] == "(" && tokens[i + 1] == "func")
        {
            i += 2;
            if (i < tokens.Count && tokens[i].StartsWith("$"))
            {
                func.Name ??= tokens[i];
                i++;
            }

            int paramIndex = func.ParamCount; // prérempli par wrapper
            int localIndex = func.LocalCount; // prérempli par inférence
            int resultCount = func.ResultCount; // prérempli par wrapper

            for (int j = i; j < tokens.Count - 1; j++)
            {
                if (tokens[j] != "(") continue;
                string head = tokens[j + 1];

                if (head == "param")
                {
                    j += 2;
                    while (j < tokens.Count && tokens[j] != ")")
                    {
                        if (tokens[j].StartsWith("$"))
                        {
                            string name = tokens[j]; j++;
                            if (j < tokens.Count && NumTypes.Contains(tokens[j]))
                            {
                                func.LocalIndexByName[name] = paramIndex;
                                paramIndex++;
                                j++;
                            }
                            else { /* ignore */ }
                        }
                        else if (NumTypes.Contains(tokens[j]))
                        {
                            paramIndex++; j++;
                        }
                        else j++;
                    }
                    func.ParamCount = paramIndex;
                }
                else if (head == "local")
                {
                    j += 2;
                    while (j < tokens.Count && tokens[j] != ")")
                    {
                        if (tokens[j].StartsWith("$"))
                        {
                            string name = tokens[j]; j++;
                            if (j < tokens.Count && NumTypes.Contains(tokens[j]))
                            {
                                func.LocalIndexByName[name] = func.ParamCount + (localIndex++);
                                j++;
                            }
                            else { /* ignore */ }
                        }
                        else if (NumTypes.Contains(tokens[j]))
                        {
                            localIndex++; j++;
                        }
                        else j++;
                    }
                    func.LocalCount = localIndex;
                }
                else if (head == "result")
                {
                    // (result i32) ou (result i32 i32)
                    j += 2;
                    int added = 0;
                    while (j < tokens.Count && tokens[j] != ")")
                    {
                        if (NumTypes.Contains(tokens[j])) { added++; j++; }
                        else j++;
                    }
                    if (added == 0 && j < tokens.Count && tokens[j] == ")") { /* (result) vide → 0 */ }
                    resultCount += added;
                    func.ResultCount = resultCount;
                }
                else if (head == ")")
                {
                    break;
                }
            }
            break;
        }
    }

    // Ensure $0..$N existent
    for (int k = 0; k < func.ParamCount + func.LocalCount; k++)
    {
        var auto = "$" + k.ToString();
        if (!func.LocalIndexByName.ContainsKey(auto))
            func.LocalIndexByName[auto] = k;
    }

    Console.WriteLine($"🧭 Signature: name={func.Name ?? "<anonymous>"}, params={func.ParamCount}, locals={func.LocalCount}, results={func.ResultCount}");
}

        // ---------------- label verification ----------------

        public void VerifyLabels(WasmModule module)
        {
            Console.WriteLine("🔍 Verifying labels...");
            foreach (var function in module.Functions)
            {
                var availableLabels = new HashSet<string>();
                var labelScopes = new Stack<HashSet<string>>();
                var labelDepths = new Dictionary<string, int>();
                labelScopes.Push(new HashSet<string>());
                VerifyLabelsInNode(function.Body, availableLabels, labelScopes, labelDepths, 0);
            }
            Console.WriteLine("✅ Label verification completed successfully.");
        }

        private void VerifyLabelsInNode(List<WasmNode> nodes, HashSet<string> availableLabels,
                                        Stack<HashSet<string>> labelScopes,
                                        Dictionary<string, int> labelDepths, int currentDepth)
        {
            foreach (var node in nodes)
                VerifyLabelsInNode(node, availableLabels, labelScopes, labelDepths, currentDepth);
        }

        private static bool IsNumericDepth(string s) => s.Length > 0 && char.IsDigit(s[0]);

        private void VerifyLabelsInNode(WasmNode node, HashSet<string> availableLabels,
                                        Stack<HashSet<string>> labelScopes,
                                        Dictionary<string, int> labelDepths, int currentDepth)
        {
            switch (node)
            {
                case BlockNode blockNode:
                    VerifyBlockNode(blockNode, availableLabels, labelScopes, labelDepths, currentDepth);
                    break;

                case LoopNode loopNode:
                    VerifyLoopNode(loopNode, availableLabels, labelScopes, labelDepths, currentDepth);
                    break;

                case BrNode brNode:
                    VerifyBranchNode(brNode, availableLabels, "br", labelDepths);
                    break;

                case BrIfNode brIfNode:
                    VerifyBranchNode(brIfNode, availableLabels, "br_if", labelDepths);
                    break;

                case BrTableNode bt:
                    {
                        void CheckLab(string lab, string kind)
                        {
                            if (IsNumericDepth(lab)) return;
                            if (!availableLabels.Contains(lab))
                            {
                                var avail = availableLabels.Count > 0 ? string.Join(", ", availableLabels) : "aucun";
                                throw new Exception($"❌ Label invalide dans br_table {kind} : {lab}. Labels disponibles : {avail}");
                            }
                        }
                        foreach (var t in bt.Targets) CheckLab(t, "target");
                        CheckLab(bt.Default, "default");
                        break;
                    }

                case UnaryOpNode unaryNode:
                    VerifyLabelsInNode(unaryNode.Operand, availableLabels, labelScopes, labelDepths, currentDepth);
                    break;

                case BinaryOpNode binaryNode:
                    VerifyLabelsInNode(binaryNode.Left, availableLabels, labelScopes, labelDepths, currentDepth);
                    VerifyLabelsInNode(binaryNode.Right, availableLabels, labelScopes, labelDepths, currentDepth);
                    break;

                case IfNode ifNode:
                    VerifyLabelsInNode(ifNode.Condition, availableLabels, labelScopes, labelDepths, currentDepth);
                    VerifyLabelsInNode(ifNode.ThenBody, availableLabels, labelScopes, labelDepths, currentDepth);
                    if (ifNode.ElseBody != null)
                        VerifyLabelsInNode(ifNode.ElseBody, availableLabels, labelScopes, labelDepths, currentDepth);
                    break;

                // benign nodes
                case ReturnNode:
                case NopNode:
                case SelectNode:
                case UnreachableNode:
                case LocalGetNode:
                case LocalSetNode:
                case LocalTeeNode:
                case CallNode:
                case ConstNode:
                case RawInstructionNode:
                //case BrTableNode:
                    break;
            }
        }

        private void VerifyBlockNode(BlockNode blockNode, HashSet<string> availableLabels,
                                     Stack<HashSet<string>> labelScopes,
                                     Dictionary<string, int> labelDepths, int currentDepth)
        {
            var newScope = new HashSet<string>();
            labelScopes.Push(newScope);
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                if (availableLabels.Contains(blockNode.Label))
                    throw new Exception($"❌ Duplicate label found: {blockNode.Label} (depth {currentDepth})");
                availableLabels.Add(blockNode.Label);
                newScope.Add(blockNode.Label);
                labelDepths[blockNode.Label] = currentDepth;
                Console.WriteLine($"🔹 Block label added: {blockNode.Label} (depth {currentDepth})");
            }
            VerifyLabelsInNode(blockNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            labelScopes.Pop();
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                availableLabels.Remove(blockNode.Label);
                labelDepths.Remove(blockNode.Label);
            }
        }

        private void VerifyLoopNode(LoopNode loopNode, HashSet<string> availableLabels,
                                    Stack<HashSet<string>> labelScopes,
                                    Dictionary<string, int> labelDepths, int currentDepth)
        {
            var newScope = new HashSet<string>();
            labelScopes.Push(newScope);
            if (!string.IsNullOrEmpty(loopNode.Label))
            {
                if (availableLabels.Contains(loopNode.Label))
                    throw new Exception($"❌ Label dupliqué trouvé : {loopNode.Label} (profondeur {currentDepth})");
                availableLabels.Add(loopNode.Label);
                newScope.Add(loopNode.Label);
                labelDepths[loopNode.Label] = currentDepth;
                Console.WriteLine($"🔹 Loop label added: {loopNode.Label} (depth {currentDepth})");
            }
            VerifyLabelsInNode(loopNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            labelScopes.Pop();
            if (!string.IsNullOrEmpty(loopNode.Label))
            {
                availableLabels.Remove(loopNode.Label);
                labelDepths.Remove(loopNode.Label);
            }
        }

        private void VerifyBranchNode(WasmNode branchNode, HashSet<string> availableLabels,
                                      string branchType, Dictionary<string, int> labelDepths)
        {
            static bool IsNumericDepthLocal(string s) => s.Length > 0 && char.IsDigit(s[0]);

            string label = branchNode switch
            {
                BrNode brNode => brNode.Label,
                BrIfNode brIfNode => brIfNode.Label,
                _ => throw new ArgumentException($"Type de nœud de branchement non supporté : {branchNode.GetType()}")
            };

            if (!IsNumericDepthLocal(label) && !availableLabels.Contains(label))
            {
                var availableLabelsList = availableLabels.Count > 0 ? string.Join(", ", availableLabels) : "aucun";
                throw new Exception($"❌ Label invalide dans {branchType} : {label}. Labels disponibles : {availableLabelsList}");
            }
            var labelDepth = labelDepths.GetValueOrDefault(label, -1);
            Console.WriteLine($"🔹 {branchType} to valid label: {label} (depth {labelDepth})");
        }

        // ---------------- tokenizer & parser ----------------

        private List<string> Tokenize(string wat)
        {
            return new List<string>(
                wat.Replace("(", " ( ").Replace(")", " ) ")
                   .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            );
        }

        private WasmNode ParseNode(List<string> tokens, ref int index)
        {
            if (tokens[index] == "(")
            {
                index++;
                string op = tokens[index++];
                Console.WriteLine($"🔸 Block start: ({op}");

                if (op.EndsWith(".const"))
                {
                    string value = tokens[index++];
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  🔹 ConstNode({op}, {value})");
                    return new ConstNode { Type = op.Split('.')[0], Value = value };
                }
                else if (op == "local.get")
                {
                    string tok = tokens[index++];
                    int n;
                    var node = new LocalGetNode();
                    if (tok.StartsWith("$")) node.Name = tok;
                    else if (int.TryParse(tok, out n)) node.Index = n;
                    ExpectToken(tokens, ref index, ")");
                    return node;
                }
                else if (op == "local.set")
                {
                    string tok = tokens[index++];
                    int n;
                    var node = new LocalSetNode();
                    if (tok.StartsWith("$")) node.Name = tok;
                    else if (int.TryParse(tok, out n)) node.Index = n;

                    if (index < tokens.Count && tokens[index] != ")")
                        node.Value = ParseNode(tokens, ref index);

                    ExpectToken(tokens, ref index, ")");
                    return node;
                }
                else if (op == "local.tee")
                {
                    string tok = tokens[index++];
                    int n;
                    var tee = new LocalTeeNode();
                    if (tok.StartsWith("$")) tee.Name = tok;
                    else if (int.TryParse(tok, out n)) tee.Index = n;

                    if (index < tokens.Count && tokens[index] != ")")
                    {
                        var valueExpr = ParseNode(tokens, ref index);
                        ExpectToken(tokens, ref index, ")");
                        return new BlockNode { Label = null, Body = { valueExpr, tee } };
                    }
                    else
                    {
                        ExpectToken(tokens, ref index, ")");
                        return tee;
                    }
                }
                else if (op == "call")
                {
                    string tgt = tokens[index++];

                    var args = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        args.Add(ParseNode(tokens, ref index));

                    ExpectToken(tokens, ref index, ")");
                    return new CallNode { Target = tgt, Args = args };
                }
                else if (op == "return")
{
    // Accept both "(return)" and folded "(return <expr> ...)"
    var pre = new List<WasmNode>();
    while (index < tokens.Count && tokens[index] != ")")
    {
        pre.Add(ParseNode(tokens, ref index));
    }
    ExpectToken(tokens, ref index, ")");

    if (pre.Count == 0)
    {
        // plain "(return)"
        return new ReturnNode();
    }
    else
    {
        // folded: evaluate the return values first, then return
        var b = new BlockNode { Label = null };
        foreach (var n in pre) b.Body.Add(n);
        b.Body.Add(new ReturnNode());
        return b;
    }
}

                else if (op == "nop")
                {
                    ExpectToken(tokens, ref index, ")");
                    return new NopNode();
                }
else if (op == "br_table")
{
    // Supporte les deux formes de WAT :
    //  A) Canonique (operand-last): (br_table t0 t1 ... default (local.get $x))
    //  B) Operand-first (rare):     (br_table (local.get $x) t0 t1 ... default)

    var targets = new List<string>();
    bool sawSelector = false;

    // 1) Parcourir les tokens jusqu'à la parenthèse fermante de ce br_table
    //    - Si on voit "(", on parse une sous-expression = le sélecteur. (ParseNode consomme sa ")")
    //    - Si on voit un atome != ")", on le considère comme une cible (label ou profondeur)
    //    - Si on voit ")", on termine.
    while (index < tokens.Count)
    {
        string tk = tokens[index];

        if (tk == ")")
        {
            index++; // consomme la ') ' de (br_table ... )
            break;
        }
        else if (tk == "(")
        {
            // Sous-expression = sélecteur
            _ = ParseNode(tokens, ref index); // consomme entièrement le sélecteur (et sa ')')
            sawSelector = true;
        }
        else
        {
            // Atome : soit label "$L", soit profondeur "0"/"1"/...
            targets.Add(tk);
            index++;
        }
    }

    if (!sawSelector)
        throw new Exception("br_table: expression sélecteur absente (attendu operand-last ou operand-first).");

    if (targets.Count == 0)
        throw new Exception("br_table: au moins une cible + un default sont requis.");

    // Le dernier élément est le default
    var def = targets[^1];
    targets.RemoveAt(targets.Count - 1);

    return new BrTableNode { Targets = targets, Default = def };
}



                else if (IsUnaryOp(op))
                {
                    Console.WriteLine($"  🔹 UnaryOpNode : {op}");
                    var operand = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new UnaryOpNode { Op = op, Operand = operand };
                }
                else if (IsBinaryOp(op))
                {
                    Console.WriteLine($"  🔹 BinaryOpNode : {op}");
                    var left = ParseNode(tokens, ref index);
                    var right = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new BinaryOpNode { Op = op, Left = left, Right = right };
                }
                else if (op == "block")
                {
                    Console.WriteLine("  🔹 Block block");
                    string? label = tokens[index].StartsWith("$") ? tokens[index++] : null;
                    var body = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        body.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = label, Body = body };
                }
                else if (op == "loop")
                {
                    Console.WriteLine("  🔹 Block loop");
                    string? label = tokens[index].StartsWith("$") ? tokens[index++] : null;
                    var body = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        body.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new LoopNode { Label = label, Body = body };
                }
                else if (op == "if")
                {
                    Console.WriteLine("  🔹 Block implicit if");
                    var condition = ParseNode(tokens, ref index);
                    var thenBody = new List<WasmNode> { ParseNode(tokens, ref index) };
                    List<WasmNode>? elseBody = null;
                    if (index < tokens.Count && tokens[index] == "(")
                        elseBody = new List<WasmNode> { ParseNode(tokens, ref index) };
                    ExpectToken(tokens, ref index, ")");
                    return new IfNode { Condition = condition, ThenBody = thenBody, ElseBody = elseBody };
                }
                else if (op == "br")
                {
                    string label = tokens[index++];
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  🔹 Br vers label {label}");
                    return new BrNode { Label = label };
                }
                else if (op == "br_if")
                {
                    string label = tokens[index++];
                    var condition = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  🔹 Br_if to label {label}");
                    return new BrIfNode { Label = label, Condition = condition };
                }
                else if (op == "module" || op == "type" || op == "func")
                {
                    Console.WriteLine($"  ⚙️ Structure block: {op}");
                    var inner = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        inner.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = op, Body = inner };
                }
                else if (op == "unreachable")
                {
                    ExpectToken(tokens, ref index, ")");
                    return new UnreachableNode();
                }
                else if (op == "select")
                {
                    // (select [t] v1 v2 cond)
                    string? maybeType = (index < tokens.Count ? tokens[index] : null);
                    if (maybeType == "i32" || maybeType == "i64" || maybeType == "f32" || maybeType == "f64")
                        index++; // ignore type tag

                    var v1 = ParseNode(tokens, ref index);
                    var v2 = ParseNode(tokens, ref index);
                    var cond = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new SelectNode { V1 = v1, V2 = v2, Cond = cond };
                }
                else
                {
                    // Generic: skip atoms until ')', recurse only on nested lists
                    Console.WriteLine($"  📦 Generic instruction: {op}");
                    while (index < tokens.Count && tokens[index] != ")")
                    {
                        if (tokens[index] == "(")
                        {
                            var _ = ParseNode(tokens, ref index);
                        }
                        else
                        {
                            index++; // plain atom
                        }
                    }
                    ExpectToken(tokens, ref index, ")");
                    return new RawInstructionNode { Instruction = op };
                }
            }
            else if (tokens[index] == ")")
            {
                // Better context around the error
                int from = Math.Max(0, index - 5);
                int to = Math.Min(tokens.Count - 1, index + 5);
                var window = string.Join(" ", tokens.GetRange(from, to - from + 1));
                throw new Exception($"❌ Parenthèse fermante inattendue. Contexte: ... {window} ...");
            }
            else
            {
                Console.WriteLine($"📌 Isolated instruction: {tokens[index]}");
                return new RawInstructionNode { Instruction = tokens[index++] };
            }
        }

        private bool IsUnaryOp(string op) =>
            op == "drop" || op.EndsWith(".eqz") || op.EndsWith(".wrap_i64");

        private bool IsBinaryOp(string op) =>
            op.EndsWith(".add") || op.EndsWith(".sub") || op.EndsWith(".mul") ||
            op.EndsWith(".div_s") || op.EndsWith(".div_u") || op.EndsWith(".div") ||
            op.EndsWith(".eq") || op.EndsWith(".ne") ||
            op.EndsWith(".lt_s") || op.EndsWith(".lt_u") ||
            op.EndsWith(".le_s") || op.EndsWith(".le_u") || op.EndsWith(".le") || op.EndsWith(".lt") ||
            op.EndsWith(".gt_s") || op.EndsWith(".gt_u") ||
            op.EndsWith(".ge_s") || op.EndsWith(".ge_u") || op.EndsWith(".ge") || op.EndsWith(".gt");

private void ExpectToken(List<string> tokens, ref int index, string expected)
{
    if (index >= tokens.Count || tokens[index] != expected)
    {
        int from = Math.Max(0, index - 5);
        int to = Math.Min(tokens.Count - 1, index + 5);
        var window = string.Join(" ", tokens.GetRange(from, to - from + 1));
        throw new Exception($"❌ Parenthèse '{expected}' attendue. Contexte: ... {window} ...");
    }
    index++;
}


        private string ConvertWatToWasm(string watPath)
        {
            string wasmPath = Path.ChangeExtension(watPath, ".wasm");
            var psi = new ProcessStartInfo
            {
                FileName = "wat2wasm",
                Arguments = $"{watPath} -o {wasmPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new Exception("wat2wasm failed: " + proc.StandardError.ReadToEnd());

            return wasmPath;
        }

        // ===== Binaryen wrapper imports =====

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr LoadWasmTextFile(string filename);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetFunctionCount(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFunctionNameByIndex(IntPtr module, int index);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFunctionBodyText(IntPtr module, int index); // strdup'ed; must FreeCString

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeCString(IntPtr s);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ValidateModule(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PrintModuleAST(IntPtr module);

        [DllImport("libbinaryenwrapper", EntryPoint = "GetFunctionParamCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetFunctionParamCount(IntPtr module, int index);

        [DllImport("libbinaryenwrapper", EntryPoint = "GetFunctionResultCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetFunctionResultCount(IntPtr module, int index);
    }
}
