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

        // NEW: keep program handle + popArgs cache + current function locals map
        private BoogieProgram? program;
        private readonly HashSet<int> popArgsMade = new();
        private List<BoogieIdentifierExpr>? currentLocalMap;
        private WasmFunction? currentFunction;
        
private HashSet<string>? neededLoopStartLabels;
private HashSet<string>? neededBlockEndLabels;
        private class LabelContext
        {
            public string? WatLabel;
            public string? StartLabel;
            public string EndLabel;
            public bool IsLoop;
        }

        private Stack<LabelContext> labelStack = new();
        private string? functionExitLabel;

        public WasmAstToBoogie(string contractName)
        {
            this.contractName = contractName;
        }

        private static bool AllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return s.Length > 0;
        }
        private static string MakeBoogieFuncName(WasmFunction func, string contractName)
        {
            if (!string.IsNullOrEmpty(func.Name))
            {
                var raw = func.Name![0] == '$' ? func.Name.Substring(1) : func.Name;
                return AllDigits(raw) ? $"func_{raw}" : raw;
            }
            return $"func_{contractName}";
        }



        private static string MapCalleeName(string target)
        {
            if (string.IsNullOrEmpty(target)) return target;

            // Strip a single leading '$' if present
            string name = target[0] == '$' ? target.Substring(1) : target;

            // If the name is purely numeric, our procs are named func_<idx>
            if (AllDigits(name)) return "func_" + name;

            // Otherwise use the (sanitized) textual name
            return name;
        }
   
private static string SanitizeFunctionName(string? watName, string contractName)
        {
            if (!string.IsNullOrEmpty(watName))
            {
                var n = watName![0] == '$' ? watName.Substring(1) : watName;

                // "$0" → "func_0" to keep a valid identifier head
                if (int.TryParse(n, out _)) return $"func_{n}";

                // Replace non [A-Za-z0-9_] with '_'
                n = System.Text.RegularExpressions.Regex.Replace(n, @"[^A-Za-z0-9_]", "_");
                if (!char.IsLetter(n[0]) && n[0] != '_') n = "_" + n;
                return n;
            }
            return $"func_{contractName}";
        }

        private string GenerateLabel(string baseName) => $"{baseName}_{++labelCounter}";

        public BoogieProgram Convert(WasmModule wasmModule)
        {
            var program = new BoogieProgram();
            this.program = program;

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
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real)));

            /* program.Declarations.Add(new BoogieFunction("bool_to_real",
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("b", BoogieType.Bool)) },
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Real)) }));
             program.Declarations.Add(new BoogieFunction("real_to_bool",
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real)) },
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Bool)) }));
           /*  program.Declarations.Add(new BoogieFunction("real_to_int",
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real)) },
                 new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Int)) }));

             var boolToRealAxiom = new BoogieAxiom(new BoogieQuantifiedExpr(
                 true,
                 new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("b") },
                 new List<BoogieType> { BoogieType.Bool },
                 new BoogieBinaryOperation(
                     BoogieBinaryOperation.Opcode.EQ,
                     new BoogieFunctionCall("bool_to_real", new List<BoogieExpr> { new BoogieIdentifierExpr("b") }),
                     new BoogieITE(new BoogieIdentifierExpr("b"), new BoogieLiteralExpr(new Pfloat(1)), new BoogieLiteralExpr(new Pfloat(0)))
                 )
             ));
             program.Declarations.Add(boolToRealAxiom);

             var realToBoolAxiom = new BoogieAxiom(new BoogieQuantifiedExpr(
                 true,
                 new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("r") },
                 new List<BoogieType> { BoogieType.Real },
                 new BoogieBinaryOperation(
                     BoogieBinaryOperation.Opcode.EQ,
                     new BoogieFunctionCall("real_to_bool", new List<BoogieExpr> { new BoogieIdentifierExpr("r") }),
                     new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.NEQ, new BoogieIdentifierExpr("r"), new BoogieLiteralExpr(new Pfloat(0)))
                 )
             ));
             program.Declarations.Add(realToBoolAxiom);

             var realToIntAxiom = new BoogieAxiom(new BoogieQuantifiedExpr(
                 true,
                 new List<BoogieIdentifierExpr> { new BoogieIdentifierExpr("r") },
                 new List<BoogieType> { BoogieType.Real },
                 new BoogieBinaryOperation(
                     BoogieBinaryOperation.Opcode.GE,
                     new BoogieFunctionCall("real_to_int", new List<BoogieExpr> { new BoogieIdentifierExpr("r") }),
                     new BoogieLiteralExpr(0)
                 )
             ));
             program.Declarations.Add(realToIntAxiom);*/


            // === fonctions Boogie avec corps (au lieu d'axiomes) ===

            // bool_to_real(b) { if b then 1.0 else 0.0 }
