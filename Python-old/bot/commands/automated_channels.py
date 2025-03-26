import discord
from discord.ext import commands
import asyncio
from logs.logger import Logger
from datetime import datetime, timedelta
import pytz

class AutomatedChannelsCog(commands.Cog):
    """Comandos para configurar canais com atualizações automáticas de ranking"""

    def __init__(self, bot):
        self.bot = bot
        self.logger = Logger("AutomatedChannels")
        self.update_tasks = {}
        self.bot.loop.create_task(self.load_ranking_channels())

    @commands.command()
    @commands.has_permissions(administrator=True)
    async def set_ranking_channel(self, ctx, channel: discord.TextChannel = None):
        """
        Define o canal onde o ranking será exibido e atualizado automaticamente a cada 10 minutos.
        Se nenhum canal for especificado, usa o canal atual.
        
        Uso: !set_ranking_channel #canal
        """
        # Usar o canal atual se nenhum for especificado
        channel = channel or ctx.channel
        
        # Registrar no banco de dados
        settings_collection = self.bot.db.get_collection('bot_settings')
        
        # Verificar se já existe uma configuração para este servidor
        server_settings = settings_collection.find_one({"server_id": ctx.guild.id})
        
        if server_settings:
            # Atualizar configuração existente
            settings_collection.update_one(
                {"server_id": ctx.guild.id},
                {"$set": {"ranking_channel_id": channel.id, "last_ranking_message_id": None}}
            )
        else:
            # Criar nova configuração
            settings_collection.insert_one({
                "server_id": ctx.guild.id,
                "ranking_channel_id": channel.id,
                "last_ranking_message_id": None
            })
        
        # Iniciar a tarefa de atualização para este servidor
        await self.start_ranking_updates(ctx.guild.id, channel.id)
        
        self.logger.info(f"Canal de ranking configurado no servidor {ctx.guild.name} (ID: {ctx.guild.id}): {channel.name} (ID: {channel.id})")
        await ctx.send(f"✅ O canal {channel.mention} foi configurado para receber atualizações automáticas do ranking a cada 10 minutos!")

    @commands.command()
    @commands.has_permissions(administrator=True)
    async def disable_ranking_channel(self, ctx):
        """Desativa a atualização automática do ranking no servidor atual."""
        settings_collection = self.bot.db.get_collection('bot_settings')
        
        # Verificar se existe configuração para este servidor
        result = settings_collection.update_one(
            {"server_id": ctx.guild.id},
            {"$unset": {"ranking_channel_id": "", "last_ranking_message_id": ""}}
        )
        
        # Parar a tarefa de atualização se existir
        if ctx.guild.id in self.update_tasks:
            self.update_tasks[ctx.guild.id].cancel()
            del self.update_tasks[ctx.guild.id]
        
        if result.modified_count > 0:
            self.logger.info(f"Atualizações automáticas de ranking desativadas no servidor {ctx.guild.name} (ID: {ctx.guild.id})")
            await ctx.send("✅ As atualizações automáticas do ranking foram desativadas para este servidor!")
        else:
            await ctx.send("⚠️ Este servidor não tinha um canal de ranking configurado.")

    async def load_ranking_channels(self):
        """Carrega todos os canais de ranking configurados e inicia as atualizações"""
        await self.bot.wait_until_ready()
        
        try:
            settings_collection = self.bot.db.get_collection('bot_settings')
            servers = list(settings_collection.find({"ranking_channel_id": {"$exists": True}}))
            
            for server in servers:
                server_id = server.get("server_id")
                channel_id = server.get("ranking_channel_id")
                
                # Iniciar tarefa de atualização para cada servidor
                await self.start_ranking_updates(server_id, channel_id)
                
            self.logger.info(f"Carregados {len(servers)} canais de ranking automático")
        except Exception as e:
            self.logger.error(f"Erro ao carregar canais de ranking: {str(e)}")

    async def start_ranking_updates(self, server_id, channel_id):
        """Inicia a tarefa de atualização periódica para um servidor específico"""
        # Cancelar tarefa existente se houver
        if server_id in self.update_tasks:
            self.update_tasks[server_id].cancel()
        
        # Criar nova tarefa de atualização
        task = self.bot.loop.create_task(self.update_ranking_task(server_id, channel_id))
        self.update_tasks[server_id] = task
        self.logger.info(f"Iniciada tarefa de atualização para o servidor {server_id}, canal {channel_id}")

    async def update_ranking_task(self, server_id, channel_id):
        """Tarefa que atualiza periodicamente o ranking em um canal específico"""
        try:
            # Atualizar imediatamente ao iniciar
            await self.update_server_ranking(server_id, channel_id)
            
            # Loop de atualização a cada 30 minutos
            while True:
                await asyncio.sleep(10 * 60)  # 30 minutos em segundos
                await self.update_server_ranking(server_id, channel_id)
                
        except asyncio.CancelledError:
            self.logger.info(f"Tarefa de atualização cancelada para servidor {server_id}")
        except Exception as e:
            self.logger.error(f"Erro na tarefa de atualização para servidor {server_id}: {str(e)}")

    async def update_server_ranking(self, server_id, channel_id):
        """Atualiza a mensagem do ranking em um canal específico"""
        settings_collection = self.bot.db.get_collection('bot_settings')
        server_settings = settings_collection.find_one({"server_id": server_id})
        
        if not server_settings:
            self.logger.error(f"Configurações não encontradas para o servidor {server_id}")
            return
            
        try:
            # Obter o canal
            channel = self.bot.get_channel(channel_id)
            if not channel:
                self.logger.error(f"Canal não encontrado: {channel_id}")
                return
                
            # Obter a última mensagem, se existir
            last_message_id = server_settings.get("last_ranking_message_id")
            last_message = None
            
            # Tentar buscar a mensagem anterior para editar
            if last_message_id:
                try:
                    last_message = await channel.fetch_message(last_message_id)
                except discord.errors.NotFound:
                    self.logger.warning(f"Mensagem antiga não encontrada: {last_message_id}")
                    last_message = None
                except Exception as e:
                    self.logger.error(f"Erro ao buscar mensagem: {str(e)}")
                    last_message = None
            
            # Obter os dados do ranking
            jogadores_collection = self.bot.db.get_collection('players')
            count = jogadores_collection.count_documents({"auto_check": True})
            jogadores_data = list(jogadores_collection.find({"auto_check": True}).sort("mmr_atual", -1).limit(25))
            
            # Preparar o conteúdo da mensagem
            if not jogadores_data:
                self.logger.warning(f"Sem jogadores para exibir no ranking para o servidor {server_id}")
                content = "Não há jogadores registrados no banco de dados para exibir no ranking."
                embed = None
                view = None
            else:
                # Criar o embed de ranking com novo estilo
                embed = discord.Embed(
                    title="🏆 Ranking de Arena",
                    color=discord.Color.blue()
                )
                
                embed.set_thumbnail(url="https://ddragon.leagueoflegends.com/cdn/14.3.1/img/champion/Jinx.png")
                
                for i, jogador in enumerate(jogadores_data[:25], start=1):
                    nome = jogador.get('nome', 'Sem Nome')
                    mmr = jogador.get('mmr_atual', 'N/A')
                    wins = jogador.get('wins', 0)
                    losses = jogador.get('losses', 0)
                    delta_mmr = jogador.get('delta_mmr', 0)

                    if i == 1:
                        emoji = "🥇"
                    elif i == 2:
                        emoji = "🥈"
                    elif i == 3:
                        emoji = "🥉"
                    else:
                        emoji = f"{i}."

                    # Show trend with arrow and format delta
                    trend = "↑" if delta_mmr > 0 else "↓" if delta_mmr < 0 else "→"
                    delta_formatted = f"+{delta_mmr}" if delta_mmr > 0 else f"{delta_mmr}" if delta_mmr != 0 else "0"
                    
                    embed.add_field(
                        name=f"{emoji} {nome}",
                        value=f"**{mmr} PDL** | W/L: {wins}/{losses} | Winrate: {0 if (wins + losses) == 0 else (wins / (wins + losses) * 100):.0f}% | {trend} {delta_formatted} pdl" + f"{'s' if abs(delta_mmr) > 1 else ''}", 
                        inline=False
                    )
                
                # Get next update 10 minutes from now
                brasilia_tz = pytz.timezone('America/Sao_Paulo')
                
                brasilia_time = (datetime.now(brasilia_tz) + timedelta(minutes=10)).strftime('%d/%m/%Y %H:%M')
                
                
                
                embed.set_footer(text=f"Próxima atualização: {brasilia_time}",
                                icon_url="https://ddragon.leagueoflegends.com/cdn/14.3.1/img/champion/Senna.png")
                
                content = None
                view = None

            # Editar a mensagem existente ou enviar uma nova
            if last_message:
                # Editar a mensagem existente
                await last_message.edit(content=content, embed=embed, view=view)
                message = last_message
                self.logger.info(f"Mensagem de ranking editada no servidor {server_id}, canal {channel_id}")
            else:
                # Enviar uma nova mensagem se não existir uma anterior
                if embed:
                    message = await channel.send(embed=embed, view=view)
                else:
                    message = await channel.send(content)
                
                # Atualizar o ID da mensagem no banco de dados
                settings_collection.update_one(
                    {"server_id": server_id},
                    {"$set": {"last_ranking_message_id": message.id}}
                )
                self.logger.info(f"Nova mensagem de ranking enviada no servidor {server_id}, canal {channel_id}")
            
            self.logger.info(f"Ranking atualizado no servidor {server_id}, canal {channel_id}")
            
        except Exception as e:
            self.logger.error(f"Erro ao atualizar ranking: {str(e)}")