using Avalonia.Controls;
using Avalonia.Interactivity;
using PlistEditor.Models;
using PlistEditor.ViewModels;

namespace PlistEditor.Views;

public partial class SmbiosGenerator : Window
{
    public SmbiosGenerator()
    {
        InitializeComponent();
        DataContext = new SmbiosGeneratorViewModel();
    }

    public SmbiosGenerator(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = new SmbiosGeneratorViewModel(mainViewModel);
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SmbiosGeneratorViewModel vm && vm.GeneratedResult != null)
        {
            Close(vm.GeneratedResult);
        }
    }
}