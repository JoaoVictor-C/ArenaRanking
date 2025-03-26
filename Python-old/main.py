import threading
from services.mmr_processor import processar_mmr_todos_jogadores
from database.mongodb_client import connect_db, create_collections
from bot.discord_bot import ArenaBot
from config import DISCORD_TOKEN, MMR_UPDATE_INTERVAL
from logs.logger import Logger
from services.scheduler import TaskScheduler

if __name__ == "__main__":
    # Configurar logger
    logger = Logger("Main")
    logger.info("Iniciando aplicação Arena Ranking")
    
    # Conectar ao MongoDB
    logger.info("Tentando conectar ao MongoDB Atlas...")
    db = connect_db()
    
    if db is not None:
        logger.info("Conectado ao MongoDB Atlas!")
        create_collections(db)
        
        # Inicializar o agendador
        scheduler = TaskScheduler()
        
        # Adicionar tarefa de processamento de MMR
        scheduler.add_task(
            name="process_mmr", 
            interval_minutes=MMR_UPDATE_INTERVAL if 'MMR_UPDATE_INTERVAL' in globals() else 2, 
            task_function=processar_mmr_todos_jogadores,
            db=db
        )
        
        # Iniciar o agendador em uma thread separada
        scheduler.start()
        logger.info("Agendamento de tarefa MMR configurado.")
        
        # Criar instância do bot
        logger.info("Inicializando o bot Discord...")
        bot = ArenaBot()
        
        # Iniciar o bot Discord 
        logger.info("Executando o bot Discord...")
        bot.run(DISCORD_TOKEN)  # No need for threading since this is a blocking call
    else:
        logger.error("Falha ao conectar ao banco de dados. Bot Discord não pode iniciar.")