procedure BoogieEntry_simple();
implementation BoogieEntry_simple()
{
call push(3);
call push(7);
call push(5);
call push((pop()) + (pop()));
call push((pop()) + (pop()));
}


