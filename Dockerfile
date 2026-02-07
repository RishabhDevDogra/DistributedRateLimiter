# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["DistributedRateLimiter/DistributedRateLimiter.csproj", "DistributedRateLimiter/"]
RUN dotnet restore "DistributedRateLimiter/DistributedRateLimiter.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "DistributedRateLimiter/DistributedRateLimiter.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DistributedRateLimiter/DistributedRateLimiter.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DistributedRateLimiter.dll"]
