FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Web/ChurchAttendance.csproj src/Web/
COPY src/Shared/SharedKernel/SharedKernel.csproj src/Shared/SharedKernel/
RUN dotnet restore src/Web/ChurchAttendance.csproj
COPY . .
RUN dotnet publish src/Web/ChurchAttendance.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChurchAttendance.dll"]
