# ==============================================================================
# STAGE 1: Build & Compile Environment
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project definition files for NuGet layer caching
COPY ["VitalQ.API/VitalQ.API.csproj", "VitalQ.API/"]
COPY ["VitalQ.BusinessLogic/VitalQ.BusinessLogic.csproj", "VitalQ.BusinessLogic/"]
COPY ["VitalQ.DataAccess/VitalQ.DataAccess.csproj", "VitalQ.DataAccess/"]

# Restore dependencies
RUN dotnet restore "VitalQ.API/VitalQ.API.csproj"

# Copy C# source code
COPY VitalQ.API/ VitalQ.API/
COPY VitalQ.BusinessLogic/ VitalQ.BusinessLogic/
COPY VitalQ.DataAccess/ VitalQ.DataAccess/

# Publish Release build
WORKDIR "/src/VitalQ.API"
RUN dotnet publish "VitalQ.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# STAGE 2: Minimal Production Runtime with ICU Globalization (for SQL Server)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra AS final
WORKDIR /app

# Copy compiled binaries from build stage
COPY --from=build /app/publish .

# Expose standard ASP.NET Core port
EXPOSE 8080

ENTRYPOINT ["dotnet", "VitalQ.API.dll"]
