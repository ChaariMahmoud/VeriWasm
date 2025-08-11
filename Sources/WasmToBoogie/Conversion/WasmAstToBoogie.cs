using BoogieAST;
using System;
using System.Collections.Generic;
using System.Linq;
using WasmToBoogie.Parser.Ast;

namespace WasmToBoogie.Conversion
{
    public class WasmAstToBoogie
    {
        private readonly string contractName;
        private int labelCounter = 0;

        // Context used to map WAT labels to Boogie labels and distinguish block vs loop.
        private class LabelContext
        {
            public string? WatLabel;    // original WAT label (without '$'), null if unnamed
            public string? StartLabel;  // start label for loop (used for continue)
            public string EndLabel;     // end label for block/loop (used for break)
            public bool IsLoop;
        }

        private Stack<LabelContext> labelStack = new();
        private string? functionExitLabel;

        public WasmAstToBoogie(string contractName)
        {
            this.contractName = contractName;
        }

        private string GenerateLabel(string baseName)
        {
            return $"{baseName}_{++labelCounter}";
        }

        public BoogieProgram Convert(WasmModule wasmModule)
        {
            var program = new BoogieProgram();

            // Add prelude variables and functions
            AddPrelude(program);

            foreach (var func in wasmModule.Functions)
            {
                var (proc, impl) = TranslateFunction(func);
                program.Declarations.Add(proc);
                program.Declarations.Add(impl);
            }

            return program;
        }

