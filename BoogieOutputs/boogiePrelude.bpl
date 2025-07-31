var $stack: [int]real;
var $sp: int;
var $tmp1:real;
var $tmp2:real;
var $tmp3:real;

function bool_to_real(b: bool): real;
function real_to_bool(r: real): bool;
function real_to_int(r: real): int;

axiom (forall b: bool :: bool_to_real(b) == (if b then 1.0 else 0.0));
axiom (forall r: real :: real_to_bool(r) == (r != 0.0));
axiom (forall r: real :: real_to_int(r) >= 0);


procedure {:inline 1} push(val: real);
  modifies $sp, $stack;
  
implementation push(val: real)
{
  $stack[$sp] := val;
  $sp := $sp + 1;
}

procedure popToTmp1();
  modifies $sp, $stack, $tmp1;
  
implementation popToTmp1()
{  

  $sp := $sp - 1;
  $tmp1 := $stack[$sp];
}

procedure popToTmp2();
  modifies $sp, $stack, $tmp2;
implementation popToTmp2()
{ 

  $sp := $sp - 1;
  $tmp2 := $stack[$sp];
}

procedure popToTmp3();
  modifies $sp, $stack, $tmp3;
implementation popToTmp3()
{  

  $sp := $sp - 1;
  $tmp3 := $stack[$sp];
}

procedure pop();
  modifies $sp;
implementation pop()
{
  $sp := $sp - 1;
}

procedure BoogieEntry_simple();
  modifies $tmp1 ,$tmp2 , $sp, $stack;

implementation BoogieEntry_simple() 
{
// // Type AST non supportÃƒÂ© : RawInstructionNode
// // Type AST non supportÃƒÂ© : RawInstructionNode
call push(5.0);
call push(2.0);
call push(3.0);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
call popToTmp1();
call popToTmp2();
call push(bool_to_real(($tmp2) == ($tmp1)));
call pop();
}