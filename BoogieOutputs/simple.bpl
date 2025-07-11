procedure BoogieEntry_simple();
implementation BoogieEntry_simple()
{
// // Type AST non supporté : RawInstructionNode
// // Type AST non supporté : RawInstructionNode
call push(5);
call push(2);
call push(3);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
call popToTmp1();
call popToTmp2();
call push(bool_to_real(($tmp2) == ($tmp1)));
call pop();
}


