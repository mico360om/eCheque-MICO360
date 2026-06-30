using System.ComponentModel;
using System.Windows.Controls;
using eCheque.MICO360.Services;
using eCheque.MICO360.ViewModels;

namespace eCheque.MICO360.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is SettingsViewModel oldVm)
                    oldVm.PropertyChanged -= OnVmPropertyChanged;
                if (e.NewValue is SettingsViewModel vm)
                {
                    vm.PropertyChanged += OnVmPropertyChanged;
                    UpdatePreview(vm);
                }
            };
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SettingsViewModel vm &&
                (e.PropertyName == nameof(SettingsViewModel.CurrencyWording) ||
                 e.PropertyName == nameof(SettingsViewModel.BaisaWording) ||
                 e.PropertyName == nameof(SettingsViewModel.CaseFormat) ||
                 e.PropertyName == nameof(SettingsViewModel.IncludeBaisa) ||
                 e.PropertyName == nameof(SettingsViewModel.AddOnly)))
                UpdatePreview(vm);
        }

        private void UpdatePreview(SettingsViewModel vm)
        {
            RunWordsPreview.Text = AmountToWordsService.Convert(
                4956.250m,
                vm.CaseFormat,
                vm.CurrencyWording,
                vm.BaisaWording,
                vm.IncludeBaisa,
                vm.AddOnly);
        }
    }
}
