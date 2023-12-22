using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SubNPhoenix.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SubNPhoenix
{
    public class MainWindowViewModel : ObservableObject
    {
        private string token;
        private int port;
        private int pid;
        private GameConnector _gameConnector;
        private readonly string _cmdPath = @"C:\Windows\System32\cmd.exe";
        private readonly string _excuteShell = "wmic PROCESS WHERE name='LeagueClientUx.exe' GET commandline";
        public const string ChampSelectChannel = @"/lol-champ-select/v1/session";
        public const string GameFlowChannel = @"/lol-gameflow/v1/gameflow-phase";
        public IEnumerable<Hero> heroes = new List<Hero>();

        /// <summary>
        /// 是否自动接收对局
        /// </summary>
        private bool _autoAcceptGame;
        public bool AutoAcceptGame
        {
            get => _autoAcceptGame;
            set
            {
                SetProperty(ref _autoAcceptGame, value);
                IniSettingsModel.Instance.WriteAutoAcceptGameAsync(value).GetAwaiter().GetResult();
            }
        }

        private bool _aramFastGetHero;
        public bool AramFastGetHero
        {
            get => _aramFastGetHero;
            set
            {
                SetProperty(ref _aramFastGetHero, value);
                IniSettingsModel.Instance.WriteAramFastGetHeroAsync(value).GetAwaiter().GetResult();
            }
        }

        
        /// <summary>
        /// 游戏状态
        /// </summary>
        private string _gameStatus;
        public string GameStatus
        {
            get => _gameStatus;
            set => SetProperty(ref _gameStatus, value);
        }
        public AsyncRelayCommand LoadCommandAsync { get; set; }
        public RelayCommand OpenAramFastGetHeroSettingDialogCommand { get; set; }
        public MainWindowViewModel()
        {
            LoadCommandAsync = new AsyncRelayCommand(LoadAsync);
            OpenAramFastGetHeroSettingDialogCommand = new RelayCommand(OpenAramFastGetHeroSettingDialog);
        }

        private async Task LoadAsync()
        {
            await IniSettingsModel.Instance.InitializeAsync();
            AutoAcceptGame = IniSettingsModel.Instance.AutoAcceptGame;
            AramFastGetHero = IniSettingsModel.Instance.AramFastGetHero;
            await ConnnectAsync();
            GameStatus = "已连接上游戏";
            _gameConnector.Subscribe(ChampSelectChannel, new EventHandler<EventArgument>(ChampSelect));
            _gameConnector.Subscribe(GameFlowChannel, new EventHandler<EventArgument>(GameFlow));
            using (var client = new HttpClient()) 
            {
                var heros = await client.GetStringAsync("https://game.gtimg.cn/images/lol/act/img/js/heroList/hero_list.js");
                heroes = JToken.Parse(heros)["hero"]!.ToObject<IEnumerable<Hero>>()!;
            }

            await LoopforClientStatus();
        }

        /// <summary>
        /// 打开大乱斗秒抢的设置界面
        /// </summary>
        private void OpenAramFastGetHeroSettingDialog() 
        {
            AramFastGetHeroSettingDialog aramFastGetHeroSettingDialog = new AramFastGetHeroSettingDialog(heroes);
            aramFastGetHeroSettingDialog.ShowDialog();
        }

        /// <summary>
        /// 选择英雄时的事件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="event">数据</param>
        private async void ChampSelect(object obj, EventArgument @event)
        {
            try
            {
                var gInfo = await _gameConnector.GetCurrentGameSessionAsync();
                var mode = JToken.Parse(gInfo)["gameData"]["queue"]["gameMode"].ToString();
                var myData = JObject.Parse(@event.Data.ToString());
                int playerCellId = int.Parse(@event.Data["localPlayerCellId"].ToString());
                IEnumerable<Teammate> teams = JsonConvert.DeserializeObject<IEnumerable<Teammate>>(@event.Data["myTeam"].ToString());
                var me = teams.FirstOrDefault(x => x.CellId == playerCellId);
                if (me == null)
                    return;

                if (mode == "ARAM") //当前模式是大乱斗
                {
                    if (AramFastGetHero) //秒抢大乱斗英雄
                    {
                        var session = await _gameConnector.GetCurrentChampSelectSessionAsync();
                        var token = JToken.Parse(session);
                        BenchChampion[] champs = token["benchChampions"]?.ToObject<BenchChampion[]>();
                        var loc = IniSettingsModel.Instance.LockHerosInAram.IndexOf(me.ChampionId);
                        //自己的英雄在不在秒抢列表，不在则所有英雄都加入集合，否则之加入当前自己英雄前面的
                        loc = loc == -1 ? IniSettingsModel.Instance.LockHerosInAram.Count : loc;
                        if (loc != 0)
                        {
                            var heros = IniSettingsModel.Instance.LockHerosInAram.Take(loc);
                            var swapHeros = new List<int>();
                            
                            foreach (var item in heros)
                            {
                                if (champs.FirstOrDefault(x => x.ChampionId == item) != null)
                                {
                                    swapHeros.Add(item);
                                    break;
                                }
                            }

                            if(swapHeros.Count()>0)
                                await _gameConnector.BenchSwapChampionsAsync(swapHeros[0]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //TODO record
            }
        }

        /// <summary>
        /// 游戏状态变化处理
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="event">数据</param>
        private async void GameFlow(object obj, EventArgument @event)
        {
            var data = $"{@event.Data}";
            if (string.IsNullOrEmpty(data))
                return;

            switch (data)
            {
                case "ReadyCheck":
                    GameStatus = "找到对局";
                    if (AutoAcceptGame) 
                    {
                        await _gameConnector.AutoAcceptGameAsync();
                    }
                    break;
                case "None":
                    GameStatus = "大厅中或正在创建对局";
                    break;
                case "Reconnect":
                    GameStatus = "游戏中,等待重新连接";
                    break;
                case "Lobby":
                    GameStatus = "房间中";
                    break;
                case "Matchmaking":
                    GameStatus = "匹配中";
                    break;
                case "InProgress":
                    GameStatus = "游戏中";
                    break;
                case "GameStart":
                    GameStatus = "游戏开始了";
                    break;
                case "WaitingForStats":
                    GameStatus = "等待结算界面";
                    break;
                case "PreEndOfGame":
                    break;
                case "EndOfGame":
                    GameStatus = "对局结束";
                    break;
                default:
                    GameStatus = "未知状态" + data;
                    break;
            }
        }

        #region 连接客户端
        private async Task ConnnectAsync()
        {
            while (true)
            {
                try
                {
                    var authenticate = await GetAuthenticate();
                    if (!string.IsNullOrEmpty(authenticate) && authenticate.Contains("--remoting-auth-token="))
                    {
                        var tokenResults = authenticate.Split("--remoting-auth-token=");
                        var portResults = authenticate.Split("--app-port=");
                        var PidResults = authenticate.Split("--app-pid=");
                        var installLocations = authenticate.Split("--install-directory=");
                        token = tokenResults[1].Substring(0, tokenResults[1].IndexOf("\""));
                        port = int.TryParse(portResults[1].Substring(0, portResults[1].IndexOf("\"")), out var temp) ? temp : 0;
                        pid = int.TryParse(PidResults[1].Substring(0, PidResults[1].IndexOf("\"")), out var temp1) ? temp1 : 0;
                        if (string.IsNullOrEmpty(token) || port == 0)
                            throw new InvalidOperationException("invalid data when try to crack.");

                        _gameConnector = new GameConnector(port, token);
                        await _gameConnector.ConnectAsync();

                        break;
                    }
                    else
                        throw new InvalidOperationException("can't read right token and port");
                }
                catch (Exception ex)
                {
                    await Task.Delay(2000);
                }
            }
        }

        /// <summary>
        /// 获取控制台的lol的相关参数
        /// </summary>
        /// <returns>lol的相关参数</returns>
        private async Task<string> GetAuthenticate()
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = _cmdPath;
                p.StartInfo.UseShellExecute = false; //是否使用操作系统shell启动
                p.StartInfo.RedirectStandardInput = true; //接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardOutput = true; //由调用程序获取输出信息
                p.StartInfo.RedirectStandardError = true; //重定向标准错误输出
                p.StartInfo.CreateNoWindow = true; //不显示程序窗口
                p.Start();
                p.StandardInput.WriteLine(_excuteShell.TrimEnd('&') + "&exit");
                p.StandardInput.AutoFlush = true;
                string output = await p.StandardOutput.ReadToEndAsync();
                p.WaitForExit();
                p.Close();

                return output;
            }
        }
        #endregion

        #region 循环感知客户端是否在线
        private bool _isLoop = false;
        private async Task LoopforClientStatus()
        {
            if (_isLoop)
                return;

            _isLoop = true;
            await Task.Yield();
            while (true)
            {
                try
                {
                    var data = await _gameConnector.FetchCurrentAccountInfoAsync();
                    if (data == null)
                        throw new Exception("未知的登录信息");

                    await Task.Delay(3000);
                }
                catch
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        GameStatus = "断线中...";
                    });

                    await LoadAsync();
                    await Task.Delay(3000);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 英雄对象
    /// </summary>
    public class Hero
    {
        [JsonProperty("heroId")]
        public int ChampId { get; set; }
        [JsonProperty("name")]
        public string Label { get; set; }
        [JsonProperty("alias")]
        public string Alias { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        public string Name => $"{Label} {Title}";
        public string Avatar => $"https://game.gtimg.cn/images/lol/act/img/champion/{Alias}.png";
    }

    /// <summary>
    /// 队友对象
    /// </summary>
    public class Teammate
    {
        public int CellId { get; set; }
        public int ChampionId { get; set; }
        public long SummonerId { get; set; }
        public string AssignedPosition { get; set; }
    }

    /// <summary>
    /// 大乱斗候选英雄对象
    /// </summary>
    public class BenchChampion
    {
        public int ChampionId { get; set; }
    }
}
