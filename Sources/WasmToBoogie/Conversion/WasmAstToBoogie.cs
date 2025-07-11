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
            var locals = new List<BoogieVariable>();
            var body = new BoogieStmtList();

            foreach (var node in func.Body)
            {
                TranslateNode(node, body);
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

        private void TranslateNode(WasmNode node, BoogieStmtList body)
        {
            switch (node)
            {
                case ConstNode cn:
                    if (int.TryParse(cn.Value, out int val))
                    {
                        var push = new BoogieCallCmd(
                            "push",
                            new List<BoogieExpr> { new BoogieLiteralExpr(val) },
                            new List<BoogieIdentifierExpr>()
                        );
                        body.AddStatement(push);
                    }
                    break;

                case UnaryOpNode un:
                    TranslateNode(un.Operand, body);

                    if (un.Op == "drop")
                    {
                        var drop = new BoogieCallCmd("pop", new(), new());
                        body.AddStatement(drop);
                    }
                    else if (un.Op == "i32.eqz")
                    {
                        body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                        var eqzExpr = new BoogieFunctionCall(
                            "bool_to_real",
                            new() {
            new BoogieBinaryOperation(
                BoogieBinaryOperation.Opcode.EQ,
                new BoogieIdentifierExpr("$tmp1"),
                new BoogieLiteralExpr(0)
            )
                            }
                        );
                        body.AddStatement(new BoogieCallCmd("push", new() { eqzExpr }, new()));
                    }
else if (un.Op == "i32.wrap_i64")
{
    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
    // $tmp2 := real_to_int($tmp1);
    body.AddStatement(new BoogieAssignCmd(
        new BoogieIdentifierExpr("$tmp2"),
        new BoogieFunctionCall("real_to_int", new() { new BoogieIdentifierExpr("$tmp1") })
    ));
    // $tmp2 := $tmp2 % 4294967296;
    var modExpr = new BoogieBinaryOperation(
        BoogieBinaryOperation.Opcode.MOD,
        new BoogieIdentifierExpr("$tmp2"),
        new BoogieLiteralExpr(4294967296)
    );
    body.AddStatement(new BoogieCallCmd("push", new() { modExpr }, new()));
}
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// Opération unaire inconnue : {un.Op}"));
                    }
                    break;

                case BinaryOpNode bn:
                    TranslateNode(bn.Left, body);
                    TranslateNode(bn.Right, body);

                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    body.AddStatement(new BoogieCallCmd("popToTmp2", new(), new()));

                    var tmp1 = new BoogieIdentifierExpr("$tmp1");
                    var tmp2 = new BoogieIdentifierExpr("$tmp2");

                    if (bn.Op is "i32.add" or "i64.add" or "f32.add" or "f64.add"
                        or "i32.sub" or "i64.sub" or "f32.sub" or "f64.sub"
                        or "i32.mul" or "i64.mul" or "f32.mul" or "f64.mul"
                        or "i32.div_s" or "i64.div_s" or "f32.div" or "f64.div")
                    {
                        var arithOpcode = bn.Op switch
                        {
                            "i32.add" or "i64.add" or "f32.add" or "f64.add" => BoogieBinaryOperation.Opcode.ADD,
                            "i32.sub" or "i64.sub" or "f32.sub" or "f64.sub" => BoogieBinaryOperation.Opcode.SUB,
                            "i32.mul" or "i64.mul" or "f32.mul" or "f64.mul" => BoogieBinaryOperation.Opcode.MUL,
                            "i32.div_s" or "i64.div_s" or "f32.div" or "f64.div" => BoogieBinaryOperation.Opcode.DIV,
                            _ => throw new NotSupportedException($"❌ Opérateur arithmétique non supporté : {bn.Op}")
                        };

                        var arithExpr = new BoogieBinaryOperation(arithOpcode, tmp2, tmp1);
                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { arithExpr }, new()));
                    }
                    else if (bn.Op is "i32.eq" or "i32.ne" or "i32.lt_s" or "i32.le_s" or "i32.gt_s" or "i32.ge_s")
                    {
                        BoogieExpr cmpExpr = bn.Op switch
                        {
                            "i32.eq"   => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, tmp2, tmp1) }),
                            "i32.ne"   => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.NEQ, tmp2, tmp1) }),
                            "i32.lt_s" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LT, tmp2, tmp1) }),
                            "i32.le_s" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LE, tmp2, tmp1) }),
                            "i32.gt_s" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GT, tmp2, tmp1) }),
                            "i32.ge_s" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, tmp2, tmp1) }),
                            _ => throw new NotSupportedException($"❌ Comparaison non supportée : {bn.Op}")
                        };

                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { cmpExpr }, new()));
                    }
                    else
                    {
                        throw new NotSupportedException($"❌ Opérateur binaire non supporté : {bn.Op}");
                    }
                    break;


case BlockNode blk:
    foreach (var child in blk.Body)
        TranslateNode(child, body);
    break;

case LoopNode loop:
    body.AddStatement(new BoogieCommentCmd($"// Début loop {loop.Label}"));
    foreach (var child in loop.Body)
        TranslateNode(child, body);
    body.AddStatement(new BoogieCommentCmd($"// Fin loop {loop.Label}"));
    break;

case IfNode ifn:
    body.AddStatement(new BoogieCommentCmd($"// Début if"));

    TranslateNode(ifn.Condition, body);
    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));

    var thenBlock = new BoogieStmtList();
    foreach (var stmt in ifn.ThenBody)
        TranslateNode(stmt, thenBlock);

    var elseBlock = new BoogieStmtList();
    if (ifn.ElseBody != null)
        foreach (var stmt in ifn.ElseBody)
            TranslateNode(stmt, elseBlock);

    var ifStmt = new BoogieIfCmd(
        new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") }),
        thenBlock,
        ifn.ElseBody != null ? elseBlock : null
    );
    body.AddStatement(ifStmt);

    body.AddStatement(new BoogieCommentCmd($"// Fin if"));
    break;

default:
    body.AddStatement(new BoogieCommentCmd($"// Type AST non supporté : {node.GetType().Name}"));
    break;
;
            }
        }
    }
}
