# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Copy csproj files and restore
COPY ./Indexer/Indexer.csproj ./Indexer/Indexer.csproj
COPY ./Indexer.Tests/Indexer.Tests.csproj ./Indexer.Tests/Indexer.Tests.csproj
COPY ./Indexer.Performance/Indexer.Performance.csproj ./Indexer.Performance/Indexer.Performance.csproj
COPY ./Indexer.sln ./Indexer.sln
RUN dotnet restore Indexer.sln

run dotnet build Indexer.sln -c Release

# Copy everything else
COPY . .

FROM build AS test
WORKDIR /app/Indexer.Tests
RUN dotnet test -c Release  -v n
ENTRYPOINT ["dotnet", "test", "-c", "Release", "-v", "n"]

# Publish stage
FROM build AS publish
WORKDIR /app
RUN dotnet publish /app/Indexer/Indexer.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Indexer.dll"]
