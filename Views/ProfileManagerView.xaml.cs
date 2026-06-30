using System.Windows.Controls;
using System.Windows.Input;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class ProfileManagerView : UserControl
    {
        public ProfileManagerView()
        {
            InitializeComponent();
            Loaded += (s, e) => { if (DataContext is ProfileManagerViewModel vm) vm.Load(); };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is ProfileManagerViewModel vm && vm.SaveCommand.CanExecute(null))
                { vm.SaveCommand.Execute(null); e.Handled = true; }
            };
        }
    }
}
