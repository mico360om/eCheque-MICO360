using System.Collections.ObjectModel;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public ObservableCollection<ChequeRecord> RecentCheques { get; } = new();

        int _total, _printed, _draft, _cancelled, _voided, _todayPrinted, _monthPrinted;
        decimal _totalAmount, _monthAmount, _yearAmount;

        public int Total { get => _total; set => Set(ref _total, value); }
        public int Printed { get => _printed; set => Set(ref _printed, value); }
        public int Draft { get => _draft; set => Set(ref _draft, value); }
        public int Cancelled { get => _cancelled; set => Set(ref _cancelled, value); }
        public int Voided { get => _voided; set => Set(ref _voided, value); }
        public int TodayPrinted { get => _todayPrinted; set => Set(ref _todayPrinted, value); }
        public int MonthPrinted { get => _monthPrinted; set => Set(ref _monthPrinted, value); }
        public decimal TotalAmount { get => _totalAmount; set { Set(ref _totalAmount, value); OnPropertyChanged(nameof(TotalAmountDisplay)); } }
        public decimal MonthAmount { get => _monthAmount; set { Set(ref _monthAmount, value); OnPropertyChanged(nameof(MonthAmountDisplay)); } }
        public decimal YearAmount { get => _yearAmount; set { Set(ref _yearAmount, value); OnPropertyChanged(nameof(YearAmountDisplay)); } }

        public string TotalAmountDisplay => $"OMR {TotalAmount:N3}";
        public string MonthAmountDisplay => $"OMR {MonthAmount:N3}";
        public string YearAmountDisplay => $"OMR {YearAmount:N3}";
        public string CompanyName => CompanyService.CurrentCompanyName;

        public void Load()
        {
            var s = ChequeService.GetDashboardStats();
            Total = s.Total; Printed = s.Printed; Draft = s.Draft; Cancelled = s.Cancelled; Voided = s.Voided;
            TodayPrinted = s.TodayPrinted; MonthPrinted = s.MonthPrinted;
            TotalAmount = s.TotalAmount; MonthAmount = s.MonthAmount; YearAmount = s.YearAmount;

            RecentCheques.Clear();
            foreach (var c in ChequeService.GetCheques().Take(10)) RecentCheques.Add(c);
        }
    }
}
