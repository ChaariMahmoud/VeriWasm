// --- Pile pour la traduction WAT vers Boogie ---

var $stack: [int]real;
var $sp: int;

procedure push(val: real)
  modifies $sp, $stack;
{
  $stack[$sp] := val;
  $sp := $sp + 1;
}

procedure pop() returns (val: real)
  modifies $sp;
{
  $sp := $sp - 1;
  val := $stack[$sp];
}
