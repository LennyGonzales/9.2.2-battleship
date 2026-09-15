FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BattleShip.slnx ./
COPY BattleShip.API/BattleShip.API.csproj BattleShip.API/
COPY BattleShip.Models/BattleShip.Models.csproj BattleShip.Models/
RUN dotnet restore BattleShip.API/BattleShip.API.csproj

COPY . .
RUN dotnet publish BattleShip.API/BattleShip.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BattleShip.API.dll"]
