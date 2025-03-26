from discord.ext import commands
import requests
import json
from services.riot_api import verify_riot_id
from config import RIOT_API_KEY
from database.mongodb_client import Player, get_jogador_by_riot_id
import discord
from discord import app_commands  # Adicionar essa importação

class PlayerDataCog(commands.Cog):
    """Comandos relacionados à obtenção e atualização de dados de jogadores da API Riot"""

    def __init__(self, bot):
        self.bot = bot
        self.region = "br1"

    @commands.command()
    @commands.has_permissions(administrator=True)
    async def verificar_mmr(self, ctx):
        """Executa manualmente a verificação e o processamento de MMR para todos os jogadores."""
        await ctx.send("Iniciando varredura manual de MMR. Isso pode levar alguns instantes...")
        if self.bot.db is None:
            await ctx.send("Erro: Conexão com o banco de dados não estabelecida.")
            return

        from services.mmr_processor import processar_mmr_todos_jogadores
        processar_mmr_todos_jogadores(self.bot.db)
        await ctx.send("Varredura de MMR manual concluída. Verifique o console para logs detalhados.")