// --- Prelude de la pile ---
var $stack: [int]int;
var $sp: int;

procedure push(val: int)
  modifies $sp, $stack;
{
  $stack[$sp] := val;
  $sp := $sp + 1;
}

procedure pop() returns (val: int)
  modifies $sp;
{
  $sp := $sp - 1;
  val := $stack[$sp];
}

// --- Point d'entrée généré ---
procedure BoogieEntry_simple();

implementation BoogieEntry_simple() 
  modifies $sp, $stack;
{
  var a: int;
  var b: int;

  call push(32);
  call push(10);
  call a := pop();
  call b := pop();
  call push((b) + (a));
}
