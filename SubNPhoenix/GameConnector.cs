using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Websocket.Client;

namespace SubNPhoenix
{
    /// <summary>
    /// 游戏通信连接
    /// </summary>
    public class GameConnector
    {
        private const string _checkUrl = "lol-matchmaking/v1/ready-check/";
        private const string _loginInfo = "/lol-login/v1/session"; //当前账号信息
        private const string _gameSessionData = "lol-gameflow/v1/session";//当前游戏数据
        private const string _champSelectSession = "lol-champ-select/v1/session"; //选择英雄当前数据
        private const string _benchSwapChampion = " /lol-champ-select/v1/session/bench/swap/{0}"; //大乱斗选择侯选英雄
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _httpClient;
        private readonly WebsocketClient _webSocket;
        private const int ClientEventData = 2;
        private const int ClientEventNumber = 8;
        private readonly IDictionary<string, List<EventHandler<EventArgument>>> _subscribers = new Dictionary<string, List<EventHandler<EventArgument>>>();

        public GameConnector(int port, string token)
        {
            //初始化lol http通信对象
            _httpClientHandler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
            };
            _httpClientHandler.ServerCertificateCustomValidationCallback = (response, cert, chain, errors) => true;

            var authTokenBytes = Encoding.ASCII.GetBytes($"riot:{token}");
            _httpClient = new HttpClient(_httpClientHandler);
            _httpClient.BaseAddress = new Uri($"https://127.0.0.1:{port}/");
            _httpClient.DefaultRequestVersion = new Version(2, 0);
            _httpClient.Timeout = TimeSpan.FromSeconds(8); //请求时间8s
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authTokenBytes));

            //初始化lol websocket通信对象
            _webSocket = new WebsocketClient(new Uri($"wss://127.0.0.1:{port}/"), () =>
            {
                var socket = new ClientWebSocket
                {
                    Options =
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(5),
                        Credentials = new NetworkCredential("riot", token),
                        RemoteCertificateValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true,
                    }
                };
                socket.Options.AddSubProtocol("wamp");
                socket.Options.SetRequestHeader("Connection", "keep-alive");
                return socket;
            });

            _webSocket.DisconnectionHappened.Subscribe(async type =>
            {
                try
                {
                    await _webSocket?.Start();
                    await _webSocket?.SendInstant("[5, \"OnJsonApiEvent\"]");
                }
                catch { }
            });

            _webSocket.ReconnectionHappened.Subscribe(async _ =>
            {
                try
                {
                    await _webSocket?.Start();
                    await _webSocket?.SendInstant("[5, \"OnJsonApiEvent\"]");
                }
                catch { }
            });

            _webSocket.ErrorReconnectTimeout = TimeSpan.FromSeconds(3);
            _webSocket.ReconnectTimeout = TimeSpan.FromSeconds(3);
            _webSocket
                .MessageReceived
                .Where(msg => msg.Text != null)
                .Where(msg => msg.Text.StartsWith('['))
                .Subscribe(msg =>
                {
                    var eventArray = JArray.Parse(msg.Text);
                    var eventNumber = eventArray?[0].ToObject<int>();
                    if (eventNumber != ClientEventNumber)
                    {
                        return;
                    }

                    var leagueEvent = eventArray[ClientEventData].ToObject<EventArgument>();
                    if (!_subscribers.TryGetValue(leagueEvent.Uri, out List<EventHandler<EventArgument>> eventHandlers))
                    {
                        return;
                    }

                    eventHandlers.ForEach(eventHandler => eventHandler?.Invoke(this, leagueEvent));
                });
        }

        public void Subscribe(string uri, EventHandler<EventArgument> eventHandler)
        {
            if (_subscribers.TryGetValue(uri, out var eventHandlers))
            {
                eventHandlers.Add(eventHandler);
            }
            else
            {
                _subscribers.Add(uri, new List<EventHandler<EventArgument>> { eventHandler });
            }
        }

        public bool Unsubscribe(string uri)
        {
            return _subscribers.Remove(uri);
        }

        public void UnsubscribeAll()
        {
            _subscribers.Clear();
        }

        public async Task ConnectAsync()
        {
            try
            {
                await _webSocket?.Start();
                await _webSocket?.SendInstant("[5, \"OnJsonApiEvent\"]");
            }
            catch { }
        }

        protected string BuildQueryParameterString(IEnumerable<string> queryParameters)
        {
            return "?" + string.Join("&", queryParameters.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        /// <summary>
        /// post请求lol客户端
        /// </summary>
        /// <param name="relativeUrl">请求地址</param>
        /// <param name="queryParameters">请求参数</param>
        /// <param name="body">请求体</param>
        /// <returns></returns>
        public async Task<string> PostAsync(string relativeUrl, IEnumerable<string> queryParameters, dynamic body)
        {
            try
            {
                var url = queryParameters == null ? relativeUrl : relativeUrl + BuildQueryParameterString(queryParameters);
                StringContent stringContent = null;
                if (body != null)
                {
                    var json = JsonConvert.SerializeObject(body);
                    stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var resp = await _httpClient.PostAsync(url, stringContent);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// get请求lol客户端
        /// </summary>
        /// <param name="relativeUrl">请求地址</param>
        /// <param name="queryParameters">请求参数</param>
        /// <returns></returns>
        public async Task<string> GetAsync(string relativeUrl, IEnumerable<string> queryParameters)
        {
            try
            {
                var url = queryParameters == null ? relativeUrl : relativeUrl + BuildQueryParameterString(queryParameters);
                var data = await _httpClient.GetStringAsync(url);

                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        //http请求
        //自动接受对局
        public async Task AutoAcceptGameAsync()
        {
            await PostAsync($"{_checkUrl}accept", null, null);
        }

        //获取游戏当前信息
        public async Task<string> GetCurrentGameSessionAsync()
        {
            return await GetAsync(_gameSessionData, null);
        }

        public async Task<string> GetCurrentChampSelectSessionAsync()
        {
            return await GetAsync(_champSelectSession, null);
        }

        public async Task BenchSwapChampionsAsync(int champID)
        {
            await PostAsync(string.Format(_benchSwapChampion, champID), null, null);
        }

        public async Task<string> FetchCurrentAccountInfoAsync()
        {
            return await GetAsync(_loginInfo,null);
        }
    }

    public class EventArgument
    {
        [JsonPropertyName("data")]
        public dynamic Data { get; set; }

        [JsonPropertyName("eventType")]
        public string EventType { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }
    }
}
