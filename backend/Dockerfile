# ------------------------------------------------------------------ build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# Restore against the project files alone so the (slow) package restore is cached
# and only re-runs when a dependency actually changes.
COPY AssignmentSystem.slnx ./
COPY src/AssignmentSystem.Domain/*.csproj src/AssignmentSystem.Domain/
COPY src/AssignmentSystem.Application/*.csproj src/AssignmentSystem.Application/
COPY src/AssignmentSystem.Infrastructure/*.csproj src/AssignmentSystem.Infrastructure/
COPY src/AssignmentSystem.Api/*.csproj src/AssignmentSystem.Api/
COPY tests/AssignmentSystem.Tests/*.csproj tests/AssignmentSystem.Tests/
RUN dotnet restore src/AssignmentSystem.Api/AssignmentSystem.Api.csproj

COPY . .
RUN dotnet publish src/AssignmentSystem.Api/AssignmentSystem.Api.csproj \
    -c Release -o /app --no-restore

# --------------------------------------------------------------- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# The health check in docker-compose.yml uses wget.
RUN apk add --no-cache wget \
    && addgroup -S app && adduser -S -G app app

COPY --from=build /app ./

# Serilog writes a rolling file alongside the console sink.
RUN mkdir -p /app/logs && chown -R app:app /app

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "AssignmentSystem.Api.dll"]
