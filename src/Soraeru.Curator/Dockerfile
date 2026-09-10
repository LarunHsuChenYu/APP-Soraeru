FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Soraeru.Curator.csproj ./
RUN dotnet restore Soraeru.Curator.csproj

COPY . ./
# region agent log
# Do not use --no-restore here: .NET 10 needs publish-time restore of
# Microsoft.AspNetCore.App.Internal.Assets for wwwroot/_framework/blazor.web.js.
RUN dotnet --version \
    && dotnet publish Soraeru.Curator.csproj -c Release -o /app/publish \
    && find /app/publish -iname '*blazor*' -print \
    && test -f /app/publish/wwwroot/_framework/blazor.web.js \
    && grep -q '"Route":"_framework/blazor.web.js"' \
        /app/publish/Soraeru.Curator.staticwebassets.endpoints.json
# endregion

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
# region agent log
RUN test -f /app/wwwroot/_framework/blazor.web.js \
    && grep -q '"Route":"_framework/blazor.web.js"' \
        /app/Soraeru.Curator.staticwebassets.endpoints.json
# endregion
COPY curator-entrypoint.sh /usr/local/bin/curator-entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/curator-entrypoint.sh \
    && chmod 0755 /usr/local/bin/curator-entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/curator-entrypoint.sh"]
CMD ["dotnet", "Soraeru.Curator.dll"]
