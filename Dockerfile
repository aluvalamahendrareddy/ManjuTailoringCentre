# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ManjuTailoringCentre/ManjuTailoringCentre.csproj", "ManjuTailoringCentre/"]
RUN dotnet restore "ManjuTailoringCentre/ManjuTailoringCentre.csproj"

COPY . .
WORKDIR "/src/ManjuTailoringCentre"
RUN dotnet publish "ManjuTailoringCentre.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose HTTP port for cloud hosting
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ManjuTailoringCentre.dll"]
