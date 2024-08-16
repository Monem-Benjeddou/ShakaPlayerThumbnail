FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5006

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ShakaPlayerThumbnail/ShakaPlayerThumbnail.csproj ShakaPlayerThumbnail/
WORKDIR /src/ShakaPlayerThumbnail
RUN dotnet restore

COPY ShakaPlayerThumbnail/. .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShakaPlayerThumbnail.dll"]
