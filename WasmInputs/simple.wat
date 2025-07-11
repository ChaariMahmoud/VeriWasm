(;;(module
  (func
    ;; i32: ((10 + 2) * (8 - 3)) / 2
    (drop
      (i32.div_s
        (i32.mul
          (i32.add
            (i32.const 10)
            (i32.const 2)
          )
          (i32.sub
            (i32.const 8)
            (i32.const 3)
          )
        )
        (i32.const 2)
      )
    )

    ;; i64: (50 - 20) + (5 * 3)
    (drop
      (i64.add
        (i64.sub
          (i64.const 50)
          (i64.const 20)
        )
        (i64.mul
          (i64.const 5)
          (i64.const 3)
        )
      )
    )

    ;; f32: ((2.5 + 1.5) * 2.0) / 4.0
    (drop
      (f32.div
        (f32.mul
          (f32.add
            (f32.const 2.5)
            (f32.const 1.5)
          )
          (f32.const 2.0)
        )
        (f32.const 4.0)
      )
    )

    ;; f64: (7.0 - 2.0) * (3.0 + 1.0)
    (drop
      (f64.mul
        (f64.sub
          (f64.const 7.0)
          (f64.const 2.0)
        )
        (f64.add
          (f64.const 3.0)
          (f64.const 1.0)
        )
      )
    )
  )
);)
(module
  (func $test
    (drop
      (i32.eq
        (i32.const 5)
        (i32.add
          (i32.const 2)
          (i32.const 3)
        )
      )
    )
  )
)



