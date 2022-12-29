using System.Windows;
using System.Drawing;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace Gaussian_Blur_Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Regex numberRegex = new Regex("[^0-9]+");

        public MainWindow()
        {
            InitializeComponent();
        }

        /**
         * All actions performed upod initial window load
         */
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // By default the selected thread count is set to the available number of processor cores
            ThreadCountSlider.Value = System.Environment.ProcessorCount;
        }

        /**
         * Response to the ValueChanged event on the ThreadCountSlider
         */
        private void ThreadCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Change description text to the current slider value
            ThreadCountText.Text = ((Slider)sender).Value.ToString();
        }

        /**
         * Response to the Checked event on either the HighLevelCheckbox or AsmCheckbox
         */
        private void DllCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // If one of the checkboxes is checked the other is unchecked
            if (sender == HighLevelCheckbox)
            {
                AsmCheckbox.IsChecked = false;
            }
            else if (sender == AsmCheckbox)
            {
                HighLevelCheckbox.IsChecked = false;
            }
        }

        /**
         * Response to the Click event on the StartButton
         */
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // If there is no image loaded a proper information will be displayed and the program will not proceed
            if (UnblurredImage.Source == null)
            {
                string text = "You first have to load an image to run the program";
                string caption = "No image loaded";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage image = MessageBoxImage.Warning;
                MessageBoxResult result = MessageBox.Show(text, caption, button, image);
                return;
            }

            // If a dll is not selected a proper information will be displayed and the program will not proceed
            if (!((bool)AsmCheckbox.IsChecked || (bool)HighLevelCheckbox.IsChecked))
            {
                string text = "You first have to choose a library to run the program";
                string caption = "No library selected";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage image = MessageBoxImage.Warning;
                MessageBoxResult result = MessageBox.Show(text, caption, button, image);
                return;
            }

            // TODO: Implement image gaussian blur algorithm
        }

        /**
         * Detect keypress when typing in one of the corner mask textboxes
         */
        private void MaskCorner_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check if pressed button is Enter
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // Set all corner mask textboxes to the same value
                MaskTopLeft.Text = MaskTopRight.Text = MaskBottomLeft.Text = MaskBottomRight.Text = ((TextBox)sender).Text;

                // Clear focus on currently seelcted textbox
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        /**
         * Detect keypress when typing in one of the side mask textboxes
         */
        private void MaskSide_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check if pressed button is Enter
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // Set all side mask textboxes to the same value
                MaskTopMiddle.Text = MaskMiddleLeft.Text = MaskMiddleRight.Text = MaskBottomMiddle.Text = ((TextBox)sender).Text;

                // Clear focus on currently selected textbox
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        /**
         * Detect keypress when typing in the center mask textbox
         */
        private void MaskCenter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check if pressed button in Enter
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // Clear focus on currently selected textbox
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        /**
         * Detects if focus on a corner mask textbox is lost through other means than pressing return
         */
        private void MaskCorner_LostFocus(object sender, RoutedEventArgs e)
        {
            // Set all corner mask textboxes to the same value
            MaskTopLeft.Text = MaskTopRight.Text = MaskBottomLeft.Text = MaskBottomRight.Text = ((TextBox)sender).Text;
        }

        /**
         * Detects if focus on a side mask textbox is lost through other means than pressing return
         */
        private void MaskSide_LostFocus(object sender, RoutedEventArgs e)
        {
            // Set all side mask textboxes to the same value
            MaskTopMiddle.Text = MaskMiddleLeft.Text = MaskMiddleRight.Text = MaskBottomMiddle.Text = ((TextBox)sender).Text;
        }

        /**
         * Prevents non integer values from being input in mask textboxes
         */
        private void Mask_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Allow the event to be handled only if it is a number
            e.Handled = numberRegex.IsMatch(e.Text);
        }
    }
}
