

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using SatoshiMinesBot.Api;

namespace SatoshiMinesBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {

        private static readonly int[] GameTiles =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
        };

        private decimal _baseBet,_currentBet;
        private double _originalBalance,_currentBalance, _profit, _growth,_maxGuesses,_game,_multiplier,_currentGuesses,_wins,_losses;
        private string _playerHash,_title,_message;
        private bool _gameIsStop,_isError,_wasLoss;
        private string _bdval,_lastSent,_lastResponse;
        private HttpWebRequest _httpRequest;
        private GameData _gameData;
        const float ConvertMultiplier = 1000000;
        private readonly Regex _rBdVal = new Regex("var bdval = '(\\d+)'");
        //private int _previousPick;
        readonly DispatcherTimer _dispatcherTimer= new DispatcherTimer();
        private List<int>_pickedNumbers= new List<int>();
        BetData _bd;

        private static int[] PickTile(int length = 1)
        {
            var identifier = new int[length];
            var randomData = new byte[length];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomData);
            }

            for (var idx = 0; idx < identifier.Length; idx++)
            {
                var pos = randomData[idx]%GameTiles.Length;
                identifier[idx] = GameTiles[pos];
            }

            return identifier;
        }

        private int NextTile()
        {
            return PickTile(13)[6];
        }

        public MainWindow()
        {
            InitializeComponent();
            Growth.LabelFormatter = x => x.ToString("##.##") + "%";
            BalanceGauge.LabelFormatter = ProfitGauge.LabelFormatter = x => "uB" + x.ToString("0.0", new CultureInfo("en-US"));
            _dispatcherTimer.Interval=TimeSpan.FromSeconds(1);
            _dispatcherTimer.Tick += _dispatcherTimer_Tick;
            //PreviousGames.ItemsSource = _cashOut;
            PlayerHash.Text = "Your hash";
            BetAmount.Text = "30";
            NumberOfGuesses.Text = "1";
            Multiplier.Text = "25.3";
        }

        private void _dispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateGauges();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }


        private void Stop_OnClick(object sender, RoutedEventArgs e)
        {
            StopGame();
        }

        private void Begin_Click(object sender, RoutedEventArgs e)
        {
            _playerHash = PlayerHash.Text.Trim();
            Getbdval();
        }
        private void Getbdval()
        {
            using (var bdvalReq = new WebClient())
            {
                bdvalReq.DownloadStringCompleted += BdvalReq_DownloadStringCompleted;
                bdvalReq.DownloadStringTaskAsync($"https://www.satoshimines.com/play/{_playerHash}");
            }
        }


        private void BdvalReq_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                var reqVal = e.Result;
                var bdMatch = _rBdVal.Match(reqVal);
                if (!bdMatch.Success)
                {
                    throw new Exception("Regex failed to match.");
                }
                var bdvalStr = bdMatch.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(bdvalStr))
                {
                    throw new Exception("Invalid bdvalue.");
                }
                _bdval = $"&bd={bdvalStr}";
                StartGame();
            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                if (ex.InnerException != null)
                {
                    ShowMessage("Failled to get bdval: " + _bdval, ex.Message + "\nInner: " + ex.InnerException.Message);
                    StopGame();
                }
                else
                {
                    StartGame();
                }
            }
        }

        private void StartGame()
        {
            Stop.IsEnabled = true;
            Begin.IsEnabled = false;
            _baseBet=_currentBet = decimal.Parse(BetAmount.Text.Trim());
            _gameIsStop = false;
            _growth = 0;
            _profit = 0;
            _currentBalance = _originalBalance = (GetBalance().balance*ConvertMultiplier);
            NewGame();
            _dispatcherTimer.Start();
        }

        private void BasicSettings()
        {
            Dispatcher.Invoke(() =>
            {
                _baseBet = decimal.Parse(BetAmount.Text.Trim());
                _multiplier = double.Parse(Multiplier.Text);
                _maxGuesses = double.Parse(NumberOfGuesses.Text);
                _game = Game();
            });
            
        }
        private void NewGame()
        {
            //UpdateGauges();
            BasicSettings();
            _currentGuesses = 0;
            PrepareRequest("https://satoshimines.com/action/newgame.php");
            var bet = _currentBet / (decimal)ConvertMultiplier;
            var newGameresponce = Bcodes("player_hash={0}&bet={1}&num_mines={2}" + _bdval, _playerHash, bet.ToString("0.000000", new CultureInfo("en-US")), _game);

            if (!_gameIsStop)
            {
                GetPostResponse(newGameresponce, EndNewGameResponce);
            }
            else
            {
                //ShowMessage("Betting Stoped", "The game was ended.");
            }
        }

        private void UpdateGauges()
        {
            BalanceGauge.Value = _currentBalance;
            while (BalanceGauge.To <= BalanceGauge.Value)
            {
                BalanceGauge.To *= 2;
            }
            ProfitGauge.Value = _profit * ConvertMultiplier;// _convertMultiplier;
            while (ProfitGauge.To <= ProfitGauge.Value)
            {
                ProfitGauge.To *= 2;
            }
            
            if (_originalBalance == 0)
            {
                _originalBalance = 1;
            }
            if (_currentBalance > 0)
            {
                Growth.Value = ((_currentBalance - _originalBalance)/_originalBalance)*100;
                while (Growth.To<=Growth.Value)
                {
                    Growth.To *= 2;
                }
            }

            if (_isError ||_gameIsStop)
            {
                ShowMessage(_title,_message);
                Stop.IsEnabled = false;
                Begin.IsEnabled = true;
                _dispatcherTimer.Stop();
            }

        }

        private double Game()
        {
            if (OneMine.IsChecked.Value)
            {
                return 1;
            }
            else if(ThreeMines.IsChecked.Value)
            {
                return 3;
            }
            else if (FiveMines.IsChecked.Value)
            {
                return 5;
            }
            else if (TwentyFourMines.IsChecked.Value)
            {
                return 24;
            }
            else
            {
                return 1;
            }
        }

        private void StopGame()
        {
            _gameIsStop = true;
            _isError = true;
            _title = "Betting Stopped";
            _message = "The game was ended";
            //_dispatcherTimer.Stop();
        }

        private static async void ShowMessage(string title,string msg)
        {
            var message = new SatoshiMinesBot.MessageDialog(title,msg);
            await DialogHost.Show(message, "AppRootDialog");
        }

        private void PrepareRequest(string url)
        {
            _httpRequest = (HttpWebRequest)WebRequest.Create(url);
            _httpRequest.Method = "POST";
            _httpRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            _httpRequest.UserAgent ="Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.132 Safari/537.36";
        }

        private byte[] Bcodes(string c, params object[] formatting)
        {
            var ret = Encoding.UTF8.GetBytes(string.Format(c, formatting));
            formatting[0] = "ProtectHash";
            _lastSent = string.Format(c, formatting);
            return ret;
        }

        private void GetPostResponse(byte[] post, AsyncCallback call)
        {
            _httpRequest.ContentLength = post.Length;
            var result = _httpRequest.BeginGetRequestStream(EndGetRequestStream, new object[] { post, call });
        }

        private void EndGetRequestStream(IAsyncResult asyncResult)
        {
            try
            {
                var nStream = _httpRequest.EndGetRequestStream(asyncResult);
                var data = (object[])asyncResult.AsyncState;
                var post = (byte[])data[0];
                var call = (AsyncCallback)data[1];
                nStream.Write(post, 0, post.Length);
                nStream.Flush();
                _httpRequest.BeginGetResponse(call, null);
            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                _isError = true;
                _title = "Failled to start new game";
                _message = ex.Message;
                StopGame();
                //ShowMessage("Failled to start new game",ex.Message);
            }
        }
        private static T Deserialize<T>(string json)
        {
            var s = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)s.ReadObject(ms);
            }
        }

        private void EndNewGameResponce(IAsyncResult ar)
        {
            try
            {

                var httpResponce = (HttpWebResponse)_httpRequest.EndGetResponse(ar);
                using (var sr = new StreamReader(httpResponce.GetResponseStream()))
                {
                    _lastResponse = sr.ReadToEnd();
                    _gameData = Deserialize<GameData>(_lastResponse);
                }
                httpResponce.Close();

                if (_gameData == null)
                    throw new Exception("Deserialize failed, object is null.");
                if (_gameData.status != "success")
                    throw new Exception("Json Error: " + _gameData.message);
                var betSquare = NextTile();
                _pickedNumbers.Add(betSquare);
                _currentGuesses++;
                PrepareRequest("https://satoshimines.com/action/checkboard.php");
                var betResponce =Bcodes("game_hash={0}&guess={1}&v04=1" + _bdval, _gameData.game_hash, betSquare);
                GetPostResponse(betResponce, EndBetResponce);
            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                _isError = true;
                _title = "Failled to start new game";
                _message = ex.Message;
                StopGame();
            }
        }

        private void EndBetResponce(IAsyncResult asyncResult)
        {
            try
            {
                var httpResponce = (HttpWebResponse) _httpRequest.EndGetResponse(asyncResult);
                using (var sr = new StreamReader(httpResponce.GetResponseStream()))
                {
                    _bd = Deserialize<BetData>(sr.ReadToEnd());
                }
                if (_bd == null || _bd.status != "success")
                    throw new Exception();


                var bal = GetBalance();
                bal.balance = bal.balance*ConvertMultiplier;

                if (_bd.outcome == "bomb")
                {
                    _wasLoss = true;
                    _losses++;
                    _currentGuesses = 0;
                    //_currentBalance -= double.Parse((_currentBet / (decimal)ConvertMultiplier).ToString("0.000000", new CultureInfo("en-US")));
                    //_profit -= double.Parse((_currentBet/(decimal)ConvertMultiplier).ToString("0.000000", new CultureInfo("en-US")));
                    if (!_gameIsStop)
                    {
                        _currentBet = _currentBet*(decimal)_multiplier;
                        if (_currentBet <= decimal.Parse(bal.balance.ToString("0.000000", new CultureInfo("en-US"))) || bal.balance == 0)
                        {
                            NewGame();
                        }
                        else
                        {
                            _gameIsStop = true;
                            _isError = true;
                            _title = "Betting Stopped";
                            _message = "The current bet is bigger than balance";
                            //ShowMessage("Betting Stoped", "The current bet is bigger than balance");
                        }
                    }
                    else
                    {
                        //ShowMessage("Betting Stoped", "The game was ended.");
                        _gameIsStop = true;
                        _isError = true;
                        _title = "Betting Stopped";
                        _message = "The game was ended";
                    }
                }
                else
                {

                    var betSquare = NextTile();
                    while (IsPicked(betSquare))
                    {
                        betSquare = NextTile();
                    }
                    _pickedNumbers.Add(betSquare);
                    if (_currentGuesses >= _maxGuesses)
                    {
                        _currentBet = _baseBet;
                        _wasLoss = false;
                        //var stk = _bd.stake*ConvertMultiplier;
                        //_currentBalance += (stk-(double)_currentBet)/ConvertMultiplier;// double.Parse(_currentBet.ToString("0.000000", new CultureInfo("en-US")));
                        //_profit += (stk - (double)_currentBet)/ConvertMultiplier;//  double.Parse(_currentBet.ToString("0.000000", new CultureInfo("en-US")));
                        _wins++;
                        _pickedNumbers.Clear();
                        PrepareRequest("https://satoshimines.com/action/cashout.php");
                        var cashoutResponce = Bcodes("game_hash={0}", _gameData.game_hash);
                        GetPostResponse(cashoutResponce, EndCashoutResponce);
                    }
                    else
                    {
                        _currentGuesses++;
                        PrepareRequest("https://satoshimines.com/action/checkboard.php");
                        var betResponce = Bcodes("game_hash={0}&guess={1}&v04=1" + _bdval, _gameData.game_hash, betSquare);
                        GetPostResponse(betResponce, EndBetResponce);
                    }

                }

            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                //ShowMessage("Failled to place bet", ex.Message);
                _isError = true;
                _title = "Failled to place bet";
                _message = ex.Message;
                StopGame();
                //PrepareRequest("https://satoshimines.com/action/checkboard.php");
                //int betSquare = NextTile();
                //byte[] betResponce = Bcodes("game_hash={0}&guess={1}&v04=1", _gameData.game_hash, betSquare);
                //GetPostResponse(betResponce, EndBetResponce);
            }
        }

        bool IsPicked(int b)
        {
            foreach (var pickedNumber in _pickedNumbers)
            {
                if (b == pickedNumber)
                {
                    return true;
                }
            }
            return false;
        }

        private void EndCashoutResponce(IAsyncResult asyncResult)
        {
            try
            {
                CashOutData cd;
                var httpResponce = (HttpWebResponse)_httpRequest.EndGetResponse(asyncResult);
                using (var sr = new StreamReader(httpResponce.GetResponseStream()))
                {
                    _lastResponse = sr.ReadToEnd();
                    cd = Deserialize<CashOutData>(_lastResponse);
                }
                httpResponce.Close();
                if (cd == null || cd.status != "success")
                {
                    throw new Exception();
                }
                //cd.game_link = $"https://satoshimines.com/s/{cd.game_id}/{cd.random_string}/";
                cd.wins = ((_wins/(_wins + _losses))*100).ToString("##.##");
                cd.outcome = !_wasLoss ? "WIN" : "LOSS";
                Dispatcher.Invoke(() =>
                {
                    PreviousGames.Items.Add(cd);
                    if (PreviousGames.Items.Count > 5)
                    {
                        PreviousGames.Items.RemoveAt(0);
                    }
                });
                _currentBalance = GetBalance().balance*ConvertMultiplier;
                _profit = (_currentBalance - _originalBalance)/ConvertMultiplier;
                NewGame();
            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                _isError = true;
                _title = "Failled to cashout";
                _message = ex.Message;
                StopGame();
            }
        }

       

        private BalanceData GetBalance()
        {
            string resp;
            string parameters = $"secret={_playerHash}";
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                resp = wc.UploadString(new Uri("https://satoshimines.com/action/refresh_balance.php"), "POST", parameters);
            }
            var bd = Deserialize<BalanceData>(resp);
            if (bd == null || bd.status != "success")
            {
                return null;
            }
            else
            {
                return bd;
            }
        }


    }
}

