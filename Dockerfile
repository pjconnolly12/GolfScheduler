# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GolfScheduler.csproj ./
RUN dotnet restore GolfScheduler.csproj

COPY . .
RUN dotnet publish GolfScheduler.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# Writable location for Gmail API token cache inside the container.
RUN mkdir -p /var/opt/golfscheduler/google-token

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "GolfScheduler.dll"]
