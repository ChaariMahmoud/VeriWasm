var $stack: [int]real;
var $sp: int;

procedure push(val: real)
  modifies $sp, $stack;
{
  $stack[$sp] := val;
  $sp := $sp + 1;
}

procedure popToTmp1()
  modifies $sp, $stack, $tmp1;
{
  $sp := $sp - 1;
  $tmp1 := $stack[$sp];
}

procedure popToTmp2()
  modifies $sp, $stack, $tmp1;
{
  $sp := $sp - 1;
  $tmp2 := $stack[$sp];
}

procedure popToTmp3()
  modifies $sp, $stack, $tmp1;
{
  $sp := $sp - 1;
  $tmp3 := $stack[$sp];
}
