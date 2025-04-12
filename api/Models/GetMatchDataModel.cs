namespace ArenaBackend.Models;

public class GetMatchDataModel
{
    public Metadados metadata { get; set; }
    public Info info { get; set; }
    public class Metadados
    {
        public List<string> participants { get; set; }
    }

    public class Info
    {
        public string gameMode { get; set; }
        public string gameCreation { get; set; }
        public List<ParticipantesInfo> participants { get; set; }

        public class ParticipantesInfo
        {
            public int placement { get; set; }
            public string riotIdGameName { get; set; }
            public string riotIdTagline { get; set; }
            public int championId { get; set; }
            public string championName { get; set; }
            public string puuid { get; set; }
            public int profileIcon { get; set; }
            public int playerAugment1 { get; set; }
            public int playerAugment2 { get; set; }
            public int playerAugment3 { get; set; }

            public int playerAugment4 { get; set; }
            public int playerAugment5 { get; set; }
            public int playerAugment6 { get; set; }

            public int item0 { get; set; }
            public int item1 { get; set; }
            public int item2 { get; set; }
            public int item3 { get; set; }
            public int item4 { get; set; }
            public int item5 { get; set; }
            public int item6 { get; set; }
            public int kills { get; set; }
            public int deaths { get; set; }
            public int assists { get; set; }
            public int totalDamageDealt { get; set; }
        }
    }
}
