FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore SwiftReview.sln --configfile NuGet.Config
RUN dotnet publish src/SwiftReview.Worker/SwiftReview.Worker.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SwiftReview.Worker.dll"]
