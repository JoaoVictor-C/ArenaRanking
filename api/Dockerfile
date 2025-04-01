FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["api.csproj", "./"]
RUN dotnet restore

# Copiar todo o código-fonte
COPY . .

# Publicar a aplicação
RUN dotnet publish -c Release -o /app/publish

# Construir a imagem final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Criar pasta para os certificados e copiar
RUN mkdir -p /app/certificado
COPY --from=build /src/certificado/fullchain.pem /app/certificado/
COPY --from=build /src/certificado/privkey.pem /app/certificado/

# Configurar variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:3002;https://+:3002
ENV ASPNETCORE_ENVIRONMENT=Production

# Expor as portas
EXPOSE 3002

ENTRYPOINT ["dotnet", "api.dll"]