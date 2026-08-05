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

COPY --from=build /app ./

# The .NET images already provide a non-root user and expose its id as APP_UID, so
# there is nothing to create here — running addgroup/adduser again fails because
# the name is already taken. Alpine's busybox also already provides the wget the
# compose health check uses, so no package install is needed either.
USER ${APP_UID:-1654}

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Platforms such as Render assign a port at runtime through PORT and route traffic
# only to that port. Preferring it — while falling back to 8080 for local
# docker compose — keeps one image working in both places. `exec` keeps dotnet as
# PID 1 so it still receives the container's stop signal.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet AssignmentSystem.Api.dll"]
