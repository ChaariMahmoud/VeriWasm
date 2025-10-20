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
┌────────────┐ ┌───────────────┐ ┌───────────┐ ┌────────────┐ ┌──────────────────┐
│ WAT File │ ───► │ AST Builder │ ───► │ Boogie │ ───► │ Z3/Corral │ ───► │ Verification Report │
└────────────┘ └───────────────┘ └───────────┘ └────────────┘ └──────────────────┘
VeriWasm translates WebAssembly programs into Boogie code,  
then leverages existing SMT-based verification tools to check correctness properties, invariants, and safety guarantees.

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
-*BoogieOutputs/example.bpl* – generated Boogie code

-*boogie.txt* – Boogie verification output

-*corral.txt* – Corral verification report

## Contributing

We welcome contributions and suggestions! Feel free to contribute to this project by:

- Reporting bugs and issues
- Suggesting new features
- Submitting pull requests
- Improving documentation
- Adding test cases

### Contact

For questions, suggestions, or contributions, please contact:
- **Email**: [chaarimahmoud55@gmail.com](mailto:chaarimahmoud55@gmail.com)

### How to Contribute

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

We appreciate all contributions that help make VeriSol Extended better!
© 2025 Mahmoud Chaari — VeriWasm: A Formal Verification Framework for WebAssembly
