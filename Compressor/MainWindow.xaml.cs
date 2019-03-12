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

        private void CompressMpeg_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Select Frames",
                Filter = "frames files (*.jpg;*.jpeg;*.bmp)|*.jpg;*.jpeg;*.bmp",
                Multiselect = true,
                RestoreDirectory = true
            };


            if (fileDialog.ShowDialog() == true)
            {
                var outPath = fileDialog.FileName.Substring(0, fileDialog.FileName.LastIndexOf("\\")) + "\\frames.cmpx";
                Task.Factory.StartNew(() =>
                {
                    var result = Compress.CompressIPFrames(fileDialog.FileNames, 12, outPath);
                    Dispatcher.Invoke((Action)(
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
                Title = "Open CMPX File",
                Filter = "cmpx files (*.cmpx)|*.cmpx",
                RestoreDirectory = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                Task.Factory.StartNew(() =>
                {
                    frames = Compress.DecompressIPFrames(fileDialog.FileName);

                    frameIndex = 0;
                    SetDisplayImage(frames[frameIndex]);
                });

            }

        }

        private Bitmap[] frames;
        private int frameIndex = 0;

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (frames == null || frames.Length < 2)
            {
                return;
            }
            if (frameIndex > 0)
            {
                SetDisplayImage(frames[--frameIndex]);
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (frames == null || frames.Length < 2)
            {
                return;
            }
            if (frameIndex < frames.Length-1)
            {
                SetDisplayImage(frames[++frameIndex]);

            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
        private void SetDisplayImage(Bitmap bm)
        {
            IntPtr handle = bm.GetHbitmap();
            try
            {
                Dispatcher.Invoke((Action)(
                    () => image.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                ));
            }
            finally { DeleteObject(handle); }

            UpdateFramePagination();
        }

        private void UpdateFramePagination()
        {
            Dispatcher.Invoke((Action)(
                () => InfoText.Text = frameIndex + 1 + " / " + frames.Length
            ));
        }


    }
}
