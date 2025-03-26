// Importing dependencies
const express = require('express');
const { MongoClient } = require('mongodb');
const cors = require('cors');

const app = express();

// Enable CORS to allow access from different origins
app.use(cors());
// Support for JSON
app.use(express.json());

// MongoDB Atlas connection URI
const uri = 'mongodb+srv://Crazzy:qyZj3aHYhsi86Gzy@arenarank.1gphd.mongodb.net/arena_rank?retryWrites=true&w=majority';
const client = new MongoClient(uri);
let db;

// Connect to MongoDB
async function connectDB() {
  try {
    await client.connect();
    console.log('Connected to MongoDB');
    db = client.db('arena_ranking');
  } catch (error) {
    console.error('Error connecting to MongoDB:', error);
  }
}

connectDB();

// Route to get players
app.get('/api/players', async (_req, res) => {
  try {
    // Get collections
    const playersCollection = db.collection('players');
    const systemInfoCollection = db.collection('bot_settings');
    
    // Fetch all players
    /*const players = await playersCollection.find({})
      .project({ nome: 1, riot_id: 1, puuid: 1, mmr_atual: 1, wins: 1, losses: 1, delta_mmr: 1 })
      .sort({ mmr_atual: -1 })
      .toArray();*/
      
      
      // Get only players that have auto_check enabled
      const players = await playersCollection.find({ auto_check: true })
      .project({ nome: 1, riot_id: 1, puuid: 1, mmr_atual: 1, wins: 1, losses: 1, delta_mmr: 1, icone_id: 1, ultima_atualizacao: 1 })
      .sort({ mmr_atual: -1 })
      .toArray();
      
      console.log("Players found:", players.length);
    // Get system info
    const systemInfo = await systemInfoCollection.findOne({ config_id: 1 });
    const ultimaAtualizacao = systemInfo ? systemInfo.ultima_atualizacao : new Date();
    
    // Format player data
    const formattedPlayers = players.map((player, index) => {
      const totalGames = player.wins + player.losses;
      const winrate = totalGames > 0 ? Math.round((player.wins / totalGames) * 100) : 0;
      
      let streak = { tipo: 'neutro', valor: 0 };
      if (player.delta_mmr > 0) {
        streak = { tipo: 'vitoria', valor: Math.min(Math.round(player.delta_mmr / 20), 10) };
      } else if (player.delta_mmr < 0) {
        streak = { tipo: 'derrota', valor: Math.min(Math.round(Math.abs(player.delta_mmr) / 20), 10) };
      }
      
      return {
        colocacao: index + 1,
        puuid: player.puuid,
        nome: player.nome,
        riot_id: player.riot_id,
        pontuacao: player.mmr_atual,
        wins: player.wins,
        losses: player.losses,
        winrate: winrate,
        icone_id: player.icone_id || 29,
        streak: streak,
        delta_mmr: player.delta_mmr || 0,
        ultima_atualizacao: player.ultima_atualizacao
      };
    });
    
    res.json({
      players: formattedPlayers,
      sistema: {
        ultima_atualizacao: ultimaAtualizacao,
        total_jogadores: formattedPlayers.length
      }
    });
  } catch (err) {
    console.error('Error fetching players:', err);
    res.status(500).json({ message: 'Error fetching players' });
  }
});

// Route for system information
app.get('/api/system', async (_req, res) => {
  try {
    const systemInfoCollection = db.collection('bot_settings');
    const systemInfo = await systemInfoCollection.findOne({ config_id: 1 });
    res.json({
      ultima_atualizacao: systemInfo ? systemInfo.ultima_atualizacao : new Date(),
      status: 'online'
    });
  } catch (err) {
    res.status(500).json({ message: 'Error fetching system information' });
  }
});

// Running the server on port 3000
const port = process.env.PORT || 3000;
app.listen(port, () => {
  console.log(`Server running on port ${port}`);
});

// To run the file, execute the command "node api.js" in the terminal
