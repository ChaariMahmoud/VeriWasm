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
            Console.WriteLine("🔄 Conversion WAT → WASM via wat2wasm...");
            string wasmPath = ConvertWatToWasm(filePath);

            Console.WriteLine("🔄 Appel à Binaryen (via wrapper) pour extraire l'AST WAT...");
            IntPtr modulePtr = LoadWasmTextFile(wasmPath);
            if (modulePtr == IntPtr.Zero)
                throw new Exception("❌ Échec de lecture du fichier WASM avec Binaryen.");

            if (!ValidateModule(modulePtr))
                throw new Exception("❌ Le module Binaryen est invalide !");

            PrintModuleAST(modulePtr);

            int funcCount = GetFunctionCount(modulePtr);
            string firstFuncName = Marshal.PtrToStringAnsi(GetFirstFunctionName(modulePtr));

            Console.WriteLine($"✅ AST généré : {funcCount} fonction(s)");
            Console.WriteLine($"🧠 Première fonction : {firstFuncName}");

            var module = new WasmModule();

            string watBody = GetFunctionBodyWat(modulePtr, 0);
            Console.WriteLine("📤 Corps extrait de la fonction :\n" + watBody);

            // 🔍 Extraire les instructions WAT en respectant l’ordre d'exécution
var bodyList = new List<string>();
var tokens = watBody.Split(new[] { '(', ')', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

foreach (var raw in tokens)
{
    string line = raw.Trim();

    // Instructions constantes
    if (line.StartsWith("i32.const") ||
        line.StartsWith("i64.const") ||
        line.StartsWith("f32.const") ||
        line.StartsWith("f64.const"))
    {
        bodyList.Add(line);
    }
    // Opérations de conversion
    else if (line == "i32.wrap_i64")
    {
        bodyList.Add(line);
    }
    // Opérations arithmétiques
    else if (
        line == "i32.add" || line == "f32.add" ||
        line == "i32.sub" || line == "f32.sub" ||
        line == "i32.mul" || line == "f32.mul" ||
        line == "i32.div" || line == "f32.div"
    )
    {
        bodyList.Add(line);
    }
    // Opérations de comparaison
    else if (
        line == "i32.eq" || line == "i32.ne" ||
        line == "i32.lt_s" || line == "i32.le_s" ||
        line == "i32.gt_s" || line == "i32.ge_s" ||
        line == "i32.eqz"
    )
    {
        bodyList.Add(line);
    }
}

          bodyList.Reverse(); // 🌀 Corriger l'ordre pour correspondre à la pile

            foreach (var instr in bodyList)
            {
                Console.WriteLine("📦 Instruction extraite : " + instr);
            }

            module.Functions.Add(new WasmFunction { Body = bodyList });

            return module;
        }

        private string ConvertWatToWasm(string watPath)
        {
            string wasmPath = Path.ChangeExtension(watPath, ".wasm");

            var startInfo = new ProcessStartInfo
            {
                FileName = "wat2wasm",
                Arguments = $"{watPath} -o {wasmPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = startInfo };
            proc.Start();
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine("❌ wat2wasm error: " + stderr);
                throw new Exception("wat2wasm conversion failed.");
            }

            return wasmPath;
        }

        private static string GetFunctionBodyWat(IntPtr module, int index)
        {
            IntPtr ptr = GetFunctionBodyText(module, index);
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }

        // 🔗 Fonctions externes du wrapper
        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr LoadWasmTextFile(string filename);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetFunctionCount(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFirstFunctionName(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PrintModuleAST(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ValidateModule(IntPtr module);

        [DllImport("libbinaryenwrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFunctionBodyText(IntPtr module, int index);
    }

    namespace Ast
    {
        public class WasmModule
        {
            public List<WasmFunction> Functions { get; set; } = new();
        }

        public class WasmFunction
        {
            public List<string> Body { get; set; } = new();
        }
    }
}
