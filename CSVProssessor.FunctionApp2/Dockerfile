# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CSVProssessor.FunctionApp2/CSVProssessor.FunctionApp2.csproj", "CSVProssessor.FunctionApp2/"]
COPY ["CSVProssessor.Application/CSVProssessor.Application.csproj", "CSVProssessor.Application/"]
COPY ["CSVProssessor.Domain/CSVProssessor.Domain.csproj", "CSVProssessor.Domain/"]
COPY ["CSVProssessor.Infrastructure/CSVProssessor.Infrastructure.csproj", "CSVProssessor.Infrastructure/"]
RUN dotnet restore "./CSVProssessor.FunctionApp2/CSVProssessor.FunctionApp2.csproj"
COPY . .
WORKDIR "/src/CSVProssessor.FunctionApp2"
RUN dotnet build "./CSVProssessor.FunctionApp2.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CSVProssessor.FunctionApp2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true