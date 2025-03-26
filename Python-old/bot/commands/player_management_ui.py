import discord
from discord.ext import commands
from discord import app_commands
from services.riot_api import verify_riot_id
from database.mongodb_client import get_jogador_by_puuid, add_jogador, update_player_name, get_bot_config, get_jogador_by_riot_id, get_jogador_by_nome


class CloseMessageView(discord.ui.View):
    """View para fechar mensagens após um timeout ou interação"""
    def __init__(self, original_message=None):
        super().__init__(timeout=30)
        self.original_message = original_message
        self.message = None
    
    async def on_timeout(self):
        """Limpa a mensagem ao expirar o timeout"""
        if self.message:
            try:
                await self.message.delete()
            except (discord.NotFound, discord.Forbidden, discord.HTTPException):
                pass


def validate_riot_id(riot_id):
    """Valida o formato do Riot ID"""
    if '#' not in riot_id:
        return False, "Formato inválido. Use Nome#TAG."
    return True, None


def validate_display_name(nome):
    """Valida o nome de exibição do jogador"""
    invalid_chars = ['@', '#', ':', '```']
    
    if len(nome) < 2 or len(nome) > 32:
        return False, "Nome deve ter entre 2 e 32 caracteres."
    
    if any(char in nome for char in invalid_chars):
        return False, "Nome contém caracteres inválidos (@, #, :, ```)."
        
    return True, None


def parse_riot_id(riot_id):
    """Processa o Riot ID e retorna nome e tag"""
    name_part, tagline_part = riot_id.split('#')
    return name_part, tagline_part


class AddPlayerModal(discord.ui.Modal, title="Adicionar Jogador"):
    riot_id = discord.ui.TextInput(label="Riot ID", placeholder="Nome#TAG", required=True)
    display_name = discord.ui.TextInput(label="Nome", placeholder="Seu nome", required=True, min_length=2, max_length=32)

    def __init__(self, bot, original_message):
        super().__init__()
        self.bot = bot
        self.original_message = original_message

    async def on_submit(self, interaction: discord.Interaction):
        riot_id_input = self.riot_id.value
        nome = self.display_name.value

        # Validar Riot ID
        is_valid, error_message = validate_riot_id(riot_id_input)
        if not is_valid:
            await interaction.response.send_message(error_message, ephemeral=True)
            return

        # Validar nome
        is_valid, error_message = validate_display_name(nome)
        if not is_valid:
            await interaction.response.send_message(error_message, ephemeral=True)
            return
        
        try:
            # Processar Riot ID
            name_part, tagline_part = parse_riot_id(riot_id_input)
            await interaction.response.defer(ephemeral=True)
            
            name_part.replace(" ", "%20")   # Remover espaços do nome
            
            # Verificar no API da Riot
            puuid = verify_riot_id(tagline_part, name_part)
            if not puuid:
                await interaction.followup.send("❌ Riot ID inválido.", ephemeral=True)
                return
                
            # Verificar se jogador já existe
            existing_player = get_jogador_by_puuid(self.bot.db, puuid)
            if existing_player and existing_player.auto_check:
                await interaction.followup.send("⚠️ Jogador já cadastrado.", ephemeral=True, view=CloseMessageView(self.original_message))
                return
                
            # Adicionar jogador
            added = add_jogador(self.bot.db, puuid, riot_id_input, nome, True)
            if added:
                await interaction.followup.send("✅ Jogador adicionado com sucesso.", ephemeral=True, view=CloseMessageView(self.original_message))
            else:
                await interaction.followup.send("⚠️ Erro ao adicionar jogador.", ephemeral=True, view=CloseMessageView(self.original_message))
        except ValueError:
            await interaction.followup.send("Formato inválido do Riot ID.", ephemeral=True)
        except Exception as e:
            await interaction.followup.send(f"❌ Erro inesperado: {e}.", ephemeral=True, view=CloseMessageView(self.original_message))
            print(f"Erro ao adicionar jogador: {e}")


