using IAlarm.ViewModels;

namespace IAlarm.Views;

public partial class StopwatchPage : ContentPage
{
    readonly StopwatchViewModel viewModel;

    public StopwatchPage(StopwatchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Appearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        viewModel.Disappearing();
    }
}
