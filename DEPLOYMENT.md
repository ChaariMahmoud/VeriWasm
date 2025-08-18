# VeriSol Extended Deployment Guide

This guide provides comprehensive instructions for deploying VeriSol Extended in various environments, from local development to production systems.

## Table of Contents

1. [Local Development](#local-development)
2. [Docker Deployment](#docker-deployment)
3. [Production Deployment](#production-deployment)
4. [CI/CD Integration](#cicd-integration)
5. [Performance Optimization](#performance-optimization)
6. [Security Considerations](#security-considerations)
7. [Monitoring and Logging](#monitoring-and-logging)

## Local Development

### Prerequisites
- .NET 9.0 SDK
- Git
- IDE (Visual Studio, VS Code, or Rider)

### Setup
```bash
# Clone the repository
git clone https://github.com/ChaariMahmoud/Verisol-Extended.git
cd Verisol-Extended

# Build the solution
dotnet build Sources/VeriSol.sln



### Development Workflow
```bash
# Run with Solidity contract
VeriSol Test/regressions/ERC20-simplified.sol ERC20

# Run with WASM contract
VeriSol --wasm WasmInputs/simple.wat

```

## Docker Deployment

### Quick Start
```bash
# Build the image
docker build -t verisol-extended .

# Run with Solidity contract
docker run -v $(pwd):/workspace verisol-extended VeriSol /workspace/contract.sol ContractName

# Run with WASM contract
docker run -v $(pwd):/workspace verisol-extended VeriSol --wasm /workspace/contract.wat
```

### Using Docker Compose
```bash
# Build and run
docker-compose up --build

# Run specific command
docker-compose run verisol-extended VeriSol --wasm /workspace/contract.wat

# Development mode with hot reload
docker-compose --profile dev up verisol-dev
```

### Docker Configuration Options

#### Resource Limits
```yaml
# docker-compose.yml
services:
  verisol-extended:
    deploy:
      resources:
        limits:
          memory: 4G
          cpus: '2.0'
        reservations:
          memory: 2G
          cpus: '1.0'
```

#### Volume Mounts
```yaml
volumes:
  - ./contracts:/workspace/contracts:ro  # Read-only contract files
  - ./outputs:/app/BoogieOutputs         # Output directory
  - verisol-cache:/app/.store            # Tool cache
```

#### Environment Variables
```yaml
environment:
  - DOTNET_RUNNING_IN_CONTAINER=true
  - DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
  - VERISOL_LOG_LEVEL=Info
  - VERISOL_TIMEOUT=300
```

## Production Deployment

### Kubernetes Deployment

#### Deployment YAML
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: verisol-extended
  labels:
    app: verisol-extended
spec:
  replicas: 3
  selector:
    matchLabels:
      app: verisol-extended
  template:
    metadata:
      labels:
        app: verisol-extended
    spec:
      containers:
      - name: verisol-extended
        image: verisol-extended:latest
        ports:
        - containerPort: 8080
        resources:
          requests:
            memory: "2Gi"
            cpu: "1"
          limits:
            memory: "4Gi"
            cpu: "2"
        volumeMounts:
        - name: contracts
          mountPath: /workspace/contracts
        - name: outputs
          mountPath: /app/BoogieOutputs
        env:
        - name: DOTNET_RUNNING_IN_CONTAINER
          value: "true"
        - name: VERISOL_LOG_LEVEL
          value: "Info"
      volumes:
      - name: contracts
        persistentVolumeClaim:
          claimName: contracts-pvc
      - name: outputs
        persistentVolumeClaim:
          claimName: outputs-pvc
```

#### Service YAML
```yaml
apiVersion: v1
kind: Service
metadata:
  name: verisol-extended-service
spec:
  selector:
    app: verisol-extended
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

### Docker Swarm Deployment
```bash
# Initialize swarm
docker swarm init

# Deploy stack
docker stack deploy -c docker-compose.yml verisol-stack

# Scale service
docker service scale verisol-stack_verisol-extended=5
```

## CI/CD Integration

### GitHub Actions
```yaml
name: VeriSol Extended CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET 9.0
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Build
      run: dotnet build Sources/VeriSol.sln
    
    - name: Test
      run: dotnet test Sources/VeriSol.sln
    
    - name: Run regression tests
      run: |
        dotnet tool install --global SolToBoogieTest --version 0.1.1-alpha --add-source $(pwd)/nupkg/
        VeriSolRegressionRunner Test/

  build-docker:
    needs: test
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Build Docker image
      run: docker build -t verisol-extended .
    
    - name: Push to registry
      run: |
        echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
        docker tag verisol-extended:latest ${{ secrets.DOCKER_REGISTRY }}/verisol-extended:latest
        docker push ${{ secrets.DOCKER_REGISTRY }}/verisol-extended:latest
```

### Azure DevOps
```yaml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

variables:
  solution: 'Sources/VeriSol.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

steps:
- task: DotNetCoreCLI@2
  displayName: 'Restore NuGet packages'
  inputs:
    command: 'restore'
    projects: '$(solution)'

- task: DotNetCoreCLI@2
  displayName: 'Build solution'
  inputs:
    command: 'build'
    projects: '$(solution)'
    arguments: '--configuration $(buildConfiguration)'

- task: DotNetCoreCLI@2
  displayName: 'Run tests'
  inputs:
    command: 'test'
    projects: '$(solution)'
    arguments: '--configuration $(buildConfiguration)'

- task: Docker@2
  displayName: 'Build Docker image'
  inputs:
    containerRegistry: 'Azure Container Registry'
    repository: 'verisol-extended'
    command: 'buildAndPush'
    Dockerfile: '**/Dockerfile'
    tags: |
      latest
      $(Build.BuildId)
```

## Performance Optimization

### Resource Tuning
```bash
# Increase memory limit for large contracts
docker run --memory=8g verisol-extended VeriSol contract.sol ContractName

# Use multiple CPU cores
docker run --cpus=4 verisol-extended VeriSol contract.sol ContractName

# Enable swap for memory-intensive operations
docker run --memory=4g --memory-swap=8g verisol-extended VeriSol contract.sol ContractName
```

### Caching Strategies
```bash
# Mount tool cache directory
docker run -v ~/.store:/app/.store verisol-extended VeriSol contract.sol ContractName

# Use Docker layer caching
docker build --cache-from verisol-extended:latest -t verisol-extended .
```

### Parallel Processing
```bash
# Run multiple verifications in parallel
parallel -j 4 'docker run verisol-extended VeriSol {}' ::: contract1.sol contract2.sol contract3.sol contract4.sol
```

## Security Considerations

### Container Security
```bash
# Run as non-root user
docker run --user 1000:1000 verisol-extended VeriSol contract.sol ContractName

# Read-only filesystem
docker run --read-only --tmpfs /tmp verisol-extended VeriSol contract.sol ContractName

# No privileged mode
docker run --security-opt=no-new-privileges verisol-extended VeriSol contract.sol ContractName
```

### Network Security
```bash
# Disable network access
docker run --network=none verisol-extended VeriSol contract.sol ContractName

# Use custom network
docker network create verisol-network
docker run --network=verisol-network verisol-extended VeriSol contract.sol ContractName
```

### Input Validation
```bash
# Validate contract files before processing
docker run -v $(pwd):/workspace verisol-extended sh -c '
  if [ -f "/workspace/contract.sol" ]; then
    VeriSol /workspace/contract.sol ContractName
  else
    echo "Contract file not found"
    exit 1
  fi
'
```

## Monitoring and Logging

### Logging Configuration
```bash
# Enable structured logging
docker run -e VERISOL_LOG_LEVEL=Debug verisol-extended VeriSol contract.sol ContractName

# Output logs to file
docker run -v $(pwd)/logs:/app/logs verisol-extended VeriSol contract.sol ContractName > logs/verification.log 2>&1
```

### Health Checks
```yaml
# docker-compose.yml
services:
  verisol-extended:
    healthcheck:
      test: ["CMD", "dotnet", "bin/Release/VeriSol.dll", "--help"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

### Metrics Collection
```bash
# Export metrics to Prometheus
docker run -p 9090:9090 -e VERISOL_METRICS_ENABLED=true verisol-extended VeriSol contract.sol ContractName

# Use custom metrics endpoint
curl http://localhost:9090/metrics
```

### Alerting
```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'verisol-extended'
    static_configs:
      - targets: ['localhost:9090']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

## Troubleshooting

### Common Issues

1. **Out of Memory Errors**
   ```bash
   # Increase memory limit
   docker run --memory=8g verisol-extended VeriSol contract.sol ContractName
   ```

2. **Tool Download Failures**
   ```bash
   # Check network connectivity
   docker run --network=host verisol-extended VeriSol contract.sol ContractName
   ```

3. **Permission Issues**
   ```bash
   # Fix file permissions
   chmod -R 755 contracts/
   docker run -v $(pwd):/workspace verisol-extended VeriSol /workspace/contract.sol ContractName
   ```

### Debug Mode
```bash
# Enable debug logging
docker run -e VERISOL_LOG_LEVEL=Debug verisol-extended VeriSol contract.sol ContractName

# Run with verbose output
docker run verisol-extended VeriSol contract.sol ContractName /verbose
```

### Support
For additional support and troubleshooting:
- Check the [GitHub Issues](https://github.com/ChaariMahmoud/verisol-extended/issues)