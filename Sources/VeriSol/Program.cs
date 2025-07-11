namespace VeriSolRunner
{
    using WasmToBoogie;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Logging;
    using SolToBoogie;
    using VeriSolRunner.ExternalTools;


    class Program
    {
        public static int Main(string[] args)
        {
            // ✅ Mode WebAssembly
            if (args.Length >= 2 && args[0] == "--wasm")
            {
                string wasmFile = args[1];
                string contractName = Path.GetFileNameWithoutExtension(wasmFile);

                var wasmTranslator = new WasmToBoogieMain(wasmFile, contractName);
                var program = wasmTranslator.Translate();

var executor = new VeriSolExecutor(
    program,
    contractName,
    corralRecursionLimit: 4,
    ignoreMethods: new HashSet<Tuple<string, string>>(),
    tryRefutation: true,
    tryProofFlag: true,
    logger: LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("WasmMode")
);

                Console.WriteLine("✅ Appel à WasmToBoogieMain réussi !");
                return executor.Execute();
            }

            // ✅ Mode Solidity classique
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            ExternalToolsManager.EnsureAllExisted();

            string solidityFile, entryPointContractName;
            bool tryProofFlag, tryRefutation;
            int recursionBound;
            ILogger logger;
            HashSet<Tuple<string, string>> ignoredMethods;
            bool printTransactionSequence = false;
            TranslatorFlags translatorFlags = new TranslatorFlags();

            SolToBoogie.ParseUtils.ParseCommandLineArgs(args,
                out solidityFile,
                out entryPointContractName,
                out tryProofFlag,
                out tryRefutation,
                out recursionBound,
                out logger,
                out ignoredMethods,
                out printTransactionSequence,
                ref translatorFlags);

            var verisolExecuter = new VeriSolExecutor(
                Path.Combine(Directory.GetCurrentDirectory(), solidityFile),
                entryPointContractName,
                recursionBound,
                ignoredMethods,
                tryRefutation,
                tryProofFlag,
                logger,
                printTransactionSequence, // ✅ Argument ajouté ici aussi
                translatorFlags
            );

            return verisolExecuter.Execute();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("VeriSol: Formal specification and verification tool for Solidity smart contracts");
            Console.WriteLine("Usage:  VeriSol <relative-path-to-solidity-file> <top-level-contractName> [options]");
            Console.WriteLine("        VeriSol --wasm <relative-path-to-wat-file>");
            Console.WriteLine("options:");
            Console.WriteLine("   /noChk                  don't perform verification, default: false");
            Console.WriteLine("   /noPrf                  don't perform inductive verification, default: false");
            Console.WriteLine("   /txBound:k              max transaction depth, default: 4");
            Console.WriteLine("   /noTxSeq                don't print transaction sequence");
            Console.WriteLine("   /contractInfer          perform module invariant inference");
            Console.WriteLine("   /inlineDepth:k          inline nested calls upto depth k");
        }
    }
}
