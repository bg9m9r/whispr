# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Whispr.sln ./
COPY src/Whispr.Core/ ./Whispr.Core/
COPY src/Whispr.Server/ ./Whispr.Server/

RUN dotnet publish Whispr.Server/Whispr.Server.csproj \
    -c Release -r linux-x64 --self-contained -o /app/publish

# Runtime stage (runtime-deps = no .NET runtime; self-contained has it)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8443
EXPOSE 8444

# Mount cert.pfx, appsettings.json, and data dir at run time
# Default config paths; override with -v
ENV CertificatePath=cert.pfx
ENV DatabasePath=whispr.db

ENTRYPOINT ["./Whispr.Server"]
