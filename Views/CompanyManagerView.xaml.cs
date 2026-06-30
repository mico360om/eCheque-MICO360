using System.Windows.Controls;
using System.Windows.Input;
using eCheque.MICO360.ViewModels;

namespace eCheque.MICO360.Views
{
    public partial class CompanyManagerView : UserControl
    {
        public CompanyManagerView()
        {
            InitializeComponent();
            Loaded += (s, e) => { if (DataContext is CompanyManagerViewModel vm) vm.Load(); };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is CompanyManagerViewModel vm && vm.IsEditing)
                { vm.SaveCommand.Execute(null); e.Handled = true; }
            };
        }
    }
}
