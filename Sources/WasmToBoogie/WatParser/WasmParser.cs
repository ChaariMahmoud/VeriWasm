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

        public WasmModule Parse()
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"\u274C Fichier WAT introuvable : {filePath}");

            Console.WriteLine("\ud83d\udcd6 Lecture du fichier WAT : " + filePath);
            string wasmPath = ConvertWatToWasm(filePath);

            IntPtr modulePtr = LoadWasmTextFile(wasmPath);
            if (modulePtr == IntPtr.Zero || !ValidateModule(modulePtr))
                throw new Exception("\u274C Erreur de lecture ou validation du module Binaryen");

            PrintModuleAST(modulePtr);

            string watBody = Marshal.PtrToStringAnsi(GetFunctionBodyText(modulePtr, 0)) ?? "";
            Console.WriteLine("\ud83d\udcc4 Corps extrait de la fonction :\n" + watBody);

            var tokens = Tokenize(watBody);
            Console.WriteLine("\ud83d\udd0d Tokens : " + string.Join(" ", tokens));

            int idx = 0;
            var body = new List<WasmNode>();

            while (idx < tokens.Count)
            {
                Console.WriteLine($"\n\ud83d\udd7d\ufe0f Appel ParseNode \u00e0 l'index {idx}");
                body.Add(ParseNode(tokens, ref idx));
            }

            Console.WriteLine("\u2705 AST WAT g\u00e9n\u00e9r\u00e9 avec succ\u00e8s.");
            
            var module = new WasmModule
            {
                Functions = { new WasmFunction { Body = body } }
            };

            // Verify labels after parsing
            VerifyLabels(module);
            
            return module;
        }

        /// <summary>
        /// Verifies that all branch instructions reference valid labels
        /// </summary>
        public void VerifyLabels(WasmModule module)
        {
            Console.WriteLine("\ud83d\udd0d Vérification des labels...");
            
            foreach (var function in module.Functions)
            {
                var availableLabels = new HashSet<string>();
                var labelScopes = new Stack<HashSet<string>>();
                var labelDepths = new Dictionary<string, int>(); // Track label nesting depth
                labelScopes.Push(new HashSet<string>());
                
                VerifyLabelsInNode(function.Body, availableLabels, labelScopes, labelDepths, 0);
            }
            
            Console.WriteLine("\u2705 Vérification des labels terminée avec succès.");
        }

        /// <summary>
        /// Recursively verifies labels in a list of nodes
        /// </summary>
        private void VerifyLabelsInNode(List<WasmNode> nodes, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
        {
            foreach (var node in nodes)
            {
                VerifyLabelsInNode(node, availableLabels, labelScopes, labelDepths, currentDepth);
            }
        }

        /// <summary>
        /// Recursively verifies labels in a single node
        /// </summary>
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
                    {
                        VerifyLabelsInNode(ifNode.ElseBody, availableLabels, labelScopes, labelDepths, currentDepth);
                    }
                    break;
            }
        }

        /// <summary>
        /// Verifies labels in a block node
        /// </summary>
        private void VerifyBlockNode(BlockNode blockNode, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
        {
            // Create new scope for this block
            var newScope = new HashSet<string>();
            labelScopes.Push(newScope);
            
            // Add block label to available labels if it exists
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                // Check for duplicates in the global available labels set
                if (availableLabels.Contains(blockNode.Label))
                {
                    throw new Exception($"\u274C Label dupliqué trouvé : {blockNode.Label} (profondeur {currentDepth})");
                }
                availableLabels.Add(blockNode.Label);
                newScope.Add(blockNode.Label);
                labelDepths[blockNode.Label] = currentDepth;
                Console.WriteLine($"\ud83d\udd39 Label de bloc ajouté : {blockNode.Label} (profondeur {currentDepth})");
            }
            
            // Verify labels in block body
            VerifyLabelsInNode(blockNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            
            // Remove block scope
            labelScopes.Pop();
            
            // Remove block label from available labels if it exists
            if (!string.IsNullOrEmpty(blockNode.Label))
            {
                availableLabels.Remove(blockNode.Label);
                labelDepths.Remove(blockNode.Label);
            }
        }

        /// <summary>
        /// Verifies labels in a loop node
        /// </summary>
        private void VerifyLoopNode(LoopNode loopNode, HashSet<string> availableLabels, Stack<HashSet<string>> labelScopes, Dictionary<string, int> labelDepths, int currentDepth)
        {
            // Create new scope for this loop
            var newScope = new HashSet<string>();
            labelScopes.Push(newScope);
            
            // Add loop label to available labels if it exists
            if (!string.IsNullOrEmpty(loopNode.Label))
            {
                // Check for duplicates in the global available labels set
                if (availableLabels.Contains(loopNode.Label))
                {
                    throw new Exception($"\u274C Label dupliqué trouvé : {loopNode.Label} (profondeur {currentDepth})");
                }
                availableLabels.Add(loopNode.Label);
                newScope.Add(loopNode.Label);
                labelDepths[loopNode.Label] = currentDepth;
                Console.WriteLine($"\ud83d\udd39 Label de boucle ajouté : {loopNode.Label} (profondeur {currentDepth})");
            }
            
            // Verify labels in loop body
            VerifyLabelsInNode(loopNode.Body, availableLabels, labelScopes, labelDepths, currentDepth + 1);
            
            // Remove loop scope
            labelScopes.Pop();
            
            // Remove loop label from available labels if it exists
            if (!string.IsNullOrEmpty(loopNode.Label))
            {
                availableLabels.Remove(loopNode.Label);
                labelDepths.Remove(loopNode.Label);
            }
        }

        /// <summary>
        /// Verifies that a branch instruction references a valid label
        /// </summary>
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
                throw new Exception($"\u274C Label invalide dans {branchType} : {label}. Labels disponibles : {availableLabelsList}");
            }
            
            var labelDepth = labelDepths.GetValueOrDefault(label, -1);
            Console.WriteLine($"\ud83d\udd39 {branchType} vers label valide : {label} (profondeur {labelDepth})");
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
                Console.WriteLine($"\ud83d\udd38 D\u00e9but bloc : ({op}");

                if (op.EndsWith(".const"))
                {
                    string value = tokens[index++];
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  \ud83d\udd39 ConstNode({op}, {value})");
                    return new ConstNode { Type = op.Split('.')[0], Value = value };
                }
                else if (IsUnaryOp(op))
                {
                    Console.WriteLine($"  \ud83d\udd39 UnaryOpNode : {op}");
                    var operand = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new UnaryOpNode { Op = op, Operand = operand };
                }
                else if (IsBinaryOp(op))
                {
                    Console.WriteLine($"  \ud83d\udd39 BinaryOpNode : {op}");
                    var left = ParseNode(tokens, ref index);
                    var right = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    return new BinaryOpNode { Op = op, Left = left, Right = right };
                }
                else if (op == "block")
                {
                    Console.WriteLine("  \ud83d\udd39 Bloc block");
                    string? label = tokens[index].StartsWith("$") ? tokens[index++] : null;
                    var body = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        body.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = label, Body = body };
                }
                else if (op == "loop")
                {
                    Console.WriteLine("  \ud83d\udd39 Bloc loop");
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

    var thenBody = new List<WasmNode>();
    thenBody.Add(ParseNode(tokens, ref index));

    List<WasmNode>? elseBody = null;
    if (index < tokens.Count && tokens[index] == "(")
    {
        elseBody = new List<WasmNode>();
        elseBody.Add(ParseNode(tokens, ref index));
    }

    ExpectToken(tokens, ref index, ")");

    return new IfNode
    {
        Condition = condition,
        ThenBody = thenBody,
        ElseBody = elseBody
    };
}


                else if (op == "br")
                {
                    string label = tokens[index++];
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  \ud83d\udd39 Br vers label {label}");
                    return new BrNode { Label = label };
                }
                else if (op == "br_if")
                {
                    string label = tokens[index++];
                    var condition = ParseNode(tokens, ref index);
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  \ud83d\udd39 Br_if vers label {label}");
                    return new BrIfNode { Label = label, Condition = condition };
                }
                else if (op == "module" || op == "type" || op == "func")
                {
                    Console.WriteLine($"  \u2699\ufe0f Bloc de structure : {op}");
                    var inner = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        inner.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new BlockNode { Label = op, Body = inner };
                }
                else
                {
                    Console.WriteLine($"  \ud83d\udce6 Instruction g\u00e9n\u00e9rique : {op}");
                    var children = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        children.Add(ParseNode(tokens, ref index));
                    ExpectToken(tokens, ref index, ")");
                    return new RawInstructionNode { Instruction = op };
                }
            }
            else if (tokens[index] == ")")
            {
                throw new Exception("\u274C Parenth\u00e8se fermante inattendue.");
            }
            else
            {
                Console.WriteLine($"\ud83d\udccc Instruction isol\u00e9e : {tokens[index]}");
                return new RawInstructionNode { Instruction = tokens[index++] };
            }
        }

        private bool IsUnaryOp(string op) =>
            op == "drop" || op.EndsWith(".eqz") || op.EndsWith(".wrap_i64");

        private bool IsBinaryOp(string op) =>
            op.EndsWith(".add") || op.EndsWith(".sub") || op.EndsWith(".mul") || op.EndsWith(".div_s")|| op.EndsWith(".div_u") || op.EndsWith(".div") ||
            op.EndsWith(".eq") || op.EndsWith(".ne") || op.EndsWith(".lt_s") || op.EndsWith(".lt_u") || op.EndsWith(".le_s") ||op.EndsWith(".le_u") ||op.EndsWith(".le") ||op.EndsWith(".lt") ||
            op.EndsWith(".gt_s") ||op.EndsWith(".gt_u") || op.EndsWith(".ge_s") || op.EndsWith(".ge_u") || op.EndsWith(".ge") || op.EndsWith(".gt");

        private void ExpectToken(List<string> tokens, ref int index, string expected)
        {
            if (index >= tokens.Count || tokens[index] != expected)
                throw new Exception($"\u274C Parenth\u00e8se fermante '{expected}' attendue.");
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

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr LoadWasmTextFile(string filename);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFunctionBodyText(IntPtr module, int index);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ValidateModule(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PrintModuleAST(IntPtr module);
    }
}
