FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["api.csproj", "./"]
RUN dotnet restore

# Copiar todo o código-fonte
COPY . .

# Publicar a aplicação
RUN dotnet publish -c Release -o /app/publish

# Construir a imagem final
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Configurar variáveis de ambiente
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "api.dll"]