        private void AddPrelude(BoogieProgram program)
        {
            // Global variables
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real)));

            // Function declarations
            program.Declarations.Add(new BoogieFunction("bool_to_real",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("b", BoogieType.Bool)) },
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Real)) }));
            program.Declarations.Add(new BoogieFunction("real_to_bool",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real)) },
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Bool)) }));
            program.Declarations.Add(new BoogieFunction("real_to_int",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real)) },
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Int)) }));

            // Axioms for conversion functions
            var boolToRealAxiom = new BoogieQuantifiedExpr(
                true,
                new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("b") },
                new List<BoogieType> { BoogieType.Bool },
                new BoogieBinaryOperation(
                    BoogieBinaryOperation.Opcode.EQ,
                    new BoogieFunctionCall("bool_to_real", new List<BoogieExpr> { new BoogieIdentifierExpr("b") }),
                    new BoogieITE(
                        new BoogieIdentifierExpr("b"),
                        new BoogieLiteralExpr(new Pfloat(1)),
                        new BoogieLiteralExpr(new Pfloat(0))
                    )
                )
            );
            program.Declarations.Add(new BoogieAxiom(boolToRealAxiom));

            var realToBoolAxiom = new BoogieQuantifiedExpr(
                true,
                new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("r") },
                new List<BoogieType> { BoogieType.Real },
                new BoogieBinaryOperation(
                    BoogieBinaryOperation.Opcode.EQ,
                    new BoogieFunctionCall("real_to_bool", new List<BoogieExpr> { new BoogieIdentifierExpr("r") }),
                    new BoogieBinaryOperation(
                        BoogieBinaryOperation.Opcode.NEQ,
                        new BoogieIdentifierExpr("r"),
                        new BoogieLiteralExpr(new Pfloat(0))
                    )
                )
            );
            program.Declarations.Add(new BoogieAxiom(realToBoolAxiom));

            var realToIntAxiom = new BoogieQuantifiedExpr(
                true,
                new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("r") },
                new List<BoogieType> { BoogieType.Real },
                new BoogieBinaryOperation(
                    BoogieBinaryOperation.Opcode.GE,
                    new BoogieFunctionCall("real_to_int", new List<BoogieExpr> { new BoogieIdentifierExpr("r") }),
                    new BoogieLiteralExpr(0)
                )
            );
            program.Declarations.Add(new BoogieAxiom(realToIntAxiom));

            // push procedure
            var pushProc = new BoogieProcedure("push",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                new List<BoogieVariable>(),
                new List<BoogieAttribute> { new BoogieAttribute("inline", true) },
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );
            program.Declarations.Add(pushProc);

            var pushBody = new BoogieStmtList();
            pushBody.AddStatement(new BoogieAssignCmd(new BoogieMapSelect(new BoogieIdentifierExpr("$stack"),
                new BoogieIdentifierExpr("$sp")), new BoogieIdentifierExpr("val")));
            pushBody.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.ADD, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            var pushImpl = new BoogieImplementation("push",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                new List<BoogieVariable>(),
                new List<BoogieVariable>(),
                pushBody
            );
            program.Declarations.Add(pushImpl);

            // popToTmp1
            var popToTmp1Proc = new BoogieProcedure("popToTmp1",
                new List<BoogieVariable>(),
                new List<BoogieVariable>(),
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real))
                },
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );
            program.Declarations.Add(popToTmp1Proc);

            var popToTmp1Body = new BoogieStmtList();
            popToTmp1Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp1Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp1"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp1Impl = new BoogieImplementation("popToTmp1",
                new List<BoogieVariable>(), new List<BoogieVariable>(), new List<BoogieVariable>(), popToTmp1Body);
            program.Declarations.Add(popToTmp1Impl);

            // popToTmp2
            var popToTmp2Proc = new BoogieProcedure("popToTmp2",
                new List<BoogieVariable>(),
                new List<BoogieVariable>(),
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real))
                },
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );
            program.Declarations.Add(popToTmp2Proc);

            var popToTmp2Body = new BoogieStmtList();
            popToTmp2Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp2Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp2"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp2Impl = new BoogieImplementation("popToTmp2",
                new List<BoogieVariable>(), new List<BoogieVariable>(), new List<BoogieVariable>(), popToTmp2Body);
            program.Declarations.Add(popToTmp2Impl);

            // popToTmp3
            var popToTmp3Proc = new BoogieProcedure("popToTmp3",
                new List<BoogieVariable>(),
                new List<BoogieVariable>(),
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real))
                },
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );
            program.Declarations.Add(popToTmp3Proc);

            var popToTmp3Body = new BoogieStmtList();
            popToTmp3Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp3Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp3"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp3Impl = new BoogieImplementation("popToTmp3",
                new List<BoogieVariable>(), new List<BoogieVariable>(), new List<BoogieVariable>(), popToTmp3Body);
            program.Declarations.Add(popToTmp3Impl);

            // pop
            var popProc = new BoogieProcedure("pop",
                new List<BoogieVariable>(),
                new List<BoogieVariable>(),
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int))
                },
                new List<BoogieExpr>(),
                new List<BoogieExpr>()
            );
            program.Declarations.Add(popProc);

            var popBody = new BoogieStmtList();
            popBody.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            var popImpl = new BoogieImplementation("pop",
                new List<BoogieVariable>(), new List<BoogieVariable>(), new List<BoogieVariable>(), popBody);
            program.Declarations.Add(popImpl);
        }

        private (BoogieProcedure, BoogieImplementation) TranslateFunction(WasmFunction func)
        {
            var inParams = new List<BoogieVariable>();
            var outParams = new List<BoogieVariable>();
            var locals = new List<BoogieVariable>();
            var body = new BoogieStmtList();

            // Generate fresh labels for function start and exit
            string exitLabel = GenerateLabel("exit");
            string startLabel = GenerateLabel("start");
            functionExitLabel = exitLabel;

            // Add labelled skips so goto targets exist
            //body.AddStatement(new BoogieSkipCmd(exitLabel + ":"));
            //body.AddStatement(new BoogieSkipCmd(startLabel + ":"));

            // Translate body
            foreach (var node in func.Body)
            {
                TranslateNode(node, body);
            }

            // Use the provided function name or fallback to a name derived from the contract
            string funcName = func.Name ?? $"func_{contractName}";

            var proc = new BoogieProcedure(
                funcName,
                inParams,
                outParams,
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
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
                    // if (int.TryParse(cn.Value, out int val))
                    // {
                    //     var push = new BoogieCallCmd(
                    //         "push",
                    //         new List<BoogieExpr> { new BoogieLiteralExpr(val) },
                    //         new List<BoogieIdentifierExpr>()
                    //     );
                    //     body.AddStatement(push);
                    // }
                    // else 
                    if (float.TryParse(cn.Value, out float longVal))
                    {
                        Pfloat fVal = new Pfloat(longVal);
                        var push = new BoogieCallCmd(
                            "push",
                            new List<BoogieExpr> { new BoogieLiteralExpr(fVal) },
                            new List<BoogieIdentifierExpr>()
                        );
                        body.AddStatement(push);
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported const value: {cn.Value}"));
                    }
                    break;

                case UnaryOpNode un:
                    // Always translate operand first for unary ops
                    if (un.Operand != null)
                    {
                        TranslateNode(un.Operand, body);
                    }

                    if (un.Op == "drop")
                    {
                        // Pop the operand off the stack
                        body.AddStatement(new BoogieCallCmd("pop", new(), new()));
                    }
                    else if (un.Op == "i32.eqz" || un.Op == "i64.eqz")
                    {
                        // eqz: pop into tmp1, compare with zero, push result as real
                        body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                        var eqzExpr = new BoogieFunctionCall(
                            "bool_to_real",
                            new List<BoogieExpr> {
                                new BoogieBinaryOperation(
                                    BoogieBinaryOperation.Opcode.EQ,
                                    new BoogieIdentifierExpr("$tmp1"),
                                    new BoogieLiteralExpr(new Pfloat(0))
                                )
                            }
                        );
                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { eqzExpr }, new()));
                    }
                    else if (un.Op == "i32.wrap_i64")
                    {
                        /*body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                        // Convert to int and wrap to 32-bit range
                        body.AddStatement(new BoogieAssignCmd(
                            new BoogieIdentifierExpr("$tmp2"),
                            new BoogieFunctionCall("real_to_int", new() { new BoogieIdentifierExpr("$tmp1") })
                        ));
                        var modExpr = new BoogieBinaryOperation(
                            BoogieBinaryOperation.Opcode.MOD,
                            new BoogieIdentifierExpr("$tmp2"),
                            new BoogieLiteralExpr(4294967296) // 2^32
                        );
                        body.AddStatement(new BoogieCallCmd("push", new() { modExpr }, new()));*/
                        body.AddStatement(new BoogieCommentCmd("// i32.wrap_i64: no-op under real semantics"));
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported unary op: {un.Op}"));
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
                        or "i32.div_s" or "i64.div_s" or "f32.div" or "f64.div" or
                            "i32.div_u" or "i64.div_u")
                    {
                        var opKind = bn.Op switch
                        {
                            "i32.add" or "i64.add" or "f32.add" or "f64.add" => BoogieBinaryOperation.Opcode.ADD,
                            "i32.sub" or "i64.sub" or "f32.sub" or "f64.sub" => BoogieBinaryOperation.Opcode.SUB,
                            "i32.mul" or "i64.mul" or "f32.mul" or "f64.mul" => BoogieBinaryOperation.Opcode.MUL,
                            "i32.div_s" or "i64.div_s" or "i32.div_u" or "i64.div_u" or "f32.div" or "f64.div" => BoogieBinaryOperation.Opcode.DIV,
                            _ => throw new NotSupportedException($"Unsupported arithmetic op: {bn.Op}")
                        };

                        var arithExpr = new BoogieBinaryOperation(opKind, tmp2, tmp1);
                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { arithExpr }, new()));
                    }
                    else if (bn.Op is
        "i32.eq" or "i64.eq" or
        "i32.ne" or "i64.ne" or "f32.eq" or "f64.eq" or "f32.ne" or "f64.ne" or
        "i32.lt_s" or "i64.lt_s" or "i32.lt_u" or "i64.lt_u" or
        "i32.le_s" or "i64.le_s" or "i32.le_u" or "i64.le_u" or
        "i32.gt_s" or "i64.gt_s" or "i32.gt_u" or "i64.gt_u" or
        "i32.ge_s" or "i64.ge_s" or "i32.ge_u" or "i64.ge_u" or
        "f32.lt" or "f64.lt" or "f32.le" or "f64.le" or
        "f32.gt" or "f64.gt" or "f32.ge" or "f64.ge")
                    {
                        BoogieExpr cmpExpr = bn.Op switch
                        {
                            "i32.eq" or "i64.eq" or "f32.eq" or "f64.eq" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, tmp2, tmp1) }),
                            "i32.ne" or "i64.ne" or "f32.ne" or "f64.ne" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.NEQ, tmp2, tmp1) }),
                            "i32.lt_s" or "i64.lt_s" or "i32.lt_u" or "i64.lt_u" or "f32.lt" or "f64.lt" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LT, tmp2, tmp1) }),
                            "i32.le_s" or "i64.le_s" or "i32.le_u" or "i64.le_u" or "f32.le" or "f64.le" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LE, tmp2, tmp1) }),
                            "i32.gt_s" or "i64.gt_s" or "i32.gt_u" or "i64.gt_u" or "f32.gt" or "f64.gt" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GT, tmp2, tmp1) }),
                            "i32.ge_s" or "i64.ge_s" or "i32.ge_u" or "i64.ge_u" or "f32.ge" or "f64.ge" => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, tmp2, tmp1) }),
                            _ => throw new NotSupportedException($"Unsupported comparison: {bn.Op}")
                        };

                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { cmpExpr }, new()));
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported binary op: {bn.Op}"));
                    }
                    break;

                case BlockNode blk:
                    // For a labelled block, generate an end label and push context
                    LabelContext? blkCtx = null;
                    if (blk.Label != null)
                    {
                        var watLabel = blk.Label.StartsWith("$") ? blk.Label.Substring(1) : blk.Label;
                        blkCtx = new LabelContext
                        {
                            WatLabel = watLabel,
                            IsLoop = false,
                            EndLabel = GenerateLabel(watLabel)
                        };
                        labelStack.Push(blkCtx);
                    }

                    // Translate children
                    foreach (var child in blk.Body)
                    {
                        TranslateNode(child, body);
                    }

                    // Emit end label for block
                    if (blkCtx != null)
                    {
                        body.AddStatement(new BoogieSkipCmd(blkCtx.EndLabel + ":"));
                        labelStack.Pop();
                    }
                    break;

