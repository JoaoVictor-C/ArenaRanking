from typing import Literal, Optional
import discord
from discord.ext.commands import Greedy, Context
from discord import app_commands
from discord.ext import commands
from config import DISCORD_TOKEN

from database.mongodb_client import connect_db
from bot.commands.player_management_ui import PlayerManagementCog
from bot.commands.player_commands import PlayerDataCog
from bot.commands.ranking_commands import PlayerListingCog
from bot.commands.automated_channels import AutomatedChannelsCog
from bot.commands.admin_commands import AdminCommandsCog

class ArenaBot(commands.Bot):
    def __init__(self):
        intents = discord.Intents.default()
        intents.messages = True
        intents.message_content = True
        intents.guild_messages = True
        super().__init__(command_prefix='!', intents=intents)
        self.guild = discord.Object(id='1235302805666005002')
        self.db = connect_db()

    async def setup_hook(self):
        # Carregar Cogs
        await self.add_cog(PlayerManagementCog(self))
        await self.add_cog(PlayerListingCog(self))
        await self.add_cog(PlayerDataCog(self))
        await self.add_cog(AutomatedChannelsCog(self))
        await self.add_cog(AdminCommandsCog(self))
        print("Bot setup completed!")

    async def on_ready(self):
        print(f'{self.user} está conectado e pronto!')
        try:
            synced = await self.tree.sync() 
            print(f"Sincronizados {len(synced)} comandos")
        except Exception as e:
            print(f"Erro ao sincronizar comandos: {e}")
        

    def run(self, token):
        """Sobrescreve o método run para permitir inicialização a partir de main.py"""
        super().run(token)

# Removida a linha que iniciava o bot diretamente