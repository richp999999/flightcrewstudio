# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore (cached when csproj is unchanged)
COPY FlightCrewStudio.csproj ./
RUN dotnet restore FlightCrewStudio.csproj

# Build + publish
COPY . ./
RUN dotnet publish FlightCrewStudio.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# The app listens on 8080 inside the container.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Directory for the server-held credential encryption key (mount a volume here).
RUN mkdir -p /keys
VOLUME ["/keys"]

ENTRYPOINT ["dotnet", "FlightCrewStudio.dll"]
