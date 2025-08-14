var $stack: [int]real;
var $sp: int;
var $tmp1: real;
var $tmp2: real;
var $tmp3: real;
function bool_to_real(b: bool) : real
{
  if b then (1.0) else (0.0)
}
function real_to_bool(r: real) : bool
{
  if (r) == (0.0) then (false) else (true)
}
procedure {:inline true} push(val: real);
modifies $sp;
modifies $stack;
implementation push(val: real)
{
$stack[$sp] := val;
$sp := ($sp) + (1);
}

procedure popToTmp1();
modifies $sp;
modifies $stack;
modifies $tmp1;
implementation popToTmp1()
{
$sp := ($sp) - (1);
$tmp1 := $stack[$sp];
}

procedure popToTmp2();
modifies $sp;
modifies $stack;
modifies $tmp2;
implementation popToTmp2()
{
$sp := ($sp) - (1);
$tmp2 := $stack[$sp];
}

procedure popToTmp3();
modifies $sp;
modifies $stack;
modifies $tmp3;
implementation popToTmp3()
{
$sp := ($sp) - (1);
$tmp3 := $stack[$sp];
}

procedure pop();
modifies $sp;
implementation pop()
{
$sp := ($sp) - (1);
}

procedure func_0();
modifies $tmp1;
modifies $tmp2;
modifies $tmp3;
modifies $sp;
modifies $stack;
implementation func_0()
{
call push(42.0);
}

procedure {:inline true} popArgs1() returns (a1: real);
modifies $sp;
modifies $stack;
implementation popArgs1() returns (a1: real)
{
$sp := ($sp) - (1);
a1 := $stack[$sp];
}

procedure func_1();
modifies $tmp1;
modifies $tmp2;
modifies $tmp3;
modifies $sp;
modifies $stack;
implementation func_1()
{
var arg1: real;
call arg1 := popArgs1();
call push(arg1);
call push(1.0);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
}

procedure {:inline true} popArgs2() returns (a1: real, a2: real);
modifies $sp;
modifies $stack;
implementation popArgs2() returns (a1: real, a2: real)
{
$sp := ($sp) - (1);
a2 := $stack[$sp];
$sp := ($sp) - (1);
a1 := $stack[$sp];
}

procedure func_2();
modifies $tmp1;
modifies $tmp2;
modifies $tmp3;
modifies $sp;
modifies $stack;
implementation func_2()
{
var arg1: real;
var arg2: real;
call arg1, arg2 := popArgs2();
call push(arg1);
call push(arg2);
call popToTmp1();
call popToTmp2();
call push(($tmp2) - ($tmp1));
}

procedure {:inline true} popArgs3() returns (a1: real, a2: real, a3: real);
modifies $sp;
modifies $stack;
implementation popArgs3() returns (a1: real, a2: real, a3: real)
{
$sp := ($sp) - (1);
a3 := $stack[$sp];
$sp := ($sp) - (1);
a2 := $stack[$sp];
$sp := ($sp) - (1);
a1 := $stack[$sp];
}

procedure func_3();
modifies $tmp1;
modifies $tmp2;
modifies $tmp3;
modifies $sp;
modifies $stack;
implementation func_3()
{
var arg1: real;
var arg2: real;
var arg3: real;
var loc1: real;
call arg1, arg2, arg3 := popArgs3();
loc1 := 0.0;
call push(arg1);
call push(arg2);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
call loc1 := popArgs1();
call push(loc1);
call push(arg3);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
}

procedure func_4();
modifies $tmp1;
modifies $tmp2;
modifies $tmp3;
modifies $sp;
modifies $stack;
implementation func_4()
{
var arg1: real;
var arg2: real;
var arg3: real;
call arg1, arg2, arg3 := popArgs3();
call push(arg1);
call func_1();
call push(arg2);
call push(arg3);
call func_2();
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
}


