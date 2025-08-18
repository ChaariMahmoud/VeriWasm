using BoogieAST;
using System.Collections.Generic;
using System.IO;
using WasmToBoogie.Parser;
using WasmToBoogie.Parser.Ast;
using WasmToBoogie.Conversion;
using System.Text;
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
            Console.WriteLine($"\uD83D\uDCD6 Reading WAT file: {wasmPath}");

            var parser = new WasmParser(wasmPath);
            var wasmAst = parser.Parse();
            Console.WriteLine($"✅ WAT AST generated with {wasmAst.Functions.Count} functions.");

            var converter = new WasmAstToBoogie(contractName);
            var boogieProgram = converter.Convert(wasmAst);

            Console.WriteLine("✅ WAT → Boogie conversion completed.");
            return boogieProgram;
        }

        // NEW: format + write without touching BoogieAST
        public void TranslateAndWrite(string outPath)
        {
            var program = Translate();           // build program
            var bpl = program.ToString();        // serialize
            bpl = BoogiePrettyPrinter.IndentBoogie(bpl); // format
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, bpl, Encoding.UTF8);
            Console.WriteLine($"📝 Boogie written: {outPath}");
        }
    }
}
