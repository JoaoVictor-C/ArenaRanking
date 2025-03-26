import logging
from datetime import datetime
import os

class Logger:
    """Classe centralizada para gerenciar logs da aplicação."""
    
    # Dicionário para armazenar instâncias únicas por nome
    _instances = {}
    
    def __new__(cls, name="ArenaRankingBot"):
        # Implementação de Singleton por nome do logger
        if name not in cls._instances:
            cls._instances[name] = super(Logger, cls).__new__(cls)
            cls._instances[name]._initialized = False
        return cls._instances[name]
    
    def __init__(self, name="ArenaRankingBot"):
        # Inicializa apenas uma vez
        if getattr(self, "_initialized", False):
            return
            
        # Configurar logger
        self.logger = logging.getLogger(name)
        self.logger.setLevel(logging.INFO)
        self.logger.propagate = False  # Evita propagação para logger pai
        
        # Remover handlers antigos se existirem
        if self.logger.handlers:
            self.logger.handlers.clear()
        
        # Garantir que o diretório de logs exista
        os.makedirs("logs", exist_ok=True)
        
        # Criar manipulador de arquivo com encoding UTF-8
        log_filename = f"logs/{datetime.now().strftime('%Y-%m-%d')}.log"
        file_handler = logging.FileHandler(log_filename, encoding='utf-8')
        
        # Criar manipulador de console
        console_handler = logging.StreamHandler()
        
        # Definir formato
        formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        file_handler.setFormatter(formatter)
        console_handler.setFormatter(formatter)
        
        # Adicionar manipuladores ao logger
        self.logger.addHandler(file_handler)
        self.logger.addHandler(console_handler)
        
        self._initialized = True
    
    def info(self, message):
        try:
            self.logger.info(message)
        except UnicodeEncodeError:
            # Fallback para caracteres problemáticos
            self.logger.info(message.encode('ascii', 'replace').decode('ascii'))
    
    def error(self, message, exc_info=True):
        try:
            self.logger.error(message, exc_info=exc_info)
        except UnicodeEncodeError:
            self.logger.error(message.encode('ascii', 'replace').decode('ascii'), exc_info=exc_info)
    
    def warning(self, message):
        try:
            self.logger.warning(message)
        except UnicodeEncodeError:
            self.logger.warning(message.encode('ascii', 'replace').decode('ascii'))
    
    def debug(self, message):
        try:
            self.logger.debug(message)
        except UnicodeEncodeError:
            self.logger.debug(message.encode('ascii', 'replace').decode('ascii'))