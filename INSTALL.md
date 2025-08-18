# VeriSol Extended Installation Guide

This guide provides instructions for installing and running VeriSol Extended, an enhanced version of Microsoft's VeriSol with WebAssembly support.

## Prerequisites

### System Requirements
- **Operating System**: Windows 10+, macOS 10.15+, or Linux (Ubuntu 18.04+)
- **Memory**: Minimum 4GB RAM, recommended 8GB+
- **Storage**: At least 2GB free space
- **Docker**: For containerized installation (recommended)

### Required Software
- **.NET 9.0 SDK**: For building from source
- **Docker**: For containerized deployment (recommended)
- **Git**: For cloning the repository

## Installation Methods

### Method 1: Docker (Recommended)

The easiest way to get started with VeriSol Extended is using Docker:

#### 1. Install Docker
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install docker.io docker-compose

# macOS
brew install docker docker-compose

# Windows
# Download Docker Desktop from https://www.docker.com/products/docker-desktop
```

#### 2. Use Pre-built Image (Recommended)
```bash
# Pull the latest image from Docker Hub
docker pull chaarimahmoud/verisol-extended:latest
```

#### 3. Build from Source (Alternative)
```bash
git clone <[your-repo-url](https://github.com/ChaariMahmoud/VeriSol-Extended.git)>
cd verisol-extended
docker build -t verisol-extended:latest .
```

#### 4. Verify Installation
```bash
# Test configuration
docker run --rm chaarimahmoud/verisol-extended:latest --config

# Test validation
docker run --rm chaarimahmoud/verisol-extended:latest --validate

# Test help
docker run --rm chaarimahmoud/verisol-extended:latest --help
```

#### 5. Quick Test
```bash
# Test WASM verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm WasmInputs/simple.wat

# Test Solidity verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20
```

### Method 2: Build from Source

#### 1. Install .NET 9.0 SDK
```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0

# macOS
brew install dotnet

# Windows
# Download from https://dotnet.microsoft.com/download
```

#### 2. Clone Repository
```bash
git clone (https://github.com/ChaariMahmoud/VeriSol-Extended.git)
cd Verisol-Extended
```

#### 3. Build the Solution
```bash
dotnet build Sources/VeriSol.sln -c Release
```

#### 4. Install Required Tools
The build process will automatically download and install:
- Boogie 3.5.1
- Corral (.NET 6.0)
- Z3 Theorem Prover
- Binaryen (for WASM support)

#### 5. Verify Installation
```bash
# Test configuration
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --config

# Test validation
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --validate

# Test help
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --help
```

### Method 3: Direct Execution

If you have the compiled binaries:

```bash
# Navigate to the bin directory
cd bin/Debug

# Run VeriSol
./VeriSol --help
./VeriSol --config
./VeriSol --validate
```

## Tool Configuration

VeriSol Extended uses a centralized configuration system for tool paths (see `Sources/SharedConfig/ToolPaths.cs`). This makes it easy to customize paths for different environments.

### Quick Configuration Check

1. **Check current tool configuration:**
```bash
VeriSol --config
```

2. **Validate that all tools are available:**
```bash
VeriSol --validate
```

### Configuring Tool Paths

#### Method 1: Environment Variables (Recommended)
Set these environment variables to override default paths:

```bash
export VERISOL_BOOGIE_PATH=/path/to/boogie
export VERISOL_CORRAL_PATH=/path/to/corral
export VERISOL_SOLC_PATH=/path/to/solc
export VERISOL_Z3_PATH=/path/to/z3
export VERISOL_BINARYEN_PATH=/path/to/libbinaryenwrapper.so
```

#### Method 2: Edit Configuration File
Edit the `tool-paths.config` file in the project root:

```bash
# Edit the base directory
BASE_TOOLS_DIRECTORY=/your/tools/directory

# Or specify individual paths
BOOGIE_PATH=/path/to/boogie
CORRAL_PATH=/path/to/corral
```

#### Method 3: Modify Source Code
Edit the `BaseToolsDirectory` in `Sources/SharedConfig/ToolPaths.cs`:

```csharp
private static readonly string BaseToolsDirectory = "/your/tools/directory";
```

#### Method 4: Default Location
Simply copy all tools to the `bin/Debug` directory of your VeriSol installation.

### Docker Tool Paths
The Docker image sets these defaults:

```text
VERISOL_BOOGIE_PATH=/app/tools/boogie
VERISOL_CORRAL_PATH=/app/tools/corral
VERISOL_Z3_PATH=/usr/bin/z3
VERISOL_BINARYEN_PATH=/app/tools/libbinaryenwrapper.so
```

You can override them via `-e VAR=value` in `docker run`.

## Verification

After installation and configuration, verify that everything works:

1. **Check VeriSol installation:**
```bash
# Docker
docker run --rm chaarimahmoud/verisol-extended:latest --help

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --help
```

2. **Validate tool configuration:**
```bash
# Docker
docker run --rm chaarimahmoud/verisol-extended:latest --validate

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --validate
```

3. **Test Solidity verification:**
```bash
# Docker
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- Test/regressions/ERC20-simplified.sol ERC20
```

4. **Test WASM verification:**
```bash
# Docker
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm WasmInputs/simple.wat

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --wasm WasmInputs/simple.wat
```

## Running VeriSol

### Basic Usage

#### Solidity Contracts
```bash
# Docker
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest <contract.sol> <ContractName>

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- <contract.sol> <ContractName>
```

#### WebAssembly Contracts
```bash
# Docker
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm <contract.wat>

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- --wasm <contract.wat>
```

### Command Line Options

#### Solidity Options
- `/noChk` - Don't perform verification (default: false)
- `/noPrf` - Don't perform inductive verification (default: false)
- `/txBound:k` - Max transaction depth (default: 4)
- `/noTxSeq` - Don't print transaction sequence
- `/contractInfer` - Perform module invariant inference
- `/inlineDepth:k` - Inline nested calls up to depth k

#### WebAssembly Options
- `--wasm <file.wat>` - Specify WAT file for verification

#### Configuration Options
- `--config` - Show tool configuration
- `--validate` - Validate tool paths
- `--help` - Show usage information

### Examples

#### Solidity Examples
```bash
# Basic contract verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20

# With options
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20 /txBound:2 /noPrf
```

#### WebAssembly Examples
```bash
# Basic WASM verification
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm WasmInputs/simple.wat

# Custom WAT file
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest --wasm my-contract.wat
```

## Regression Testing

Run the regression test suite:

```bash
# Docker
docker run --rm -v "$PWD":/workspace -w /workspace chaarimahmoud/verisol-extended:latest Test/regressions/ERC20-simplified.sol ERC20

# Local
dotnet run --project Sources/VeriSol/VeriSol.csproj -- Test/regressions/ERC20-simplified.sol ERC20
```

## Troubleshooting

### Common Issues

#### Docker Issues
1. **Permission denied**: Ensure Docker has proper permissions
```bash
sudo usermod -aG docker $USER
# Log out and back in
```

2. **Volume mounting issues**: Use absolute paths
```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace verisol-extended:latest --wasm WasmInputs/simple.wat
```

#### Build Issues
1. **.NET SDK not found**: Install .NET 9.0 SDK
2. **Missing dependencies**: Run `dotnet restore` before building
3. **Tool path issues**: Use `--config` and `--validate` to check configuration

#### Runtime Issues
1. **Tool not found**: Check tool paths with `--validate`
2. **Permission errors**: Ensure output directories are writable
3. **Memory issues**: Increase Docker memory allocation or system RAM

### Getting Help

1. **Check configuration**: `VeriSol --config`
2. **Validate tools**: `VeriSol --validate`
3. **Show help**: `VeriSol --help`
4. **Check logs**: Look for error messages in output

## Platform-Specific Notes

### Windows
- Use PowerShell or Command Prompt
- Ensure Docker Desktop is running
- Use Windows-style paths in volume mounts

### macOS
- Use Homebrew for package installation
- Docker Desktop for Mac is recommended
- Use Unix-style paths

### Linux
- Use package manager for dependencies
- Docker Engine installation recommended
- Ensure proper user permissions

## Next Steps

After successful installation:

1. **Read the documentation**: Check the README.md for detailed usage
2. **Try examples**: Run the provided test cases
3. **Create your own contracts**: Start with simple examples
4. **Join the community**: Contribute to the project

For more information, see the main [README.md](README.md) file.