class RenamePlayerModal(discord.ui.Modal, title="Alterar Nome"):
    riot_id = discord.ui.TextInput(label="Riot ID", placeholder="Nome#TAG", required=True)
    novo_nome = discord.ui.TextInput(label="Novo Nome", placeholder="Novo nome", required=True, min_length=2, max_length=32)

    def __init__(self, bot, original_message):
        super().__init__()
        self.bot = bot
        self.original_message = original_message

    async def on_submit(self, interaction: discord.Interaction):
        riot_id_input = self.riot_id.value
        novo_nome = self.novo_nome.value
        
        # Validar Riot ID
        is_valid, error_message = validate_riot_id(riot_id_input)
        if not is_valid:
            await interaction.response.send_message(error_message, ephemeral=True)
            return
            
        # Validar nome
        is_valid, error_message = validate_display_name(novo_nome)
        if not is_valid:
            await interaction.response.send_message(error_message, ephemeral=True)
            return
            
        await interaction.response.defer(ephemeral=True)
        
        try:
            # Processar Riot ID
            name_part, tagline_part = parse_riot_id(riot_id_input)
            
            # Verificar no API da Riot
            puuid = verify_riot_id(tagline_part, name_part)
            if not puuid:
                await interaction.followup.send("❌ Riot ID inválido.", ephemeral=True, view=CloseMessageView(self.original_message))
                return
                
            # Verificar se jogador existe
            jogador_existente = get_jogador_by_puuid(self.bot.db, puuid)
            if not jogador_existente:
                await interaction.followup.send("❌ Jogador não encontrado.", ephemeral=True, view=CloseMessageView(self.original_message))
                return
                
            # Verificar se novo nome é igual ao atual
            if jogador_existente.nome == novo_nome:
                await interaction.followup.send("⚠️ O novo nome é igual ao atual.", ephemeral=True, view=CloseMessageView(self.original_message))
                return
                
            # Mostrar confirmação
            view = ConfirmRenameView(self.bot, interaction.user, puuid, novo_nome, jogador_existente.nome, self.original_message)
            await interaction.followup.send(
                content=f"⚠️ Confirmar mudança de '{jogador_existente.nome}' para '{novo_nome}'?",
                view=view,
                ephemeral=True
            )
        except Exception as e:
            await interaction.followup.send(f"❌ Erro inesperado: {e}.", ephemeral=True, view=CloseMessageView(self.original_message))
            print(f"Erro ao renomear jogador: {e}")


class ConfirmRenameView(discord.ui.View):
    """View para confirmar alteração de nome do jogador"""
    def __init__(self, bot, user, puuid, novo_nome, nome_atual, original_message):
        super().__init__(timeout=60)
        self.bot = bot
        self.user = user
        self.puuid = puuid
        self.novo_nome = novo_nome
        self.nome_atual = nome_atual
        self.original_message = original_message

    @discord.ui.button(label="Confirmar", style=discord.ButtonStyle.success)
    async def confirm_button(self, interaction: discord.Interaction, button: discord.ui.Button):
        if interaction.user != self.user:
            await interaction.response.send_message("Apenas quem iniciou pode confirmar.", ephemeral=True)
            return
        
        success = update_player_name(self.bot.db, self.puuid, self.novo_nome)
        for item in self.children:
            item.disabled = True
            
        if success:
            await interaction.response.edit_message(content=f"✅ Nome alterado de '{self.nome_atual}' para '{self.novo_nome}'!", view=self)
        else:
            await interaction.response.edit_message(content=f"❌ Erro ao alterar o nome.", view=self)

    @discord.ui.button(label="Cancelar", style=discord.ButtonStyle.danger)
    async def cancel_button(self, interaction: discord.Interaction, button: discord.ui.Button):
        if interaction.user != self.user:
            await interaction.response.send_message("Apenas quem iniciou pode cancelar.", ephemeral=True)
            return
            
        for item in self.children:
            item.disabled = True
            
        await interaction.response.edit_message(content="❌ Operação cancelada.", view=self)


