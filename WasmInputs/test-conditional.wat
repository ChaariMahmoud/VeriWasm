(module
  (func $factorial (param i32) (result i32)
    (local i32)
    (local.set 1 (i32.const 1))
    (block $loop
      (loop $start
        (local.get 0)
        (i32.const 1)
        (i32.le_s)
        (br_if $loop)
        (local.get 1)
        (local.get 0)
        (i32.mul)
        (local.set 1)
        (local.get 0)
        (i32.const 1)
        (i32.sub)
        (local.set 0)
        (br $start)
      )
    )
    (local.get 1)
  )
  
  (func $fibonacci (param i32) (result i32)
    (local i32 i32 i32)
    (local.get 0)
    (i32.const 2)
    (i32.lt_s)
    (if
      (then
        (local.get 0)
        (return)
      )
    )
    (local.set 1 (i32.const 0))
    (local.set 2 (i32.const 1))
    (block $loop
      (loop $start
        (local.get 0)
        (i32.const 2)
        (i32.le_s)
        (br_if $loop)
        (local.get 1)
        (local.get 2)
        (i32.add)
        (local.set 3)
        (local.get 2)
        (local.set 1)
        (local.get 3)
        (local.set 2)
        (local.get 0)
        (i32.const 1)
        (i32.sub)
        (local.set 0)
        (br $start)
      )
    )
    (local.get 2)
  )
  
  (func $main
    (drop
      (call $factorial
        (i32.const 5)
      )
    )
    (drop
      (call $fibonacci
        (i32.const 7)
      )
    )
  )
) 