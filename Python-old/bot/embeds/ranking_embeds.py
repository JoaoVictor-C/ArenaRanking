import discord

def create_ranking_embed(jogadores_data, pagina, tamanho, skip, count):
    """
    Cria um embed formatado para exibir o ranking de jogadores
    
    Args:
        jogadores_data: Lista de dados de jogadores
        pagina: Número da página atual
        tamanho: Número de jogadores por página
        skip: Número de jogadores ignorados para paginação
        count: Total de jogadores
        
    Returns:
        discord.Embed: Embed formatado com os dados do ranking
    """
    embed = discord.Embed(
        title="🏆 Ranking de Arena",
        description=f"Página {pagina} | Jogadores {skip+1}-{min(skip+tamanho, count)} de {count}",
        color=0x3498db
    )

    for i, jogador in enumerate(jogadores_data, start=1):
        position = skip + i
        nome = jogador.get('nome', 'Sem Nome')
        mmr = jogador.get('mmr_atual', 'N/A')
        wins = jogador.get('wins', 0)
        loses = jogador.get('losses', 0)

        if position == 1:
            emoji = "🥇"
        elif position == 2:
            emoji = "🥈"
        elif position == 3:
            emoji = "🥉"
        else:
            emoji = f"{position}."
        embed.add_field(
            name=f"{emoji} {nome}",
            value=f"**MMR:** {mmr} | **W/L:** {wins}/{loses}",
            inline=False
        )
    embed.set_footer(text=f"Use !lista {pagina+1} {tamanho} para ver a próxima página")
    return embed