FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ApiBaseUrl=http://localhost:8080/
WORKDIR /src

COPY BattleShip.slnx ./
COPY BattleShip.App/BattleShip.App.csproj BattleShip.App/
COPY BattleShip.Models/BattleShip.Models.csproj BattleShip.Models/
RUN dotnet restore BattleShip.App/BattleShip.App.csproj

COPY . .
RUN printf '%s\n' "{\"ApiBaseUrl\":\"${ApiBaseUrl}\"}" > BattleShip.App/wwwroot/appsettings.json
RUN dotnet publish BattleShip.App/BattleShip.App.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:alpine AS final
COPY BattleShip.App/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
