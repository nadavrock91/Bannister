using Bannister.Services;

namespace Bannister.Views;

public class DevWorkflowPage : ContentPage
{
    private readonly DevWorkflowView _view;

    public DevWorkflowPage(AuthService auth, DevWorkflowService service)
    {
        Title = "Dev Workflow";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        _view = new DevWorkflowView(
            () => auth.CurrentUsername,
            service,
            showAlert: (title, message) => DisplayAlert(title, message, "OK"));
        Content = _view;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _view.ActivateAsync();
    }
}
