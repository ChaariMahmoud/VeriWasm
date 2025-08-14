using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WasmToBoogie.Parser.Ast;

namespace WasmToBoogie.Parser
{
    public class WasmParser
    {
        private readonly string filePath;

        public WasmParser(string filePath)
        {
            this.filePath = filePath;
        }
        private static int ComputeMaxLocalIndexInBody(List<WasmNode> body)
        {
            int max = -1;

            void Walk(WasmNode n)
            {
                switch (n)
                {
                    case LocalGetNode g:
                        {
                            int? k = g.Index ?? TryParseAutoName(g.Name);
                            if (k.HasValue) max = Math.Max(max, k.Value);
                            break;
                        }
                    case LocalSetNode s:
                        {
                            int? k = s.Index ?? TryParseAutoName(s.Name);
                            if (k.HasValue) max = Math.Max(max, k.Value);
                            if (s.Value != null) Walk(s.Value);
                            break;
                        }
                    case LocalTeeNode t:
                        {
                            int? k = t.Index ?? TryParseAutoName(t.Name);
                            if (k.HasValue) max = Math.Max(max, k.Value);
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
                // nothing
                break;
                }
            }

            foreach (var n in body) Walk(n);
            return max; // -1 means “no locals referenced”
        }

        public WasmModule Parse()
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"❌ Fichier WAT introuvable : {filePath}");

            Console.WriteLine("📖 Lecture du fichier WAT : " + filePath);
            string wasmPath = ConvertWatToWasm(filePath);

            IntPtr modulePtr = LoadWasmTextFile(wasmPath);
            if (modulePtr == IntPtr.Zero || !ValidateModule(modulePtr))
                throw new Exception("❌ Erreur de lecture ou validation du module Binaryen");

            PrintModuleAST(modulePtr);

            var module = new WasmModule();

            int fnCount = GetFunctionCount(modulePtr);
            Console.WriteLine($"🔢 Nombre de fonctions : {fnCount}");

            for (int fi = 0; fi < fnCount; fi++)
            {
                // --- name from Binaryen (optional but nice) ---
                string? funcName = null;
                try
                {
                    IntPtr namePtr = GetFunctionNameByIndex(modulePtr, fi);
                    if (namePtr != IntPtr.Zero)
                    {
                        var nm = Marshal.PtrToStringAnsi(namePtr);
                        if (!string.IsNullOrEmpty(nm)) funcName = "$" + nm; // keep '$' to match your sanitizer later
                    }
                }
                catch { /* harmless if wrapper lacks the symbol */ }

                // --- body text from Binaryen (temp (module (func $temp ...))) ---
                IntPtr bodyPtr = GetFunctionBodyText(modulePtr, fi);
                string watBody = bodyPtr != IntPtr.Zero ? (Marshal.PtrToStringAnsi(bodyPtr) ?? "") : "";
                if (bodyPtr != IntPtr.Zero) FreeCString(bodyPtr);

                Console.WriteLine($"\n📄 Corps extrait de la fonction #{fi} :\n{watBody}");

                var tokens = Tokenize(watBody);
                Console.WriteLine("🔍 Tokens : " + string.Join(" ", tokens));

                int idx = 0;
                var body = new List<WasmNode>();
                while (idx < tokens.Count)
                {
                    Console.WriteLine($"\n🕽️ Appel ParseNode à l'index {idx}");
                    body.Add(ParseNode(tokens, ref idx));
                }

                var func = new WasmFunction { Body = body, Name = funcName };
                int paramCount = 0;
                try { paramCount = GetFunctionParamCount(modulePtr, fi); } catch { /* ignore */ }
                func.ParamCount = Math.Max(0, paramCount);

                // 2) Compute how many indices are actually referenced in the body
                int maxIdx = ComputeMaxLocalIndexInBody(func.Body); // -1 if none
                int total = (maxIdx >= 0 ? (maxIdx + 1) : 0);

                // Ensure total covers at least the params
                if (total < func.ParamCount) total = func.ParamCount;

                // 3) Derive local count
                func.LocalCount = Math.Max(0, total - func.ParamCount);

                // 4) Fill Binaryen-style auto names $0..$(total-1)
                for (int k = 0; k < total; k++)
                    func.LocalIndexByName["$" + k] = k;
                // Try to read signature from header inside snippet (if Binaryen included it)
                PopulateFunctionSignature(tokens, func);

                // Fallback: infer from body if header info is missing
                /* if (func.ParamCount == 0 && func.LocalCount == 0)
                     InferSignatureFromBody(func);*/

                Console.WriteLine($"🧭 Signature: name={func.Name ?? "<anonymous>"}, params={func.ParamCount}, locals={func.LocalCount}");

                module.Functions.Add(func);
            }

