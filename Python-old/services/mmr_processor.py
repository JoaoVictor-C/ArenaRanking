# mmr_processor.py
from database.mongodb_client import add_jogador
from services.riot_api import get_match_history_puuid, get_match_details, get_player # Adapte conforme necessário
import math
import datetime
from logs.logger import Logger
import traceback
from config.constants import K_FACTOR_BASE, K_FACTOR_NEW_PLAYER, K_MAX, PLACEMENT_MULTIPLIERS, MIN_MATCHES_STABLE, DEFAULT_MMR, MATCHES_TO_FETCH

def processar_mmr_jogador(db, jogador_data, logger=None):
    """
    Processa o MMR de um jogador baseado em suas partidas recentes.
    
    Args:
        db: Conexão com o banco de dados
        jogador_data: Dados do jogador do banco de dados
        logger: Instância do logger (opcional)
    
    Returns:
        bool: True se processado com sucesso, False caso contrário
    """
    if logger is None:
        logger = Logger()
    
    puuid = jogador_data['puuid']
    riot_id = jogador_data['riot_id']
    auto_check = jogador_data.get('auto_check', False)
    date_added = jogador_data.get('date_added')
    
    if not auto_check:
        return False
    
    
    ultimo_match_id_processado = jogador_data.get('ultimo_match_id_processado')
    
    partidas_ids = get_match_history_puuid(puuid)
    
    if not partidas_ids:
        logger.warning(f"Não foi possível obter o histórico de partidas para {riot_id}.")
        return False

    # Determinar novas partidas a serem processadas
    novas_partidas_ids = _obter_novas_partidas(partidas_ids, ultimo_match_id_processado)
    
    if not novas_partidas_ids:
        return True
    
    
    # Resetar delta_mmr para 0 no início do processamento
    players_collection = db.get_collection('players')
    players_collection.update_one(
        {"puuid": puuid},
        {"$set": {"delta_mmr": 0}}
    )
    
    # Processar cada partida
    for match_id in reversed(novas_partidas_ids):  # Processar na ordem cronológica correta
        _processar_partida(db, match_id, riot_id, puuid, logger, date_added)
        
    return True

def _obter_novas_partidas(partidas_ids, ultimo_match_id_processado):
    """Função auxiliar para obter apenas as novas partidas."""
    if not ultimo_match_id_processado:
        return partidas_ids  # Processar todas se não houver registro
        
    novas_partidas_ids = []
    for match_id in partidas_ids:
        if match_id == ultimo_match_id_processado:
            break  # Já processou até aqui
        novas_partidas_ids.append(match_id)
    
    return novas_partidas_ids

