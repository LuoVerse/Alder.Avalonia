using System.Globalization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class RadioGroupViewModel : GalleryViewModel
{
    public IReadOnlyList<string> Plan
    {
        get => field;
        set
        {
            if (field.SequenceEqual(value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PlanSummary));
        }
    } = ["pro"];

    public IReadOnlyList<string> ValidationPlan
    {
        get => field;
        set
        {
            if (field.SequenceEqual(value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            SavedPlan = null;
            NotifyValidation();
        }
    } = [];

    private string LastPlan
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(LastPlanSummary));
    } = "pro";

    private bool Submitted { get; set; }
    private string? SavedPlan { get; set; }

    public string PlanSummary => Plan.FirstOrDefault() is { } plan
        ? Format("RadioChosenPlan", PlanName(plan))
        : Text("RadioChoosePlan");
    public string LastPlanSummary => Format("RadioLastPlan", PlanName(LastPlan));
    public bool ShowValidationError => Submitted && ValidationPlan.Count == 0;
    public string? ValidationMessage => ShowValidationError
        ? Text("RadioChoosePlan")
        : SavedPlan is { } plan ? Format("RadioPlanSaved", PlanName(plan)) : null;

    public string StandardPrice => Dollars(5);
    public string ExpressPrice => Dollars(16);
    public string FastPrice => Dollars(25);
    public string MonthlyPrice => Format("RadioMonthlyPrice", Dollars(12));
    public string YearlyPrice => Format("RadioYearlyPrice", Dollars(120));
    public string CardExpiration => Format("RadioCardExpires", "01/2027");
    public string MastercardName => Format("RadioPayCardName", "Mastercard", "8304");
    public string VisaName => Format("RadioPayCardName", "Visa", "0123");

    public void SelectPro() => Plan = ["pro"];
    public void ClearPlan() => Plan = [];
    public void RecordSelection(string plan) => LastPlan = plan;
    public void Submit()
    {
        Submitted = true;
        SavedPlan = ValidationPlan.FirstOrDefault();
        NotifyValidation();
    }

    private void NotifyValidation()
    {
        this.RaisePropertyChanged(nameof(ShowValidationError));
        this.RaisePropertyChanged(nameof(ValidationMessage));
    }

    private string PlanName(string key) => key switch
    {
        "starter" => Text("Starter"),
        "teams" => Text("Teams"),
        _ => "Pro"
    };

    private static string Dollars(decimal amount) => amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
}