            Console.WriteLine($"✅ AST WAT généré avec {module.Functions.Count} fonctions.");

            // Verify labels (optional)
            VerifyLabels(module);
            return module;
        }

        // ------- signature inference from body (unchanged) -------
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
            if (total <= 0) return; // nothing to infer

            int paramCount = assigned.Count > 0 ? assigned.Min() : total;
            if (paramCount < 0 || paramCount > total) paramCount = total;

            func.ParamCount = paramCount;
            func.LocalCount = total - paramCount;

            for (int k = 0; k < total; k++)
                func.LocalIndexByName["$" + k] = k;
        }

        // --- signature extraction (very lightweight) ---
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
            // Find first "( func ... )" and scan for (param ...) and (local ...)
            for (int i = 0; i + 1 < tokens.Count; i++)
            {
                if (tokens[i] == "(" && tokens[i + 1] == "func")
                {
                    i += 2;
                    // optional function name
                    if (i < tokens.Count && tokens[i].StartsWith("$"))
                    {
                        func.Name ??= tokens[i];
                        i++;
                    }

                    int paramIndex = 0;
                    int localIndex = 0;
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
                                        func.LocalIndexByName[name] = func.ParamCount + localIndex;
                                        localIndex++;
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
                        else if (head == ")")
                        {
                            break;
                        }
                    }
                    break; // first func header found
                }
            }
            // Ensure default Binaryen-style auto-names $0..$(n+m-1)
            for (int k = 0; k < func.ParamCount + func.LocalCount; k++)
            {
                var auto = "$" + k.ToString();
                if (!func.LocalIndexByName.ContainsKey(auto))
                    func.LocalIndexByName[auto] = k;
            }

            Console.WriteLine($"🧭 Signature: name={func.Name ?? "<anonymous>"}, params={func.ParamCount}, locals={func.LocalCount}");
        }

        // --- label verification (unchanged) ---
        public void VerifyLabels(WasmModule module)
        {
            Console.WriteLine("🔍 Vérification des labels...");
            foreach (var function in module.Functions)
            {
                var availableLabels = new HashSet<string>();
                var labelScopes = new Stack<HashSet<string>>();
                var labelDepths = new Dictionary<string, int>();
                labelScopes.Push(new HashSet<string>());
                VerifyLabelsInNode(function.Body, availableLabels, labelScopes, labelDepths, 0);
            }
            Console.WriteLine("✅ Vérification des labels terminée avec succès.");
        }

        private void VerifyLabelsInNode(List<WasmNode> nodes, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
        {
            foreach (var node in nodes)
                VerifyLabelsInNode(node, availableLabels, labelScopes, labelDepths, currentDepth);
        }

        private void VerifyLabelsInNode(WasmNode node, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
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
            }
        }

        private void VerifyBlockNode(BlockNode blockNode, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
        {
            var newScope = new HashSet<string>();
            labelScopes.Push(newScope);
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                if (availableLabels.Contains(blockNode.Label))
                    throw new Exception($"❌ Label dupliqué trouvé : {blockNode.Label} (profondeur {currentDepth})");
                availableLabels.Add(blockNode.Label);
                newScope.Add(blockNode.Label);
                labelDepths[blockNode.Label] = currentDepth;
                Console.WriteLine($"🔹 Label de bloc ajouté : {blockNode.Label} (profondeur {currentDepth})");
            }
            VerifyLabelsInNode(blockNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            labelScopes.Pop();
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                availableLabels.Remove(blockNode.Label);
                labelDepths.Remove(blockNode.Label);
            }
        }

        private void VerifyLoopNode(LoopNode loopNode, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
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
                Console.WriteLine($"🔹 Label de boucle ajouté : {loopNode.Label} (profondeur {currentDepth})");
            }
            VerifyLabelsInNode(loopNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            labelScopes.Pop();
            if (!string.IsNullOrEmpty(loopNode.Label))
            {
                availableLabels.Remove(loopNode.Label);
                labelDepths.Remove(loopNode.Label);
            }
        }

        private void VerifyBranchNode(WasmNode branchNode, HashSet<string> availableLabels, string branchType, Dictionary<string, int> labelDepths)
        {
            string label = branchNode switch
            {
                BrNode brNode => brNode.Label,
                BrIfNode brIfNode => brIfNode.Label,
                _ => throw new ArgumentException($"Type de nœud de branchement non supporté : {branchNode.GetType()}")
            };
            if (!availableLabels.Contains(label))
            {
                var availableLabelsList = availableLabels.Count > 0 ? string.Join(", ", availableLabels) : "aucun";
                throw new Exception($"❌ Label invalide dans {branchType} : {label}. Labels disponibles : {availableLabelsList}");
            }
            var labelDepth = labelDepths.GetValueOrDefault(label, -1);
            Console.WriteLine($"🔹 {branchType} vers label valide : {label} (profondeur {labelDepth})");
        }

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
                Console.WriteLine($"🔸 Début bloc : ({op}");

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

                    // folded form? (local.set X <expr>)
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
                    // Folded form: (call $f <arg1> <arg2> ...)
                    while (index < tokens.Count && tokens[index] != ")")
                        args.Add(ParseNode(tokens, ref index));

                    ExpectToken(tokens, ref index, ")");

                    return new CallNode { Target = tgt, Args = args };
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
                    Console.WriteLine("  🔹 Bloc block");
                    string? label = tokens[index].StartsWith("$") ? tokens[index++] : null;
                    var body = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        body.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = label, Body = body };
                }
                else if (op == "loop")
                {
                    Console.WriteLine("  🔹 Bloc loop");
                    string? label = tokens[index].StartsWith("$") ? tokens[index++] : null;
                    var body = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        body.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new LoopNode { Label = label, Body = body };
                }
                else if (op == "if")
                {
                    Console.WriteLine("  🔹 Bloc if implicite");
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
                    Console.WriteLine($"  🔹 Br_if vers label {label}");
                    return new BrIfNode { Label = label, Condition = condition };
                }
                else if (op == "module" || op == "type" || op == "func")
                {
                    Console.WriteLine($"  ⚙️ Bloc de structure : {op}");
                    var inner = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        inner.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = op, Body = inner };
                }
                else if (op == "unreachable")
                {
                    // (unreachable)
                    ExpectToken(tokens, ref index, ")");
                    return new UnreachableNode();
                }
                else if (op == "select")
                {
                    // (select v1 v2 cond)    // Binaryen usually emits this folded shape
                    // also supports typed form: (select t v1 v2 cond) — we ignore 't' if present
                    // Peek for an optional numeric type token (i32/i64/f32/f64)
                    string? maybeType = (index < tokens.Count ? tokens[index] : null);
                    if (maybeType == "i32" || maybeType == "i64" || maybeType == "f32" || maybeType == "f64")
                        index++; // skip the annotated type

                    var v1 = ParseNode(tokens, ref index);
                    var v2 = ParseNode(tokens, ref index);
                    var cond = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new SelectNode { V1 = v1, V2 = v2, Cond = cond };
                }
                else
                {
                    Console.WriteLine($"  📦 Instruction générique : {op}");
                    while (index < tokens.Count && tokens[index] != ")")
                        ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new RawInstructionNode { Instruction = op };
                }
            }
            else if (tokens[index] == ")")
            {
                throw new Exception("❌ Parenthèse fermante inattendue.");
            }
            else
            {
                Console.WriteLine($"📌 Instruction isolée : {tokens[index]}");
                return new RawInstructionNode { Instruction = tokens[index++] };
            }
        }

        private bool IsUnaryOp(string op) =>
            op == "drop" || op.EndsWith(".eqz") || op.EndsWith(".wrap_i64");

        private bool IsBinaryOp(string op) =>
            op.EndsWith(".add") || op.EndsWith(".sub") || op.EndsWith(".mul") || op.EndsWith(".div_s") || op.EndsWith(".div_u") || op.EndsWith(".div") ||
            op.EndsWith(".eq") || op.EndsWith(".ne") || op.EndsWith(".lt_s") || op.EndsWith(".lt_u") || op.EndsWith(".le_s") || op.EndsWith(".le_u") || op.EndsWith(".le") || op.EndsWith(".lt") ||
            op.EndsWith(".gt_s") || op.EndsWith(".gt_u") || op.EndsWith(".ge_s") || op.EndsWith(".ge_u") || op.EndsWith(".ge") || op.EndsWith(".gt");

        private void ExpectToken(List<string> tokens, ref int index, string expected)
        {
            if (index >= tokens.Count || tokens[index] != expected)
                throw new Exception($"❌ Parenthèse fermante '{expected}' attendue.");
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
        private static extern IntPtr GetFunctionNameByIndex(IntPtr module, int index); // may return pointer owned by Binaryen

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

    }
}
