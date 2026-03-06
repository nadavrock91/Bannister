using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Views;

public partial class DragonSelector : ContentPage
{
    private string? _selectedImage = null;
    private readonly TaskCompletionSource<string?> _completion = new();
    private Frame? _selectedFrame = null;

    public string? SelectedImage => _selectedImage;

    public DragonSelector(string[] availableImages)
    {
        InitializeComponent();

        chooseButton.IsEnabled = false;
        chooseButton.Opacity = 0.6;

        if (availableImages == null || availableImages.Length == 0)
        {
            noImagesLabel.IsVisible = true;
            imageGrid.IsVisible = false;
        }
        else
        {
            BindableLayout.SetItemsSource(imageGrid, availableImages);
            noImagesLabel.IsVisible = false;
            imageGrid.IsVisible = true;
        }
    }

    public Task<string?> WaitForSelectionAsync()
    {
        return _completion.Task;
    }

    private void OnImageTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is string imageName)
        {
            System.Diagnostics.Debug.WriteLine($"Dragon image tapped: {imageName}");

            // Deselect previous frame
            if (_selectedFrame != null)
            {
                _selectedFrame.BackgroundColor = Colors.White;
                _selectedFrame.BorderColor = Colors.Transparent;
            }

            // Select new frame
            _selectedImage = imageName;
            _selectedFrame = frame;
            frame.BackgroundColor = Color.FromArgb("#E9EBFF");
            frame.BorderColor = Color.FromArgb("#5B63EE");

            // Enable choose button
            chooseButton.IsEnabled = true;
            chooseButton.Opacity = 1.0;

            System.Diagnostics.Debug.WriteLine($"Selected dragon image: {_selectedImage}");
        }
    }

    private async void OnChooseClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedImage))
        {
            await DisplayAlert("Select Image", "Please select an image first.", "OK");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Returning selected image: {_selectedImage}");
        _completion.TrySetResult(_selectedImage);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _selectedImage = null;
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
