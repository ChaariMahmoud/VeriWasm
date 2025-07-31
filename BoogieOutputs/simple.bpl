var $stack: [int]real;
var $sp: int;
var $tmp1: real;
var $tmp2: real;
var $tmp3: real;
function bool_to_real(b: bool) returns (result: real);
function real_to_bool(r: real) returns (result: bool);
function real_to_int(r: real) returns (result: int);

axiom(forall  b:bool ::  ((bool_to_real(b)) == (if b then (1.0) else (0.0))));

axiom(forall  r:real ::  ((real_to_bool(r)) == ((r) != (0.0))));

axiom(forall  r:real ::  ((real_to_int(r)) >= (0)));
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

procedure func_simple();
modifies $tmp1;
modifies $tmp2;
modifies $sp;
modifies $stack;
implementation func_simple()
{
exit_1:
start_2:
// // unhandled raw instruction: $none_=>_none
func_5:
type_4:
// // unhandled raw instruction: $temp
label$2_start_8:
call push(5.0);
call push(5.0);
call popToTmp1();
call popToTmp2();
call push(bool_to_real(($tmp2) == ($tmp1)));
call popToTmp1();
if (real_to_bool($tmp1)) {
goto label$1_7;
}
call push(3.0);
call push(4.0);
call popToTmp1();
call popToTmp2();
call push(($tmp2) + ($tmp1));
call pop();
call push(1.0);
call popToTmp1();
if (real_to_bool($tmp1)) {
goto label$2_start_8;
}
label$2_end_9:
label$1_7:
call push(1.0);
call popToTmp1();
if (real_to_bool($tmp1)) {
call push(42.0);
call pop();
} else {
call push(7.0);
call pop();
}
func_6:
module_3:
}


