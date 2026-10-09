FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY LkFinances.Core/LkFinances.Core.csproj LkFinances.Core/
COPY LkFinances.Api/LkFinances.Api.csproj LkFinances.Api/
RUN dotnet restore LkFinances.Api/LkFinances.Api.csproj
COPY LkFinances.Core/ LkFinances.Core/
COPY LkFinances.Api/ LkFinances.Api/
RUN dotnet publish LkFinances.Api/LkFinances.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir /keys && chown $APP_UID /keys
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LkFinances.Api.dll"]
