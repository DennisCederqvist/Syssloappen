# Builds the Angular frontend and the ASP.NET Core API into a single image: the API serves the
# built Angular app as static files (see Program.cs's UseDefaultFiles/UseStaticFiles/
# MapFallbackToFile), so frontend and API share one origin in production — no CORS, no
# cross-site cookie configuration. Build from the repo root:
#   docker build -f backend/Syssloappen.Api/Dockerfile -t syssloappen-api .

FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npx ng build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/Syssloappen.Api/Syssloappen.Api.csproj backend/Syssloappen.Api/
RUN dotnet restore backend/Syssloappen.Api/Syssloappen.Api.csproj
COPY backend/Syssloappen.Api/ backend/Syssloappen.Api/
RUN dotnet publish backend/Syssloappen.Api/Syssloappen.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SkiaSharp's native library (used by RewardImageProcessor) fails to load on the minimal aspnet
# image without fontconfig — confirmed via DllNotFoundException: "libfontconfig.so.1: cannot
# open shared object file" the first time an image was uploaded on a real deploy.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend-build /app/publish .
COPY --from=frontend-build /src/frontend/dist/syssloappen/browser ./wwwroot

# Render (and most container hosts) inject PORT at runtime; default to 8080 for local `docker run`.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Syssloappen.Api.dll"]