class PlayerManagementView(discord.ui.View):
    """View com botões para gerenciamento de jogadores"""

    def __init__(self, bot):
        super().__init__(timeout=None)
        self.bot = bot

    @discord.ui.button(label="Adicionar Jogador", style=discord.ButtonStyle.green, custom_id="add_player")
    async def add_player_button(self, interaction: discord.Interaction, button: discord.ui.Button):
        await interaction.response.send_modal(AddPlayerModal(self.bot, interaction.message))

    @discord.ui.button(label="Alterar Nome", style=discord.ButtonStyle.blurple, custom_id="rename_player")
    async def rename_player_button(self, interaction: discord.Interaction, button: discord.ui.Button):
        await interaction.response.send_modal(RenamePlayerModal(self.bot, interaction.message))

    @discord.ui.button(label="Informações", style=discord.ButtonStyle.grey, custom_id="info_button")
    async def info_button(self, interaction: discord.Interaction, button: discord.ui.Button):
        # Combinando as duas partes da mensagem para enviar uma só vez
        info_text = (
            "# 🏆 Bem-vindo ao Sistema de Ranqueamento do Arena! 🏆\n\n"
            "Prepare-se para competir e subir nos rankings do nosso modo Arena!  Veja como funciona:\n\n"
            "## 1. Pontuação (`PDL`) 📈\n\n"
            "- Cada jogador tem uma pontuação, chamada `PDL`, que representa sua habilidade.\n"
            "- Você **ganha** `PDL` ao vencer partidas e **perde** ao ser derrotado.\n"
            "- A quantidade de `PDL` ganha ou perdida depende de alguns fatores:\n"
            "    - Sua **colocação** na partida: Quanto melhor você se sair, mais pontos você ganha (ou menos perde).\n"
            "    - A **força dos seus oponentes**: Vencer jogadores com `MMR` mais alto que o seu recompensa mais pontos! 🥇\n"
            "    - Se você é um **novo jogador**: Suas primeiras partidas terão um impacto *maior* no seu `MMR`, para te posicionar mais rapidamente. 🚀\n\n"
            "## 2. Tiers (Elos) 🏅\n\n"
            "Seu `MMR` determina seu **tier** (ou elo), que é uma forma visual de representar sua habilidade.  Temos os seguintes tiers, do menor para o maior:\n\n"
            "-   🟫 Madeira\n"
            "-   ⬜ Prata\n"
            "-   🟨 Ouro\n"
            "-   🟦 Platina\n"
            "-   ⚔️ Gladiador!\n\n"
            "## 3. Atualizações do Ranking 🔄\n\n"
            "- O sistema é atualizado **constantemente**. Isso significa que, após *cada partida*, o `MMR` de todos os jogadores envolvidos é recalculado.\n"
            "- A cada 30 minutos, suas últimas partidas serão adicionadas à sua pontuação. ⏱️\n\n"
            "## 4. Adição de Novos Jogadores 🌱\n\n"
            "- Quando você joga o Arena pela primeira vez, o sistema te dará um `MMR` inicial baseado no seu desempenho em outros modos de jogo (se houver dados disponíveis).\n"
            "- As partidas serão contabilizadas *a partir da sua adesão* no nosso sistema, descartando completamente as partidas anteriores.\n"
            "- Se você for um jogador completamente novo, começará com um `MMR` padrão um pouco mais baixo, para ter a chance de aprender e subir!\n\n"
            "## 5. Pontos Importantes ❗\n\n"
            "-   **Seja consistente:** O sistema recompensa jogadores que jogam regularmente e se esforçam para melhorar.\n"
            "-   **Não desanime com derrotas:** Elas fazem parte do aprendizado. Use-as para identificar pontos fracos e evoluir!\n"
            "-   **O sistema é dinâmico:** Ele se ajusta com o tempo, então continue jogando para alcançar seu verdadeiro tier!\n"
            "-   **Anomalias:** O sistema detecta ganhos ou perdas anormais de `MMR`.\n"
            "- **Partidas Recentes**: O sistema busca suas partidas recentes, sempre que você termina uma, para calcular sua nova pontuação com base no MMR médio dos participantes e na sua colocação final.\n\n"
            "---\n\n"
            "Divirta-se e boa sorte na sua jornada rumo ao topo do Arena! 🚀🎉\n\n"
            "> _Desenvolvido por Presente e Crazzyboy_"
        )
        view = CloseMessageView(interaction.message)
        message = await interaction.response.send_message(content=info_text, ephemeral=True, view=view)
        view.message = await interaction.original_response()


