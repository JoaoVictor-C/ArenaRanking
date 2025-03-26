from discord.ext import commands
import datetime
from bot.embeds.ranking_embeds import create_ranking_embed

class PlayerListingCog(commands.Cog):
    """Comandos relacionados à listagem e consulta de jogadores"""

    def __init__(self, bot):
        self.bot = bot

    @commands.command()
    @commands.has_permissions(administrator=True)
    async def lista(self, ctx, pagina: int = 1, tamanho: int = 10):
        """
        Lista todos os jogadores registrados com MMR, Ranque e Ultima Partida Processada.

        Argumentos:
            pagina: Número da página (começa em 1)
            tamanho: Número de jogadores por página
        """
        if pagina <= 0:
            await ctx.send("Número de página deve ser maior que zero.")
            return

        if tamanho <= 0 or tamanho > 25:
            await ctx.send("Tamanho da página deve estar entre 1 e 25.")
            return

        skip = (pagina - 1) * tamanho

        jogadores_collection = self.bot.db.get_collection('players')
        count = jogadores_collection.count_documents({"auto_check": True})
        jogadores_data = list(jogadores_collection.find({"auto_check": True}).sort("mmr_atual", -1).skip(skip).limit(tamanho))

        if not jogadores_data:
            if skip >= count:
                await ctx.send(f"Não há jogadores para exibir na página {pagina}. Total de jogadores: {count}")
            else:
                await ctx.send("Não há jogadores registrados no banco de dados.")
            return
            
        embed = create_ranking_embed(jogadores_data, pagina, tamanho, skip, count)
        await ctx.send(embed=embed)