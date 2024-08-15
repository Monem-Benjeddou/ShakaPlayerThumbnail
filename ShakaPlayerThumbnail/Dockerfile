FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5004
ENV ASPNETCORE_ENVIRONMENT=Development
ENV access_key=Qqt3KMXNlK4iCKqPhgEd
ENV secret_key=Kncx7QKlHyaN1rmbRRrAqDvDLGhGt8IAPdwhyjg6
ENV service_url=http://10.13.111.3
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ShakaPlayerThumbnail/ShakaPlayerThumbnail.csproj", "ShakaPlayerThumbnail/"]
RUN dotnet restore "ShakaPlayerThumbnail/ShakaPlayerThumbnail.csproj"
COPY . .
WORKDIR "/src/ShakaPlayerThumbnail"
RUN dotnet build "ShakaPlayerThumbnail.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "ShakaPlayerThumbnail.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShakaPlayerThumbnail.dll"]
