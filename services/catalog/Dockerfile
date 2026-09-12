# Build com contexto = raiz do repo (Central Package Management em
# Directory.Packages.props/Directory.Build.props/NuGet.Config na raiz).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj services/catalog/src/1-Services/LocalizeStay.Catalog.Api/
COPY services/catalog/src/2-Application/LocalizeStay.Catalog.Application/LocalizeStay.Catalog.Application.csproj services/catalog/src/2-Application/LocalizeStay.Catalog.Application/
COPY services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/LocalizeStay.Catalog.Domain.csproj services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/
COPY services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/LocalizeStay.Catalog.Infra.csproj services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/
RUN dotnet restore services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj

COPY services/catalog/src services/catalog/src
RUN dotnet publish services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
USER appuser
COPY --from=build /app/publish .

# appsettings.json fixa "urls" em localhost (dev); em container precisa
# escutar em todas as interfaces. Env var tem precedência sobre appsettings.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LocalizeStay.Catalog.Api.dll"]
