using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using System.Drawing;
using System.IO;
using Microsoft.Win32;

namespace BarcodeGenerator
{
    public partial class MainWindow : Window
    {
        private Bitmap? _currentBarcode;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void GenerateBarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (BarcodeTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a barcode type.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string barcodeType = ((ComboBoxItem)BarcodeTypeComboBox.SelectedItem)?.Content.ToString() ?? string.Empty;
            string data = DataTextBox.Text;

            if (string.IsNullOrWhiteSpace(data))
            {
                MessageBox.Show("Please enter data to generate the barcode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BarcodePanel.Children.Clear();
            GenerateBarcode(barcodeType, data);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Select a text file containing barcodes"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string[] lines = File.ReadAllLines(openFileDialog.FileName);
                BarcodePanel.Children.Clear();
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        GenerateBarcode(((ComboBoxItem)BarcodeTypeComboBox.SelectedItem)?.Content.ToString() ?? string.Empty, line);
                    }
                }
            }
        }

        private void GenerateBarcode(string barcodeType, string data)
        {
            try
            {
                var writer = new BarcodeWriterPixelData
                {
                    Format = GetBarcodeFormat(barcodeType),
                    Options = new EncodingOptions
                    {
                        Width = 750,
                        Height = 200
                    }
                };

                var pixelData = writer.Write(data);
                using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                    try
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }

                    _currentBarcode = new Bitmap(bitmap);
                    var image = new System.Windows.Controls.Image
                    {
                        Source = BitmapToImageSource(bitmap),
                        Width = 400,
                        Height = 100,
                        Margin = new Thickness(10)
                    };
                    BarcodePanel.Children.Add(image);

                    var textBlock = new TextBlock
                    {
                        Text = data,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(10),
                        FontSize = 14
                    };
                    BarcodePanel.Children.Add(textBlock);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while generating the barcode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveBarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (BarcodePanel.Children.Count == 0)
            {
                MessageBox.Show("Please generate barcodes first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Save Barcodes",
                FileName = "barcode.png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string? directory = Path.GetDirectoryName(saveFileDialog.FileName);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(saveFileDialog.FileName) ?? "barcode";

                if (directory == null)
                {
                    MessageBox.Show("Invalid directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int index = 1;
                for (int i = 0; i < BarcodePanel.Children.Count; i += 2)
                {
                    if (BarcodePanel.Children[i] is System.Windows.Controls.Image image && BarcodePanel.Children[i + 1] is TextBlock textBlock)
                    {
                        var bitmapSource = (BitmapSource)image.Source;
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                        using (var bitmap = new Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight + 30))
                        {
                            using (var g = Graphics.FromImage(bitmap))
                            {
                                g.Clear(System.Drawing.Color.White);
                                g.DrawImage(_currentBarcode ?? throw new InvalidOperationException("Current barcode is null"), 0, 0);
                                var textSize = g.MeasureString(textBlock.Text, new System.Drawing.Font("Arial", 14));
                                var textX = (bitmap.Width - textSize.Width) / 2;
                                g.DrawString(textBlock.Text, new System.Drawing.Font("Arial", 14), System.Drawing.Brushes.Black, new System.Drawing.PointF(textX, bitmapSource.PixelHeight));
                            }

                            string filePath = Path.Combine(directory ?? string.Empty, $"{fileNameWithoutExtension}_{index}.png");
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        }

                        index++;
                    }
                }

                MessageBox.Show("Barcodes saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private ZXing.BarcodeFormat GetBarcodeFormat(string barcodeType)
        {
            return barcodeType switch
            {
                "Code128" => ZXing.BarcodeFormat.CODE_128,
                "EAN13" => ZXing.BarcodeFormat.EAN_13,
                "QRCode" => ZXing.BarcodeFormat.QR_CODE,
                "Code39" => ZXing.BarcodeFormat.CODE_39,
                "Code93" => ZXing.BarcodeFormat.CODE_93,
                "GS1-128 (UCC/EAN-128)" => ZXing.BarcodeFormat.CODE_128,
                "MSI" => ZXing.BarcodeFormat.MSI,
                "EAN8" => ZXing.BarcodeFormat.EAN_8,
                _ => throw new NotSupportedException("Barcode type not supported")
            };
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}