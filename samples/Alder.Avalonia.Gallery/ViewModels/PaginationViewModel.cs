using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class PaginationViewModel : GalleryViewModel
{
    private const int ItemsPerPage = 10;
    private const int InvoicesPerPage = 5;

    public int TotalItems => 120;
    public int PageCount => (TotalItems + ItemsPerPage - 1) / ItemsPerPage;
    public int InvoiceCount => 50;
    public int InvoicePageCount => InvoiceCount / InvoicesPerPage;
    public ICommand FirstPage { get; }
    public ICommand LastPage { get; }

    public int CurrentPage
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = 1;

    public int SummaryPage
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ResultSummary));
        }
    } = 1;

    public int InvoicePage
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(InvoiceSummary));
        }
    } = 1;

    public IReadOnlyList<AlderPaginationItem> FixedPages { get; } =
        [new(1), new(2, IsEnabled: false), new(null), new(12)];
    public string Summary => FormatRange("PaginationItemSummary", CurrentPage, ItemsPerPage, TotalItems);
    public string ResultSummary => FormatRange("PaginationItemSummary", SummaryPage, ItemsPerPage, TotalItems);
    public string InvoiceSummary => FormatRange("PaginationInvoiceSummary", InvoicePage, InvoicesPerPage, InvoiceCount);

    public PaginationViewModel()
    {
        FirstPage = ReactiveCommand.Create(() => { CurrentPage = 1; });
        LastPage = ReactiveCommand.Create(() => { CurrentPage = PageCount; });
    }

    private string FormatRange(string key, int page, int size, int total) =>
        Format(key, (page - 1) * size + 1, Math.Min(page * size, total), total);
}
