using System;
using Bannister.Models;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

public partial class SetMultiplierPage : ContentPage
{
    private readonly Activity _activity;
    private readonly TaskCompletionSource<int?> _completion = new();

    public SetMultiplierPage(Activity activity)
    {
        InitializeComponent();
        _activity = activity;
        
        lblActivityName.Text = $"\"{activity.Name}\"";
        txtMultiplier.Text = activity.Multiplier.ToString();
        UpdatePreview();
    }

    public Task<int?> WaitForResultAsync()
    {
        return _completion.Task;
    }

    private void OnMultiplierChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (int.TryParse(txtMultiplier.Text, out int multiplier))
        {
            int total = _activity.ExpGain * multiplier;
            lblPreview.Text = $"→ {_activity.ExpGain} × {multiplier} = {total} EXP";
        }
        else
        {
            lblPreview.Text = $"→ {_activity.ExpGain} × ? = ? EXP";
        }
    }

    private async void OnSetClicked(object sender, EventArgs e)
    {
        if (int.TryParse(txtMultiplier.Text, out int multiplier))
        {
            if (multiplier < 1)
            {
                await DisplayAlert("Invalid", "Multiplier must be at least 1.", "OK");
                return;
            }

            _completion.TrySetResult(multiplier);
            await Navigation.PopModalAsync();
        }
        else
        {
            await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _completion.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        if (!_completion.Task.IsCompleted)
        {
            _completion.TrySetResult(null);
        }
        base.OnDisappearing();
    }
}
