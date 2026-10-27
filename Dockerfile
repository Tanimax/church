FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Project files first, so the restore layer is cached until a dependency changes. Every project
# the host references must be listed here (CI's docker build fails if one is missing).
COPY src/Web/ChurchAttendance.csproj src/Web/
COPY src/Shared/SharedKernel/SharedKernel.csproj src/Shared/SharedKernel/
COPY src/Modules/Identity/Identity.Contracts/Identity.Contracts.csproj src/Modules/Identity/Identity.Contracts/
COPY src/Modules/Identity/Identity.Core/Identity.Core.csproj src/Modules/Identity/Identity.Core/
COPY src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj src/Modules/Identity/Identity.Infrastructure/
RUN dotnet restore src/Web/ChurchAttendance.csproj
COPY . .
RUN dotnet publish src/Web/ChurchAttendance.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChurchAttendance.dll"]
