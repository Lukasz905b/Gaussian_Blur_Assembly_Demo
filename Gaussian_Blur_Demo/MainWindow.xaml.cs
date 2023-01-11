using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Office.Interop.Excel;

using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace Gaussian_Blur_Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        // Import of the C++ Gaussian Blur function
        [DllImport(@"..\\..\\..\\Libs\\Gaussian_Blur_Lib.dll")]
        private static extern void blur_image(byte[] image, byte[] result, byte[] mask, int mask_sum, int width, int height);

        // Import of the Assembly Gaussian Blur function
        [DllImport(@"..\\..\\..\\Libs\\Gaussian_Blur_Lib_Asm.dll")]
        private static extern void BlurImageAsm(byte[] image, byte[] result, byte[] mask, int mask_sum, int width, int height);

        // Regex used to detect number symbols
        private static readonly Regex numberRegex = new Regex("[^0-9]+");

        // Variable used to store the path to the currently selected image
        private string imagePath;

        // A list used to store execution times when running a test
        private List<int> executionTimes;

        // Test counter
        private int testCounter;


        public MainWindow()
        {
            InitializeComponent();
            testCounter = 1;
            executionTimes = new List<int>();
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

            // If the current value of the selected TextBox is larger than 64 set it to 64
            if (System.Convert.ToInt32(((TextBox)sender).Text) > 64)
            {
                ((TextBox)sender).Text = "64";
                return;
            }

            // If the current value of the selected TextBox is equal to 0 set it to 1
            if (System.Convert.ToInt32(((System.Windows.Controls.TextBox)sender).Text) == 0)
            {
                ((TextBox)sender).Text = "1";
            }
        }

        /**
         * Adds a one pixel wide gray border around the image
         */
        private byte[] AddImagePadding(byte[] image, int width, int height)
        {
            // Calculate the size of the image after padding
            int paddedImageLength = image.Length + (4 * 2 * width) + (4 * 2 * height) + 16;
            int paddedWidth = width + 2; // Padded width in pixels

            // Create a new array of the correct size
            byte[] paddedImage = new byte[paddedImageLength];

            // Create the top of the padding border
            for (int i = 0; i < paddedWidth * 4; i += 4)
            {
                // Create one gray pixel
                paddedImage[i] = 128;
                paddedImage[i + 1] = 128;
                paddedImage[i + 2] = 128;
                paddedImage[i + 3] = 255;
            }

            // Copy original image and add side padding border
            for (int i = paddedWidth * 4, j = 0; i < paddedImage.Length - (paddedWidth * 4); i += 4)
            {
                if (i % (paddedWidth * 4) == 0 || i % (paddedWidth * 4) == ((paddedWidth - 1) * 4))
                {
                    // One side padding gray pixel
                    paddedImage[i] = 128;
                    paddedImage[i + 1] = 128;
                    paddedImage[i + 2] = 128;
                    paddedImage[i + 3] = 255;
                    continue;
                }

                // Copy one pixel from original image
                paddedImage[i] = image[j];
                paddedImage[i + 1] = image[j + 1];
                paddedImage[i + 2] = image[j + 2];
                paddedImage[i + 3] = image[j + 3];
                j += 4;
            }

            // Create the bottom of the padding border
            for (int i = paddedImage.Length - (paddedWidth * 4); i < paddedImage.Length; i += 4)
            {
                // Create one gray pixel
                paddedImage[i] = 128;
                paddedImage[i + 1] = 128;
                paddedImage[i + 2] = 128;
                paddedImage[i + 3] = 255;
            }

            return paddedImage;
        }

        private byte[][] SplitImageForThreads(byte[] image, int width, int height, int threadCount)
        {
            // Padded width of the image in bytes
            int widthBytes = (width + 2) * 4;

            // Create an array that will store the height of the image passed to each thread
            int[] splitImageHeights = new int[threadCount];

            // Equalize size of images in each thread, so the difference in size is at most equal to one row
            for(int i = 0; i < (height % threadCount); i++)
            {
                splitImageHeights[i] = (height / threadCount) + 1;
            }
            for(int i = (height % threadCount); i < splitImageHeights.Length; i++)
            {
                splitImageHeights[i] = (height / threadCount);
            }

            // Create an image array for each thread
            byte[][] splitImage = new byte[threadCount][];

            // Split image for each thread
            for(int i = 0; i < threadCount; i++)
            {
                // Calculate the length of the split image and starting index of the image part
                int length = splitImageHeights[i] * widthBytes + 2 * widthBytes;

                // Calculate the index from which copying should start
                int startingIndex = 0;
                for(int j = 0; j < i; j++)
                {
                    startingIndex += splitImage[j].Length;
                }
                startingIndex = startingIndex - i * 2 * widthBytes;

                // Create an array of the correct size
                splitImage[i] = new byte[length];

                // Copy part of original image into array
                System.Array.Copy(image, startingIndex, splitImage[i], 0, length);
            }

            return splitImage;
        }

        private void PrintToExcel()
        {
            Microsoft.Office.Interop.Excel.Application excelApp = new Microsoft.Office.Interop.Excel.Application();
            excelApp.Visible = true;

            _Workbook excelWorkbook = (_Workbook)(excelApp.Workbooks.Add(""));
            _Worksheet excelWorksheet = (_Worksheet)excelWorkbook.ActiveSheet;

            for (int i = 0; i < 64; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    excelWorksheet.Cells[(i + 1), (j + 1)] = executionTimes[j + (i * 15)];
                }
            }

            excelApp.Visible = false;
            excelApp.UserControl = false;
            string path = Path.GetDirectoryName(imagePath) + "\\test_" + testCounter + ".xlsx";
            testCounter++;
            excelWorkbook.SaveAs(path, XlFileFormat.xlWorkbookDefault, Type.Missing, Type.Missing,
                      false, false, XlSaveAsAccessMode.xlNoChange,
                      Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
            excelWorkbook.Close();
            excelApp.Quit();
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

            // Create an array for the blurred image
            byte[] blurredImageArray = new byte[initialImageArray.Length];

            // Add image padding
            initialImageArray = AddImagePadding(initialImageArray, width, height);

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

            #region Split data for multithreading

            // Get current selected thread count
            int threadCount = System.Convert.ToInt32(ThreadCountText.Text);

            // Create tasks
            Task[] tasks = new Task[threadCount];

            // Split image for each task
            byte[][] initialImageSplitArrays = SplitImageForThreads(initialImageArray, width, height, threadCount);

            // Create a result array for each task
            byte[][] blurredImageSplitArrays = new byte[threadCount][];

            // Create an array to save image height for each task
            int[] threadImageHeight = new int[threadCount];

            // Set the array for each task to the correct size and save height for each task
            for (int i = 0; i < (height % threadCount); i++)
            {
                threadImageHeight[i] = (height / threadCount) + 1;
                blurredImageSplitArrays[i] = new byte[threadImageHeight[i] * width * 4];
            }
            for(int i = (height % threadCount); i < threadCount; i++)
            {
                threadImageHeight[i] = (height / threadCount);
                blurredImageSplitArrays[i] = new byte[threadImageHeight[i] * width * 4];
            }

            #endregion

            #region Function execution

            // Stopwatch used to measure execution time
            Stopwatch stopwatch = new Stopwatch();

            // Run the C++ function
            if((bool)HighLevelCheckbox.IsChecked)
            {
                // Start the stopwatch
                stopwatch.Start();

                // Start all threads for C++ function
                for (int i = 0; i < threadCount; i++)
                {
                    // This stops out of bound exceptions
                    int y = i;

                    tasks[y] = Task.Factory.StartNew(() => blur_image(initialImageSplitArrays[y], blurredImageSplitArrays[y], mask, maskWeightSum, width, threadImageHeight[y]));
                }

                // Wait until all tasks are done
                Task.WaitAll(tasks);

                // Stop measuring time and display result
                stopwatch.Stop();
                ExecutionTimeText.Text = "" + stopwatch.ElapsedMilliseconds + " ms";
            }

            //Run the Assembly function
            else if ((bool)AsmCheckbox.IsChecked)
            {
                // Start the stopwatch
                stopwatch.Start();

                // Start all threads for assembly function
                for (int i = 0; i < threadCount; i++)
                {
                    // This stops out of bound exceptions
                    int y = i;

                    tasks[y] = Task.Factory.StartNew(() => BlurImageAsm(initialImageSplitArrays[y], blurredImageSplitArrays[y], mask, maskWeightSum, width, threadImageHeight[y]));
                }

                // Wait until all tasks are done
                Task.WaitAll(tasks);

                // Stop measuring time and display result
                stopwatch.Stop();
                ExecutionTimeText.Text = "" + stopwatch.ElapsedMilliseconds + " ms";
            }

            #endregion

            #region Display and save result

            int currentIndex = 0;
            // Join all parts of the blurred image
            for (int i = 0; i < threadCount; i++)
            {
                System.Array.Copy(blurredImageSplitArrays[i], 0, blurredImageArray, currentIndex, blurredImageSplitArrays[i].Length);
                currentIndex += blurredImageSplitArrays[i].Length;
            }

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
            #endregion

            #region Run test

            string executionTimeString = ExecutionTimeText.Text;
            string[] executionTimeStringSplit = executionTimeString.Split(" ");
            int executionTimeMiliseconds = System.Convert.ToInt32(executionTimeStringSplit[0]);

            if((bool)TestCheckbox.IsChecked)
            {
                executionTimes.Clear();
                TestCheckbox.IsChecked = false;
                TestCheckbox.IsEnabled = false;
                for(int i = 1; i <= 64; i++)
                {
                    ThreadCountSlider.Value = i;
                    for (int j = 0; j < 15; j++)
                    {
                        StartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                }
                TestCheckbox.IsEnabled = true;
                TestCheckbox.IsChecked = true;
                PrintToExcel();
            }

            executionTimes.Add(executionTimeMiliseconds);

            #endregion

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
                BlurredImage.Source = null;
            }
        }

        #endregion

    }
}
