FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY LexFlow.sln ./
COPY src/LexFlow.Domain/LexFlow.Domain.csproj src/LexFlow.Domain/
COPY src/LexFlow.Application/LexFlow.Application.csproj src/LexFlow.Application/
COPY src/LexFlow.Infrastructure/LexFlow.Infrastructure.csproj src/LexFlow.Infrastructure/
COPY src/LexFlow.Api/LexFlow.Api.csproj src/LexFlow.Api/
COPY src/LexFlow.Workers/LexFlow.Workers.csproj src/LexFlow.Workers/
COPY tests/LexFlow.UnitTests/LexFlow.UnitTests.csproj tests/LexFlow.UnitTests/
COPY tests/LexFlow.IntegrationTests/LexFlow.IntegrationTests.csproj tests/LexFlow.IntegrationTests/
COPY tests/LexFlow.E2ETests/LexFlow.E2ETests.csproj tests/LexFlow.E2ETests/
RUN dotnet restore LexFlow.sln

COPY . .
RUN dotnet publish src/LexFlow.Workers/LexFlow.Workers.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

RUN groupadd -r lexflow && useradd -r -g lexflow lexflow
USER lexflow

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LexFlow.Workers.dll"]
