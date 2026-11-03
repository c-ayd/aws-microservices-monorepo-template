FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/AdminService/src/**/*.csproj ./
COPY --parents src/Common/src/**/*.csproj ./
COPY --parents src/Shared/src/**/*.csproj ./

RUN dotnet restore "src/AdminService/src/AdminService.Api/AdminService.Api.csproj"

FROM restore AS publish

COPY src/AdminService/src src/AdminService/src
COPY src/Common/src src/Common/src
COPY src/Shared/src src/Shared/src

RUN dotnet publish "src/AdminService/src/AdminService.Api/AdminService.Api.csproj" -c Release -o /template/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "AdminService.Api.dll" ]
