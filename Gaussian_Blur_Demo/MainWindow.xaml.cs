using System.Windows;
using System.Drawing;
using System.Windows.Controls;

namespace Gaussian_Blur_Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void ThreadCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ThreadCountText.Text = ((Slider)sender).Value.ToString();
        }
    }
}
