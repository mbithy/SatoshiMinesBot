using System;
using System.Collections.Generic;
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
using sminesdb.Entities;
using sminesdb.Entities.Principal;
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

        private decimal _baseBet;
        private decimal _currentBet,_beforePreroll;
        private decimal _maxBet;
        private double _originalBalance;
        private double _currentBalance;
        private double _profit,_profitThreshhold,_profitWithdrawn;
        private double _maxGuesses;
        private double _game;
        private double _multiplier;
        private double _currentGuesses;
        private double _wins;
        private double _losses,_elapsedTime;
        private string _playerHash,_title,_message,_withdrawAddress;
        private bool _gameIsStop;
        private bool _isError;
        private bool _isPreroll;
        private bool _isPractice;
        private string _bdval,_lastSent,_lastResponse;
        private HttpWebRequest _httpRequest;
        private GameData _gameData;
        private const float ConvertMultiplier = 1000000;
        private readonly Regex _rBdVal = new Regex("var bdval = '(\\d+)'");
        private readonly DispatcherTimer _dispatcherTimer= new DispatcherTimer();
        private readonly List<int>_pickedNumbers= new List<int>();
        private BetData _bd;
        private readonly Random _random= new Random();
        private readonly gameDataContext _gameDataContext= new gameDataContext();
        private SatoshiMinesAPI _satoshiMinesAPI;

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
            return PickTile(13)[_random.Next(0,12)];
        }

        public MainWindow()
        {
            InitializeComponent();
            AmountWithdrawn.LabelFormatter = BalanceGauge.LabelFormatter = ProfitGauge.LabelFormatter = x => "uB" + x.ToString("0.0", new CultureInfo("en-US"));
            _dispatcherTimer.Interval=TimeSpan.FromSeconds(1);
            _dispatcherTimer.Tick += _dispatcherTimer_Tick;

            PlayerHash.Text = "Your player hash";
            BetAmount.Text = "30";
            NumberOfGuesses.Text = "1";
            Multiplier.Text = "25.3";
            MaxBetAmount.Text = "19500";
            WithdrawAddress.Text = "Your withdrawal address";
            WithdrawAmount.Text = "5000";
        }

        private void _dispatcherTimer_Tick(object sender, EventArgs e)
        {
            _elapsedTime += 1;
            var x = _wins + _losses;
            ElapsedTime.Text = "GP: " + x + " Time: " + State(_elapsedTime);
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

        #region Begin
        private void Begin_Click(object sender, RoutedEventArgs e)
        {
            _playerHash = PlayerHash.Text.Trim();
            Getbdval();
            _satoshiMinesAPI= new SatoshiMinesAPI(_playerHash);
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
#endregion

        #region Game Settings
        private void StartGame()
        {
            _isError = false;
            Stop.IsEnabled = true;
            Begin.IsEnabled = false;
            _currentBalance = _originalBalance = 0;
            _baseBet=_currentBet = decimal.Parse(BetAmount.Text.Trim());
            _gameIsStop = false;
            _profit = 0;
            _profitWithdrawn = 0;
            _currentBalance = _originalBalance = GetBalance().balance*ConvertMultiplier;
            if (_currentBalance == 0)
            {
                _isPractice = true;
                _originalBalance = _currentBalance = 25000;
            }
            else
            {
                _isPractice = false;
            }
            NewGame();
            _dispatcherTimer.Start();
            _elapsedTime = 0;
        }

        private void BasicSettings()
        {
            Dispatcher.Invoke(() =>
            {
                _baseBet = decimal.Parse(BetAmount.Text.Trim());
                _multiplier = double.Parse(Multiplier.Text);
                _maxGuesses = double.Parse(NumberOfGuesses.Text);
                _maxBet = decimal.Parse(MaxBetAmount.Text.Trim());
                _profitThreshhold = double.Parse(WithdrawAmount.Text.Trim());
                _withdrawAddress = WithdrawAddress.Text.Trim();
                _game = Game();
            });
            
        }
        private void UpdateGauges()
        {
            Dispatcher.Invoke(() =>
            {
                BalanceGauge.Value = _currentBalance;
                while (BalanceGauge.To <= BalanceGauge.Value)
                {
                    BalanceGauge.To *= 2;
                }
                ProfitGauge.Value = _profit * ConvertMultiplier;
                ProfitGauge.Value += _profitWithdrawn;
                while (ProfitGauge.To <= ProfitGauge.Value)
                {
                    ProfitGauge.To *= 2;
                }

                if (_originalBalance == 0)
                {
                    _originalBalance = 1;
                }

                if (_profitWithdrawn > 0)
                {
                    AmountWithdrawn.Value = _profitWithdrawn;
                    while (AmountWithdrawn.To <= AmountWithdrawn.Value)
                    {
                        AmountWithdrawn.To *= 2;
                    }
                }

                if (_isError || _gameIsStop)
                {
                    ShowMessage(_title, _message);
                    Stop.IsEnabled = false;
                    Begin.IsEnabled = true;
                    _dispatcherTimer.Stop();
                }
            });
        }

        private double Game()
        {
            if (OneMine.IsChecked.Value)
            {
                return 1;
            }
            else if (ThreeMines.IsChecked.Value)
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
            _dispatcherTimer.Stop();
        }

        #endregion

        private void NewGame()
        {
            UpdateGauges();
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
                StopGame();
            }
        }


        private static async void ShowMessage(string title,string msg)
        {
            var message = new MessageDialog(title,msg);
            await DialogHost.Show(message, "AppRootDialog");
        }

        #region Game Prep
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
        #endregion

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

        #region Game Response

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
                    OnLoss(bal);
                }
                else
                {
                    OnWin();
                }
            }
            catch (Exception ex)
            {
                _gameIsStop = true;
                _isError = true;
                _title = "Failled to place bet";
                _message = ex.Message;
                StopGame();
            }
        }

        private void OnWin()
        {
            var betSquare = NextTile();
            while (IsPicked(betSquare))
            {
                betSquare = NextTile();
            }
            _pickedNumbers.Add(betSquare);
            if (_currentGuesses >= _maxGuesses)
            {
                if (!_isPreroll && _currentBet > 0)
                {
                    _currentBet = _baseBet;
                    NextGameOnWin();
                }
                else if (_isPreroll && _currentBet == 0)
                {
                    _currentBet = _beforePreroll;
                    _isPreroll = false;
                    NextGameOnWin();
                }
            }
            else
            {
                _currentGuesses++;
                PrepareRequest("https://satoshimines.com/action/checkboard.php");
                var betResponce = Bcodes("game_hash={0}&guess={1}&v04=1" + _bdval, _gameData.game_hash, betSquare);
                GetPostResponse(betResponce, EndBetResponce);
            }
        }

        private void OnLoss(BalanceData bal)
        {
            _losses++;
            _currentGuesses = 0;
            _isPreroll = true;
            var lss = new CashOutData
            {
                game_id = _gameData.id.ToString(),
                outcome = "LOSS",
                win = 0,
                wins = ((_wins/(_wins + _losses))*100).ToString("##.##"),
            };
            foreach (var pickedNumber in _pickedNumbers)
            {
                lss.mines += pickedNumber;
            }
            _pickedNumbers.Clear();
            AddToView(lss);

            if (_isPractice)
            {
                _currentBalance -= double.Parse((_currentBet).ToString("0.000000", new CultureInfo("en-US")));
                _profit -= double.Parse((_currentBet/(decimal) ConvertMultiplier).ToString("0.000000", new CultureInfo("en-US")));
            }

            if (!_gameIsStop)
            {
                if (_currentBet > 0)
                {
                    _currentBet = _currentBet*(decimal) _multiplier;
                    _beforePreroll = _currentBet;
                }

                if (_currentBet > 0)
                {
                    _currentBet = 0;
                    NewGame();
                }
                else
                {
                    if (_currentBet <= (decimal) bal.balance || bal.balance == 0)
                    {
                        if (_currentBet > _maxBet)
                        {
                            _currentBet = _baseBet;
                        }
                        NewGame();
                    }
                    else
                    {
                        _gameIsStop = true;
                        _isError = true;
                        _title = "Betting Stopped";
                        _message = "The current bet is bigger than balance";
                    }
                }
            }
            else
            {
                _gameIsStop = true;
                _isError = true;
                _title = "Betting Stopped";
                _message = "The game was ended";
            }
        }

        #endregion

        private void NextGameOnWin()
        {
            if (_isPractice)
            {
                var stk = _bd.stake*ConvertMultiplier;
                _currentBalance += (stk - (double) _currentBet); // double.Parse(_currentBet.ToString("0.000000", new CultureInfo("en-US")));
                _profit += (stk - (double) _currentBet)/ConvertMultiplier; //  double.Parse(_currentBet.ToString("0.000000", new CultureInfo("en-US")));
            }
            _wins++;
            _pickedNumbers.Clear();
            PrepareRequest("https://satoshimines.com/action/cashout.php");
            var cashoutResponce = Bcodes("game_hash={0}", _gameData.game_hash);
            GetPostResponse(cashoutResponce, EndCashoutResponce);
        }


        private bool IsPicked(int b)
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

        #region Cash Out

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
                cd.gameLink = $"https://satoshimines.com/s/{cd.game_id}/{cd.random_string}/";
                cd.wins = ((_wins/(_wins + _losses))*100).ToString("##.##");
                cd.outcome = "WIN";

                AddToView(cd);

                if (!_isPractice)
                {
                    _currentBalance = GetBalance().balance*ConvertMultiplier;
                    _profit = (_currentBalance - _originalBalance)/ConvertMultiplier;
                }

                if ((_profit*ConvertMultiplier) >= _profitThreshhold)
                {
                    var withdrawResponse = _satoshiMinesAPI.TryWithdraw(_withdrawAddress, _profitThreshhold);
                    if (withdrawResponse.Contains("success"))
                    {
                        _profitWithdrawn += _profitThreshhold;
                        _profit = 0;
                    }
                }

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

        private void AddToView(CashOutData cd)
        {
            Dispatcher.Invoke(() =>
            {
                PreviousGames.Items.Add(cd);
                if (PreviousGames.Items.Count > 5)
                {
                    PreviousGames.Items.RemoveAt(0);
                }
                var dbCashOut = new Cashout
                {
                    GameId = cd.game_id,
                    GameLink = cd.gameLink,
                    Id = _gameDataContext.Cashout.NextId(),
                    LastUpdate = DateTime.Now.ToFileTime(),
                    Mines = cd.mines,
                    RandomString = cd.random_string,
                    Win = (long)cd.win,
                    BalanceAfter = (GetBalance().balance * ConvertMultiplier).ToString("0.0", new CultureInfo("en-US"))
                };
                _gameDataContext.Cashout.Insert(dbCashOut);
            });
        }

        #endregion

        private String State(double point)
        {
            var state = TimeSpan.FromSeconds(point);
            if (state.Days > 0)
            {
                return state.Days.ToString("00") + ":" + state.Hours.ToString("00") + ":" + state.Minutes.ToString("00") + ":" + state.Seconds.ToString("00");
            }
            else if (state.Hours > 0)
            {
                return state.Hours.ToString("00") + ":" + state.Minutes.ToString("00") + ":" + state.Seconds.ToString("00");
            }
            else if (state.Minutes > 0)
            {
                return state.Minutes.ToString("00") + ":" + state.Seconds.ToString("00");
            }
            else
            {
                return state.Seconds.ToString("00");
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

