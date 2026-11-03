FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/Initializers/src/Initializer.Databases/Initializer.Databases.csproj ./
COPY --parents src/AuthService/src/AuthService.Persistence/AuthService.Persistence.csproj ./
COPY --parents src/Common/src/**/*.csproj ./
COPY --parents src/Shared/src/**/*.csproj ./

RUN dotnet restore "src/Initializers/src/Initializer.Databases/Initializer.Databases.csproj"

FROM restore AS publish

COPY src/Initializers/src/Initializer.Databases src/Initializers/src/Initializer.Databases
COPY src/AuthService/src src/AuthService/src
COPY src/Common/src src/Common/src
COPY src/Shared/src src/Shared/src

RUN dotnet publish "src/Initializers/src/Initializer.Databases/Initializer.Databases.csproj" -c Release -o /template/app

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "Initializer.Databases.dll" ]
