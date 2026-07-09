FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY LagoaSportRpa.csproj ./
RUN dotnet restore LagoaSportRpa.csproj

COPY . .
RUN dotnet publish LagoaSportRpa.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

EXPOSE 8080

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LagoaSportRpa.dll"]