def _processar_partida(db, match_id, riot_id, puuid, logger, date_added):
    """
    Processa uma partida específica e atualiza o MMR dos jogadores.
    
    Args:
        db: Conexão com o banco de dados
        match_id: ID da partida a ser processada
        riot_id: ID do jogador principal
        puuid: PUUID do jogador principal
        logger: Instância do logger
    """
    detalhes_partida = get_match_details(match_id)
    # If the date added is after the match date, ignore it
    if date_added and detalhes_partida.get('info', {}).get('gameCreation'):
        game_creation_time = datetime.datetime.utcfromtimestamp(detalhes_partida.get('info', {}).get('gameCreation') / 1000)
        # Ensure date_added is a datetime object in UTC for comparison
        if isinstance(date_added, str):
            date_added = datetime.datetime.fromisoformat(date_added.replace('Z', '+00:00'))
        
        if game_creation_time < date_added:
            return False
    
    if not detalhes_partida:
        print(f"Não foi possível obter detalhes da partida {match_id}.")
        return False
        
    if detalhes_partida.get('info', {}).get('gameMode') != "CHERRY":
        return False
        
    
    # Buscar todos os jogadores de uma vez para evitar múltiplas consultas
    players_puuids = detalhes_partida.get('metadata', {}).get('participants', [])
    players_collection = db.get_collection('players')
    existing_players = {p['puuid']: p for p in players_collection.find({"puuid": {"$in": players_puuids}})}
    
    # Preparar informações de participantes
    participant_info = {}
    for p in detalhes_partida.get('info', {}).get('participants', []):
        if p.get('puuid'):
            participant_info[p.get('puuid')] = {
                'placement': p.get('placement'),
                'riotIdGameName': p.get('riotIdGameName'),
                'riotIdTagline': p.get('riotIdTagline')
            }
    
    average_mmr = 0
    player_updates = []
    total_players = len(players_puuids) + 1
    
    for player_puuid in players_puuids:
        if player_puuid not in participant_info:
            logger.warning(f"PUUID {player_puuid} não encontrado nos detalhes dos participantes")
            continue
            
        info = participant_info[player_puuid]
        placement = info['placement']
        
        # Determinar se é uma vitória ou derrota (top 4 = vitória)
        is_win = placement <= 4
        
        if player_puuid in existing_players:
            player = existing_players[player_puuid]
            mmr = player.get('mmr_atual', 0)
            # Obter valores atuais de vitórias e derrotas
            wins = player.get('wins', 0)
            losses = player.get('losses', 0)
            
            # Verificar se esta partida já foi processada para este jogador
            if player.get('ultimo_match_id_processado') == match_id or (player.get('auto_check') == True and player_puuid != puuid):
                average_mmr += mmr
                continue
            player_riot_id = player.get('riot_id')
        else:
            # Jogador não existe no banco, criar novo
            player_riot_id = f"{info['riotIdGameName']}#{info['riotIdTagline']}"
            tier = get_player(player_puuid)
            mmr = DEFAULT_MMR.get(tier, DEFAULT_MMR['UNRANKED'])
            # Para novos jogadores, começar com 0 vitórias e 0 derrotas
            wins = 0
            losses = 0
            # Adicionar novo jogador ao banco
            add_jogador(db, player_puuid, player_riot_id, info['riotIdGameName'], False, mmr)
            
        average_mmr += mmr
        player_updates.append({
            'puuid': player_puuid,
            'riot_id': player_riot_id,
            'placement': placement,
            'mmr_atual': mmr,
            'wins': wins + (1 if is_win else 0),
            'losses': losses + (0 if is_win else 1)
        })
    
    # Evitar divisão por zero
    if total_players > 0:
        average_mmr = average_mmr / total_players
    
    # Processar atualizações de MMR em lote
    for player in player_updates:
        novo_mmr = calculate_mmr_change(player['mmr_atual'], average_mmr, int(player['placement']))
        mmr_final = player['mmr_atual'] + novo_mmr
        
        atualizar_mmr_jogador_db(
            db, 
            player['riot_id'], 
            mmr_final, 
            match_id, 
            player['wins'],
            player['losses']
        )
        
        # Log MMR changes only for players with auto_check enabled
        player_data = players_collection.find_one({"puuid": player['puuid']})
        if player_data and player_data.get('auto_check', False):
            logger.info(f"Jogador {player['riot_id']}: Placement {player['placement']}, MMR {player['mmr_atual']} -> {mmr_final} (Δ{novo_mmr})")
    
    # Atualizar o registro do jogador principal
    players_collection.update_one(
        {"puuid": puuid},
        {"$set": {"ultimo_match_id_processado": match_id}}
    )
    
    return True

def calculate_mmr_change(player_mmr, average_mmr, placement, partidas_jogadas=0):
    """
    Calcula a mudança de MMR baseado na colocação e MMR médio da partida.
    
    Args:
        player_mmr: MMR atual do jogador
        average_mmr: MMR médio da partida
        placement: Colocação do jogador (1-8)
        partidas_jogadas: Número de partidas que o jogador já jogou
        
    Returns:
        int: Alteração de MMR (pode ser positivo ou negativo)
    """
    
    # Determinar fator K apropriado
    if partidas_jogadas < MIN_MATCHES_STABLE:
        k = K_FACTOR_NEW_PLAYER
    else:
        # Fator dinâmico baseado na diferença entre MMR do jogador e MMR médio
        if average_mmr == 0:
            k = K_FACTOR_BASE
        else:
            mmr_diff = abs(player_mmr - average_mmr)
            # Quanto maior a diferença, mais ajuste é necessário
            k = K_FACTOR_BASE + min(K_MAX - K_FACTOR_BASE, (100 / max(1, mmr_diff)) * abs(math.tanh(mmr_diff / 100)))
            
            # Ajuste adicional se o jogador tem MMR muito maior ou menor que a média
            if player_mmr > average_mmr and placement > 4:
                k *= 1.2  # Penalidade maior para jogadores fortes indo mal
            elif player_mmr < average_mmr and placement <= 2:
                k *= 1.3  # Bônus maior para jogadores mais fracos indo bem
    
    # Obter o multiplicador para a colocação
    multiplier = PLACEMENT_MULTIPLIERS.get(placement, 0)
    
    # Calcular o ajuste final de MMR
    mmr_change = int(k * multiplier)
    
    # Limitar mudanças extremas
    return max(-100, min(100, mmr_change))

