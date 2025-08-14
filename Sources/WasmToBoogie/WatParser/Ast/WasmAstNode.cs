using System.Collections.Generic;

namespace WasmToBoogie.Parser.Ast
{
    public abstract class WasmNode { }

    public class ConstNode : WasmNode
    {
        public string Type { get; set; }  // e.g., i32
        public string Value { get; set; } // e.g., 5
    }

    public class UnaryOpNode : WasmNode
    {
        public string Op { get; set; }     // e.g., drop, i32.eqz
        public WasmNode Operand { get; set; }
    }

    public class BinaryOpNode : WasmNode
    {
        public string Op { get; set; }      // e.g., i32.add, i32.lt_s
        public WasmNode Left { get; set; }
        public WasmNode Right { get; set; }
    }

    public class IfNode : WasmNode
    {
        public WasmNode Condition { get; set; }
        public List<WasmNode> ThenBody { get; set; } = new();
        public List<WasmNode>? ElseBody { get; set; } = null;
    }

    public class BlockNode : WasmNode
    {
        public string? Label { get; set; }
        public List<WasmNode> Body { get; set; } = new();
    }

    public class LoopNode : WasmNode
    {
        public string? Label { get; set; }
        public List<WasmNode> Body { get; set; } = new();
    }

    public class BrNode : WasmNode
    {
        public string Label { get; set; }  // e.g., $label
    }

    public class BrIfNode : WasmNode
    {
        public string Label { get; set; }  // e.g., $label
        public WasmNode Condition { get; set; }
    }

    public class LocalGetNode : WasmNode
    {
        public int? Index { get; set; }
        public string? Name { get; set; }  // if used like (local.get $a)
    }

    public class LocalSetNode : WasmNode
    {
        public int? Index { get; set; }
        public string? Name { get; set; }
        public WasmNode? Value { get; set; } // not used in current parser style (stack form)
    }

    public class LocalTeeNode : WasmNode
    {
        public int? Index { get; set; }
        public string? Name { get; set; }
    }

    public class CallNode : WasmNode
    {
        public string Target { get; set; }
        public List<WasmNode> Args { get; set; } = new();  // e.g., $compute or function index
    }

    public class RawInstructionNode : WasmNode
    {
        public string Instruction { get; set; }
    }

    public class WasmFunction
    {
        public string? Name { get; set; }
        public List<WasmNode> Body { get; set; } = new();

        // new: signature info (filled by parser)
        public int ParamCount { get; set; } = 0;
        public int LocalCount { get; set; } = 0;
        public Dictionary<string, int> LocalIndexByName { get; set; } = new(); // params first [0..n-1], then locals
    }

    public class WasmModule
    {
        public List<WasmFunction> Functions { get; set; } = new();
    }
}
