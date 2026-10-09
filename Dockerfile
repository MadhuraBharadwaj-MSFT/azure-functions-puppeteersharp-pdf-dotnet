FROM mcr.microsoft.com/dotnet/sdk:8.0 AS installer-env

WORKDIR /src
COPY AzureFunctions.PuppeteerSharpPdf.csproj ./
ARG NUGET_SOURCE=https://api.nuget.org/v3/index.json
RUN dotnet nuget update source nuget.org --source "${NUGET_SOURCE}" \
    && dotnet restore AzureFunctions.PuppeteerSharpPdf.csproj

COPY . ./
RUN dotnet publish AzureFunctions.PuppeteerSharpPdf.csproj \
    --configuration Release \
    --no-restore \
    --output /home/site/wwwroot

# This is the current .NET 8 isolated Linux base image emitted by Functions Core Tools 4.
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    CHROMIUM_PATH=/usr/bin/chromium

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        chromium \
        fonts-liberation \
        fonts-noto-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]