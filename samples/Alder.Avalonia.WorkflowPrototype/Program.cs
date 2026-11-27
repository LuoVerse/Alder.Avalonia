using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Alder.Avalonia.Themes;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Alder.Avalonia.WorkflowPrototype;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<PrototypeApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
}

public sealed class PrototypeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new AlderTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new AlderWindow
            {
                Title = "工作流编辑器",
                Width = 1460,
                Height = 920,
                MinWidth = 700,
                MinHeight = 540,
                Content = new AlderWorkflowEditor
                {
                    Workflow = WorkflowExampleData.Create(),
                    UserItems = WorkflowExampleData.Users,
                    RoleItems = WorkflowExampleData.Roles,
                    ConditionalTypes = WorkflowExampleData.Conditions
                }
            };
        base.OnFrameworkInitializationCompleted();
    }
}
