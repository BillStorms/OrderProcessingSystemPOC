FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Prepare project folder and copy project file
RUN mkdir -p /src/fulfillment-service/FulfillmentService.Worker
COPY ["fulfillment-service/FulfillmentService.Worker/FulfillmentService.Worker.csproj", "fulfillment-service/FulfillmentService.Worker/"]

# Copy central package files so dotnet restore can honor Directory.Packages.props
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]

# Restore dependencies for the specific project
RUN dotnet restore "fulfillment-service/FulfillmentService.Worker/FulfillmentService.Worker.csproj"

# Copy only the worker project source into its folder
COPY ["fulfillment-service/FulfillmentService.Worker/", "fulfillment-service/FulfillmentService.Worker/"]

# Build the application
RUN dotnet build "fulfillment-service/FulfillmentService.Worker/FulfillmentService.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "fulfillment-service/FulfillmentService.Worker/FulfillmentService.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FulfillmentService.Worker.dll"]
