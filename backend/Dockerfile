FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet publish src/Stena.$SERVICE -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SERVICE
ENV SERVICE=$SERVICE
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["sh", "-c", "exec dotnet Stena.$SERVICE.dll"]
