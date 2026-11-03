// Location: MainWindow.xaml.cs
using System.Windows;
using BomComparer.ViewModels;

namespace BomComparer
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel; // Binds the UI to our ViewModel
        }



        // Location: MainWindow.xaml.cs
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dataGrid && dataGrid.CurrentItem is Models.BomDifference diff)
            
                
                {
                    var vm = (ViewModels.MainViewModel)DataContext;

                    // Pass the entire loaded dictionary instead of base/new files
                    var popup = new FrequencyMismatchWindow(diff.PartNo, vm.LoadedBoms);
                    popup.Owner = this;
                    popup.ShowDialog();
                }
            
        }












    }
}