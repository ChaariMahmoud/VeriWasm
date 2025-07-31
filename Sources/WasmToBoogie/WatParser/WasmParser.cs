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
            op.EndsWith(".add") || op.EndsWith(".sub") || op.EndsWith(".mul") || op.EndsWith(".div_s") || op.EndsWith(".div") ||
            op.EndsWith(".eq") || op.EndsWith(".ne") || op.EndsWith(".lt_s") || op.EndsWith(".le_s") ||
            op.EndsWith(".gt_s") || op.EndsWith(".ge_s");

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
