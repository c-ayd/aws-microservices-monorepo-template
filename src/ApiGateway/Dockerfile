FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/ApiGateway/src/**/*.csproj ./
COPY --parents src/Common/src/**/*.csproj ./

RUN dotnet restore "src/ApiGateway/src/ApiGateway.Web/ApiGateway.Web.csproj"

FROM restore AS publish

COPY src/ApiGateway/src src/ApiGateway/src
COPY src/Common/src src/Common/src

RUN dotnet publish "src/ApiGateway/src/ApiGateway.Web/ApiGateway.Web.csproj" -c Release -o /template/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app

COPY --from=publish /template/app .

ENTRYPOINT [ "dotnet", "ApiGateway.Web.dll" ]
