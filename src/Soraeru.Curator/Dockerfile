FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Soraeru.Curator.csproj ./
RUN dotnet restore Soraeru.Curator.csproj

COPY . ./
RUN dotnet publish Soraeru.Curator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY curator-entrypoint.sh /usr/local/bin/curator-entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/curator-entrypoint.sh \
    && chmod 0755 /usr/local/bin/curator-entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/curator-entrypoint.sh"]
CMD ["dotnet", "Soraeru.Curator.dll"]
