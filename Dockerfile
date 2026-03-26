FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "./LanBot.slnx"
RUN dotnet publish "./src/LanBot/LanBot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "LanBot.dll"]

