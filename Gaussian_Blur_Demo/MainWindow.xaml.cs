using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Gaussian_Blur_Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public unsafe partial class MainWindow : Window
    {

        // Import of the C++ Gaussian Blur function
        [DllImport(@"..\\..\\..\\Libs\\Gaussian_Blur_Lib.dll")]
        private static extern void blur_image(byte[] image, byte[] result, byte[] mask, int mask_sum, int width, int height);

        // Regex used to detect number symbols
        private static readonly Regex numberRegex = new Regex("[^0-9]+");

        // Variable used to store the path to the currently selected image
        private string imagePath;
        

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Helper methods
        /**
         * Check if a string can be converted to an integer
         * @param text string to be tested
         * @returns information if text can be converted to an integer
         */
        private bool IsInteger(string text)
        {
            // If the attempt to convert the string to an integer throws an exception return false, otherwise return true
            try
            {
                System.Convert.ToInt32(text);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /**
         * Check if the value of the mask TextBox is correct and change it accordingly if it is not
         */
        private void CheckMaskWeight(object sender)
        {
            // If the current value of the selected TextBox is not an integer restore the old value and stop function
            if (!IsInteger(((TextBox)sender).Text))
            {
                ((TextBox)sender).Text = "1";
                return;
            }

            // If the current value of the selected TextBox is larger than 255 set it to 255
            if (System.Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                ((TextBox)sender).Text = "255";
                return;
            }

            // If the current value of the selected TextBox is equal to 0 set it to 1
            if (System.Convert.ToInt32(((TextBox)sender).Text) == 0)
            {
                ((TextBox)sender).Text = "1";
            }
        }

        /**
         * Removes the alpha channel from given array of pixels
         */
        private byte[] RemoveAlphaChannel(byte[] RGBAPixels)
        {
            // Calculate the length of the array after the removal of the alpha channel
            int RGBPixelsLength = (RGBAPixels.Length / 4) * 3;

            // Create a new array of the correct size
            byte[] RGBPixels = new byte[RGBPixelsLength];

            // Iterate over the original array
            for(int i = 0, j = 0; i < RGBAPixels.Length; i++)
            {
                // Copy values to the new array, ignoring alpha channel values
                if ((i + 1) % 4 == 0)
                {
                    continue;
                }
                RGBPixels[j] = RGBAPixels[i];
                j++;
            }
            return RGBPixels;
        }

        /**
         * Adds the alpha channel to a given array of pixels
         */
        private byte[] AddAlphaChannel(byte[] RGBPixels)
        {
            // Calculate the length of the array after the addition of the alpha channel
            int RGBAPixelsLength = (RGBPixels.Length / 3) * 4;

            // Create a new array of the correct size
            byte[] RGBAPixels = new byte[RGBAPixelsLength];

            // Iterate over the original array
            for(int i = 0, j = 0; i < RGBAPixels.Length; i++)
            {
                // Copy values to the new array, adding an additional alpha channel value every four iterations
                if ((i + 1) % 4 == 0)
                {
                    RGBAPixels[i] = 255;
                }
                else
                {
                    RGBAPixels[i] = RGBPixels[j];
                    j++;
                }
            }

            return RGBAPixels;
        }

        /**
         * Adds a one pixel wide gray border around the image
         */
        private byte[] AddImagePadding(byte[] image, int width, int height)
        {
            // Calculate the size of the image after padding
            int paddedImageLength = image.Length + (3 * 2 * width) + (3 * 2 * height) + 12;
            int paddedWidth = width + 2; // Padded width in pixels

            // Create a new array of the correct size
            byte[] paddedImage = new byte[paddedImageLength];

            // Create the top of the padding border
            for (int i = 0; i < paddedWidth * 3; i += 3)
            {
                // Create one gray pixel
                paddedImage[i] = 128;
                paddedImage[i + 1] = 128;
                paddedImage[i + 2] = 128;
            }

            // Copy original image and add side padding border
            for (int i = paddedWidth * 3, j = 0; i < paddedImage.Length - (paddedWidth * 3); i += 3)
            {
                if (i % (paddedWidth * 3) == 0 || i % (paddedWidth * 3) == ((paddedWidth - 1) * 3))
                {
                    paddedImage[i] = 128;
                    paddedImage[i + 1] = 128;
                    paddedImage[i + 2] = 128;
                    continue;
                }
                paddedImage[i] = image[j];
                paddedImage[i + 1] = image[j + 1];
                paddedImage[i + 2] = image[j + 2];
                j += 3;
            }

            // Create the bottom of the padding border
            for (int i = paddedImage.Length - (paddedWidth * 3); i < paddedImage.Length; i += 3)
            {
                // Create one gray pixel
                paddedImage[i] = 128;
                paddedImage[i + 1] = 128;
                paddedImage[i + 2] = 128;
            }

            return paddedImage;
        }

        /**
         * Removes a one pixel wide border around the image
         */
        private byte[] RemoveImagePadding(byte[] image, int paddedWidth, int paddedHeight)
        {
            // Calculate the size of the image after removing padding
            int unpaddedImageLenght = image.Length - (3 * 2 * paddedWidth) - (3 * 2 * paddedHeight);

            // Create a new array of the correct size
            byte[] unpaddedImage = new byte[unpaddedImageLenght];

            // Copy image while ignoring borders
            for (int i = paddedWidth * 3, j = 0; i < image.Length - (paddedWidth * 3); i += 3)
            {
                if (i % (paddedWidth * 3) == 0 || i % (paddedWidth * 3) == ((paddedWidth - 1) * 3))
                {
                    continue;
                }
                unpaddedImage[j] = image[i];
                unpaddedImage[j + 1] = image[i + 1];
                unpaddedImage[j + 2] = image[i + 2];
                j += 3;
            }

            return unpaddedImage;
        }

        #endregion

        #region Misc. events
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

        #endregion

        #region Run program

        /**
         * Response to the Click event on the StartButton
         */
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            #region Initial checks

            // If an image is not loaded display proper information and stop program
            if (UnblurredImage.Source == null)
            {
                string text = "You first have to load an image to run the program";
                string caption = "No image loaded";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult result = MessageBox.Show(text, caption, button, icon);
                return;
            }

            // If a dll is not selected display proper information and stop program
            if (!((bool)AsmCheckbox.IsChecked || (bool)HighLevelCheckbox.IsChecked))
            {
                string text = "You first have to choose a library to run the program";
                string caption = "No library selected";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult result = MessageBox.Show(text, caption, button, icon);
                return;
            }

            #endregion

            #region Data preparation

            // Create a new bitmapImage from chosen image
            BitmapImage initialImage = new BitmapImage();
            initialImage.BeginInit();
            initialImage.UriSource = new System.Uri(imagePath, System.UriKind.Absolute);
            initialImage.EndInit();

            // Save information about the image
            int height = initialImage.PixelHeight;
            int width = initialImage.PixelWidth;
            int stride = width * initialImage.Format.BitsPerPixel / 8;
            byte[] initialImageArray = new byte[height * stride];

            // Convert bitmapImage to an array of pixel values
            initialImage.CopyPixels(initialImageArray, stride, 0);

            // Remove the alpha channel from the array of pixel values
            initialImageArray = RemoveAlphaChannel(initialImageArray);

            // Create an array for the blurred image
            byte[] blurredImageArray = new byte[initialImageArray.Length];

            /////////////////////////////////////////////////////////////////////
            // Temporary for testing ////////////////////////////////////////////
            //initialImageArray.CopyTo(blurredImageArray, 0);
            /////////////////////////////////////////////////////////////////////

            // Add image padding
            initialImageArray = AddImagePadding(initialImageArray, width, height);

            //TODO: Implement image splitting for multithreading

            // Save mask values to an array
            byte[] mask = new byte[9];
            mask[0] = System.Convert.ToByte(MaskTopLeft.Text);
            mask[1] = System.Convert.ToByte(MaskTopMiddle.Text);
            mask[2] = System.Convert.ToByte(MaskTopRight.Text);
            mask[3] = System.Convert.ToByte(MaskMiddleLeft.Text);
            mask[4] = System.Convert.ToByte(MaskCenter.Text);
            mask[5] = System.Convert.ToByte(MaskMiddleRight.Text);
            mask[6] = System.Convert.ToByte(MaskBottomLeft.Text);
            mask[7] = System.Convert.ToByte(MaskBottomMiddle.Text);
            mask[8] = System.Convert.ToByte(MaskBottomRight.Text);

            // Calculate sum of mask weights
            int maskWeightSum = 0;
            for (int i = 0; i < 9; i++)
            {
                maskWeightSum += mask[i];
            }

            #endregion

            // Run the C++ library function
            if ((bool)HighLevelCheckbox.IsChecked)
            {
                blur_image(initialImageArray, blurredImageArray, mask, maskWeightSum, width, height);
            }

            // Run the Assembly function
            else if ((bool)AsmCheckbox.IsChecked)
            {

            }

            // Add the alpha channel back to the blurred image
            blurredImageArray = AddAlphaChannel(blurredImageArray);

            // Paste the contents of the blurredImageArray onto a WriteableBitmap
            WriteableBitmap blurredImageWriteable = new WriteableBitmap(width, height, initialImage.DpiX, initialImage.DpiY, initialImage.Format, initialImage.Palette);
            Int32Rect blurredImageRectangle = new Int32Rect(0, 0, width, height);
            blurredImageWriteable.WritePixels(blurredImageRectangle, blurredImageArray, stride, 0);

            // Convert the blurredImageWriteable to a BitmapImage
            BitmapImage blurredImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                // Save a BitmapFrame of the blurredImageWriteable to a MemoryStream
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(blurredImageWriteable));
                encoder.Save(stream);

                // Create a BitmapImage from MemoryStream
                blurredImage.BeginInit();
                blurredImage.CacheOption = BitmapCacheOption.OnLoad;
                blurredImage.StreamSource = stream;
                blurredImage.EndInit();
                blurredImage.Freeze();
            }

            // Display the blurred image on the UI
            BlurredImage.Source = blurredImage;

            // Create a filename and path for the blurred image
            string blurredImagePath = Path.GetDirectoryName(imagePath) + "\\";
            blurredImagePath += Path.GetFileNameWithoutExtension(imagePath) + "_Blurred" + Path.GetExtension(imagePath);

            // Create a frame of the blurredImage
            PngBitmapEncoder blurredImageEncoder = new PngBitmapEncoder();
            blurredImageEncoder.Frames.Add(BitmapFrame.Create(blurredImage));

            // Save the created frame as a file
            using (System.IO.FileStream fileStream = new System.IO.FileStream(blurredImagePath, System.IO.FileMode.Create))
            {
                blurredImageEncoder.Save(fileStream);
            }
        }

        #endregion

        #region Mask Textbox events

        /**
         * Detect keypress when typing in one of the corner mask textboxes
         */
        private void MaskCorner_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check if pressed button is Enter
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // Check if the current value of the selected TextBox is correct
                CheckMaskWeight(sender);

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
                // Check if the current value of the selected TextBox is correct
                CheckMaskWeight(sender);

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
            // Check if the current value of the selected TextBox is correct
            CheckMaskWeight(sender);

            // Check if pressed button in Enter
            if (e.Key == System.Windows.Input.Key.Return)
            {
                // Clear focus on currently selected textbox
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        /**
         * Detect if focus on a corner mask textbox is lost through other means than pressing return
         */
        private void MaskCorner_LostFocus(object sender, RoutedEventArgs e)
        {
            // Check if the current value of the selected TextBox is correct
            CheckMaskWeight(sender);

            // Set all corner mask textboxes to the same value
            MaskTopLeft.Text = MaskTopRight.Text = MaskBottomLeft.Text = MaskBottomRight.Text = ((TextBox)sender).Text;
        }

        /**
         * Detect if focus on a side mask textbox is lost through other means than pressing return
         */
        private void MaskSide_LostFocus(object sender, RoutedEventArgs e)
        {
            // Check if the current value of the selected TextBox is correct
            CheckMaskWeight(sender);

            // Set all side mask textboxes to the same value
            MaskTopMiddle.Text = MaskMiddleLeft.Text = MaskMiddleRight.Text = MaskBottomMiddle.Text = ((TextBox)sender).Text;
        }

        /**
         * Detect if focus on the center mask textbox is lost through other means than pressing return
         */
        private void MaskCenter_LostFocus(object sender, RoutedEventArgs e)
        {
            // Check if the current value of the selected TextBox is correct
            CheckMaskWeight(sender);
        }

        /**
         * Prevent non integer values from being input in mask textboxes
         */
        private void Mask_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Allow the event to be handled only if the input is a number
            e.Handled = numberRegex.IsMatch(e.Text);
        }

        #endregion

        #region Load image
        /**
         * Response to the Click event on the LoadImageButton
         */
        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            // Create new instance of an OpenFileDialog, that will be used to find the image to be opened
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Allow choosing only image type files
            openFileDialog.Filter = "Image files (*.JPG;*.PNG;*.BMP)|*.JPG;*.PNG;*.BMP";

            // Save the full path of the selected file to the imagePath variable
            if(openFileDialog.ShowDialog() == true)
            {
                imagePath = new string(openFileDialog.FileName);
            }

            // Check if chosen file is an image
            if(Path.GetExtension(imagePath) == ".jpg" || Path.GetExtension(imagePath) == ".png" || Path.GetExtension(imagePath) == ".bmp")
            {
                // Create a new bitmapImage from chosen file path
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new System.Uri(imagePath, System.UriKind.Absolute);
                bitmapImage.EndInit();

                // Set the new bitmapImage as source of the UnblurredImage to be displayed
                UnblurredImage.Source = bitmapImage;
            }
        }

        #endregion

    }
}
