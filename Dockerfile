## VeriSol Extended Docker Image
## Multi-stage build for optimized image size

# -----------------------------
# Stage 1: Build stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Install system dependencies needed during build
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    unzip \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Copy source code
COPY Sources/ ./Sources/
COPY README.md ./README.md
COPY INSTALL.md ./INSTALL.md
# Also copy to container root to satisfy absolute paths used by project files
COPY README.md /README.md
COPY INSTALL.md /INSTALL.md

# Publish VeriSol (framework-dependent)
RUN dotnet publish Sources/VeriSol/VeriSol.csproj -c Release -o /app/publish

# -----------------------------
# Stage 2: Runtime stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime

WORKDIR /app

# Install runtime dependencies (z3 solver is needed by Boogie)
RUN apt-get update && apt-get install -y \
    z3 \
    wget \
    curl \
    unzip \
    binaryen \
    wabt \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r verisol && useradd -r -g verisol verisol

# Copy published VeriSol application
COPY --from=build /app/publish/ ./

# Copy documentation
COPY --from=build /src/README.md ./
COPY --from=build /src/INSTALL.md ./

# Tools directory (for Boogie, Corral, Binaryen)
RUN mkdir -p /app/tools /app/BoogieOutputs /app/WasmInputs && \
    chown -R verisol:verisol /app

# Copy Binaryen wrapper and local tools if available in context
# These are optional; if present they will be used via env vars below
COPY libbinaryenwrapper.so /app/tools/libbinaryenwrapper.so
COPY bin/Debug/boogie /app/tools/boogie
COPY bin/Debug/corral /app/tools/corral

# Create temp directory with proper permissions
RUN mkdir -p /appTemp && chown -R verisol:verisol /appTemp

# Switch to non-root user
USER verisol

# Centralized tool configuration via environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    VERISOL_BOOGIE_PATH=/app/tools/boogie \
    VERISOL_CORRAL_PATH=/app/tools/corral \
    VERISOL_Z3_PATH=/usr/bin/z3 \
    VERISOL_BINARYEN_PATH=/app/tools/libbinaryenwrapper.so \
    LD_LIBRARY_PATH=/app/tools:$LD_LIBRARY_PATH

# Entrypoint wrapper
RUN echo '#!/bin/bash\n\
set -e\n\
if [ "$1" = "--wasm" ]; then\n\
  exec dotnet /app/VeriSol.dll --wasm "$2"\n\
elif [ "$1" = "--config" ] || [ "$1" = "--validate" ] || [ "$1" = "--help" ]; then\n\
  exec dotnet /app/VeriSol.dll "$@"\n\
elif [ "$1" = "" ]; then\n\
  exec dotnet /app/VeriSol.dll --help\n\
else\n\
  # Check if first argument looks like a VeriSol command (starts with -- or ends with .sol)\n\
  if [[ "$1" == --* ]] || [[ "$1" == *.sol ]]; then\n\
    exec dotnet /app/VeriSol.dll "$@"\n\
  else\n\
    # For other commands, execute them directly\n\
    exec "$@"\n\
  fi\n\
fi' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]

# Default command
CMD ["--help"]

# Work volume for users
VOLUME ["/workspace"]

# Labels
LABEL maintainer="VeriSol Extended Team"
LABEL description="VeriSol Extended - Formal verification tool for Solidity and WebAssembly smart contracts"
LABEL version="0.1.1-alpha"
LABEL org.opencontainers.image.source="https://github.com/your-username/verisol-extended"