// bool_to_real(b) : real { if b then 1.0 else 0.0 }
{
    var b = new BoogieFormalParam(new BoogieTypedIdent("b", BoogieType.Bool));
    var body = new BoogieITE(
        new BoogieIdentifierExpr("b"),
        new BoogieLiteralExpr(new Pfloat(1)),
        new BoogieLiteralExpr(new Pfloat(0))
    );
    program.Declarations.Add(
        new BoogieFunctionDef("bool_to_real",
            new List<BoogieVariable> { b },
            BoogieType.Real,
            body));
}

// real_to_bool(r) : bool { if r == 0.0 then false else true }
{
    var r = new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real));
    var body = new BoogieITE(
        new BoogieBinaryOperation(
            BoogieBinaryOperation.Opcode.EQ,
            new BoogieIdentifierExpr("r"),
            new BoogieLiteralExpr(new Pfloat(0))
        ),
        new BoogieLiteralExpr(false),
        new BoogieLiteralExpr(true)
    );
    program.Declarations.Add(
        new BoogieFunctionDef("real_to_bool",
            new List<BoogieVariable> { r },
            BoogieType.Bool,
            body));
}




            // push
            var pushProc = new BoogieProcedure("push",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                new List<BoogieVariable>(),
                new List<BoogieAttribute> { new BoogieAttribute("inline", true) },
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new List<BoogieExpr>(), new List<BoogieExpr>());
            program.Declarations.Add(pushProc);
            var pushBody = new BoogieStmtList();
            pushBody.AddStatement(new BoogieAssignCmd(new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp")), new BoogieIdentifierExpr("val")));
            pushBody.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.ADD, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            var pushImpl = new BoogieImplementation("push",
                new List<BoogieVariable> { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                new List<BoogieVariable>(), new List<BoogieVariable>(), pushBody);
            program.Declarations.Add(pushImpl);

            // popToTmp1
            var popToTmp1Proc = new BoogieProcedure("popToTmp1", new(), new(), new(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real))
                }, new(), new());
            program.Declarations.Add(popToTmp1Proc);
            var popToTmp1Body = new BoogieStmtList();
            popToTmp1Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp1Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp1"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp1Impl = new BoogieImplementation("popToTmp1", new(), new(), new(), popToTmp1Body);
            program.Declarations.Add(popToTmp1Impl);

            // popToTmp2
            var popToTmp2Proc = new BoogieProcedure("popToTmp2", new(), new(), new(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real))
                }, new(), new());
            program.Declarations.Add(popToTmp2Proc);
            var popToTmp2Body = new BoogieStmtList();
            popToTmp2Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp2Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp2"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp2Impl = new BoogieImplementation("popToTmp2", new(), new(), new(), popToTmp2Body);
            program.Declarations.Add(popToTmp2Impl);

            // popToTmp3
            var popToTmp3Proc = new BoogieProcedure("popToTmp3", new(), new(), new(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real))
                }, new(), new());
            program.Declarations.Add(popToTmp3Proc);
            var popToTmp3Body = new BoogieStmtList();
            popToTmp3Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            popToTmp3Body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp3"),
                new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            var popToTmp3Impl = new BoogieImplementation("popToTmp3", new(), new(), new(), popToTmp3Body);
            program.Declarations.Add(popToTmp3Impl);

            // pop
            var popProc = new BoogieProcedure("pop", new(), new(), new(),
                new List<BoogieGlobalVariable> { new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)) }, new(), new());
            program.Declarations.Add(popProc);
            var popBody = new BoogieStmtList();
            popBody.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
            var popImpl = new BoogieImplementation("pop", new(), new(), new(), popBody);
            program.Declarations.Add(popImpl);
        }

        // === new: generate popArgsN once ===
        private void EnsurePopArgsProc(int n)
        {
            if (n <= 0 || program == null || popArgsMade.Contains(n)) return;
            popArgsMade.Add(n);

            var outs = new List<BoogieVariable>();
            for (int i = 1; i <= n; i++)
                outs.Add(new BoogieFormalParam(new BoogieTypedIdent($"a{i}", BoogieType.Real)));

            var proc = new BoogieProcedure(
                $"popArgs{n}",
                new List<BoogieVariable>(),
                outs,
                new List<BoogieAttribute> { new BoogieAttribute("inline", true) },
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new List<BoogieExpr>(), new List<BoogieExpr>());

            var stmts = new BoogieStmtList();
            for (int i = n; i >= 1; i--)
            {
                stmts.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
                stmts.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr($"a{i}"),
                    new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            }
            var impl = new BoogieImplementation($"popArgs{n}", new(), outs, new(), stmts);

            program.Declarations.Add(proc);
            program.Declarations.Add(impl);
        }

        private (BoogieProcedure, BoogieImplementation) TranslateFunction(WasmFunction func)
        {
            var inParams = new List<BoogieVariable>();
            var outParams = new List<BoogieVariable>();
            var locals = new List<BoogieVariable>();
            var body = new BoogieStmtList();

            currentFunction = func;
           

            // Build locals map: arg1..argN then loc1..locM
            int n = func.ParamCount;
            int m = func.LocalCount;
            var indexToId = new List<BoogieIdentifierExpr>(n + m);

            for (int i = 1; i <= n; i++)
            {
                var name = $"arg{i}";
                locals.Add(new BoogieLocalVariable(new BoogieTypedIdent(name, BoogieType.Real)));
                indexToId.Add(new BoogieIdentifierExpr(name));
            }
            for (int i = 1; i <= m; i++)
            {
                var name = $"loc{i}";
                locals.Add(new BoogieLocalVariable(new BoogieTypedIdent(name, BoogieType.Real)));
                indexToId.Add(new BoogieIdentifierExpr(name));
            }
            currentLocalMap = indexToId;

            // Callee pops its own args
            if (n > 0)
            {
                EnsurePopArgsProc(n);
                body.AddStatement(new BoogieCallCmd($"popArgs{n}", new(), indexToId.Take(n).ToList()));
            }
            // Initialize WASM locals to 0.0
            for (int i = n; i < n + m; i++)
                body.AddStatement(new BoogieAssignCmd(indexToId[i], new BoogieLiteralExpr(new Pfloat(0))));

            // Translate body
            foreach (var node in func.Body)
                TranslateNode(node, body);

string funcName = MakeBoogieFuncName(func, contractName);



            var proc = new BoogieProcedure(
                funcName,
                inParams, outParams,
                new List<BoogieAttribute>(),
                new List<BoogieGlobalVariable> {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new List<BoogieExpr>(), new List<BoogieExpr>());

            var impl = new BoogieImplementation(proc.Name, inParams, outParams, locals, body);

            // clear per-function state
            currentLocalMap = null;
            currentFunction = null;

            return (proc, impl);
        }

private int ResolveLocalIndex(int? index, string? name)
{
    if (index.HasValue) return index.Value;

    if (!string.IsNullOrEmpty(name))
    {
        // First: real names collected from (param $x T) / (local $y T)
        if (currentFunction != null &&
            currentFunction.LocalIndexByName.TryGetValue(name, out var idx))
            return idx;

        // Fallback: auto-names like $0, $1 ... (Binaryen style)
        if (name[0] == '$' && int.TryParse(name.AsSpan(1), out var autoIdx))
            return autoIdx;
    }

    throw new NotSupportedException($"Unknown local index/name: {name ?? "<null>"}");
}

        private void TranslateNode(WasmNode node, BoogieStmtList body)
        {
            switch (node)
            {
                case ConstNode cn:
                    if (float.TryParse(cn.Value, out float longVal))
                    {
                        Pfloat fVal = new Pfloat(longVal);
                        var push = new BoogieCallCmd("push", new List<BoogieExpr> { new BoogieLiteralExpr(fVal) }, new());
                        body.AddStatement(push);
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported const value: {cn.Value}"));
                    }
                    break;

                case LocalGetNode lg:
                {
                    int idx = ResolveLocalIndex(lg.Index, lg.Name);
                    var id = currentLocalMap![idx];
                    body.AddStatement(new BoogieCallCmd("push", new() { id }, new()));
                    break;
                }
case LocalSetNode ls:
{
    int idx = ResolveLocalIndex(ls.Index, ls.Name);
    var id = currentLocalMap![idx];

    // If parser gave us a folded value expression, evaluate it first
    if (ls.Value != null)
        TranslateNode(ls.Value, body);

    EnsurePopArgsProc(1);
    body.AddStatement(new BoogieCallCmd("popArgs1", new(), new() { id }));
    break;
}
                case LocalTeeNode lt:
                {
                    int idx = ResolveLocalIndex(lt.Index, lt.Name);
                    var id = currentLocalMap![idx];
                    EnsurePopArgsProc(1);
                    body.AddStatement(new BoogieCallCmd("popArgs1", new(), new() { id }));
                    body.AddStatement(new BoogieCallCmd("push", new() { id }, new()));
                    break;
                }
                /*case CallNode call:
                {
                    string target = SanitizeFunctionName(call.Target, contractName);
                    body.AddStatement(new BoogieCallCmd(target, new(), new()));
                    break;
                }*/

case CallNode call:
{
    // Evaluate arguments left-to-right; each pushes onto the stack
    if (call.Args != null)
        foreach (var a in call.Args)
            TranslateNode(a, body);

    // Callee pops its own args in its prologue (popArgsN)
    string target = MapCalleeName(call.Target);
    body.AddStatement(new BoogieCallCmd(target, new(), new()));
    break;
}



                case UnaryOpNode un:
                    if (un.Operand != null) TranslateNode(un.Operand, body);

                    if (un.Op == "drop")
                    {
                        body.AddStatement(new BoogieCallCmd("pop", new(), new()));
                    }
                    else if (un.Op == "i32.eqz" || un.Op == "i64.eqz")
                    {
                        body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                        var eqzExpr = new BoogieFunctionCall("bool_to_real", new List<BoogieExpr> {
                            new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ,
                                new BoogieIdentifierExpr("$tmp1"), new BoogieLiteralExpr(new Pfloat(0)))
                        });
                        body.AddStatement(new BoogieCallCmd("push", new List<BoogieExpr> { eqzExpr }, new()));
                    }
                    else if (un.Op == "i32.wrap_i64")
                    {
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
                        or "i32.div_s" or "i64.div_s" or "f32.div" or "f64.div"
                        or "i32.div_u" or "i64.div_u")
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
                            "i32.eq" or "i64.eq" or "f32.eq" or "f64.eq"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, tmp2, tmp1) }),
                            "i32.ne" or "i64.ne" or "f32.ne" or "f64.ne"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.NEQ, tmp2, tmp1) }),
                            "i32.lt_s" or "i64.lt_s" or "i32.lt_u" or "i64.lt_u" or "f32.lt" or "f64.lt"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LT, tmp2, tmp1) }),
                            "i32.le_s" or "i64.le_s" or "i32.le_u" or "i64.le_u" or "f32.le" or "f64.le"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LE, tmp2, tmp1) }),
                            "i32.gt_s" or "i64.gt_s" or "i32.gt_u" or "i64.gt_u" or "f32.gt" or "f64.gt"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GT, tmp2, tmp1) }),
                            "i32.ge_s" or "i64.ge_s" or "i32.ge_u" or "i64.ge_u" or "f32.ge" or "f64.ge"
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, tmp2, tmp1) }),
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
{
    bool isUserLabel = blk.Label != null && blk.Label.StartsWith("$");

    LabelContext? blkCtx = null;
    if (isUserLabel)
    {
        var watLabel = blk.Label!.Substring(1); // drop '$'
        blkCtx = new LabelContext { WatLabel = watLabel, IsLoop = false, EndLabel = GenerateLabel(watLabel) };
        labelStack.Push(blkCtx);
    }

    foreach (var child in blk.Body) TranslateNode(child, body);

    if (blkCtx != null)
    {
        body.AddStatement(new BoogieSkipCmd(blkCtx.EndLabel + ":"));
        labelStack.Pop();
    }
    break;
}


