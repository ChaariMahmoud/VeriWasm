# Tool Configuration Guide for VeriSol Extended

## Overview

VeriSol Extended now uses a centralized configuration system for all external tool paths. This makes it much easier for users to customize paths for their specific environment without having to modify multiple source files.

## What Was Refactored

### Before (Hardcoded Paths)
Previously, tool paths were hardcoded in multiple locations:
- `Sources/VeriSol/VeriSolExecuter.cs` - Multiple hardcoded paths for Boogie, Corral, Solc
- `Sources/WasmToBoogie/WatParser/WasmParser.cs` - Hardcoded Binaryen library path
- Manual path corrections scattered throughout the code

### After (Centralized Configuration)
All tool paths are now managed through a single `ToolPaths` class in `Sources/SharedConfig/ToolPaths.cs`:

```csharp
public static class ToolPaths
{
    public static string BoogiePath { get; }
    public static string CorralPath { get; }
    public static string SolcPath { get; }
    public static string Z3Path { get; }
    public static string BinaryenLibraryPath { get; }
}
```

## Configuration Methods

### 1. Environment Variables (Recommended)

Set these environment variables to override default paths:

```bash
export VERISOL_BOOGIE_PATH=/path/to/boogie
export VERISOL_CORRAL_PATH=/path/to/corral
export VERISOL_SOLC_PATH=/path/to/solc
export VERISOL_Z3_PATH=/path/to/z3
export VERISOL_BINARYEN_PATH=/path/to/libbinaryenwrapper.so
```

### 2. Configuration File

Edit the `tool-paths.config` file in the project root:

```bash
# Base directory where all tools are located
BASE_TOOLS_DIRECTORY=/your/tools/directory

# Individual tool paths (optional)
BOOGIE_PATH=/path/to/boogie
CORRAL_PATH=/path/to/corral
```

### 3. Source Code Modification

Edit the `BaseToolsDirectory` in `Sources/SharedConfig/ToolPaths.cs`:

```csharp
private static readonly string BaseToolsDirectory = "/your/tools/directory";
```

### 4. Default Location

Simply copy all tools to the `bin/Debug` directory of your VeriSol installation.

## Automatic Path Detection

The system automatically tries to find tools in the following order:

1. **Environment Variables** - Highest priority
2. **Base Tools Directory** - `bin/Debug` by default
3. **System-wide Locations** - `/usr/local/bin`, `/usr/bin`, etc.
4. **Fallback Paths** - Original hardcoded paths as last resort

## New Command-Line Options

### Check Configuration
```bash
VeriSol --config
```
Shows current tool paths and configuration instructions.

### Validate Tools
```bash
VeriSol --validate
```
Checks if all required tools are available and accessible.

## Project Structure Changes

### New Files Created
- `Sources/SharedConfig/SharedConfig.csproj` - Shared configuration project
- `Sources/SharedConfig/ToolPaths.cs` - Centralized tool path management
- `tool-paths.config` - User-editable configuration file

### Files Modified
- `Sources/VeriSol/VeriSolExecuter.cs` - Now uses `ToolPaths` class
- `Sources/VeriSol/Program.cs` - Added `--config` and `--validate` options
- `Sources/WasmToBoogie/WatParser/WasmParser.cs` - Uses centralized Binaryen path
- `Sources/VeriSol.sln` - Added SharedConfig and WasmToBoogie projects

### Files Removed
- `Sources/VeriSol/ToolPaths.cs` - Moved to SharedConfig project

## Benefits

1. **Easier Configuration** - Users can set paths in one place
2. **Environment Flexibility** - Support for different deployment scenarios
3. **Better Error Handling** - Clear validation and error messages
4. **Maintainability** - No more scattered hardcoded paths
5. **Cross-Platform** - Works on Windows, Linux, and macOS

## Migration Guide

### For Existing Users
1. Run `VeriSol --config` to see current configuration
2. Run `VeriSol --validate` to check if tools are accessible
3. If tools are missing, set environment variables or copy tools to `bin/Debug`

### For New Users
1. Install required tools (Boogie, Corral, Solc, Z3, Binaryen)
2. Set environment variables or copy tools to `bin/Debug`
3. Run `VeriSol --validate` to verify setup
4. Start using VeriSol Extended!

## Troubleshooting

### Common Issues

1. **Tools not found**
   - Run `VeriSol --validate` to see which tools are missing
   - Check the paths shown by `VeriSol --config`
   - Ensure tools are executable and in the correct location

2. **Permission denied**
   - Make sure tools have execute permissions: `chmod +x /path/to/tool`
   - Check if tools are in your PATH

3. **Wrong tool versions**
   - VeriSol Extended requires specific versions:
     - Boogie 3.5.1
     - Corral with .NET 6.0
     - Solidity compiler (solc)
     - Z3 theorem prover
     - Binaryen library

### Getting Help
- Run `VeriSol --help` for usage information
- Check the `INSTALL.md` file for detailed installation instructions
- Review the `tool-paths.config` file for configuration examples

## Example Usage

```bash
# Check current configuration
VeriSol --config

# Validate all tools
VeriSol --validate

# Use with custom paths via environment variables
export VERISOL_BOOGIE_PATH=/custom/path/to/boogie
export VERISOL_CORRAL_PATH=/custom/path/to/corral
VeriSol --validate

# Run Solidity verification
VeriSol contract.sol MyContract

# Run WASM verification
VeriSol --wasm contract.wat
```

This refactoring makes VeriSol Extended much more user-friendly and easier to deploy in different environments! 