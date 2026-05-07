using CommunityToolkit.Mvvm.ComponentModel;

namespace FormaStream.Shell.ViewModels;

public partial class SilkViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Экран сеток";
}