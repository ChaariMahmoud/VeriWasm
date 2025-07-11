using BoogieAST;
using System.Collections.Generic;
using System.IO;
using WasmToBoogie.Parser;
using WasmToBoogie.Parser.Ast;
using WasmToBoogie.Conversion;
namespace WasmToBoogie
{
    public class WasmToBoogieMain
    {
        private readonly string wasmPath;
        private readonly string contractName;

        public WasmToBoogieMain(string wasmPath, string contractName)
        {
            this.wasmPath = wasmPath;
            this.contractName = contractName;
        }

        public BoogieProgram Translate()
        {
            Console.WriteLine($"\uD83D\uDCD6 Lecture du fichier WAT : {wasmPath}");

            // 1. Lire et parser le fichier .wat pour construire un AST WAT
            var parser = new WasmParser(wasmPath);
            WasmModule wasmAst = parser.Parse();
            Console.WriteLine($"✅ AST WAT généré avec {wasmAst.Functions.Count} fonctions.");
            
            // 2. Convertir l'AST WAT vers un programme Boogie
            var converter = new WasmAstToBoogie(contractName);
            BoogieProgram boogieProgram = converter.Convert(wasmAst);

            Console.WriteLine("✅ Conversion WAT → Boogie terminée.");
            return boogieProgram;
        }
    }
}
