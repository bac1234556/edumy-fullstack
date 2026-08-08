FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EduMy.Backend.csproj", "./"]
RUN dotnet restore "./EduMy.Backend.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "EduMy.Backend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EduMy.Backend.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduMy.Backend.dll"]
