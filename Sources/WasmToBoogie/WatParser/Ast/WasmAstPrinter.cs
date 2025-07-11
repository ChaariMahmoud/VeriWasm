using System;
using WasmToBoogie.Parser.Ast;
using System.Collections.Generic;

namespace WasmToBoogie.Tools
{
    public static class WasmAstPrinter
    {
        public static void PrettyPrint(WasmModule module)
        {
            Console.WriteLine("📦 AST complet :");

            int funcIndex = 0;
            foreach (var func in module.Functions)
            {
                Console.WriteLine($"\n🔧 Fonction {funcIndex++}:");
                foreach (var node in func.Body)
                {
                    PrettyPrintNode(node, 1);
                }
            }
        }

        private static void PrettyPrintNode(WasmNode node, int indent)
        {
            string prefix = new string(' ', indent * 2);

            switch (node)
            {
                case ConstNode c:
                    Console.WriteLine($"{prefix}ConstNode(type={c.Type}, value={c.Value})");
                    break;

                case UnaryOpNode u:
                    Console.WriteLine($"{prefix}UnaryOpNode(op={u.Op})");
                    PrettyPrintNode(u.Operand, indent + 1);
                    break;

                case BinaryOpNode b:
                    Console.WriteLine($"{prefix}BinaryOpNode(op={b.Op})");
                    PrettyPrintNode(b.Left, indent + 1);
                    PrettyPrintNode(b.Right, indent + 1);
                    break;

                case IfNode ifn:
                    Console.WriteLine($"{prefix}IfNode");
                    Console.WriteLine($"{prefix}  Condition:");
                    PrettyPrintNode(ifn.Condition, indent + 2);

                    Console.WriteLine($"{prefix}  Then:");
                    foreach (var stmt in ifn.ThenBody)
                        PrettyPrintNode(stmt, indent + 2);

                    if (ifn.ElseBody != null)
                    {
                        Console.WriteLine($"{prefix}  Else:");
                        foreach (var stmt in ifn.ElseBody)
                            PrettyPrintNode(stmt, indent + 2);
                    }
                    break;

                case BlockNode blk:
                    Console.WriteLine($"{prefix}BlockNode(label={blk.Label})");
                    foreach (var stmt in blk.Body)
                        PrettyPrintNode(stmt, indent + 1);
                    break;

                case LoopNode loop:
                    Console.WriteLine($"{prefix}LoopNode(label={loop.Label})");
                    foreach (var stmt in loop.Body)
                        PrettyPrintNode(stmt, indent + 1);
                    break;

                case RawInstructionNode raw:
                    Console.WriteLine($"{prefix}RawInstructionNode(\"{raw.Instruction}\")");
                    break;

                default:
                    Console.WriteLine($"{prefix}Unknown node type: {node.GetType().Name}");
                    break;
            }
        }
    }
}
