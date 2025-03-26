# Expor variáveis de configuração
from config.config import (
    RIOT_API_KEY, 
    REGION, 
    DISCORD_TOKEN, 
    DATABASE_URL, 
    MMR_UPDATE_INTERVAL
)

# Expor constantes para cálculo de MMR
from config.constants import (
    DEFAULT_MMR,
    K_FACTOR_BASE,
    K_FACTOR_NEW_PLAYER,
    K_MAX,
    PLACEMENT_MULTIPLIERS,
    MATCHES_TO_FETCH,
    MIN_MATCHES_STABLE
)