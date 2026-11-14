using IAlarm.ViewModels;

namespace IAlarm.Views;

public partial class AlarmsPage : ContentPage
{
    readonly AlarmsViewModel viewModel;

    public AlarmsPage(AlarmsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.AppearingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        viewModel.Disappearing();
    }
}
