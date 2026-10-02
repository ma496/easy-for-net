# The API, built from src/backend (the build context). Nothing here names the project, so it builds
# unchanged whatever the .csproj is called.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore on its own layer, so a code-only change does not download the packages again
COPY Source/*.csproj Source/
RUN dotnet restore Source
COPY Source/ Source/
RUN dotnet publish Source -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /out .
# Uploaded files and the Data Protection key ring are volumes; create them owned by the non-root
# user so a fresh named volume inherits that ownership
RUN mkdir -p uploads keys && chown -R "$APP_UID" uploads keys
USER $APP_UID
EXPOSE 8080
# The entry assembly is the one with a runtimeconfig.json beside it
ENTRYPOINT ["sh", "-c", "exec dotnet \"$(basename /app/*.runtimeconfig.json .runtimeconfig.json).dll\""]
