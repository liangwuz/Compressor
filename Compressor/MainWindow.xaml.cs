using System.Windows;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace Compressor
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

        private void CompressJpeg_Click(object sender, RoutedEventArgs e)
        {
            // file dialog
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Open Image File",
                Filter = "image files (*.jpg;*.jpeg;*.bmp)|*.jpg;*.jpeg;*.bmp",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                // out file path/image name.icmpx
                var outPath = fileDialog.FileName.Substring(0, fileDialog.FileName.LastIndexOf(".")) + ".icmpx";
                Task.Factory.StartNew(() =>
                {
                    var result = Compress.CompressImage(fileDialog.FileName, outPath);
                    InfoText.Dispatcher.Invoke((Action)(
                        () => InfoText.Text = result.ToString()
                    ));
                });
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        private void DeCompressJpeg_Click(object sender, RoutedEventArgs e)
        {
            // file dialog
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Open Icmpx File",
                Filter = "ICMPX files (*.icmpx)|*.icmpx",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                // out file path/image name.icmpx
                //var outPath = fileDialog.FileName.Substring(0, fileDialog.FileName.LastIndexOf(".")) + ".jpeg";
                Task.Factory.StartNew(() =>
                {   
                    Bitmap bm = Compress.DecompressImage(fileDialog.FileName);
                    var handle = bm.GetHbitmap();
                    try
                    {
                        InfoText.Dispatcher.Invoke((Action)(
                            () => image1.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                        ));
                    } finally { DeleteObject(handle); }
                });
            }
        }


        private void CompressMpeg_Click(object sender, RoutedEventArgs e)
        {
            string iframe = null, pframe = null;

            // file dialog
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Open I Frame",
                Filter = "image files (*.jpg;*.jpeg;*.bmp)|*.jpg;*.jpeg;*.bmp",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                iframe = fileDialog.FileName;
            }

            // file dialog
            fileDialog = new OpenFileDialog
            {
                Title = "Open P Frame",
                Filter = "image files (*.jpg;*.jpeg;*.bmp)|*.jpg;*.jpeg;*.bmp",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                pframe = fileDialog.FileName;
            }

            if (iframe != null && pframe != null)
            {
                // out file path/image name.icmpx
                var outPath = fileDialog.FileName.Substring(0, fileDialog.FileName.LastIndexOf(".")) + ".mcmpx";
                Task.Factory.StartNew(() =>
                {
                    var result = Compress.CompressIPframes(iframe, pframe, outPath);
                    InfoText.Dispatcher.Invoke((Action)(
                        () => InfoText.Text = result.ToString()
                    ));
                });
            }
        }


        private void DecompressMpeg_Click(object sender, RoutedEventArgs e)
        {

            // file dialog
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Open Mcmpx File",
                Filter = "MCMPX files (*.mcmpx)|*.mcmpx",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                Task.Factory.StartNew(() =>
                {
                    Bitmap[] bms = Compress.DecompressIPframes(fileDialog.FileName);
                    var handle = bms[0].GetHbitmap();
                    try
                    {
                        InfoText.Dispatcher.Invoke((Action)(
                            () => image1.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                        ));
                    }
                    finally { DeleteObject(handle); }

                    handle = bms[1].GetHbitmap();
                    try
                    {
                        InfoText.Dispatcher.Invoke((Action)(
                            () => image2.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                        ));
                    }
                    finally { DeleteObject(handle); }
                });

            }

        }


    }
}
