using Avalonia.Controls;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class ProfileManagerView : UserControl
    {
        public ProfileManagerView() => InitializeComponent();

        public ProfileManagerView(ProfileManagerViewModel vm) : this()
        {
            DataContext = vm;
            vm.DesignLayoutRequested += OpenDesigner;
        }

        async void OpenDesigner(ChequeProfile profile)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            var win = new ChequeDesignerWindow(profile);
            await win.ShowDialog(owner);
            if (win.Saved && DataContext is ProfileManagerViewModel vm) vm.Load();
        }
    }
}
