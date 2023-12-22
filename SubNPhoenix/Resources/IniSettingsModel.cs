using Newtonsoft.Json;
using SubNPhoenix.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SubNPhoenix.Resources
{
    public class IniSettingsModel
    {
        private readonly string _iniLocation = "subNsettings.ini";
        #region ini section
        public const string GameKey = nameof(GameKey);
        public const string LockHerosInAramKey = nameof(LockHerosInAramKey);
        public const string AutoAcceptGameKey = nameof(AutoAcceptGameKey);
        public const string AramFastGetHeroKey = nameof(AramFastGetHeroKey);
        #endregion
        public List<int> LockHerosInAram { get; set; }
        public bool AutoAcceptGame { get; set; }
        public bool AramFastGetHero { get; set; }
        public async Task InitializeAsync()
        {
            if (!File.Exists(_iniLocation))
            {
                File.Create(_iniLocation);
            }
            var lockHerosInAramData = await IniHelper.ReadAsync(_iniLocation, GameKey, LockHerosInAramKey);
            LockHerosInAram = string.IsNullOrEmpty(lockHerosInAramData) ? new List<int>() : JsonConvert.DeserializeObject<List<int>>(lockHerosInAramData);
            AutoAcceptGame = bool.TryParse(
                await IniHelper.ReadAsync(_iniLocation, GameKey, AutoAcceptGameKey), out var tempAutoAcceptGame) ? tempAutoAcceptGame : false;
            AramFastGetHero = bool.TryParse(
                await IniHelper.ReadAsync(_iniLocation, GameKey, AramFastGetHeroKey), out var tempAramFastGetHero) ? tempAramFastGetHero : false;
        }

        public async Task WriteLockHerosInAramAsync(List<int> values)
        {
            var data = JsonConvert.SerializeObject(values);
            await IniHelper.WriteAsync(_iniLocation, GameKey, LockHerosInAramKey, data);
            LockHerosInAram = values;
        }

        public async Task WriteAutoAcceptGameAsync(bool value)
        {
            await IniHelper.WriteAsync(_iniLocation, GameKey, AutoAcceptGameKey, value.ToString());
            AutoAcceptGame = value;
        }

        public async Task WriteAramFastGetHeroAsync(bool value)
        {
            await IniHelper.WriteAsync(_iniLocation, GameKey, AramFastGetHeroKey, value.ToString());
            AramFastGetHero = value;
        }
        private IniSettingsModel() { }

        private static IniSettingsModel _instance;
        public static IniSettingsModel Instance => _instance ?? (_instance = new IniSettingsModel());
    }
}
