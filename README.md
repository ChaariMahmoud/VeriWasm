[![Build Status](https://shuvendu-lahiri.visualstudio.com/VeriSol%20Azure%20pipeline%20Build/_apis/build/status/microsoft.verisol?branchName=master)](https://shuvendu-lahiri.visualstudio.com/VeriSol%20Azure%20pipeline%20Build/_build/latest?definitionId=3&branchName=master)

# VeriSol Extended

VeriSol Extended is an enhanced version of the original [Microsoft VeriSol project](https://www.microsoft.com/en-us/research/project/verisol-a-formal-verifier-for-solidity-based-smart-contracts/) with additional support for WebAssembly (WASM) smart contracts and modern .NET 9.0 framework.

## What's New

This extended version includes:

- **WasmToBoogie Translator**: A new module that translates WebAssembly (WASM) smart contracts to Boogie intermediate verification language
- **Multi-Contract Support**: Now supports both Solidity (0.5.x) and WebAssembly smart contracts
- **Modern .NET 9.0**: Upgraded from .NET Core 2.2 to .NET 9.0 for better performance and security
- **Updated Dependencies**: 
  - Boogie 3.5.1 (upgraded from older versions)
  - Corral with .NET 6.0 support
  - Enhanced toolchain compatibility
- **Centralized Tool Configuration**: Manage Boogie/Corral/Z3/Binaryen paths via `ToolPaths` with `--config` and `--validate`
- **Docker Support**: Complete containerized solution for easy deployment

## Original VeriSol

VeriSol (Verifier for Solidity) is a [Microsoft Research project](https://www.microsoft.com/en-us/research/project/verisol-a-formal-verifier-for-solidity-based-smart-contracts/) for prototyping a formal verification and analysis system for smart contracts developed in the popular [Solidity](https://solidity.readthedocs.io/) programming language. It is based on translating
programs in Solidity language to programs in [Boogie](https://github.com/boogie-org/boogie) intermediate 
verification language, and then leveraging and extending the verification toolchain for Boogie programs. The following [blog](https://www.microsoft.com/en-us/research/blog/researchers-work-to-secure-azure-blockchain-smart-contracts-with-formal-verification/) provides a high-level overview of the initial goals or VeriSol.

The following paper describes the design of VeriSol and application of smart contract verification for [Azure Blockchain](https://azure.microsoft.com/en-us/solutions/blockchain/):

> [__Formal Specification and Verification of Smart Contracts for Azure Blockchain__](https://www.microsoft.com/en-us/research/publication/formal-specification-and-verification-of-smart-contracts-for-azure-blockchain/),  Yuepeng Wang, Shuvendu K. Lahiri, Shuo Chen, Rong Pan, Isil Dillig, Cody Born, Immad Naseer, https://arxiv.org/abs/1812.08829

## Features

### Solidity Support
- Formal verification of Solidity 0.5.x smart contracts
- Pre/post conditions, loop invariants, contract invariants
- Modifies clauses and extended assertion language
- Support for ERC20, DAO, and other common contract patterns

### WebAssembly Support (NEW)
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

## Quick Start with Docker

The easiest way to use VeriSol Extended is through Docker:

### 1. Use the Pre-built Docker Image (Recommended)
```bash
# Pull the latest image from Docker Hub
docker pull chaarimahmoud/verisol-extended:latest

# Or use a specific version
docker pull chaarimahmoud/verisol-extended:v1.0.0
```

### 2. Build from Source (Alternative)
```bash
git clone <your-repo-url>
cd verisol-extended
docker build -t verisol-extended:latest .
```

### 3. Test the Installation
```bash
# Show configuration
docker run --rm chaarimahmoud/verisol-extended:latest --config

# Validate tools
docker run --rm chaarimahmoud/verisol-extended:latest --validate

# Show help
docker run --rm chaarimahmoud/verisol-extended:latest --help
```

### 4. Verify WebAssembly Contracts
```bash
# Basic usage
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm WasmInputs/simple.wat

# With custom WAT file
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm your-contract.wat
```

### 5. Verify Solidity Contracts
```bash
# Basic usage
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20

# With custom contract
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest your-contract.sol ContractName
```

## Usage

### Solidity Contracts
```bash
VeriSol <solidity-file.sol> <contract-name> [options]
```

### WebAssembly Contracts
```bash
VeriSol --wasm <wat-file.wat>
```

### Configuration and Validation
```bash
VeriSol --config      # Show tool configuration
VeriSol --validate    # Validate tool paths
```

### Examples

**Solidity Example:**
```bash
VeriSol Test/regressions/ERC20-simplified.sol ERC20
```

**WebAssembly Example:**
```bash
VeriSol --wasm WasmInputs/simple.wat
```

## Docker Usage Guide

### Prerequisites
- Docker installed on your system
- WAT files for WebAssembly contracts
- Solidity files for Solidity contracts

### Docker Commands

#### Basic Commands
```bash
# Pull the pre-built image
docker pull chaarimahmoud/verisol-extended:latest

# Build from source (alternative)
docker build -t verisol-extended:latest .

# Show help
docker run --rm chaarimahmoud/verisol-extended:latest --help

# Show configuration
docker run --rm chaarimahmoud/verisol-extended:latest --config

# Validate tools
docker run --rm chaarimahmoud/verisol-extended:latest --validate
```

#### WebAssembly Verification
```bash
# Basic WASM verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm WasmInputs/simple.wat

# With custom output directory
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm your-contract.wat

# View generated Boogie output
ls -la BoogieOutputs/
cat BoogieOutputs/your-contract.bpl
```

#### Solidity Verification
```bash
# Basic Solidity verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20

# With custom contract
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest your-contract.sol ContractName
```

#### Custom Configuration
```bash
# Override tool paths
docker run --rm \
  -e VERISOL_BOOGIE_PATH=/custom/boogie \
  -e VERISOL_CORRAL_PATH=/custom/corral \
  -e VERISOL_BINARYEN_PATH=/custom/libbinaryenwrapper.so \
  -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --validate
```

### Docker Configuration

Inside the container, tool paths are pre-configured:
```text
VERISOL_BOOGIE_PATH=/app/tools/boogie
VERISOL_CORRAL_PATH=/app/tools/corral
VERISOL_Z3_PATH=/usr/bin/z3
VERISOL_BINARYEN_PATH=/app/tools/libbinaryenwrapper.so
```

### Output Files

The Docker container generates:
- `BoogieOutputs/<contract-name>.bpl` - Generated Boogie code
- `boogie.txt` - Boogie verification results
- `corral.txt` - Corral verification results

## INSTALL

For detailed installation instructions, see [INSTALL.md](INSTALL.md).

## VeriSol Code Contracts library

The code contract library **VeriSolContracts.sol** is present [here](/Test/regressions/Libraries/VeriSolContracts.sol). This allows adding specifications in the form of pre/post conditions, loop invariants, contract invariants, modifies clauses, and extending the assertion language with constructs such as old, sum, etc.

## Architecture

### WasmToBoogie Module
The new WasmToBoogie module consists of:

- **WasmParser**: Parses WASM Text Format (.wat) files into an Abstract Syntax Tree (AST)
- **WasmAstToBoogie**: Translates WASM AST to Boogie intermediate language
- **Stack Management**: Handles WASM's stack-based execution model
- **Control Flow**: Translates WASM control structures (blocks, loops, branches) to Boogie

### Key Components
- `WasmToBoogieMain.cs`: Main entry point for WASM translation
- `WasmAstToBoogie.cs`: Core translation logic
- `WatParser/`: WASM text format parser
- `Conversion/`: AST to Boogie translation
- `ToolPaths.cs`: Centralized tool configuration

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
