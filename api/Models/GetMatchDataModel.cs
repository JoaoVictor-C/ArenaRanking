namespace api.Models;

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
            public string placement { get; set; }
            public string riotIdGameName { get; set; }
            public string riotIdTagline { get; set; }
            public string championId { get; set; }
        }
    }
}
