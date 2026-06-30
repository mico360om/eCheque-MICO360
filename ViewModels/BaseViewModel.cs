using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace eCheque.MICO360.ViewModels
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
        protected bool Set<T>(ref T f,T v,[CallerMemberName]string? n=null){if(EqualityComparer<T>.Default.Equals(f,v))return false;f=v;OnPropertyChanged(n);return true;}
    }
}
