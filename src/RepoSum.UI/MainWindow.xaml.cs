using System.Windows;
using RepoSum.UI.ViewModels;

namespace RepoSum.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        PatPasswordBox.Password = viewModel.PersonalAccessToken ?? string.Empty;
    }

    private void PatPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PersonalAccessToken = PatPasswordBox.Password;
        }
    }
}