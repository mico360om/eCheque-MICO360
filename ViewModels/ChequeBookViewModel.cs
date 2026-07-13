using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    /// <summary>
    /// Cheque-book inventory screen: list issued leaf ranges per account, see used/remaining/spoiled leaves and
    /// the next number to use, add books, and mark individual leaves spoiled. Read-only roles cannot edit.
    /// </summary>
    public class ChequeBookViewModel : BaseViewModel
    {
        ObservableCollection<ChequeBook> _books = new();
        ObservableCollection<string> _banks = new();
        ChequeBook? _selected;
        ChequeBook _edit = new();
        ChequeBookStats? _stats;
        string _status = "";
        bool _isEditing;

        public ObservableCollection<ChequeBook> Books { get => _books; set => Set(ref _books, value); }
        public ObservableCollection<string> Banks { get => _banks; set => Set(ref _banks, value); }
        public ChequeBook Edit { get => _edit; set => Set(ref _edit, value); }
        public bool IsEditing { get => _isEditing; set { Set(ref _isEditing, value); OnPropertyChanged(nameof(ShowDetail)); } }
        public bool ShowDetail => !IsEditing && Selected != null;
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public bool CanEdit => AuthService.CanEdit;

        public ChequeBook? Selected
        {
            get => _selected;
            set { Set(ref _selected, value); IsEditing = false; RecomputeStats(); OnPropertyChanged(nameof(ShowDetail)); }
        }

        public ChequeBookStats? Stats { get => _stats; set => Set(ref _stats, value); }
        public string UsageText => _stats == null ? "" :
            $"Total {_stats.Total}   ·   Used {_stats.Used}   ·   Spoiled {_stats.Spoiled}   ·   Remaining {_stats.Remaining}";
        public string NextText => _stats?.NextNumber is int n && Selected != null
            ? $"Next available leaf:  {Selected.Format(n)}" : "Book fully used — no leaves remaining";
        public string GapsText => _stats == null || _stats.Gaps.Count == 0
            ? "No skipped leaves." : $"Skipped/unused below the last-used leaf: {string.Join(", ", _stats.Gaps.Take(40).Select(g => Selected!.Format(g)))}";

        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand MarkSpoiledCommand { get; }
        public ICommand CancelBookCommand { get; }

        public ChequeBookViewModel()
        {
            NewCommand        = new RelayCommand(NewBook, () => CanEdit);
            SaveCommand       = new RelayCommand(Save, () => CanEdit);
            CancelEditCommand = new RelayCommand(() => { IsEditing = false; StatusMessage = ""; });
            RefreshCommand    = new RelayCommand(Load);
            MarkSpoiledCommand= new RelayCommand(MarkSpoiled, () => CanEdit && Selected != null);
            CancelBookCommand = new RelayCommand(CancelBook, () => CanEdit && Selected != null);
        }

        public void Load()
        {
            var sel = Selected?.Id ?? 0;
            Books = new ObservableCollection<ChequeBook>(ChequeBookService.GetBooks());
            Banks = new ObservableCollection<string>(ChequeService.GetBanks());
            Selected = Books.FirstOrDefault(b => b.Id == sel) ?? Books.FirstOrDefault();
            StatusMessage = "";
        }

        void RecomputeStats()
        {
            Stats = Selected != null ? ChequeBookService.Stats(Selected) : null;
            OnPropertyChanged(nameof(UsageText));
            OnPropertyChanged(nameof(NextText));
            OnPropertyChanged(nameof(GapsText));
        }

        void NewBook()
        {
            Edit = new ChequeBook
            {
                BankName = Banks.FirstOrDefault() ?? "",
                PadWidth = 6, StartNumber = 1, EndNumber = 50, IssueDate = DateTime.Today, Status = "Active"
            };
            IsEditing = true;
            StatusMessage = "";
        }

        void Save()
        {
            try
            {
                ChequeBookService.SaveBook(Edit);
                IsEditing = false;
                StatusMessage = $"Saved cheque book {Edit.DisplayName}.";
                ToastService.Success("Cheque book saved.");
                Load();
                Selected = Books.FirstOrDefault(b => b.Id == Edit.Id);
            }
            catch (Exception ex) { StatusMessage = ex.Message; ToastService.Warn(ex.Message); }
        }

        void MarkSpoiled()
        {
            if (Selected == null) return;
            var input = InputBox.Show($"Enter the leaf number to mark spoiled (range {Selected.StartNumber}–{Selected.EndNumber}):", "Mark Leaf Spoiled");
            if (input == null) return;
            if (!int.TryParse(input.Trim(), out var n) || n < Selected.StartNumber || n > Selected.EndNumber)
            { StatusMessage = "That number is not within this book's range."; return; }
            ChequeBookService.MarkSpoiled(Selected.Id, n, true);
            var again = ChequeBookService.GetBook(Selected.Id);
            if (again != null) { _selected = again; OnPropertyChanged(nameof(Selected)); RecomputeStats(); }
            StatusMessage = $"Leaf {Selected!.Format(n)} marked spoiled.";
        }

        void CancelBook()
        {
            if (Selected == null) return;
            if (System.Windows.MessageBox.Show(
                    $"Cancel cheque book '{Selected.DisplayName}'?\nIt will no longer suggest numbers or validate cheques.",
                    "Cancel Cheque Book", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
                != System.Windows.MessageBoxResult.Yes) return;
            ChequeBookService.SetStatus(Selected.Id, "Cancelled");
            StatusMessage = "Cheque book cancelled.";
            Load();
        }
    }
}
