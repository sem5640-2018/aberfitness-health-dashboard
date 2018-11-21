FROM microsoft/dotnet:2.1-sdk AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
RUN /root/.dotnet/tools/libman restore
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1-aspnetcore-runtime
EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS=https://+:443;http://+:80;
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "health-dashboard.dll"]
