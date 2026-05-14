FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

WORKDIR /src/LoginDB
RUN dotnet publish "DatabazyApiStarter.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Ensure uploads directory exists and is writable
RUN mkdir -p wwwroot/uploads && chmod 777 wwwroot/uploads

ENTRYPOINT ["dotnet", "DatabazyApiStarter.dll"]
