# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080
RUN apk add --no-cache krb5-libs

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
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
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj", "--startup-project", "src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj", "--context", "WorkspaceEcommerce.Infrastructure.Persistence.AppDbContext"]

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WorkspaceEcommerce.Api.dll"]
