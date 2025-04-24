using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services.Configuration
{
    public static class DotEnvLoader
    {
        public static void Load(ILogger logger, string filePath = null)
        {
            filePath = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");

            if (!File.Exists(filePath))
            {
                logger.LogWarning("Arquivo .env não encontrado em: {FilePath}", filePath);
                return;
            }

            logger.LogInformation("Carregando variáveis de ambiente do arquivo: {FilePath}", filePath);

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmedLine = line.Trim();

                // Ignora comentários e linhas vazias
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                var parts = trimmedLine.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Remove aspas das strings se necessário
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                Environment.SetEnvironmentVariable(key, value);
                logger.LogInformation("Variável de ambiente carregada: {Key}", key);
            }
        }
    }
}
