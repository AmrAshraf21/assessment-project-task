FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/TaskManagement.API/TaskManagement.API.csproj", "src/TaskManagement.API/"]
COPY ["src/TaskManagement.Application/TaskManagement.Application.csproj", "src/TaskManagement.Application/"]
COPY ["src/TaskManagement.Domain/TaskManagement.Domain.csproj", "src/TaskManagement.Domain/"]
COPY ["src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj", "src/TaskManagement.Infrastructure/"]

RUN dotnet restore "src/TaskManagement.API/TaskManagement.API.csproj"

COPY . .

RUN dotnet build "src/TaskManagement.API/TaskManagement.API.csproj" -c Release -o /app/build

# ── Publish Stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "src/TaskManagement.API/TaskManagement.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TaskManagement.API.dll"]