case LoopNode loop:
{
    // Is this a real WAT label like "$start"?
    string? wat = (!string.IsNullOrEmpty(loop.Label) && loop.Label.StartsWith("$"))
                    ? loop.Label.Substring(1)
                    : null;

    LabelContext? ctx = null;

    if (wat != null)
    {
        // create start/end targets for continue/break
        var start = GenerateLabel($"{wat}_start");
        var end   = GenerateLabel($"{wat}_end");

        ctx = new LabelContext
        {
            WatLabel = wat,
            IsLoop = true,
            StartLabel = start,
            EndLabel = end
        };

        labelStack.Push(ctx);

        // continue target
        body.AddStatement(new BoogieSkipCmd(start + ":"));
    }

    // translate loop body once; repeating requires an explicit br/br_if
    foreach (var child in loop.Body)
        TranslateNode(child, body);

    if (ctx != null)
    {
        // break target
        body.AddStatement(new BoogieSkipCmd(ctx.EndLabel + ":"));
        labelStack.Pop();
    }

    break;
}


                case IfNode ifn:
                    // Translate condition and pop to tmp1
                    TranslateNode(ifn.Condition, body);
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));

                    var thenBlock = new BoogieStmtList();
                    foreach (var stmt in ifn.ThenBody)
                        TranslateNode(stmt, thenBlock);

                    BoogieStmtList? elseBlock = null;
                    if (ifn.ElseBody != null)
                    {
                        elseBlock = new BoogieStmtList();
                        foreach (var stmt in ifn.ElseBody)
                            TranslateNode(stmt, elseBlock);
                    }

                    var ifStmt = new BoogieIfCmd(
                        new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") }),
                        thenBlock,
                        elseBlock
                    );
                    body.AddStatement(ifStmt);
                    break;

                case BrNode br:
                    // Remove '$' and find matching context
                    var brLabel = br.Label.StartsWith("$") ? br.Label.Substring(1) : br.Label;
                    var targetCtx = labelStack.FirstOrDefault(ctx => ctx.WatLabel == brLabel);

                    if (targetCtx != null)
                    {
                        string target = targetCtx.IsLoop ? (targetCtx.StartLabel ?? targetCtx.EndLabel) : targetCtx.EndLabel;
                        body.AddStatement(new BoogieGotoCmd(target));
                    }
                    else
                    {
                        // If no matching context, jump to function exit or raw label
                        body.AddStatement(new BoogieGotoCmd(functionExitLabel ?? brLabel));
                    }
                    break;

                case BrIfNode brIf:
                    // Evaluate condition and pop to tmp1
                    TranslateNode(brIf.Condition, body);
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));

                    // Determine branch target
                    var ifLabel = brIf.Label.StartsWith("$") ? brIf.Label.Substring(1) : brIf.Label;
                    var ctxMatch = labelStack.FirstOrDefault(ctx => ctx.WatLabel == ifLabel);

                    string targetLabel = ctxMatch != null
                        ? (ctxMatch.IsLoop ? (ctxMatch.StartLabel ?? ctxMatch.EndLabel) : ctxMatch.EndLabel)
                        : (functionExitLabel ?? ifLabel);

                    var branchBlock = new BoogieStmtList();
                    branchBlock.AddStatement(new BoogieGotoCmd(targetLabel));

                    var brIfStmt = new BoogieIfCmd(
                        new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") }),
                        branchBlock,
                        null
                    );
                    body.AddStatement(brIfStmt);
                    break;

case RawInstructionNode raw:
{
    var s = raw.Instruction;
    // Ignore function/type names and signatures like "$temp", "$none_=>_none"
    if (s.StartsWith("$") || s.Contains("=>")||  s == "module" || s == "type" || s == "func")
    {
        // no-op
    }
    else
    {
        // keep this if you still want visibility on truly unhandled ops
        body.AddStatement(new BoogieCommentCmd($"// unhandled raw instruction: {s}"));
    }
    break;
}
            }
        }
    }
}