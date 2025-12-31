# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 프로젝트 파일 복사 및 종속성 복원
COPY *.csproj ./
RUN dotnet restore

# 소스 코드 복사 및 빌드
COPY . ./
RUN dotnet publish -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Render.com은 PORT 환경 변수 사용
EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

ENTRYPOINT ["dotnet", "StupidGuysServer.dll"]
