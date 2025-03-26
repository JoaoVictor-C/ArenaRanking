from typing import List, Optional
from datetime import datetime
from pymongo.collection import Collection
from bson import ObjectId

class Player:
   """
   Represents a player entity in the arena-ranking system.
   """
   
   def __init__(self, 
             puuid: str = "",
             riot_id: str = "",
             nome: str = "",
             mmr_atual: int = 1000,
             date_added: Optional[datetime] = None,
             ultimo_match_id_processado: Optional[str] = None,
             wins: int = 0,
             losses: int = 0,
             auto_check: bool = False,
             delta_mmr: int = 0,
             _id: Optional[ObjectId] = None):
      """
      Initialize a player entity.
      
      Args:
         puuid: Player's unique identifier from Riot API
         riot_id: Player's Riot ID 
         nome: Player's display name
         mmr_atual: Current MMR (Matchmaking Rating)
         date_added: Date when player was added to the database
         ultimo_match_id_processado: ID of the last processed match
         wins: Number of wins
         losses: Number of losses
         auto_check: Whether the player is automatically checked for updates
         _id: MongoDB's ObjectId (optional)
      """
      self.puuid = puuid
      self.riot_id = riot_id
      self.nome = nome
      self.mmr_atual = mmr_atual
      self.date_added = date_added if date_added else datetime.utcnow()
      self.ultimo_match_id_processado = ultimo_match_id_processado
      self.wins = wins
      self.losses = losses
      self.auto_check = auto_check
      self.delta_mmr = delta_mmr
      self._id = _id

   @property
   def total_games(self) -> int:
      """Total number of games played."""
      return self.wins + self.losses
   
   @property
   def win_rate(self) -> float:
      """Win rate as a percentage."""
      if self.total_games == 0:
         return 0.0
      return (self.wins / self.total_games) * 100
   
   @classmethod
   def from_document(cls, doc: dict) -> 'Player':
      """Create a Player instance from a MongoDB document."""
      return cls(
         puuid=doc.get('puuid', ''),
         riot_id=doc.get('riot_id', ''),
         nome=doc.get('nome', ''),
         mmr_atual=doc.get('mmr_atual', 1000),
         date_added=doc.get('date_added'),
         ultimo_match_id_processado=doc.get('ultimo_match_id_processado'),
         wins=doc.get('wins', 0),
         losses=doc.get('losses', 0),
         auto_check=doc.get('auto_check', False),
         delta_mmr=doc.get('delta_mmr', 0),
         _id=doc.get('_id')
      )
   
   def to_document(self) -> dict:
      """Convert the Player instance to a MongoDB document."""
      doc = {
         'puuid': self.puuid,
         'riot_id': self.riot_id,
         'nome': self.nome,
         'mmr_atual': self.mmr_atual,
         'date_added': self.date_added,
         'ultimo_match_id_processado': self.ultimo_match_id_processado,
         'wins': self.wins,
         'losses': self.losses,
         'auto_check': self.auto_check,
         'delta_mmr': self.delta_mmr,
         }
      if self._id:
         doc['_id'] = self._id
      return doc