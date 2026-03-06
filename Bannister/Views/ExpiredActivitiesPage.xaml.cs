using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public partial class ExpiredActivitiesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private Activity? _selectedActivity;

    public ExpiredActivitiesPage(AuthService auth, ActivityService activities)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExpiredAsync();
    }

    private async Task LoadExpiredAsync()
    {
        var expired = await _activities.GetExpiredActivitiesAsync(_auth.CurrentUsername);
        
        if (expired.Count == 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }
        
        expiredList.ItemsSource = expired.Select(a => new ExpiredActivityViewModel(a)).ToList();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ExpiredActivityViewModel vm)
        {
            _selectedActivity = vm.Activity;
        }
    }

    private async void OnPostponeClicked(object sender, EventArgs e)
    {
        if (_selectedActivity == null)
        {
            await DisplayAlert("No Selection", "Please select an activity first.", "OK");
            return;
        }

        var newDate = await DisplayPromptAsync(
            "Postpone Activity",
            $"Postpone '{_selectedActivity.Name}' until (YYYY-MM-DD):",
            placeholder: DateTime.Now.AddDays(7).ToString("yyyy-MM-dd"));

        if (string.IsNullOrWhiteSpace(newDate))
            return;

        if (DateTime.TryParse(newDate, out var date))
        {
            await _activities.PostponeActivityAsync(_selectedActivity.Id, date);
            await LoadExpiredAsync();
        }
        else
        {
            await DisplayAlert("Invalid Date", "Please enter a valid date.", "OK");
        }
    }

    private async void OnExpireClicked(object sender, EventArgs e)
    {
        if (_selectedActivity == null)
        {
            await DisplayAlert("No Selection", "Please select an activity first.", "OK");
            return;
        }

        await _activities.MoveToExpiredAsync(_selectedActivity.Id);
        _selectedActivity = null;
        await LoadExpiredAsync();
    }

    private async void OnExpireAllClicked(object sender, EventArgs e)
    {
        var expired = await _activities.GetExpiredActivitiesAsync(_auth.CurrentUsername);
        
        if (expired.Count == 0)
            return;

        bool confirm = await DisplayAlert(
            "Expire All",
            $"Move all {expired.Count} expired activities to 'Expired' category?",
            "Yes",
            "No");

        if (confirm)
        {
            foreach (var activity in expired)
            {
                await _activities.MoveToExpiredAsync(activity.Id);
            }
            await LoadExpiredAsync();
        }
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class ExpiredActivityViewModel
{
    public Activity Activity { get; }

    public ExpiredActivityViewModel(Activity activity)
    {
        Activity = activity;
    }

    public string Name => Activity.Name;
    public string Game => Activity.Game;
    public string Category => Activity.Category;
    
    public string EndDateDisplay => Activity.EndDate.HasValue
        ? $"Expired on: {Activity.EndDate.Value:MMM dd, yyyy}"
        : "No end date";
}