class PlayerManagementCog(commands.Cog):
    """Sistema de gerenciamento de jogadores via UI interativa"""

    def __init__(self, bot):
        self.bot = bot
        self.setup_message_id = None
        self.setup_channel_id = None
        # Agendar a tarefa para carregar e configurar a UI depois que o bot estiver pronto
        self.bot.loop.create_task(self.load_and_setup_ui())

    async def load_and_setup_ui(self):
        """Carrega configuração da UI do banco de dados e configura se existir"""
        await self.bot.wait_until_ready()
        
        try:
            # Carregar configurações do banco de dados
            config = get_bot_config(self.bot.db)
            
            if config and "setup_message_id" in config and "setup_channel_id" in config:
                self.setup_message_id = config["setup_message_id"]
                self.setup_channel_id = config["setup_channel_id"]
                
                # Tentar buscar o canal e a mensagem
                channel = self.bot.get_channel(self.setup_channel_id)
                if channel:
                    try:
                        # Verificar se a mensagem existe
                        message = await channel.fetch_message(self.setup_message_id)
                        # Se chegamos aqui, a mensagem existe, mas precisamos recolocar a view
                        view = PlayerManagementView(self.bot)
                        await message.edit(view=view)
                        print(f"Player Management UI recolocado na mensagem {self.setup_message_id} no canal {self.setup_channel_id}")
                    except discord.NotFound:
                        # Mensagem foi deletada, então precisaremos recriá-la
                        print(f"Mensagem de UI {self.setup_message_id} não encontrada, será recriada no próximo setup")
                        self.setup_message_id = None
                else:
                    print(f"Canal {self.setup_channel_id} não encontrado")
        except Exception as e:
            print(f"Erro ao carregar configuração da Player Management UI: {e}")

    def save_config(self):
        """Salva a configuração atual da UI no banco de dados"""
        try:
            config = get_bot_config(self.bot.db) or {}
            config["setup_message_id"] = self.setup_message_id
            config["setup_channel_id"] = self.setup_channel_id
            config["config_id"] = 1  # Garantir que config_id está definido para upserts
            
            # Atualizar a configuração no banco de dados
            settings_collection = self.bot.db.get_collection('bot_settings')
            settings_collection.update_one(
                {"config_id": 1},
                {"$set": config},
                upsert=True
            )
            print(f"Configuração da Player Management UI salva: message_id={self.setup_message_id}, channel_id={self.setup_channel_id}")
        except Exception as e:
            print(f"Erro ao salvar configuração da Player Management UI: {e}")

    @commands.command(name="adicionar_ui")
    @commands.has_permissions(administrator=True)
    async def adicionar_ui(self, ctx, channel: discord.TextChannel = None):
        """
        Comando para configurar o painel de gerenciamento.
        Requer permissões de administrador.
        
        Args:
            channel: O canal onde a UI será adicionada. Se não for fornecido, usa o canal atual.
        """
        channel = channel or ctx.channel
        self.setup_channel_id = channel.id
        
        # Verificar se a UI já está configurada
        if self.setup_message_id and self.setup_channel_id:
            try:
                old_channel = self.bot.get_channel(self.setup_channel_id)
                await old_channel.fetch_message(self.setup_message_id)
                await ctx.send("A UI já está configurada!", delete_after=10)
                await ctx.message.delete()
                return  # Sair se a UI existir
            except discord.NotFound:
                pass  # Mensagem deletada, prosseguir para criar
            except Exception as e:
                print(f"Erro ao verificar: {e}")
                # Ainda prosseguir, em caso de outros erros

        # Criar o embed
        embed = discord.Embed(
            title="🏆 Sistema de Gerenciamento de Jogadores",
            description="Use os botões abaixo para gerenciar sua participação no ranking",
            color=0x3498db
        )
        embed.add_field(name="Adicionar Jogador", value="Registre-se no sistema de ranking", inline=False)
        embed.add_field(name="Alterar Nome", value="Atualize seu nome de exibição", inline=False)
        embed.add_field(name="Informações", value="Saiba como funciona o sistema", inline=False)
        embed.set_footer(text="Sistema desenvolvido por Presente e Crazzyboy")
        
        # Definir a imagem de fundo
        background_image_url = "https://trackercdn.com/ghost/images/2023/7/7920_arena-league-of-legends-map.png"
        embed.set_image(url=background_image_url)

        view = PlayerManagementView(self.bot)
        setup_message = await channel.send(embed=embed, view=view)

        self.setup_message_id = setup_message.id
        self.setup_channel_id = channel.id
        
        # Salvar configuração no banco de dados
        self.save_config()
        
        try:
            await ctx.message.delete()  # Deletar a mensagem de comando
        except Exception as e:
            print(f"Erro ao apagar mensagem: {e}")
            
    
    @app_commands.command(name="pdl", description="Verifica PDL de um jogador")
    async def pdl(self, interaction: discord.Interaction, nome_de_invocador: str):
        """
        Verifica PDL de um jogador usando slash command
        
        Args:
            interaction: A interação do Discord
            nome_de_invocador: Nome ou Riot ID do jogador
        """
        await interaction.response.defer(ephemeral=True)

        pdl = self.get_player_pdl(nome_de_invocador)
        if pdl is not None:
            # Get player's position in the ranking
            players_collection = self.bot.db.get_collection('players')
            # Count players with higher MMR than this player
            rank_position = players_collection.count_documents({"mmr_atual": {"$gt": pdl}, "auto_check": True}) + 1
            # Get total number of players
            total_players = players_collection.count_documents({"auto_check": True})
            
            await interaction.followup.send(
                f"O PDL de {nome_de_invocador} é: **{pdl}**\n"+
                f"Posição no ranking: **{rank_position}º** de {total_players} jogadores.",
                ephemeral=True
            )
        else:
            await interaction.followup.send(
                f"Jogador '{nome_de_invocador}' não encontrado.",
                ephemeral=True
            )

    def get_player_pdl(self, player_identifier: str) -> int:
        """
        Obtém o PDL de um jogador específico usando nome ou Riot ID.
        
        Args:
            player_identifier: Nome ou Riot ID (formato "PlayerName#Tag")
            
        Returns:
            int: PDL do jogador ou None se não encontrado
        """
        player = None
        
        # Se contiver '#', tratamos como Riot ID; caso contrário, como nome simples
        if '#' in player_identifier:
            try:
                name_part, tagline_part = parse_riot_id(player_identifier)
                puuid = verify_riot_id(tagline_part, name_part)
                if puuid:
                    player = get_jogador_by_puuid(self.bot.db, puuid)
            except Exception as e:
                print(f"Erro ao buscar jogador por Riot ID: {e}")
                return None
        else:
            player = get_jogador_by_nome(self.bot.db, player_identifier)

        if player is None:
            return None

        return getattr(player, 'mmr_atual', None)  # Ajuste a propriedade conforme necessário