# Base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER nobody  # Adjust if needed
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5004

# SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore
COPY ShakaPlayerThumbnail/ShakaPlayerThumbnail.csproj ShakaPlayerThumbnail/
WORKDIR /src/ShakaPlayerThumbnail
RUN dotnet restore

# Copy the rest of the source code and build
COPY ShakaPlayerThumbnail/. .
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShakaPlayerThumbnail.dll"]
