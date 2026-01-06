FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SmartHotel.Api/SmartHotel.Api.csproj", "SmartHotel.Api/"]
COPY ["SmartHotel.Domain/SmartHotel.Domain.csproj", "SmartHotel.Domain/"]
COPY ["SmartHotel.Infrastructure/SmartHotel.Infrastructure.csproj", "SmartHotel.Infrastructure/"]
RUN dotnet restore "SmartHotel.Api/SmartHotel.Api.csproj"
COPY . .
WORKDIR "/src/SmartHotel.Api"
RUN dotnet build "SmartHotel.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartHotel.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartHotel.Api.dll"]
