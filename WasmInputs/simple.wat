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
(;;(module
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
);)


(;;(module
  (func (export "test")
    (block $exit
      (loop $start
        (i32.const 0)
        (i32.eqz)
        (br_if $exit)
        (i32.const 42)
        (drop)
        (br $start)
      )
    )
  )
);)



(;;(module
  (func $test
    (i32.const 1)
    (if
      (then
        (i32.const 100)
        (drop)
      )
      (else
        (i32.const 200)
        (drop)
      )
    ))
  );)
(;;(module
  (func $test
    (i32.const 1)
    (if
      (then
        (i32.const 1)
        (i32.const 2)
        (i32.add)
        (drop)
      )
      (else
        (i32.const 3)
        (i32.const 4)
        (i32.sub)
        (drop)
      )
    )
  )
);)

(;;(module
  (func $test
    ;; Partie 1 — Arithmétique + logique
    (i32.const 5)
    (i32.const 3)
    (i32.add)       ;; stack: 8
    (i32.const 8)
    (i32.eq)        ;; stack: 1 (true)
    (drop)

    (i32.const 0)
    (i32.eqz)       ;; stack: 1
    (drop)

    ;; Partie 2 — if imbriqués (sans block)
    (i32.const 1)
    (if
      (then
        (i32.const 2)
        (i32.const 2)
        (i32.eq)
        (if
          (then
            (f32.const 42.746)
            (drop)
          )
          (else
            (i32.const 99)
            (drop)
          )
        )
        (i32.const 77)
        (drop)
      )
      (else
        (i32.const 0)
        (i32.eqz)
        (if
          (then
            (i32.const 100)
            (drop)
          )
          (else
            (i32.const 200)
            (drop)
          )
        )
        (i32.const 888)
        (drop)
      )
    )
  )
);)



(;;(module
  (func $test
    ;; Push condition = 1 (true)
    (i32.const 1)

    (if
      (then
        ;; début du bloc implicite
        (loop $loop_label
          ;; Calcul: 3 + 4
          (i32.const 3)
          (i32.const 4)
          (i32.add)
          (drop)

          ;; Condition: push 0 (false)
          (i32.const 0)
          (br_if $loop_label) ;; ne saute pas car 0 → continue

          ;; Condition: push 1 (true)
          (i32.const 1)
          (br_if $loop_label) ;; saute en haut de la boucle
        )
      )
      (else
        ;; branche else : simple opération
        (i32.const 42)
        (drop)
      )
    )
  )
);)
(;;(module
  (func $test
    (block $exit
      (i32.const 1)      ;; condition (true)
      (br $exit)      ;; saute directement à la fin du bloc
      (i32.const 99)     ;; jamais atteint
      (drop)
    )
    (i32.const 42)
    (drop)
  )
);)

(;;(module
  (func $test
    ;; Arithmétique simple : 3 + 4
    (i32.const 3)
    (i32.const 4)
    (i32.add)
    (drop)

    ;; Bloc externe avec saut conditionnel
    (block $exit
      (i32.const 0)
      (br_if $exit) ;; ne saute pas car 0

      ;; Boucle avec label
      (loop $top
        (i32.const 5)
        (i32.const 2)
        (i32.mul)
        (drop)

        ;; Test logique : 1 == 1
        (i32.const 1)
        (i32.const 1)
        (i32.eq)
        (br_if $top) ;; saute : boucle infinie sans drop
      )
    )

    ;; if / else logique
    (i32.const 1) ;; condition
    (if
      (then
        (i32.const 42)
        (drop)
      )
      (else
        (i32.const 99)
        (drop)
      )
    )
  )
);)


(module
  (func $test
    (block $exit
      (loop $start
        ;; push 5, 5 → eq → br_if $exit (ne saute pas)
        (i32.const 5)
        (i32.const 5)
        (i32.eq)
        (br_if $exit)

        ;; push 3, 4 → add → drop
        (i32.const 3)
        (i32.const 4)
        (i32.add)
        (drop)

        ;; push 1 → br_if $start (loop again)
        (i32.const 1)
        (br_if $start)
      )
    )

    ;; if (1) then 42 else 7
    (i32.const 1)
    (if
      (then
        (i32.const 42)
        (drop)
      )
      (else
        (i32.const 7)
        (drop)
      )
    )
  )
)











