
# Valores de MMR padrão por tier
DEFAULT_MMR = {
    "CHALLENGER": 2000,
    "GRANDMASTER": 1600,
    "MASTER": 1300,
    "DIAMOND": 900,
    "PLATINUM": 700,
    "UNRANKED": 600,
    "EMERALD": 550,
    "GOLD": 350,
    "SILVER": 250,
    "BRONZE": 200,
    "IRON": 100
}

# Configuração do fator K para cálculo de MMR
K_FACTOR_BASE = 40  # Fator K base
K_FACTOR_NEW_PLAYER = 50  # Fator K para jogadores novos (primeiras 20 partidas)
K_MAX = 80  # Limite máximo do fator K

# Multiplicadores de MMR baseados na posição
PLACEMENT_MULTIPLIERS = {
    1: 1.5,    # 1º lugar
    2: 1.2,    # 2º lugar
    3: 0.7,    # 3º lugar
    4: 0.5,    # 4º lugar
    5: -0.5,   # 5º lugar
    6: -0.7,   # 6º lugar
    7: -0.8,   # 7º lugar
    8: -1    # 8º lugar
}

# Número de partidas recentes a buscar por jogador
MATCHES_TO_FETCH = 20

# Número mínimo de partidas para considerar o MMR estável
MIN_MATCHES_STABLE = 20