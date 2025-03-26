import os
from dotenv import load_dotenv

# Carregar variáveis de ambiente do arquivo .env
load_dotenv()

# Configurações da API Riot
RIOT_API_KEY = os.getenv('RIOT_API_KEY')
REGION = os.getenv('REGION', 'americas')  # Valor padrão caso não esteja definido

# Configurações do Discord
DISCORD_TOKEN = os.getenv('DISCORD_TOKEN')

# Configurações do banco de dados
DATABASE_URL = os.getenv('DATABASE_URL')

# Configurações de agendamento
MMR_UPDATE_INTERVAL = int(os.getenv('MMR_UPDATE_INTERVAL', '2'))  # Em minutos, padrão 2