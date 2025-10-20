# VeriWasm

**VeriWasm** is a formal verification framework for **WebAssembly (WASM)** programs.  
It translates **WAT (WebAssembly Text Format)** into **Boogie**, enabling reasoning with SMT solvers such as **Z3** and **Corral**, built on the modern **.NET 9.0** platform.

## 🧩 Origin & Motivation

The project originates from the formal verification concepts explored in **Microsoft VeriSol**,  
which translated Solidity smart contracts into Boogie for verification.  
However, **VeriWasm** redefines the vision: instead of focusing on one programming language,  
it uses **WebAssembly (WASM)** as a *universal intermediate representation (IR)* for formal verification.

WebAssembly’s stack-based and deterministic nature makes it ideal for certifiable reasoning,  
allowing verification of programs compiled from various source languages (Rust, C, Go, etc.)  
and across different execution environments — not only blockchains.

## 🌍 Why WebAssembly?

WebAssembly (WASM) provides several properties that make it particularly suitable for formal verification:

- **Portability:** acts as a universal target for multiple languages (Rust, C, C++, Go, etc.)  
- **Determinism:** well-defined execution semantics  
- **Safety:** sandboxed linear memory model prevents undefined behavior  
- **Verifiability:** simple, stack-based operational semantics  
- **Adoption:** increasingly used for blockchain, embedded, and critical systems  

Together, these features make WASM an excellent foundation for scalable and provable verification frameworks.

## Features


### WebAssembly Support 
- Translation of WASM smart contracts to Boogie
- Support for WASM Text Format (.wat) files
- Formal verification of WASM-based smart contracts
- Stack-based execution model translation
- Support for complex WASM features: loops, conditionals, function calls, error handling

### Verification Capabilities
- Inductive verification
- Bounded model checking
- Counterexample generation
- Transaction sequence analysis
- Contract invariant inference


## ⚙️ Verification Pipeline

```
┌────────────┐      ┌───────────────┐      ┌───────────┐      ┌────────────┐      ┌──────────────────┐
│  WAT File  │ ───► │   AST Builder │ ───► │   Boogie   │ ───► │   Z3/Corral │ ───► │ Verification Report │
└────────────┘      └───────────────┘      └───────────┘      └────────────┘      └──────────────────┘
```

VeriWasm translates WebAssembly programs written in **WAT (WebAssembly Text Format)**  
into **Boogie** for formal reasoning. The translated Boogie code is then verified using  
SMT solvers like **Z3** or model checkers like **Corral**, producing formal verification reports.

**Pipeline summary:**
1. Parse WAT and build an Abstract Syntax Tree (AST)  
2. Convert the AST to Boogie code  
3. Generate Verification Conditions (VCs)  
4. Solve VCs with SMT backends (Z3 / Corral)  
5. Produce verification reports and counterexamples if violations occur  

---

## 🚀 Key Features

### WebAssembly to Boogie Translation
- Support for **WASM Text Format (.wat)**  
- **AST-based translation** with stack-typed model (`push`, `pop`, `popToTmp`)  
- Control flow constructs: `if`, `loop`, `block`, `br`, `br_if`  
- Arithmetic, logical, and comparison operators  
- Label management for structured flow control  

### Verification Infrastructure
- Automatic generation of **Verification Conditions (VCs)**  
- Verification via **Boogie → Z3 / Corral**  
- Modular translation for scalability  
- Support for multiple backends (Z3, CVC5, IC3/PDR, BMC)  
- Detailed verification reports and counterexamples  

### Modular Architecture
- `Parser/` → Parses WAT files and builds an AST  
- `Conversion/` → Translates AST nodes into Boogie statements  
- `Verification/` → Interfaces with Boogie and SMT solvers  
- `ToolPaths.cs` → Centralized configuration for Boogie, Z3, Corral, and Binaryen  

---

## 🧠 Research Context

VeriWasm is developed as part of a **doctoral research project** on  
**“Specification and Formal Verification of WebAssembly Programs”**  
conducted at the **Laboratoire de Recherche de l’EPITA (LRE)**,  
under the supervision of **Souheib Baarir**, in collaboration with **Sorbonne Université – EDITE**.

The project aims to design a **certifiable verification condition generator (VCG)**  
and scalable reasoning framework for WebAssembly using Boogie and modern SMT-based approaches.

---
## 🧩 Installation

### Prerequisites
- .NET 9.0 SDK  
- Boogie  
- Z3  
- Corral  
- Binaryen or WABT tools  

### Build from Source
```bash
git clone https://github.com/ChaariMahmoud/VeriWasm.git
cd VeriWasm/Sources
dotnet build
```
##🧪 Usage
Basic Command
```bash
dotnet run --wasm <file.wat>
```
## Output files:
-*BoogieOutputs/example.bpl* : generated Boogie code

-*boogie.txt* : Boogie verification output

-*corral.txt* : Corral verification report

## 🐳 Docker Support

VeriWasm can also be executed inside a containerized environment.

### Pull the latest image
```bash
docker pull chaarimahmoud/veriwasm:latest
```

### Run verification
```bash
docker run --rm -v "$PWD":/workspace -w /workspace   chaarimahmoud/veriwasm:latest --wasm WasmInputs/example.wat
```

---

## 📈 Roadmap

- Add memory operations (`load`, `store`, `heap`)
- Multi-function and modular verification
- Automatic invariant inference
- Alternative solver integration (CVC5, IC3/PDR)
- Web-based visualization for verification results

---

## 🤝 Contributing

We welcome contributions and suggestions!  
You can:

- Report issues  
- Propose new features  
- Submit pull requests  
- Improve documentation  

### How to Contribute
1. Fork the repository  
2. Create a feature branch  
3. Commit your changes  
4. Push and open a Pull Request  

---

## 📧 Contact

**Author:** Mahmoud Chaari  
**Email:** [chaarimahmoud55@gmail.com](mailto:chaarimahmoud55@gmail.com)  
**Affiliation:** Laboratoire de Recherche de l’EPITA (LRE) – Sorbonne Université, EDITE  
**Keywords:** WebAssembly, Boogie, Formal Verification, Z3, Corral, .NET 9.0  

---

## 📚 References

- [WebAssembly.org](https://webassembly.org/)  
- [Boogie: Intermediate Verification Language](https://www.microsoft.com/en-us/research/project/boogie-an-intermediate-verification-language/)  
- [Z3 SMT Solver](https://github.com/Z3Prover/z3)  
- [Corral Model Checker](https://github.com/boogie-org/corral)  

---

> © 2025 Mahmoud Chaari — VeriWasm: A Formal Verification Framework for WebAssembly

