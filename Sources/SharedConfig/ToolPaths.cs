using System;
using System.IO;

namespace SharedConfig
{
    /// <summary>
    /// Centralized configuration for all external tool paths used by VeriSol Extended
    /// Users can modify these paths to match their local installation
    /// </summary>
    public static class ToolPaths
    {
        // Base directory for tools - modify this to match your installation
        private static readonly string BaseToolsDirectory = GetBaseToolsDirectory();

        /// <summary>
        /// Gets the base tools directory based on the current environment
        /// </summary>
        private static string GetBaseToolsDirectory()
        {
            // Try to detect the base directory automatically
            var currentDir = Directory.GetCurrentDirectory();
            
            // Check if we're in the project root
            if (File.Exists(Path.Combine(currentDir, "Sources", "VeriSol.sln")))
            {
                return Path.Combine(currentDir, "bin", "Debug");
            }
            
            // Check if we're in the bin/Debug directory
            if (Directory.Exists(Path.Combine(currentDir, "..", "..", "Sources")))
            {
                return currentDir;
            }
            
            // Default fallback - users should modify this
            return Path.Combine(currentDir, "tools");
        }

        /// <summary>
        /// Path to the Boogie verifier executable
        /// </summary>
        public static string BoogiePath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("VERISOL_BOOGIE_PATH");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
                
                // Default paths to try
                var defaultPaths = new[]
                {
                    Path.Combine(BaseToolsDirectory, "boogie"),
                    Path.Combine(BaseToolsDirectory, "boogie.exe"),
                    "/usr/local/bin/boogie",
                    "C:\\Program Files\\Boogie\\boogie.exe"
                };

                foreach (var defaultPath in defaultPaths)
                {
                    if (File.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }

                // Fallback to the original hardcoded path
                return Path.Combine(BaseToolsDirectory, "boogie");
            }
        }

        /// <summary>
        /// Path to the Corral bounded model checker executable
        /// </summary>
        public static string CorralPath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("VERISOL_CORRAL_PATH");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
                
                // Default paths to try
                var defaultPaths = new[]
                {
                    Path.Combine(BaseToolsDirectory, "corral"),
                    Path.Combine(BaseToolsDirectory, "corral.exe"),
                    "/usr/local/bin/corral",
                    "C:\\Program Files\\Corral\\corral.exe"
                };

                foreach (var defaultPath in defaultPaths)
                {
                    if (File.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }

                // Fallback to the original hardcoded path
                return Path.Combine(BaseToolsDirectory, "corral");
            }
        }

        /// <summary>
        /// Path to the Solidity compiler executable
        /// </summary>
        public static string SolcPath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("VERISOL_SOLC_PATH");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
                
                // Default paths to try
                var defaultPaths = new[]
                {
                    Path.Combine(BaseToolsDirectory, "solc"),
                    Path.Combine(BaseToolsDirectory, "solc.exe"),
                    "/usr/local/bin/solc",
                    "/usr/bin/solc",
                    "C:\\Program Files\\Solidity\\solc.exe"
                };

                foreach (var defaultPath in defaultPaths)
                {
                    if (File.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }

                // Fallback to the original hardcoded path
                return Path.Combine(BaseToolsDirectory, "solc");
            }
        }

        /// <summary>
        /// Path to the Z3 theorem prover executable
        /// </summary>
        public static string Z3Path
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("VERISOL_Z3_PATH");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
                
                // Default paths to try
                var defaultPaths = new[]
                {
                    Path.Combine(BaseToolsDirectory, "z3"),
                    Path.Combine(BaseToolsDirectory, "z3.exe"),
                    "/usr/local/bin/z3",
                    "/usr/bin/z3",
                    "C:\\Program Files\\Z3\\z3.exe"
                };

                foreach (var defaultPath in defaultPaths)
                {
                    if (File.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }

                // Fallback to the original hardcoded path
                return Path.Combine(BaseToolsDirectory, "z3");
            }
        }

        /// <summary>
        /// Path to the Binaryen library (for WASM support)
        /// </summary>
        public static string BinaryenLibraryPath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("VERISOL_BINARYEN_PATH");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
                
                // Default paths to try
                var defaultPaths = new[]
                {
                    Path.Combine(BaseToolsDirectory, "libbinaryenwrapper.so"),
                    Path.Combine(BaseToolsDirectory, "binaryenwrapper.dll"),
                    "/usr/local/lib/libbinaryenwrapper.so",
                    "/usr/lib/libbinaryenwrapper.so"
                };

                foreach (var defaultPath in defaultPaths)
                {
                    if (File.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }

                // Fallback to the original hardcoded path
                return Path.Combine(BaseToolsDirectory, "libbinaryenwrapper.so");
            }
        }

        /// <summary>
        /// Validates that all required tools are available
        /// </summary>
        /// <returns>True if all tools are found, false otherwise</returns>
        public static bool ValidateTools()
        {
            var tools = new[]
            {
                ("Boogie", BoogiePath),
                ("Corral", CorralPath),
                ("Solc", SolcPath),
                ("Z3", Z3Path),
                ("Binaryen", BinaryenLibraryPath)
            };

            bool allValid = true;
            foreach (var (name, path) in tools)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"❌ {name} not found at: {path}");
                    allValid = false;
                }
                else
                {
                    Console.WriteLine($"✅ {name} found at: {path}");
                }
            }

            return allValid;
        }

        /// <summary>
        /// Prints the current tool configuration
        /// </summary>
        public static void PrintConfiguration()
        {
            Console.WriteLine("🔧 VeriSol Extended Tool Configuration:");
            Console.WriteLine($"   Base Tools Directory: {BaseToolsDirectory}");
            Console.WriteLine($"   Boogie Path: {BoogiePath}");
            Console.WriteLine($"   Corral Path: {CorralPath}");
            Console.WriteLine($"   Solc Path: {SolcPath}");
            Console.WriteLine($"   Z3 Path: {Z3Path}");
            Console.WriteLine($"   Binaryen Path: {BinaryenLibraryPath}");
            Console.WriteLine();
        }

        /// <summary>
        /// Gets instructions for configuring tool paths
        /// </summary>
        public static string GetConfigurationInstructions()
        {
            return @"
📋 Tool Path Configuration Instructions:

1. Environment Variables (Recommended):
   Set these environment variables to override default paths:
   - VERISOL_BOOGIE_PATH: Path to boogie executable
   - VERISOL_CORRAL_PATH: Path to corral executable  
   - VERISOL_SOLC_PATH: Path to solc executable
   - VERISOL_Z3_PATH: Path to z3 executable
   - VERISOL_BINARYEN_PATH: Path to binaryen library

2. Modify Source Code:
   Edit the BaseToolsDirectory in ToolPaths.cs to point to your tools directory.

3. Place Tools in Default Location:
   Copy all tools to the bin/Debug directory of your VeriSol installation.

4. System-wide Installation:
   Install tools in standard system locations (/usr/local/bin, /usr/bin, etc.)

For more information, see the INSTALL.md file.
";
        }
    }
} 