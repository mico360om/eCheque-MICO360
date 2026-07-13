using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace eCheque.MICO360.Models
{
    /// <summary>One imported line in a batch print run (payee + amount + optional details). Transient — never
    /// persisted directly; each valid row becomes a <see cref="ChequeRecord"/> when saved/printed.</summary>
    public class BatchRow : INotifyPropertyChanged
    {
        int _rowNo; string _num = ""; string _payee = ""; decimal _amount; DateTime _date = DateTime.Today;
        string _reference = ""; string _invoice = ""; string _memo = ""; string _error = "";

        public int RowNo { get => _rowNo; set => Set(ref _rowNo, value); }
        public string ChequeNumber { get => _num; set => Set(ref _num, value); }
        public string PayeeName { get => _payee; set => Set(ref _payee, value); }
        public decimal Amount { get => _amount; set => Set(ref _amount, value); }
        public DateTime ChequeDate { get => _date; set => Set(ref _date, value); }
        public string Reference { get => _reference; set => Set(ref _reference, value); }
        public string Invoice { get => _invoice; set => Set(ref _invoice, value); }
        public string Memo { get => _memo; set => Set(ref _memo, value); }

        /// <summary>Validation problem for this row, or empty when the row is good to print.</summary>
        public string Error { get => _error; set { Set(ref _error, value); OnPropertyChanged(nameof(IsValid)); OnPropertyChanged(nameof(StatusLabel)); } }
        public bool IsValid => string.IsNullOrEmpty(_error);
        public string StatusLabel => IsValid ? "✓ Ready" : "⚠ " + _error;

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; OnPropertyChanged(n); }
        }
    }
}
