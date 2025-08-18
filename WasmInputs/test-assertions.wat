(module
  (func $safe_divide (param i32 i32) (result i32)
    (local.get 1)
    (i32.const 0)
    (i32.eq)
    (if
      (then
        (unreachable)  ;; Division by zero
      )
    )
    (local.get 0)
    (local.get 1)
    (i32.div_s)
  )
  
  (func $check_range (param i32) (result i32)
    (local.get 0)
    (i32.const 0)
    (i32.lt_s)
    (if
      (then
        (i32.const 0)
        (return)
      )
    )
    (local.get 0)
    (i32.const 100)
    (i32.gt_s)
    (if
      (then
        (i32.const 100)
        (return)
      )
    )
    (local.get 0)
  )
  
  (func $main
    (drop
      (call $safe_divide
        (i32.const 10)
        (i32.const 2)
      )
    )
    (drop
      (call $check_range
        (i32.const 50)
      )
    )
  )
) 