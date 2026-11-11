FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/DineDesk.Api/DineDesk.Api.csproj", "src/DineDesk.Api/"]
RUN dotnet restore "src/DineDesk.Api/DineDesk.Api.csproj"

COPY src/DineDesk.Api/ src/DineDesk.Api/
WORKDIR /src/src/DineDesk.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "DineDesk.Api.dll"]
