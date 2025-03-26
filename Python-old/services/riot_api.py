# riot_api.py
import requests
from config import RIOT_API_KEY, REGION
import time

def get_summoner_by_puuid(puuid):
    url = f"https://{REGION}.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/{puuid}"
    headers = {"X-Riot-Token": RIOT_API_KEY}
    response = requests.get(url, headers=headers)
    if response.status_code == 200:
        return response.json()
    else:
        return None

async def get_player(puuid):
    url = f"https://{REGION}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}"
    headers = {"X-Riot-Token": RIOT_API_KEY}

    try:
        response = requests.get(url, headers=headers)
        if response.status_code == 200:
            data = response.json()

            for entry in data:
                if entry.get('queueType') == "RANKED_SOLO_5x5":
                    return entry.get('tier')

            return "UNRANKED"  # Retorna UNRANKED se não encontrar RANKED_SOLO_5x5

        elif response.status_code == 404:
            return None  # Sem dados de ranked para este PUUID

        elif response.status_code == 429:  # Rate Limit Excedido
            print("Rate limit exceeded while fetching Ranked data by PUUID. Waiting 10 seconds...")
            await asyncio.sleep(10)  # Usa asyncio.sleep() para não travar o loop
            return await get_player(puuid)  # Tentar novamente de forma assíncrona

        else:
            print(f"Error fetching Ranked data for puuid {puuid}: Status Code {response.status_code}")
            return None
    except requests.exceptions.RequestException as e:
        print(f"Request exception while fetching Ranked data by PUUID: {e}")
        return None

def get_match_history_puuid(puuid):
    url = f"https://{REGION}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?type=normal&start=0&count=5"
    headers = {"X-Riot-Token": RIOT_API_KEY}
    response = requests.get(url, headers=headers)
    if response.status_code == 200:
        return response.json()
    elif response.status_code == 429:
        time.sleep(10)
        return get_match_history_puuid(puuid)
    else:
        print(f"Error fetching match history for puuid {puuid}: {response.status_code}")
        return None

def get_match_details(match_id):
    url = f"https://{REGION}.api.riotgames.com/lol/match/v5/matches/{match_id}"
    headers = {"X-Riot-Token": RIOT_API_KEY}
    response = requests.get(url, headers=headers)
    if response.status_code == 200:
        return response.json()
    elif response.status_code == 429:
        time.sleep(10)
        return get_match_details(match_id)
    else:
        print(f"Error fetching match details for matchId {match_id}: {response.status_code}")
        return None

def verify_riot_id(tagline, name):
    name = name.replace(" ", "%20") # Substitui espaços por %20 para URL
    url = f"https://americas.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{name}/{tagline}" # Endpoint CORRETO para PUUID e verificação de Riot ID
    headers = {"X-Riot-Token": RIOT_API_KEY}
    try:
        response = requests.get(url, headers=headers)
        if response.status_code == 200:
            response_json = response.json() # Get the JSON response
            puuid = response_json.get("puuid") # Extract the PUUID from the JSON
            return puuid  # **Return the PUUID if Riot ID is valid**
        elif response.status_code == 400 and response.message == "Unknown apikey":
            print("API Key inválida. Verifique o arquivo config.py e tente novamente.")
            return None
        elif response.status_code == 404:
            return None # Riot ID não encontrado, return None
        elif response.status_code == 429:
            time.sleep(10)
            return verify_riot_id(tagline, name) # Tentar novamente após esperar
        else:
            print(f"Erro ao verificar Riot ID {name}#{tagline}: Status Code {response.status_code}")
            return None # Outros erros tratados como inválidos por simplicidade, return None
    except requests.exceptions.RequestException as e:
        print(f"Exceção de requisição durante verificação de Riot ID: {e}")
        return None # Erros de rede também tratados como inválidos, return None
        
