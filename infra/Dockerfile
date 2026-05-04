# infra/Dockerfile
# syntax=docker/dockerfile:1.7

# ===== Stage 1: build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Instalar clang e zlib (necessários para AOT publish)
RUN apt-get update && apt-get install -y --no-install-recommends \
    clang zlib1g-dev \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /src

# Restore primeiro (cache de NuGet)
COPY global.json Rinha.slnx ./
COPY src/Rinha.Indexing/Rinha.Indexing.csproj src/Rinha.Indexing/
COPY src/Rinha.IndexBuilder/Rinha.IndexBuilder.csproj src/Rinha.IndexBuilder/
COPY src/Rinha.Api/Rinha.Api.csproj src/Rinha.Api/
COPY tests/Rinha.Tests/Rinha.Tests.csproj tests/Rinha.Tests/
RUN dotnet restore Rinha.slnx

# Copy fontes
COPY src/ src/

# Build IndexBuilder (JIT, beefy)
RUN dotnet publish src/Rinha.IndexBuilder/Rinha.IndexBuilder.csproj -c Release -o /tmp/builder

# Build Api (AOT)
RUN dotnet publish src/Rinha.Api/Rinha.Api.csproj -c Release -r linux-x64 -o /tmp/api

# ===== Stage 2: build index.bin =====
FROM build AS indexer
COPY resources/references.json.gz /tmp/refs/references.json.gz
RUN /tmp/builder/Rinha.IndexBuilder \
    --in /tmp/refs/references.json.gz \
    --out /tmp/data/index.bin \
 && ls -la /tmp/data/

# ===== Stage 3: runtime =====
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /tmp/api/Rinha.Api /app/Rinha.Api
COPY --from=indexer /tmp/data/index.bin /app/data/index.bin
COPY resources/mcc_risk.json /app/data/mcc_risk.json
ENV INDEX_PATH=/app/data/index.bin \
    MCC_RISK_PATH=/app/data/mcc_risk.json \
    NPROBE=8 \
    DOTNET_GCConserveMemory=9 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8080
ENTRYPOINT ["/app/Rinha.Api"]
