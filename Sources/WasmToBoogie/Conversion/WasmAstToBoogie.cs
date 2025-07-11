using BoogieAST;
using System;
using System.Collections.Generic;
using WasmToBoogie.Parser.Ast;

namespace WasmToBoogie.Conversion
{
    public class WasmAstToBoogie
    {
        private readonly string contractName;

        public WasmAstToBoogie(string contractName)
        {
            this.contractName = contractName;
        }

        public BoogieProgram Convert(WasmModule wasmModule)
        {
            var program = new BoogieProgram();

            foreach (var func in wasmModule.Functions)
            {
                var (proc, impl) = TranslateFunction(func);
                program.Declarations.Add(proc);
                program.Declarations.Add(impl);
            }

            return program;
        }

        private (BoogieProcedure, BoogieImplementation) TranslateFunction(WasmFunction func)
        {
            var inParams = new List<BoogieVariable>();
            var outParams = new List<BoogieVariable>();
            var locals = new List<BoogieVariable>(); // Empty since we don’t use temp vars
            var body = new BoogieStmtList();

foreach (var instr in func.Body)
{
    if (instr.StartsWith("i32.const"))
    {
        var valueStr = instr.Substring("i32.const".Length).Trim();
        if (int.TryParse(valueStr, out int val))
        {
            var pushCall = new BoogieCallCmd(
                "push",
                new List<BoogieExpr> { new BoogieLiteralExpr(val) },
                new List<BoogieIdentifierExpr>()
            );
            body.AddStatement(pushCall);
        }
    }
    else if (instr == "i32.add")
    {
        // push(pop() + pop())
        var pop1 = new BoogieFuncCallExpr("pop", new List<BoogieExpr>());
        var pop2 = new BoogieFuncCallExpr("pop", new List<BoogieExpr>());

        var expr = new BoogieBinaryOperation(
            BoogieBinaryOperation.Opcode.ADD,
            pop2, // ⚠️ ordre important (WASM dépile 2e d’abord)
            pop1
        );

        var pushCall = new BoogieCallCmd("push", new List<BoogieExpr> { expr }, new List<BoogieIdentifierExpr>());
        body.AddStatement(pushCall);
    }
    else if (instr == "drop")
    {
        var dropCall = new BoogieCallCmd("pop", new List<BoogieExpr>(), new List<BoogieIdentifierExpr>());
        body.AddStatement(dropCall);
    }
    else
    {
        body.AddStatement(new BoogieCommentCmd($"// Instruction inconnue ou non supportée : {instr}"));
    }
}


            var proc = new BoogieProcedure(
                $"BoogieEntry_{contractName}",
                inParams,
                outParams,
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable>(),
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );

            var impl = new BoogieImplementation(proc.Name, inParams, outParams, locals, body);
            return (proc, impl);
        }
    }
}
