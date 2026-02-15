FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY AscierWeb.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
# ffmpeg do ekstrakcji klatek wideo + icu do globalizacji
RUN apk add --no-cache ffmpeg icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app .
# katalog tymczasowy na cache klatek wideo
RUN mkdir -p /tmp/ascier_video
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80
ENTRYPOINT ["dotnet", "AscierWeb.dll"]
