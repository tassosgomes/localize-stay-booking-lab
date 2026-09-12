# Build com contexto = raiz do repo (Central Package Management em
# Directory.Packages.props/Directory.Build.props/NuGet.Config na raiz).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY services/booking/src/1-Services/LocalizeStay.Booking.Api/LocalizeStay.Booking.Api.csproj services/booking/src/1-Services/LocalizeStay.Booking.Api/
COPY services/booking/src/2-Application/LocalizeStay.Booking.Application/LocalizeStay.Booking.Application.csproj services/booking/src/2-Application/LocalizeStay.Booking.Application/
COPY services/booking/src/3-Domain/LocalizeStay.Booking.Domain/LocalizeStay.Booking.Domain.csproj services/booking/src/3-Domain/LocalizeStay.Booking.Domain/
COPY services/booking/src/4-Infra/LocalizeStay.Booking.Infra/LocalizeStay.Booking.Infra.csproj services/booking/src/4-Infra/LocalizeStay.Booking.Infra/
RUN dotnet restore services/booking/src/1-Services/LocalizeStay.Booking.Api/LocalizeStay.Booking.Api.csproj

COPY services/booking/src services/booking/src
RUN dotnet publish services/booking/src/1-Services/LocalizeStay.Booking.Api/LocalizeStay.Booking.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# mcr.microsoft.com/dotnet/aspnet é Ubuntu minimalista (sem adduser/useradd),
# mas já traz um usuário non-root pronto: "app".
USER app
COPY --from=build /app/publish .

# appsettings.json fixa "urls" em localhost (dev); em container precisa
# escutar em todas as interfaces. Env var tem precedência sobre appsettings.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LocalizeStay.Booking.Api.dll"]
