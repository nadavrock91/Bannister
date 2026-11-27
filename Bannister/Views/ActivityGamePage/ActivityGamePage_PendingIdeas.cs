using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public partial class ActivityGamePage
{
    private void AddPendingActivityIdeaControls(VerticalStackLayout stack)
    {
        btnAddPendingActivityIdea = (new Button
        {
            Text = "+ Add Pending Activity Idea",
            CornerRadius = 8,
            FontSize = 13,
            IsVisible = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, _db.IsReadOnly ? ThemeKey.WarningButtonBackground : ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnAddPendingActivityIdea.Clicked += OnAddPendingActivityIdeaClicked;
        stack.Children.Add(btnAddPendingActivityIdea);

        btnProcessPendingIdeas = (new Button
        {
            Text = "Process Pending Ideas",
            CornerRadius = 8,
            FontSize = 13,
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnProcessPendingIdeas.Clicked += OnProcessPendingIdeasClicked;
        stack.Children.Add(btnProcessPendingIdeas);

        lblPendingActivityIdeas = (new Label
        {
            Text = "",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true);
        stack.Children.Add(lblPendingActivityIdeas);
    }

    private async Task RefreshPendingActivityIdeaCountAsync()
    {
        if (btnAddPendingActivityIdea == null || btnProcessPendingIdeas == null || lblPendingActivityIdeas == null)
            return;

        bool hasGame = _game != null;
        bool isMaster = !_db.IsReadOnly;
        btnAddPendingActivityIdea.IsVisible = hasGame;
        btnAddPendingActivityIdea.ThemeColor(VisualElement.BackgroundColorProperty, isMaster ? ThemeKey.SuccessButtonBackground : ThemeKey.WarningButtonBackground);

        if (!hasGame || !isMaster)
        {
            btnProcessPendingIdeas.IsVisible = false;
            lblPendingActivityIdeas.IsVisible = false;
            return;
        }

        try
        {
            int count = await _pendingIdeas.GetPendingCountForGameAsync(_auth.CurrentUsername, _game!.GameId);
            lblPendingActivityIdeas.Text = count == 1 ? "1 pending" : $"{count} pending";
            lblPendingActivityIdeas.IsVisible = count > 0;
            btnProcessPendingIdeas.IsVisible = count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh pending activity idea count: {ex.Message}");
            btnProcessPendingIdeas.IsVisible = false;
            lblPendingActivityIdeas.IsVisible = false;
        }
    }

    private async void OnAddPendingActivityIdeaClicked(object? sender, EventArgs e)
    {
        if (_game == null)
            return;

        var categories = await _pendingIdeas.GetCategoriesForGameAsync(_auth.CurrentUsername, _game.GameId);
        var result = await PendingActivityIdeaModalPage.ShowAsync(Navigation, categories);
        if (result == null)
            return;

        int id = await _pendingIdeas.AddAsync(
            _auth.CurrentUsername,
            _game.GameId,
            result.Value.ActivityName,
            result.Value.ActivityCategory);

        if (_ideaLogger != null)
            await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = _auth.CurrentUsername,
                Title = result.Value.ActivityName,
                Body = result.Value.ActivityName,
                Category = "activities"
            });

        await RefreshPendingActivityIdeaCountAsync();
    }

    private async void OnProcessPendingIdeasClicked(object? sender, EventArgs e)
    {
        await ProcessPendingIdeasAsync();
    }

    private async Task ProcessPendingIdeasAsync()
    {
        if (_db.IsReadOnly || _game == null)
            return;

        while (true)
        {
            var pending = await _pendingIdeas.GetPendingForGameAsync(_auth.CurrentUsername, _game.GameId);
            if (pending.Count == 0)
            {
                await RefreshPendingActivityIdeaCountAsync();
                return;
            }

            var next = pending.First();
            var action = await PendingActivityIdeaProcessPage.ShowAsync(Navigation, next, pending.Count);

            if (action == PendingIdeaProcessAction.Cancel)
            {
                await RefreshPendingActivityIdeaCountAsync();
                return;
            }

            if (action == PendingIdeaProcessAction.Skip)
            {
                await _pendingIdeas.MarkDismissedAsync(next.Id);
                await RefreshPendingActivityIdeaCountAsync();
                continue;
            }

            var created = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation,
                _auth,
                _activities,
                _games,
                _game.GameId,
                prefillName: next.ActivityName,
                prefillCategory: next.ActivityCategory);

            if (created == null)
            {
                await RefreshPendingActivityIdeaCountAsync();
                return;
            }

            await _pendingIdeas.MarkConvertedAsync(next.Id);
            await RefreshActivitiesAsync();

            var remaining = await _pendingIdeas.GetPendingCountForGameAsync(_auth.CurrentUsername, _game.GameId);
            if (remaining == 0)
            {
                await RefreshPendingActivityIdeaCountAsync();
                return;
            }

            bool continueProcessing = await DisplayAlert(
                "Activity Created",
                $"Activity created. {remaining} pending {(remaining == 1 ? "idea remains" : "ideas remain")}.\n\nProcess next pending idea?",
                "Yes",
                "No");

            if (!continueProcessing)
            {
                await RefreshPendingActivityIdeaCountAsync();
                return;
            }
        }
    }

    private readonly record struct PendingActivityIdeaInput(string ActivityName, string ActivityCategory);

    private enum PendingIdeaProcessAction
    {
        Create,
        Skip,
        Cancel
    }

    private sealed class PendingActivityIdeaModalPage : ContentPage
    {
        private const string NewCategoryOption = "+ New";
        private readonly TaskCompletionSource<PendingActivityIdeaInput?> _completion = new();
        private readonly Entry _activityNameEntry;
        private readonly Picker _categoryPicker;
        private readonly Entry _newCategoryEntry;
        private readonly Label _errorLabel;

        private PendingActivityIdeaModalPage(List<string> categories)
        {
            Title = "Add Pending Idea";
            this.ThemeColor(BackgroundColorProperty, ThemeKey.Overlay);

            _activityNameEntry = (new Entry
            {
                Placeholder = "Activity name",
                HorizontalOptions = LayoutOptions.Fill
            }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

            _categoryPicker = (new Picker
            {
                Title = "Category",
                HorizontalOptions = LayoutOptions.Fill
            }).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText).ThemeColor(Picker.TitleColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

            foreach (var category in categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                _categoryPicker.Items.Add(category);
            _categoryPicker.Items.Add(NewCategoryOption);
            if (_categoryPicker.Items.Count > 1)
                _categoryPicker.SelectedIndex = 0;

            _newCategoryEntry = (new Entry
            {
                Placeholder = "New category",
                HorizontalOptions = LayoutOptions.Fill,
                IsVisible = false
            }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

            _categoryPicker.SelectedIndexChanged += (_, _) =>
            {
                _newCategoryEntry.IsVisible = _categoryPicker.SelectedItem?.ToString() == NewCategoryOption;
            };

            _errorLabel = (new Label
            {
                Text = "",
                FontSize = 12,
                IsVisible = false
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText3, classifyPageText: true);

            var saveButton = (new Button
            {
                Text = "Save",
                CornerRadius = 8,
                HeightRequest = 44
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            saveButton.Clicked += OnSaveClicked;

            var cancelButton = (new Button
            {
                Text = "Cancel",
                CornerRadius = 8,
                HeightRequest = 44
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
            cancelButton.Clicked += async (_, _) =>
            {
                _completion.TrySetResult(null);
                await Navigation.PopModalAsync();
            };

            var buttonGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };
            buttonGrid.Add(cancelButton, 0, 0);
            buttonGrid.Add(saveButton, 1, 0);

            Content = new Grid
            {
                Padding = 20,
                Children =
                {
                    (new Frame
                    {
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Fill,
                        MaximumWidthRequest = 420,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 14,
                            Children =
                            {
                                (new Label
                                {
                                    Text = "Add Pending Idea",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold
                                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                                _activityNameEntry,
                                _categoryPicker,
                                _newCategoryEntry,
                                _errorLabel,
                                buttonGrid
                            }
                        }
                    }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground)
                }
            };
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            var activityName = _activityNameEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(activityName))
            {
                ShowError("Enter an activity name.");
                return;
            }

            var selected = _categoryPicker.SelectedItem?.ToString() ?? "";
            var category = selected == NewCategoryOption
                ? _newCategoryEntry.Text?.Trim() ?? ""
                : selected.Trim();

            if (string.IsNullOrWhiteSpace(category))
            {
                ShowError("Choose or enter a category.");
                return;
            }

            _completion.TrySetResult(new PendingActivityIdeaInput(activityName, category));
            await Navigation.PopModalAsync();
        }

        private void ShowError(string message)
        {
            _errorLabel.Text = message;
            _errorLabel.IsVisible = true;
        }

        protected override bool OnBackButtonPressed()
        {
            _completion.TrySetResult(null);
            return base.OnBackButtonPressed();
        }

        public static async Task<PendingActivityIdeaInput?> ShowAsync(INavigation navigation, List<string> categories)
        {
            var page = new PendingActivityIdeaModalPage(categories);
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
    }

    private sealed class PendingActivityIdeaProcessPage : Bannister.Views.ModeThemedPage
    {
        private readonly TaskCompletionSource<PendingIdeaProcessAction> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _isCompleting;

        private PendingActivityIdeaProcessPage(PendingActivityIdea idea, int count)
        {
            Title = "Process Pending Idea";


            var createButton = (new Button
            {
                Text = "Create",
                CornerRadius = 8,
                HeightRequest = 44
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            createButton.Clicked += async (_, _) => await CompleteAsync(PendingIdeaProcessAction.Create);

            var skipButton = (new Button
            {
                Text = "Skip",
                CornerRadius = 8,
                HeightRequest = 44
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            skipButton.Clicked += async (_, _) => await CompleteAsync(PendingIdeaProcessAction.Skip);

            var cancelButton = (new Button
            {
                Text = "Cancel",
                CornerRadius = 8,
                HeightRequest = 44
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
            cancelButton.Clicked += async (_, _) => await CompleteAsync(PendingIdeaProcessAction.Cancel);

            var buttonGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                RowSpacing = 10,
                ColumnSpacing = 10
            };
            buttonGrid.Add(createButton, 0, 0);
            buttonGrid.Add(skipButton, 1, 0);
            buttonGrid.Add(cancelButton, 0, 1);
            Grid.SetColumnSpan(cancelButton, 2);

            Content = new Grid
            {
                Padding = 20,
                Children =
                {
                    (new Frame
                    {
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Fill,
                        MaximumWidthRequest = 460,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 14,
                            Children =
                            {
                                (new Label
                                {
                                    Text = "Process Pending Idea",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold
                                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                                (new Label
                                {
                                    Text = $"Pending idea 1 of {count}",
                                    FontSize = 13,
                                    FontAttributes = FontAttributes.Bold
                                }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true),
                                (new Label
                                {
                                    Text = $"Name: {idea.ActivityName}\nCategory: {idea.ActivityCategory}",
                                    FontSize = 15,
                                    LineBreakMode = LineBreakMode.WordWrap
                                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                                (new Label
                                {
                                    Text = "Open Activity Creation with these pre-filled? You can edit them before saving.",
                                    FontSize = 13,
                                    LineBreakMode = LineBreakMode.WordWrap
                                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                                buttonGrid
                            }
                        }
                    }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground)
                }
            };
        }

        private async Task CompleteAsync(PendingIdeaProcessAction action)
        {
            if (_isCompleting)
                return;

            _isCompleting = true;
            _completion.TrySetResult(action);
            await Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            if (_isCompleting)
                return true;

            _isCompleting = true;
            _completion.TrySetResult(PendingIdeaProcessAction.Cancel);
            return base.OnBackButtonPressed();
        }

        public static async Task<PendingIdeaProcessAction> ShowAsync(INavigation navigation, PendingActivityIdea idea, int count)
        {
            var page = new PendingActivityIdeaProcessPage(idea, count);
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
    }
}
