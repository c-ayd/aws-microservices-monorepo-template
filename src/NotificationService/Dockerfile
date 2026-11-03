FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/NotificationService/src/**/*.csproj ./
COPY --parents src/Common/src/**/*.csproj ./
COPY --parents src/Shared/src/**/*.csproj ./

RUN dotnet restore "src/NotificationService/src/NotificationService.Worker/NotificationService.Worker.csproj"

FROM restore AS publish

COPY src/NotificationService/src src/NotificationService/src
COPY src/Common/src src/Common/src
COPY src/Shared/src src/Shared/src

RUN dotnet publish "src/NotificationService/src/NotificationService.Worker/NotificationService.Worker.csproj" -c Release -o /template/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "NotificationService.Worker.dll" ]