def get_summoner_id_by_name(summoner_name):
    url = f"https://br1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{summoner_name}"
    headers = {"X-Riot-Token": RIOT_API_KEY}
    try:
        response = requests.get(url, headers=headers)
        if response.status_code == 200:
            response_json = response.json()
            summoner_id = response_json.get("id") # Extrai o encryptedSummonerId (id)
            return summoner_id
        elif response.status_code == 404:
            return None # Invocador não encontrado
        elif response.status_code == 429:
            time.sleep(10)
            return get_summoner_id_by_name(summoner_name) # Tentar novamente
        else:
            print(f"Erro ao obter Summoner ID por nome {summoner_name}: Status Code {response.status_code}")
            return None
    except requests.exceptions.RequestException as e:
        print(f"Exceção de requisição ao obter Summoner ID por nome: {e}")
        return None

def get_ranked_data_by_puuid(puuid): # Função RENOMEADA e ATUALIZADA para PUUID
    url = f"https://{REGION}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}" # Endpoint CORRETO para PUUID
    headers = {"X-Riot-Token": RIOT_API_KEY}
    try:
        response = requests.get(url, headers=headers)
        if response.status_code == 200:
            return response.json() # Retorna os dados de Ranked (lista de filas ranqueadas)
        elif response.status_code == 404:
            return None # Sem dados de Ranked encontrados para este puuid
        elif response.status_code == 429:
            time.sleep(10)
            return get_ranked_data_by_puuid(puuid) # Tentar novamente
        else:
            print(f"Erro ao obter dados de Ranked para puuid {puuid}: Status Code {response.status_code}")
            return None
    except requests.exceptions.RequestException as e:
        print(f"Exceção de requisição ao obter dados de Ranked por PUUID: {e}")
        return None
    
def consultar_riot_api(riot_id):
    """
    Consults the Riot API to get information about a player and their latest match.

    Args:
        riot_id (str): The Riot ID of the player (e.g., "PlayerName#Tag").

    Returns:
        str: A message indicating the player's status or an error message.
             Returns None if there is an unrecoverable error during API calls and logging is handled internally.
    """
    summoner_name = riot_id.replace('#', ' ')
    summoner_url = f"https://americas.api.riotgames.com/lol/summoner/v4/summoners/by-name/{summoner_name}"
    headers = {"X-Riot-Token": RIOT_API_KEY}

    try:
        response = requests.get(summoner_url, headers=headers)
        response.raise_for_status()  # Raise HTTPError for bad responses (4xx or 5xx)
        summoner_data = response.json()
        puuid = summoner_data["puuid"]

        matches_url = f"https://americas.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids"
        match_response = requests.get(matches_url, headers=headers)
        match_response.raise_for_status()
        matches_data = match_response.json()

        if matches_data:
            latest_match = matches_data[0]
            return f"Jogador {riot_id} encontrado. Última partida: {latest_match}"
        else:
            return f"O jogador {riot_id} não tem partidas recentes."

    except requests.exceptions.HTTPError as http_err:
        if response.status_code == 404: # Player Not Found
            return f"Jogador {riot_id} não encontrado na Riot API."
        else:
            print(f"Erro HTTP ao consultar a Riot API para {riot_id}: {http_err}") # Log detailed error for server-side debugging.
            return f"Erro ao consultar o jogador: Status Code {response.status_code}" # User-friendly error
    except requests.exceptions.RequestException as req_err:
        print(f"Erro de requisição ao consultar a Riot API para {riot_id}: {req_err}") # Log detailed error.
        return f"Erro ao consultar o jogador: Problema de conexão com a API." # User-friendly error
    except KeyError as key_err: # Handle cases where expected keys are missing in JSON response
        print(f"Erro ao processar resposta da Riot API para {riot_id}: KeyError: {key_err}")
        return f"Erro ao processar dados do jogador." # User-friendly error
    except Exception as e: # Catch-all for other unexpected errors. Log them.
        print(f"Erro inesperado ao consultar a Riot API para {riot_id}: {e}")
        return "Ocorreu um erro inesperado ao consultar o jogador." # Generic error for user.
    return None # Indicate unrecoverable error and logging was handled.