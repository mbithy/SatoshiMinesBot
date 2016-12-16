namespace SatoshiMinesBot.Api
{
    public class BetData
    {
        public string status { get; set; }
        public int guess { get; set; }
        public string outcome { get; set; }
        public double stake { get; set; }
        public double next { get; set; }
        public string message { get; set; }
        public double change { get; set; }
        public string bombs { get; set; }
    }
    public class BalanceData
    {
        public string status { get; set; }
        public float balance { get; set; }
    }
}