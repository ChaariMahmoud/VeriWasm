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
        throw new FileNotFoundException($"❌ Fichier WAT introuvable : {filePath}");

    Console.WriteLine("📖 Lecture du fichier WAT : " + filePath);
    string wasmPath = ConvertWatToWasm(filePath);

    IntPtr modulePtr = LoadWasmTextFile(wasmPath);
    if (modulePtr == IntPtr.Zero || !ValidateModule(modulePtr))
        throw new Exception("❌ Erreur de lecture ou validation du module Binaryen");

    PrintModuleAST(modulePtr);

    string watBody = Marshal.PtrToStringAnsi(GetFunctionBodyText(modulePtr, 0)) ?? "";
    Console.WriteLine("📤 Corps extrait de la fonction :\n" + watBody);

    var tokens = Tokenize(watBody);
    Console.WriteLine("🔍 Tokens : " + string.Join(" ", tokens));

    int idx = 0;
    var body = new List<WasmNode>();

    while (idx < tokens.Count)
    {
        Console.WriteLine($"\n🔽 Appel ParseNode à l'index {idx}");
        body.Add(ParseNode(tokens, ref idx));
    }

    Console.WriteLine("✅ AST WAT généré avec succès.");
    return new WasmModule
    
    {
        Functions = { new WasmFunction { Body = body } }
    };
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
                index++; // consume '('
                string op = tokens[index++];
                Console.WriteLine($"🔸 Début bloc : ({op}");

                if (op.EndsWith(".const"))
                {
                    string value = tokens[index++];
                    ExpectToken(tokens, ref index, ")");
                    Console.WriteLine($"  🔹 ConstNode({op}, {value})");
                    return new ConstNode
                    {
                        Type = op.Split('.')[0],
                        Value = value
                    };
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
                    Console.WriteLine("  🔹 Bloc if");
                    var condition = ParseNode(tokens, ref index);
                    var thenBody = new List<WasmNode>();
                    List<WasmNode>? elseBody = null;

                    while (index < tokens.Count && tokens[index] != ")" && tokens[index] != "(")
                        thenBody.Add(ParseNode(tokens, ref index));

                    if (index < tokens.Count - 1 && tokens[index] == "(" && tokens[index + 1] == "else")
                    {
                        Console.WriteLine("  🔹 Bloc else");
                        index += 2;
                        elseBody = new List<WasmNode>();
                        while (index < tokens.Count && tokens[index] != ")")
                            elseBody.Add(ParseNode(tokens, ref index));
                        ExpectToken(tokens, ref index, ")");
                    }

                    ExpectToken(tokens, ref index, ")");
                    return new IfNode { Condition = condition, ThenBody = thenBody, ElseBody = elseBody };
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
                else
                {
                    Console.WriteLine($"  📦 Instruction générique : {op}");
                    var children = new List<WasmNode>();
                    while (index < tokens.Count && tokens[index] != ")")
                        children.Add(ParseNode(tokens, ref index));
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
            op.EndsWith(".add") || op.EndsWith(".sub") || op.EndsWith(".mul") || op.EndsWith(".div_s") || op.EndsWith(".div") ||
            op.EndsWith(".eq") || op.EndsWith(".ne") || op.EndsWith(".lt_s") || op.EndsWith(".le_s") ||
            op.EndsWith(".gt_s") || op.EndsWith(".ge_s");

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
