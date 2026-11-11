FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/DineDesk.Web/DineDesk.Web.csproj", "src/DineDesk.Web/"]
RUN dotnet restore "src/DineDesk.Web/DineDesk.Web.csproj"

COPY src/DineDesk.Web/ src/DineDesk.Web/
WORKDIR /src/src/DineDesk.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "DineDesk.Web.dll"]
