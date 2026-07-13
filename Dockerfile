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
RUN dotnet publish src/LexFlow.Api/LexFlow.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# curl isn't in the base aspnet runtime image (it's deliberately minimal) —
# installed so `HEALTHCHECK`/compose healthchecks can hit /health from
# inside the container (see docker-compose.full.yml, which gates the web
# dev servers on this service being reported healthy).
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r lexflow && useradd -r -g lexflow lexflow
USER lexflow

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LexFlow.Api.dll"]
