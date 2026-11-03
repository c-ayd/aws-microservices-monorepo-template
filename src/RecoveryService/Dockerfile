FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/RecoveryService/src/**/*.csproj ./
COPY --parents src/Common/src/**/*.csproj ./

RUN dotnet restore "src/RecoveryService/src/RecoveryService.Worker/RecoveryService.Worker.csproj"

FROM restore AS publish

COPY src/RecoveryService/src src/RecoveryService/src
COPY src/Common/src src/Common/src

RUN dotnet publish "src/RecoveryService/src/RecoveryService.Worker/RecoveryService.Worker.csproj" -c Release -o /template/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "RecoveryService.Worker.dll" ]
