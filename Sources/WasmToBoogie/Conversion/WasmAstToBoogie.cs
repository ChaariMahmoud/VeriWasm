using BoogieAST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WasmToBoogie.Parser.Ast;

namespace WasmToBoogie.Conversion
{
    public class WasmAstToBoogie
    {
        private readonly string contractName;
        private int labelCounter = 0;

        // Flag: assertion de pile à la sortie de fonction
        public bool EnableFooterStackAssert { get; set; } = false;

        // Module Boogie en construction
        private BoogieProgram? program;

        // Générateur unique pour popArgsN
        private readonly HashSet<int> popArgsMade = new();

        // État par fonction
        private List<BoogieIdentifierExpr>? currentLocalMap; // arg1..argN, loc1..locM
        private WasmFunction? currentFunction;
        private HashSet<string>? neededLoopStartLabels;
        private HashSet<string>? neededBlockEndLabels;
        private readonly Stack<LabelContext> labelStack = new();
        private string? functionExitLabel;

        private class LabelContext
        {
            public string? WatLabel;
            public string? StartLabel;   // loop "continue"
            public string  EndLabel = ""; // block/loop end ("break")
            public bool    IsLoop;
            public bool MarkEndUsed;
        }

        public WasmAstToBoogie(string contractName) => this.contractName = contractName;

        // ======== Helpers ========

        private static bool AllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        private static string NormalizeLabel(string raw)
            => string.IsNullOrEmpty(raw) ? raw : (raw[0] == '$' ? raw[1..] : raw);

        private static string MapCalleeName(string target)
        {
            if (string.IsNullOrEmpty(target)) return target;
            string name = target[0] == '$' ? target[1..] : target;
            return AllDigits(name) ? "func_" + name : name;
        }

        private static string SanitizeFunctionName(string? watName, string contractName)
        {
            if (!string.IsNullOrEmpty(watName))
            {
                var n = watName![0] == '$' ? watName.Substring(1) : watName;
                if (int.TryParse(n, out _)) return $"func_{n}";
                n = Regex.Replace(n, @"[^A-Za-z0-9_]", "_");
                if (!char.IsLetter(n[0]) && n[0] != '_') n = "_" + n;
                return n;
            }
            return $"func_{contractName}";
        }

        private string GenerateLabel(string baseName) => $"{baseName}_{++labelCounter}";

        // Résolution d'une cible de branchement (label texte ou profondeur numérique)
private string ResolveBranchTarget(string labOrDepth)
{
    if (AllDigits(labOrDepth))
    {
        int depth = int.Parse(labOrDepth);
        if (depth < 0 || depth >= labelStack.Count)
            return functionExitLabel ?? (functionExitLabel = GenerateLabel("func_exit"));

        var arr = labelStack.ToArray();          // top -> bottom
        var ctx = arr[depth];

        // NEW: on note que l'end de ce bloc peut être ciblé par profondeur
        ctx.MarkEndUsed = true;

        return ctx.IsLoop ? (ctx.StartLabel ?? ctx.EndLabel) : ctx.EndLabel;
    }

    var norm = NormalizeLabel(labOrDepth);
    var hit = labelStack.FirstOrDefault(c => c.WatLabel == norm);
    if (hit != null)
        return hit.IsLoop ? (hit.StartLabel ?? hit.EndLabel) : hit.EndLabel;

    return functionExitLabel ?? (functionExitLabel = GenerateLabel("func_exit"));
}

        // ======== Entrée publique ========

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

        // ======== Préambule Boogie (pile + helpers) ========

