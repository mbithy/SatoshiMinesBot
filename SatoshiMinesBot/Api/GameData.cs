namespace SatoshiMinesBot.Api
{
    public class GameData
    {
        public string status { get; set; }
        public int id { get; set; }
        public string game_hash { get; set; }
        public string secret { get; set; }
        public double bet { get; set; }
        public double stake { get; set; }
        public double next { get; set; }
        public string betNumber { get; set; }
        public string gametype { get; set; }
        public int num_mines { get; set; }
        public string message { get; set; }
    }
}