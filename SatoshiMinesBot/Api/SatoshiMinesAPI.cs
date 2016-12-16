using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;

// ReSharper disable AssignNullToNotNullAttribute

namespace SatoshiMinesBot.Api
{
    public class SatoshiMinesAPI
    {
        private HttpWebRequest _httpRequests;

        public GameData Data { get; private set; }
        public string PlayerHash { get; private set; }
        private DepositData _lastDepositData = null;

        public SatoshiMinesAPI(string hash)
        {
            PlayerHash = hash;
        }

        public void NewGame(int mines, decimal betSatoshi)
        {
            try
            {
                PrepRequest("https://satoshimines.com/action/newgame.php");
                var newGameresponce =Encoding.UTF8.GetBytes($"player_hash={PlayerHash}&bet={(betSatoshi/1000000).ToString("0.000000", new CultureInfo("en-US"))}&num_mines={mines}");
                var responce = GetPostResponce(newGameresponce);
                Data = Deserialize<GameData>(WebUtility.HtmlDecode(responce));
            }
            catch
            {
                Data = null;
            }
        }

        public BetData Bet(int squaer)
        {
            try
            {
                PrepRequest("https://satoshimines.com/action/checkboard.php");
                var betResponce =Encoding.UTF8.GetBytes($"game_hash={Data.game_hash}&guess={squaer}&v04=1");
                var responce = GetPostResponce(betResponce);
                return Deserialize<BetData>(responce);
            }
            catch
            {
                return null;
            }
        }

        public CashOutData CashOut()
        {
            try
            {
                PrepRequest("https://satoshimines.com/action/cashout.php");
                var cashoutResponce =Encoding.UTF8.GetBytes($"game_hash={Data.game_hash}");
                var responce = GetPostResponce(cashoutResponce);
                return Deserialize<CashOutData>(responce);
            }
            catch
            {
                return null;
            }
        }

        public DepositData GetDepositData()
        {
            if (_lastDepositData != null && _lastDepositData.status == "success")
                return _lastDepositData;
            try
            {
                PrepRequest("https://satoshimines.com/action/getaddr.php");
                var withdrawResponce =Encoding.UTF8.GetBytes($"secret={PlayerHash}");
                var responce = GetPostResponce(withdrawResponce);
                return Deserialize<DepositData>(responce);
            }
            catch
            {
                return null;
            }
        }

        public void TryWithdraw(string btcAddr, int ammountSatoshi)
        {
            try
            {
                PrepRequest("https://satoshimines.com/action/full_cashout.php");
                var withdrawResponce =Encoding.UTF8.GetBytes($"secret={PlayerHash}&payto_address={btcAddr}&amount={(ammountSatoshi/1000000).ToString("0.000000", new CultureInfo("en-US"))}");
                GetPostResponce(withdrawResponce);
            }
            catch
            {

            }
        }

        private void PrepRequest(string url)
        {
            _httpRequests = (HttpWebRequest) WebRequest.Create(url);
            _httpRequests.Method = "POST";
            _httpRequests.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
        }

        private string GetPostResponce(byte[] post)
        {
            _httpRequests.ContentLength = post.Length;
            using (var nStream = _httpRequests.GetRequestStream())
            {
                nStream.Write(post, 0, post.Length);
                nStream.Flush();
            }
            var hhtpResounce = (HttpWebResponse) _httpRequests.GetResponse();
            var responceStream = new StreamReader(hhtpResounce.GetResponseStream());
            var responce = responceStream.ReadToEnd();
            return responce;
        }

        //https://gist.github.com/PatPositron/10076559
        private static T Deserialize<T>(string json)
        {
            var s = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T) s.ReadObject(ms);
            }
        }
    }
}