        private void AddPrelude(BoogieProgram program)
        {
            // Globals
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)));
            program.Declarations.Add(new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real)));

            // bool_to_real
            {
                var b = new BoogieFormalParam(new BoogieTypedIdent("b", BoogieType.Bool));
                var body = new BoogieITE(
                    new BoogieIdentifierExpr("b"),
                    new BoogieLiteralExpr(new Pfloat(1)),
                    new BoogieLiteralExpr(new Pfloat(0))
                );
                program.Declarations.Add(new BoogieFunctionDef("bool_to_real", new() { b }, BoogieType.Real, body));
            }

            // real_to_bool
            {
                var r = new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real));
                var body = new BoogieITE(
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, new BoogieIdentifierExpr("r"), new BoogieLiteralExpr(new Pfloat(0))),
                    new BoogieLiteralExpr(false),
                    new BoogieLiteralExpr(true)
                );
                program.Declarations.Add(new BoogieFunctionDef("real_to_bool", new() { r }, BoogieType.Bool, body));
            }

            // real_to_int : uninterpreted cast
            {
                var r = new BoogieFormalParam(new BoogieTypedIdent("r", BoogieType.Real));
                var res = new BoogieFormalParam(new BoogieTypedIdent("result", BoogieType.Int));
                program.Declarations.Add(new BoogieFunction("real_to_int", new() { r }, new() { res }));
            }

            // push(val)
            {
                var proc = new BoogieProcedure(
                    "push",
                    new() { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                    new(),
                    new() { new BoogieAttribute("inline", true) },
                    new()
                    {
                        new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                        new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                    },
                    new(), new());
                program.Declarations.Add(proc);

                var body = new BoogieStmtList();
                body.AddStatement(new BoogieAssignCmd(
                    new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp")),
                    new BoogieIdentifierExpr("val")));
                var oldSp = new BoogieIdentifierExpr("$sp");
                body.AddStatement(new BoogieAssignCmd(oldSp,
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.ADD, oldSp, new BoogieLiteralExpr(1))));
                var impl = new BoogieImplementation(
                    "push",
                    new() { new BoogieFormalParam(new BoogieTypedIdent("val", BoogieType.Real)) },
                    new(), new(), body);
                program.Declarations.Add(impl);
            }

            // popToTmp1/2/3
            AddPopToTmp("popToTmp1", "$tmp1");
            AddPopToTmp("popToTmp2", "$tmp2");
            AddPopToTmp("popToTmp3", "$tmp3");

            // pop() (drop 1)
            {
                var proc = new BoogieProcedure("pop", new(), new(), new(),
                    new() { new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)) },
                    new(), new());
                program.Declarations.Add(proc);

                var body = new BoogieStmtList();
                body.AddStatement(new BoogieAssumeCmd(
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GT, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(0))));
                body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
                var impl = new BoogieImplementation("pop", new(), new(), new(), body);
                program.Declarations.Add(impl);
            }

            // local helper
            void AddPopToTmp(string name, string tmp)
            {
                var proc = new BoogieProcedure(name, new(), new(), new(),
                    new()
                    {
                        new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                        new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real))),
                        new BoogieGlobalVariable(new BoogieTypedIdent(tmp, BoogieType.Real))
                    },
                    new(), new());
                program.Declarations.Add(proc);

                var body = new BoogieStmtList();
                body.AddStatement(new BoogieAssumeCmd(new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GT,
                    new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(0))));
                body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
                body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr(tmp),
                    new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));

                var impl = new BoogieImplementation(name, new(), new(), new(), body);
                program.Declarations.Add(impl);
            }
        }

        // popArgsN (inline, retourne a1..aN) — callee pops args
        private void EnsurePopArgsProc(int n)
        {
            if (n <= 0 || program == null || popArgsMade.Contains(n)) return;
            popArgsMade.Add(n);

            var outs = new List<BoogieVariable>();
            for (int i = 1; i <= n; i++)
                outs.Add(new BoogieFormalParam(new BoogieTypedIdent($"a{i}", BoogieType.Real)));

            var proc = new BoogieProcedure(
                $"popArgs{n}",
                new(), outs,
                new() { new BoogieAttribute("inline", true) },
                new()
                {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new(), new());
            program!.Declarations.Add(proc);

            var body = new BoogieStmtList();
            body.AddStatement(new BoogieAssumeCmd(
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(n))
            ));
            for (int i = n; i >= 1; i--)
            {
                body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$sp"),
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(1))));
                body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr($"a{i}"),
                    new BoogieMapSelect(new BoogieIdentifierExpr("$stack"), new BoogieIdentifierExpr("$sp"))));
            }
            var impl = new BoogieImplementation($"popArgs{n}", new(), outs, new(), body);
            program!.Declarations.Add(impl);
        }

        // ======== Traduction de fonction ========

        private (BoogieProcedure, BoogieImplementation) TranslateFunction(WasmFunction func)
        {
            var inParams = new List<BoogieVariable>();   // (pile)
            var outParams = new List<BoogieVariable>();  // (pile)
            var locals = new List<BoogieVariable>();
            var body = new BoogieStmtList();

            // état par fonction
            currentFunction = func;
            functionExitLabel = null;
            PrecomputeLabelNeeds(func);

            // Construire la table arg/loc
            int n = func.ParamCount;
            int m = func.LocalCount;
            int r = Math.Max(0, func.ResultCount);
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
            locals.Add(new BoogieLocalVariable(new BoogieTypedIdent("idx", BoogieType.Int)));
            locals.Add(new BoogieLocalVariable(new BoogieTypedIdent("entry_sp", BoogieType.Int)));

            currentLocalMap = indexToId;

            // Prologue
            body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("entry_sp"), new BoogieIdentifierExpr("$sp")));
            body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp1"), new BoogieLiteralExpr(new Pfloat(0))));
            body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp2"), new BoogieLiteralExpr(new Pfloat(0))));
            body.AddStatement(new BoogieAssignCmd(new BoogieIdentifierExpr("$tmp3"), new BoogieLiteralExpr(new Pfloat(0))));

            // Args : callee pop ses args
            if (n > 0)
            {
                EnsurePopArgsProc(n);
                body.AddStatement(new BoogieAssumeCmd(
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, new BoogieIdentifierExpr("$sp"), new BoogieLiteralExpr(n))
                ));
                body.AddStatement(new BoogieCallCmd($"popArgs{n}", new(), indexToId.Take(n).ToList()));
            }

            // Locals init 0
            for (int i = n; i < n + m; i++)
                body.AddStatement(new BoogieAssignCmd(indexToId[i], new BoogieLiteralExpr(new Pfloat(0))));

            // Corps
            foreach (var node in func.Body)
                TranslateNode(node, body);

            // Matérialiser label de sortie si utilisé
            if (!string.IsNullOrEmpty(functionExitLabel))
            {
                body.AddStatement(new BoogieSkipCmd(functionExitLabel + ":"));
                functionExitLabel = null;
            }

            // Épilogue : assertion de discipline de pile (optionnelle)
            var expected = new BoogieBinaryOperation(
                BoogieBinaryOperation.Opcode.ADD,
                new BoogieIdentifierExpr("entry_sp"),
                new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.SUB,
                    new BoogieLiteralExpr(r),
                    new BoogieLiteralExpr(n))
            );

            if (EnableFooterStackAssert)
            {
                body.AddStatement(new BoogieAssertCmd(
                    new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, new BoogieIdentifierExpr("$sp"), expected)));
            }
            else
            {
                body.AddStatement(new BoogieCommentCmd("// footer stack assert disabled"));
            }

            string funcName = SanitizeFunctionName(func.Name, contractName);

            var proc = new BoogieProcedure(
                funcName,
                inParams, outParams,
                new(),
                new()
                {
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp1", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp2", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$tmp3", BoogieType.Real)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$sp", BoogieType.Int)),
                    new BoogieGlobalVariable(new BoogieTypedIdent("$stack", new BoogieMapType(BoogieType.Int, BoogieType.Real)))
                },
                new(), new());

            var impl = new BoogieImplementation(proc.Name, inParams, outParams, locals, body);

            // reset état
            currentLocalMap = null;
            currentFunction = null;
            neededLoopStartLabels = null;
            neededBlockEndLabels = null;
            labelStack.Clear();

            return (proc, impl);
        }

        // ======== Pré-scan des labels ========

        private void PrecomputeLabelNeeds(WasmFunction func)
        {
            neededLoopStartLabels = new HashSet<string>(StringComparer.Ordinal);
            neededBlockEndLabels  = new HashSet<string>(StringComparer.Ordinal);
            var scope = new Stack<(string label, bool isLoop)>();

            void Walk(WasmNode n)
            {
                switch (n)
                {
                    case BlockNode blk:
                        {
                            bool hasUser = !string.IsNullOrEmpty(blk.Label) && blk.Label!.StartsWith("$", StringComparison.Ordinal);
                            if (hasUser) scope.Push((blk.Label!.Substring(1), false));
                            foreach (var m in blk.Body) Walk(m);
                            if (hasUser) scope.Pop();
                            break;
                        }
                    case LoopNode lp:
                        {
                            bool hasUser = !string.IsNullOrEmpty(lp.Label) && lp.Label!.StartsWith("$", StringComparison.Ordinal);
                            if (hasUser) scope.Push((lp.Label!.Substring(1), true));
                            foreach (var m in lp.Body) Walk(m);
                            if (hasUser) scope.Pop();
                            break;
                        }
                    case IfNode iff:
                        {
                            Walk(iff.Condition);
                            foreach (var m in iff.ThenBody) Walk(m);
                            if (iff.ElseBody != null) foreach (var m in iff.ElseBody) Walk(m);
                            break;
                        }
                    case BinaryOpNode b:
                        Walk(b.Left); Walk(b.Right);
                        break;
                    case UnaryOpNode u:
                        if (u.Operand != null) Walk(u.Operand);
                        break;
                    case BrNode br:
                        {
                            var target = NormalizeLabel(br.Label);
                            foreach (var (lab, isLoop) in scope)
                            {
                                if (lab == target)
                                {
                                    if (isLoop) neededLoopStartLabels!.Add(lab);
                                    else neededBlockEndLabels!.Add(lab);
                                    break;
                                }
                            }
                            break;
                        }
                    case BrIfNode bri:
                        {
                            Walk(bri.Condition);
                            var target = NormalizeLabel(bri.Label);
                            foreach (var (lab, isLoop) in scope)
                            {
                                if (lab == target)
                                {
                                    if (isLoop) neededLoopStartLabels!.Add(lab);
                                    else neededBlockEndLabels!.Add(lab);
                                    break;
                                }
                            }
                            break;
                        }
                }
            }

            foreach (var n in func.Body) Walk(n);
        }

        // ======== Traduction des nœuds ========

        private int ResolveLocalIndex(int? index, string? name)
        {
            if (index.HasValue) return index.Value;

            if (!string.IsNullOrEmpty(name))
            {
                if (currentFunction != null &&
                    currentFunction.LocalIndexByName.TryGetValue(name, out var idx))
                    return idx;

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
                {
                    if (double.TryParse(cn.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var dv))
                    {
                        body.AddStatement(new BoogieCallCmd("push",
                            new() { new BoogieLiteralExpr(new Pfloat((float)dv)) }, new()));
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported const value: {cn.Value}"));
                    }
                    break;
                }

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
                    if (ls.Value != null) TranslateNode(ls.Value, body);
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

                case CallNode call:
                {
                    if (call.Args != null)
                        foreach (var a in call.Args) TranslateNode(a, body);
                    string target = MapCalleeName(call.Target);
                    body.AddStatement(new BoogieCallCmd(target, new(), new()));
                    break;
                }

                case UnaryOpNode un:
                {
                    if (un.Operand != null) TranslateNode(un.Operand, body);

                    if (un.Op == "drop")
                    {
                        body.AddStatement(new BoogieCallCmd("pop", new(), new()));
                    }
                    else if (un.Op == "i32.eqz" || un.Op == "i64.eqz")
                    {
                        body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                        var eqzExpr = new BoogieFunctionCall("bool_to_real", new()
                        {
                            new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ,
                                new BoogieIdentifierExpr("$tmp1"),
                                new BoogieLiteralExpr(new Pfloat(0)))
                        });
                        body.AddStatement(new BoogieCallCmd("push", new() { eqzExpr }, new()));
                    }
                    else if (un.Op == "i32.wrap_i64" || un.Op == "i64.wrap_i64")
                    {
                        body.AddStatement(new BoogieCommentCmd("// wrap: no-op under real semantics"));
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported unary op: {un.Op}"));
                    }
                    break;
                }

                case BinaryOpNode bn:
                {
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
                            _ => BoogieBinaryOperation.Opcode.DIV
                        };
                        var arithExpr = new BoogieBinaryOperation(opKind, tmp2, tmp1);
                        body.AddStatement(new BoogieCallCmd("push", new() { arithExpr }, new()));
                    }
                    else if (bn.Op is
                        "i32.eq" or "i64.eq" or "f32.eq" or "f64.eq" or
                        "i32.ne" or "i64.ne" or "f32.ne" or "f64.ne" or
                        "i32.lt_s" or "i64.lt_s" or "i32.lt_u" or "i64.lt_u" or "f32.lt" or "f64.lt" or
                        "i32.le_s" or "i64.le_s" or "i32.le_u" or "i64.le_u" or "f32.le" or "f64.le" or
                        "i32.gt_s" or "i64.gt_s" or "i32.gt_u" or "i64.gt_u" or "f32.gt" or "f64.gt" or
                        "i32.ge_s" or "i64.ge_s" or "i32.ge_u" or "i64.ge_u" or "f32.ge" or "f64.ge")
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
                            _ // ge
                                => new BoogieFunctionCall("bool_to_real", new() { new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, tmp2, tmp1) }),
                        };
                        body.AddStatement(new BoogieCallCmd("push", new() { cmpExpr }, new()));
                    }
                    else
                    {
                        body.AddStatement(new BoogieCommentCmd($"// unsupported binary op: {bn.Op}"));
                    }
                    break;
                }

                case BlockNode blk:
                {
                        if (blk.Label == "module" || blk.Label == "func" || blk.Label == "type")
    {
        foreach (var child in blk.Body)
            TranslateNode(child, body);
        break;
    }
                    // Toujours matérialiser l'EndLabel d'un block (cible de "break"/br depth sur block)
                    string? wat = blk.Label != null && blk.Label.StartsWith("$") ? blk.Label.Substring(1) : null;

                    var ctx = new LabelContext
                    {
                        WatLabel = wat,
                        IsLoop = false,
                        StartLabel = null,
                        EndLabel = GenerateLabel(wat != null ? $"{wat}_end" : "block_end")
                    };
                    labelStack.Push(ctx);

                    foreach (var child in blk.Body) TranslateNode(child, body);

                    body.AddStatement(new BoogieSkipCmd(ctx.EndLabel + ":"));
                    labelStack.Pop();
                    break;
                }

                case LoopNode loop:
                {
                    // Toujours matérialiser StartLabel (continue) ET EndLabel (break)
                    string? wat = loop.Label != null && loop.Label.StartsWith("$") ? loop.Label.Substring(1) : null;

                    var ctx = new LabelContext
                    {
                        WatLabel = wat,
                        IsLoop = true,
                        StartLabel = GenerateLabel(wat != null ? $"{wat}_start" : "loop_start"),
                        EndLabel   = GenerateLabel(wat != null ? $"{wat}_end"   : "loop_end")
                    };
                    labelStack.Push(ctx);

                    body.AddStatement(new BoogieSkipCmd(ctx.StartLabel + ":")); // cible "continue"
                    foreach (var child in loop.Body) TranslateNode(child, body);
                    body.AddStatement(new BoogieSkipCmd(ctx.EndLabel + ":"));   // cible "break"

                    labelStack.Pop();
                    break;
                }

                case BrNode br:
                {
                    var target = ResolveBranchTarget(br.Label);
                    body.AddStatement(new BoogieGotoCmd(target));
                    break;
                }

                case BrIfNode brIf:
                {
                    TranslateNode(brIf.Condition, body);
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    var target = ResolveBranchTarget(brIf.Label);
                    var thenBlk = new BoogieStmtList();
                    thenBlk.AddStatement(new BoogieGotoCmd(target));
                    var cond = new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") });
                    body.AddStatement(new BoogieIfCmd(cond, thenBlk, null));
                    break;
                }

                case BrTableNode bt:
                {
                    // idx := int(pop)
                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    body.AddStatement(new BoogieAssignCmd(
                        new BoogieIdentifierExpr("idx"),
                        new BoogieFunctionCall("real_to_int", new() { new BoogieIdentifierExpr("$tmp1") })
                    ));

                    int k = bt.Targets.Count;
                    var idx = new BoogieIdentifierExpr("idx");

                    // Si idx hors bornes -> default
                    var outCond = new BoogieBinaryOperation(
                        BoogieBinaryOperation.Opcode.OR,
                        new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.LT, idx, new BoogieLiteralExpr(0)),
                        new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.GE, idx, new BoogieLiteralExpr(k))
                    );
                    var outBlk = new BoogieStmtList();
                    outBlk.AddStatement(new BoogieGotoCmd(ResolveBranchTarget(bt.Default)));

                    var inBlk = new BoogieStmtList();
                    for (int i = 0; i < k; i++)
                    {
                        var condEq = new BoogieBinaryOperation(BoogieBinaryOperation.Opcode.EQ, idx, new BoogieLiteralExpr(i));
                        var thenBlk = new BoogieStmtList();
                        thenBlk.AddStatement(new BoogieGotoCmd(ResolveBranchTarget(bt.Targets[i])));
                        inBlk.AddStatement(new BoogieIfCmd(condEq, thenBlk, null));
                    }
                    // garde : si aucune case ne match, saute au default
                    inBlk.AddStatement(new BoogieGotoCmd(ResolveBranchTarget(bt.Default)));

                    body.AddStatement(new BoogieIfCmd(outCond, outBlk, inBlk));
                    break;
                }

                case UnreachableNode:
                {
                    // coupe l’exploration au lieu d'exiger une preuve d’inatteignabilité
                    body.AddStatement(new BoogieAssumeCmd(new BoogieLiteralExpr(false)));
                    break;
                }

                case SelectNode sel:
                {
                    TranslateNode(sel.V1, body);
                    TranslateNode(sel.V2, body);
                    TranslateNode(sel.Cond, body);

                    body.AddStatement(new BoogieCallCmd("popToTmp1", new(), new()));
                    body.AddStatement(new BoogieCallCmd("popToTmp2", new(), new()));
                    body.AddStatement(new BoogieCallCmd("popToTmp3", new(), new()));

                    var cond = new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") });

                    var thenBlk = new BoogieStmtList();
                    thenBlk.AddStatement(new BoogieCallCmd("push", new() { new BoogieIdentifierExpr("$tmp3") }, new()));

                    var elseBlk = new BoogieStmtList();
                    elseBlk.AddStatement(new BoogieCallCmd("push", new() { new BoogieIdentifierExpr("$tmp2") }, new()));

                    body.AddStatement(new BoogieIfCmd(cond, thenBlk, elseBlk));
                    break;
                }

                case IfNode ifn:
                {
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

                    var cond = new BoogieFunctionCall("real_to_bool", new() { new BoogieIdentifierExpr("$tmp1") });
                    body.AddStatement(new BoogieIfCmd(cond, thenBlock, elseBlock));
                    break;
                }

                case ReturnNode:
                {
                    if (functionExitLabel == null) functionExitLabel = GenerateLabel("func_exit");
                    body.AddStatement(new BoogieGotoCmd(functionExitLabel));
                    break;
                }

                case NopNode:
                {
                    body.AddStatement(new BoogieSkipCmd());
                    break;
                }

                case RawInstructionNode raw:
                {
                    var s = raw.Instruction;
                    if (s.StartsWith("$") || s.Contains("=>") || s == "module" || s == "type" || s == "func")
                    {
                        // ignore
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
