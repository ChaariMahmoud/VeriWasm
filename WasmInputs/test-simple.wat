(module
  (func $add (param i32 i32) (result i32)
    (i32.add
      (local.get 0)
      (local.get 1)
    )
  )
  
  (func $multiply (param i32 i32) (result i32)
    (i32.mul
      (local.get 0)
      (local.get 1)
    )
  )
  
  (func $main
    (drop
      (call $add
        (i32.const 5)
        (i32.const 3)
      )
    )
    (drop
      (call $multiply
        (i32.const 4)
        (i32.const 6)
      )
    )
  )
) 