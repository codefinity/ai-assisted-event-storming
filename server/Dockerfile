# syntax=docker/dockerfile:1
# Build context: the repository root. The host embeds the sample board (for seeding) and the
# Markdown guides it serves at /docs/*.md.
#   docker build -f server/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/global.json server/Directory.Build.props server/Directory.Packages.props server/EventStorming.slnx server/
COPY server/src/ server/src/
COPY docs/examples/ docs/examples/
COPY docs/api.md docs/llm-guide.md docs/
RUN dotnet publish server/src/driving-adapters/EventStorming.Host/EventStorming.Host.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# wget is only for the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir /keys && chown app:app /keys
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_NOLOGO=1
USER app
EXPOSE 8080
# `docker compose run --rm api seed` passes "seed" through to the host.
ENTRYPOINT ["dotnet", "EventStorming.Host.dll"]
