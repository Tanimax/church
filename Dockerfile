FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Build settings and project files first, so the restore layer is cached until a dependency
# changes. Every project the Api references must be listed here (CI's docker build fails if
# one is missing).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Api/ChurchAttendance.Api.csproj src/Api/
COPY src/Application/ChurchAttendance.Application.csproj src/Application/
COPY src/Domain/ChurchAttendance.Domain.csproj src/Domain/
COPY src/Infrastructure/ChurchAttendance.Infrastructure.csproj src/Infrastructure/
COPY src/SharedKernel/ChurchAttendance.SharedKernel.csproj src/SharedKernel/
RUN dotnet restore src/Api/ChurchAttendance.Api.csproj
COPY . .
RUN dotnet publish src/Api/ChurchAttendance.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChurchAttendance.dll"]