def atualizar_mmr_jogador_db(db, riot_id, novo_mmr, ultimo_match_id, wins, losses):
    """
    Atualiza o MMR e o último match processado no banco de dados.
    
    Args:
        db: Conexão com o banco de dados
        riot_id: ID do jogador
        novo_mmr: Novo valor de MMR
        ultimo_match_id: ID da última partida processada
        wins: Número de vitórias do jogador
        losses: Número de derrotas do jogador
    
    Returns:
        bool: True se atualizado com sucesso, False caso contrário
    """
    players_collection = db.get_collection('players')
    try:
        # Adicionar histórico de mudanças de MMR
        agora = datetime.datetime.now()
        
        # Buscar o jogador para calcular o delta de MMR
        jogador = players_collection.find_one({"riot_id": riot_id})
        if jogador:
            mmr_atual = jogador.get('mmr_atual', novo_mmr)
            mmr_change = novo_mmr - mmr_atual
            delta_mmr_atual = jogador.get('delta_mmr', 0)
            novo_delta_mmr = delta_mmr_atual + mmr_change
        else:
            novo_delta_mmr = 0
        
        result = players_collection.update_one(
            {"riot_id": riot_id},
            {
                "$set": {
                    "mmr_atual": novo_mmr,
                    "ultimo_match_id_processado": ultimo_match_id,
                    "ultima_atualizacao": agora,
                    "wins": wins,
                    "losses": losses,
                    "delta_mmr": novo_delta_mmr
                },
                "$push": {
                    "historico_mmr": {
                        "mmr": novo_mmr,
                        "match_id": ultimo_match_id,
                        "data": agora
                    }
                }
            }
        )
        
        return result.modified_count > 0
    except Exception as e:
        traceback.print_exc()
        print(f"Erro ao atualizar MMR de {riot_id} no banco de dados: {e}")
        return False

def processar_mmr_todos_jogadores(db):
    """Processa o MMR de todos os jogadores registrados."""
    print("Iniciando processamento de MMR para todos os jogadores...")
    players_collection = db.get_collection('players') # Obtenha a coleção correta
    jogadores = list(players_collection.find()) # Busca todos os jogadores
    if not jogadores:
        print("Não há jogadores registrados para processar MMR.")
        return

    for jogador_data in jogadores:
        processar_mmr_jogador(db, jogador_data)

    print("Processamento de MMR para todos os jogadores concluído.")

def detectar_anomalias_mmr(db):
    """
    Detecta anomalias no sistema de MMR, como jogadores com ganhos ou perdas anormais.
    """
    players_collection = db.get_collection('players')
    anomalias = []
    
    # Encontrar jogadores com mais de 10 partidas
    jogadores = list(players_collection.find({"historico_mmr": {"$exists": True, "$ne": []}}))
    
    for jogador in jogadores:
        historico = jogador.get('historico_mmr', [])
        if len(historico) < 10:
            continue
            
        # Analisar variações recentes
        ultimas_10 = historico[-10:]
        mmr_inicial = ultimas_10[0]['mmr']
        mmr_final = ultimas_10[-1]['mmr']
        variacao = mmr_final - mmr_inicial
        
        # Detectar variações extremas (mais de 500 pontos em 10 partidas)
        if abs(variacao) > 500:
            anomalias.append({
                'riot_id': jogador['riot_id'],
                'variacao': variacao,
                'periodo': '10 partidas'
            })
    
    return anomalias