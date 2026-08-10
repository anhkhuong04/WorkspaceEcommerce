# syntax=docker/dockerfile:1

# Update the version tag and digest together through the reviewed base-image
# process. The SDK feature band must remain aligned with global.json.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.6-alpine3.23@sha256:1201dde897ab436b7c6b386f6dbd4f9a3ca0245f9c5a8aac8f8bcdccb4c7d484 AS runtime
WORKDIR /app
EXPOSE 8080
RUN apk add --no-cache krb5-libs \
    && addgroup -S -g 10001 workspace \
    && adduser -S -D -H -u 10001 -G workspace workspace \
    && mkdir -p /app/wwwroot/media /var/lib/workspace-ecommerce/keys /home/workspace \
    && chown -R workspace:workspace /app /var/lib/workspace-ecommerce /home/workspace \
    && chmod 0750 /app /app/wwwroot /app/wwwroot/media /var/lib/workspace-ecommerce \
    && chmod 0700 /var/lib/workspace-ecommerce/keys /home/workspace

FROM mcr.microsoft.com/dotnet/sdk:10.0.202-alpine3.23@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build
WORKDIR /src
RUN apk add --no-cache krb5-libs

COPY ["src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj", "src/WorkspaceEcommerce.Api/"]
COPY ["src/WorkspaceEcommerce.Application/WorkspaceEcommerce.Application.csproj", "src/WorkspaceEcommerce.Application/"]
COPY ["src/WorkspaceEcommerce.Domain/WorkspaceEcommerce.Domain.csproj", "src/WorkspaceEcommerce.Domain/"]
COPY ["src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj", "src/WorkspaceEcommerce.Infrastructure/"]
RUN dotnet restore "src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj"

COPY . .
RUN dotnet publish "src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migrate
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENTRYPOINT ["dotnet-ef", "database", "update", "--project", "src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj", "--startup-project", "src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj", "--context", "WorkspaceEcommerce.Infrastructure.Persistence.AppDbContext"]

FROM runtime AS final
WORKDIR /app
COPY --from=build --chown=workspace:workspace /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    HOME=/home/workspace
USER workspace
ENTRYPOINT ["dotnet", "WorkspaceEcommerce.Api.dll"]
