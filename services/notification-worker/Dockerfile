# Build com contexto = raiz do repo (Central Package Management em
# Directory.Packages.props/Directory.Build.props/NuGet.Config na raiz).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/LocalizeStay.Notification.Worker.csproj services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/
RUN dotnet restore services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/LocalizeStay.Notification.Worker.csproj

COPY services/notification-worker/src services/notification-worker/src
RUN dotnet publish services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/LocalizeStay.Notification.Worker.csproj \
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
ENTRYPOINT ["dotnet", "LocalizeStay.Notification.Worker.dll"]
