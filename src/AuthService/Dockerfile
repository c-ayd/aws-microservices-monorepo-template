FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/AuthService/src/**/*.csproj ./
COPY --parents src/Common/src/**/*.csproj ./
COPY --parents src/Shared/src/**/*.csproj ./

RUN dotnet restore "src/AuthService/src/AuthService.Api/AuthService.Api.csproj"

FROM restore AS publish

COPY src/AuthService/src src/AuthService/src
COPY src/Common/src src/Common/src
COPY src/Shared/src src/Shared/src

RUN dotnet publish "src/AuthService/src/AuthService.Api/AuthService.Api.csproj" -c Relase -o /template/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "AuthService.Api.dll" ]
