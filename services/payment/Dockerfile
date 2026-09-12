# Build com contexto = raiz do repo (Central Package Management em
# Directory.Packages.props/Directory.Build.props/NuGet.Config na raiz).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY services/payment/src/1-Services/LocalizeStay.Payment.Api/LocalizeStay.Payment.Api.csproj services/payment/src/1-Services/LocalizeStay.Payment.Api/
COPY services/payment/src/2-Application/LocalizeStay.Payment.Application/LocalizeStay.Payment.Application.csproj services/payment/src/2-Application/LocalizeStay.Payment.Application/
COPY services/payment/src/3-Domain/LocalizeStay.Payment.Domain/LocalizeStay.Payment.Domain.csproj services/payment/src/3-Domain/LocalizeStay.Payment.Domain/
COPY services/payment/src/4-Infra/LocalizeStay.Payment.Infra/LocalizeStay.Payment.Infra.csproj services/payment/src/4-Infra/LocalizeStay.Payment.Infra/
RUN dotnet restore services/payment/src/1-Services/LocalizeStay.Payment.Api/LocalizeStay.Payment.Api.csproj

COPY services/payment/src services/payment/src
RUN dotnet publish services/payment/src/1-Services/LocalizeStay.Payment.Api/LocalizeStay.Payment.Api.csproj \
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
ENTRYPOINT ["dotnet", "LocalizeStay.Payment.Api.dll"]
