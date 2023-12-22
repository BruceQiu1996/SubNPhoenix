using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SubNPhoenix.Resources;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SubNPhoenix
{
    public class AramFastGetHeroSettingDialogViewModel : ObservableObject
    {
        public AsyncRelayCommand SelectHerosLockCommandAsync { get; set; } //选择事件
        public AsyncRelayCommand UnSelectHerosLockCommandAsync { get; set; } //取消选择事件
        public RelayCommand LoadCommand { get; set; }

        private ObservableCollection<Hero> _chooseHeros; //未被秒选的英雄
        public ObservableCollection<Hero> ChooseHeros
        {
            get => _chooseHeros;
            set => SetProperty(ref _chooseHeros, value);
        }

        private ObservableCollection<Hero> _subQuickChooseHeros; //当前选择秒选的英雄
        public ObservableCollection<Hero> SubQuickChooseHeros
        {
            get => _subQuickChooseHeros;
            set => SetProperty(ref _subQuickChooseHeros, value);
        }

        private ObservableCollection<Hero> _selectedQuickChooseHeros; //已经秒选的英雄
        public ObservableCollection<Hero> SelectedQuickChooseHeros
        {
            get => _selectedQuickChooseHeros;
            set => SetProperty(ref _selectedQuickChooseHeros, value);
        }

        private ObservableCollection<Hero> _subSelectedQuickChooseHeros;
        public ObservableCollection<Hero> SubSelectedQuickChooseHeros //当前取消秒选的英雄
        {
            get => _subSelectedQuickChooseHeros;
            set => SetProperty(ref _subSelectedQuickChooseHeros, value);
        }

        private readonly IEnumerable<Hero> _heros;
        public AramFastGetHeroSettingDialogViewModel(IEnumerable<Hero> heros)
        {
            _heros = heros;
            LoadCommand = new RelayCommand(Load);
            SelectHerosLockCommandAsync = new AsyncRelayCommand(SelectHerosLockAsync);
            UnSelectHerosLockCommandAsync = new AsyncRelayCommand(UnSelectHerosLockAsync);
        }

        private void Load()
        {
            ChooseHeros = new ObservableCollection<Hero>(_heros.Where(x => !IniSettingsModel.Instance.LockHerosInAram.Contains(x.ChampId)).OrderBy(x => x.Name));
            SubQuickChooseHeros = new ObservableCollection<Hero>();
            SubSelectedQuickChooseHeros = new ObservableCollection<Hero>();
            var list = new List<Hero>();
            foreach (var item in IniSettingsModel.Instance.LockHerosInAram)
            {
                var h = _heros.FirstOrDefault(x => x.ChampId == item);
                if (h != null)
                {
                    list.Add(h);
                }
            }
            SelectedQuickChooseHeros = new ObservableCollection<Hero>(list);
        }

        private async Task SelectHerosLockAsync()
        {
            if (SubQuickChooseHeros.Count <= 0)
                return;

            var temp = new List<Hero>();
            foreach (var item in SubQuickChooseHeros)
            {
                temp.Add(item);
            }

            foreach (var item in temp)
            {
                ChooseHeros.Remove(item);
                SelectedQuickChooseHeros.Add(item);
            }

            await IniSettingsModel.Instance.WriteLockHerosInAramAsync(SelectedQuickChooseHeros.Select(x => x.ChampId).ToList());
            SubQuickChooseHeros.Clear();
        }

        public async Task WriteIntoSetting()
        {
            await IniSettingsModel.Instance.WriteLockHerosInAramAsync(SelectedQuickChooseHeros.Select(x => x.ChampId).ToList());
        }

        private async Task UnSelectHerosLockAsync()
        {
            if (SubSelectedQuickChooseHeros.Count <= 0)
                return;

            var temp = new List<Hero>();
            foreach (var item in SubSelectedQuickChooseHeros)
            {
                temp.Add(item);
            }

            foreach (var item in temp)
            {
                ChooseHeros.Add(item);
                SelectedQuickChooseHeros.Remove(item);
            }
            ChooseHeros = new ObservableCollection<Hero>(ChooseHeros.OrderBy(x => x.Name));
            await IniSettingsModel.Instance.WriteLockHerosInAramAsync(SelectedQuickChooseHeros.Select(x => x.ChampId).ToList());
            SubSelectedQuickChooseHeros.Clear();
        }
    }
}
