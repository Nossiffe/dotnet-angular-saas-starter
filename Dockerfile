# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy repo-level config files needed for restore and build
COPY Directory.Build.props Directory.Packages.props NuGet.config global.json .editorconfig ./

# Copy all source projects (lock files are inside each project dir)
COPY src/ src/

# Restore against the lock files (src/**/packages.lock.json): a stale one fails the build (NU1004)
RUN dotnet restore "src/Api/Saas.Api.csproj" --locked-mode

# Publish
RUN dotnet publish "src/Api/Saas.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:OpenApiGenerateDocuments=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Saas.Api.dll"]