case LoopNode loop:
{
    bool isUserLabel = !string.IsNullOrEmpty(loop.Label) && loop.Label.StartsWith("$");

    LabelContext? ctx = null;
    if (isUserLabel)
    {
        var wat = loop.Label!.Substring(1); // drop '$'
        var start = GenerateLabel($"{wat}_start");
        var end   = GenerateLabel($"{wat}_end");

        ctx = new LabelContext {
            WatLabel  = wat,
            IsLoop    = true,
            StartLabel= start,
            EndLabel  = end
        };
        labelStack.Push(ctx);

        // loop entry (target for br to the loop)
        body.AddStatement(new BoogieSkipCmd(start + ":"));
    }

    // body
    foreach (var child in loop.Body)
        TranslateNode(child, body);

    // loop end label only if user labeled
    if (ctx != null)
    {
        body.AddStatement(new BoogieSkipCmd(ctx.EndLabel + ":"));
        labelStack.Pop();
    }
    break;
}


                case IfNode ifn:
                    TranslateNode(ifn.Condition, body);
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    var thenBlock = new BoogieStmtList();
                    foreach (var stmt in ifn.ThenBody) TranslateNode(stmt, thenBlock);
                    BoogieStmtList? elseBlock = null;
                    if (ifn.ElseBody != null)
                    {
                        elseBlock = new BoogieStmtList();
                        foreach (var stmt in ifn.ElseBody) TranslateNode(stmt, elseBlock);
                    }
                    var ifStmt = new BoogieIfCmd(new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") }), thenBlock, elseBlock);
                    body.AddStatement(ifStmt);
                    break;

                case BrNode br:
                    var brLabel = br.Label.StartsWith("$") ? br.Label.Substring(1) : br.Label;
                    var targetCtx = labelStack.FirstOrDefault(ctx => ctx.WatLabel == brLabel);
                    if (targetCtx != null)
                    {
                        string target = targetCtx.IsLoop ? (targetCtx.StartLabel ?? targetCtx.EndLabel) : targetCtx.EndLabel;
                        body.AddStatement(new BoogieGotoCmd(target));
                    }
                    else
                    {
                        body.AddStatement(new BoogieGotoCmd(functionExitLabel ?? brLabel));
                    }
                    break;

                case BrIfNode brIf:
                    TranslateNode(brIf.Condition, body);
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    var ifLabel = brIf.Label.StartsWith("$") ? brIf.Label.Substring(1) : brIf.Label;
                    var ctxMatch = labelStack.FirstOrDefault(ctx => ctx.WatLabel == ifLabel);
                    string targetLabel = ctxMatch != null
                        ? (ctxMatch.IsLoop ? (ctxMatch.StartLabel ?? ctxMatch.EndLabel) : ctxMatch.EndLabel)
                        : (functionExitLabel ?? ifLabel);
                    var branchBlock = new BoogieStmtList();
                    branchBlock.AddStatement(new BoogieGotoCmd(targetLabel));
                    var brIfStmt = new BoogieIfCmd(new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") }), branchBlock, null);
                    body.AddStatement(brIfStmt);
                    break;

                case RawInstructionNode raw:
                {
                    var s = raw.Instruction;
                    if (s.StartsWith("$") || s.Contains("=>") || s == "module" || s == "type" || s == "func")
                    {
                        // ignore structural/name crumbs
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unhandled raw instruction: {s}"));
                    }
                    break;
                }
            }
        }
    }
}
