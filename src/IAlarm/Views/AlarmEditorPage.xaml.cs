using IAlarm.ViewModels;

namespace IAlarm.Views;

public partial class AlarmEditorPage : ContentPage
{
    public AlarmEditorPage(AlarmEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
