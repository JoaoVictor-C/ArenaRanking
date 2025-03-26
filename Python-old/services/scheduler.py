import time
import schedule
import threading
from datetime import datetime
from logs.logger import Logger

class TaskScheduler:
    """
    Classe responsável pelo agendamento e gerenciamento de tarefas periódicas.
    """
    def __init__(self):
        self.logger = Logger("Scheduler")
        self.running = False
        self.scheduler_thread = None
        self.tasks = {}
    
    def add_task(self, name, interval_minutes, task_function, *args, **kwargs):
        """
        Adiciona uma nova tarefa ao agendador.
        
        Args:
            name: Nome da tarefa (para registro e referência)
            interval_minutes: Intervalo em minutos para execução da tarefa
            task_function: Função a ser executada
            *args, **kwargs: Argumentos para a função da tarefa
        """
        self.logger.info(f"Adicionando tarefa '{name}' para executar a cada {interval_minutes} minutos")
        
        def wrapper_task():
            try:
                self.logger.info(f"Executando tarefa agendada: {name}")
                start_time = datetime.now()
                task_function(*args, **kwargs)
                elapsed = (datetime.now() - start_time).total_seconds()
                self.logger.info(f"Tarefa '{name}' concluída em {elapsed:.2f} segundos")
            except Exception as e:
                self.logger.error(f"Erro ao executar tarefa '{name}': {str(e)}")
        
        # Configurar a tarefa no schedule
        self.tasks[name] = schedule.every(interval_minutes).minutes.do(wrapper_task)
        return self.tasks[name]
    
    def start(self):
        """
        Inicia o agendador em uma thread separada.
        """
        if self.running:
            self.logger.warning("O agendador já está em execução")
            return False
        
        self.running = True
        self.scheduler_thread = threading.Thread(target=self._run_scheduler, daemon=True)
        self.scheduler_thread.start()
        self.logger.info("Agendador iniciado em thread separada")
        return True
    
    def _run_scheduler(self):
        """
        Método interno que executa o loop do agendador.
        """
        self.logger.info("Loop do agendador iniciado")
        while self.running:
            try:
                schedule.run_pending()
                time.sleep(1)
            except Exception as e:
                self.logger.error(f"Erro no loop do agendador: {str(e)}")
                time.sleep(5)  # Esperar um pouco mais antes de tentar novamente
    
    def stop(self):
        """
        Para o agendador.
        """
        if not self.running:
            self.logger.warning("O agendador não está em execução")
            return
            
        self.logger.info("Parando o agendador...")
        self.running = False
        if self.scheduler_thread:
            self.scheduler_thread.join(timeout=5)
        self.logger.info("Agendador parado")
    
    def execute_task_now(self, task_name):
        """
        Executa uma tarefa agendada imediatamente.
        
        Args:
            task_name: Nome da tarefa a ser executada
        
        Returns:
            bool: True se a tarefa foi executada, False caso contrário
        """
        if task_name in self.tasks:
            self.logger.info(f"Executando tarefa '{task_name}' manualmente")
            try:
                schedule.run_job(self.tasks[task_name])
                return True
            except Exception as e:
                self.logger.error(f"Erro ao executar tarefa '{task_name}' manualmente: {str(e)}")
                return False
        else:
            self.logger.warning(f"Tarefa '{task_name}' não encontrada")
            return False