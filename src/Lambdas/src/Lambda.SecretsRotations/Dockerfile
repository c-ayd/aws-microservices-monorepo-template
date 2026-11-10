FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

WORKDIR /template

COPY --parents src/Lambdas/src/Lambda.SecretsRotations/Lambda.SecretsRotations.csproj ./
COPY --parents src/Common/src/**/*.csproj ./
COPY --parents src/Shared/src/**/*.csproj ./

RUN dotnet restore "src/Lambdas/src/Lambda.SecretsRotations/Lambda.SecretsRotations.csproj" -r linux-x64

FROM restore AS publish

COPY src/Lambdas/src/Lambda.SecretsRotations src/Lambdas/src/Lambda.SecretsRotations
COPY src/Common/src src/Common/src
COPY src/Shared/src src/Shared/src

RUN dotnet publish "src/Lambdas/src/Lambda.SecretsRotations/Lambda.SecretsRotations.csproj" -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o /template/app

FROM public.ecr.aws/lambda/dotnet:10 AS final

COPY --from=publish /template/app ${LAMBDA_TASK_ROOT}

CMD [ "Lambda.SecretsRotations::Lambda.SecretsRotations.Function::FunctionHandler" ]
