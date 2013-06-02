using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Threading;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using AForge.Video.FFMPEG;
using AviFile;


namespace kinectApptest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor kinectSensor;
        private WriteableBitmap colorBitmap;
        private byte[] pixelData;

        //private System.Drawing.Image[] bufferedFrames;
        private List<Bitmap> bufferedFrames = new List<Bitmap>();
        private List<Bitmap> finalFrames = new List<Bitmap>();

        private WriteableBitmap depthColorBitmap;
        private DepthImagePixel[] depthPixels;
        private byte[] pixelDataDepth;

        private bool startedCapture = false;
        private int index = 0;
        private int MAX_BUFFERED = 300;

        private ImageCodecInfo jpgEncoder;
        EncoderParameters encoderParams;

        public MainWindow()
        {
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //initializare senzor: se verifica mai intai ce senzor este conectat, iar acesta se pastreaza in atributul clasei
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.kinectSensor = potentialSensor;
                    break;
                }
            }
            //daca s-a gasit un senzor conectat, acesta isi initializeaza camerele
            if (this.kinectSensor != null)
            {
                //camera web
                this.kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.pixelData = new byte[this.kinectSensor.ColorStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(this.kinectSensor.ColorStream.FrameWidth,
                    this.kinectSensor.ColorStream.FrameHeight,
                    96.0, 96.0,
                    PixelFormats.Bgr32,
                    null);
                this.image1.Source = this.colorBitmap;
                this.kinectSensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(ColorImageReady);


                //senzorii de adancime
                this.kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                this.depthPixels = new DepthImagePixel[this.kinectSensor.DepthStream.FramePixelDataLength];
                this.pixelDataDepth = new byte[this.kinectSensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.depthColorBitmap = new WriteableBitmap(this.kinectSensor.DepthStream.FrameWidth,
                    this.kinectSensor.DepthStream.FrameHeight,
                    96.0, 96.0,
                    PixelFormats.Bgr32, null);
                this.image2.Source = this.depthColorBitmap;
                this.kinectSensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(DepthImageReady);                
                try
                {
                    this.kinectSensor.Start();
                }
                catch (Exception)
                {
                    this.kinectSensor = null;
                }
            }
            //se initializeaza un encoder pentru compresia jpg
            jpgEncoder = GetEncoder(System.Drawing.Imaging.ImageFormat.Jpeg);
            encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);

            button2.IsEnabled = false;
        }

        /**
         * Atunci cand se inchide fereastra aplicatiei, senzorul trebuie oprit pentru
         * a nu ramane diverse servicii pornite.
         **/
        private void Window_Closed(object sender, EventArgs e)
        {
            kinectSensor.Stop();
        }

        /**
         * Metoda handler pentru evenimentul creat de captarea imaginilor
         * prin camera web a senzorului.
         **/
        void ColorImageReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            bool receiveData = false;

            using (ColorImageFrame colorImageFrame = e.OpenColorImageFrame())
            {
                if (colorImageFrame != null)
                {
                    if (pixelData == null)
                    {
                        pixelData = new byte[colorImageFrame.PixelDataLength];
                    }
                    colorImageFrame.CopyPixelDataTo(pixelData);
                    receiveData = true;
                }
                else
                {
                    //nu s-au primit date
                }
                if (receiveData)
                {
                    image1.Source = colorImageFrame.ToBitmapSource();
                    //daca s-a apasat butonul "Captura" se incepe umplerea bufferului
                    if (startedCapture)
                    {
                        if (index == MAX_BUFFERED)
                        {
                            startedCapture = false;
                            button1.Content = "Captura finalizata";
                            createMovie("D:\\teste\\original.avi", bufferedFrames);
                            button2.IsEnabled = true;
                        }
                        else
                        {
                            index++;
                            bufferedFrames.Add(BitmapFromSource(colorImageFrame.ToBitmapSource()));
                        }
                    }
                }
            }
        }

        /** Metoda pentru extragerea unui Bitmap
         * dintr-un obiect de timp BitmapSource
         **/
        private Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            Bitmap bitmap;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(memoryStream);
                bitmap = new Bitmap(memoryStream);

                bitmap.Save("D:\\teste\\original" + index + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            }
           
            return bitmap;
        }


        /**
         * Metoda handler pentru evenimentele create
         * de captarea imaginilor prin senzorii de adancime.
         **/
        void DepthImageReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            bool receiveData = false;

            using (DepthImageFrame depthImageFrame = e.OpenDepthImageFrame())
            {
                if (depthImageFrame != null)
                {
                    if (pixelDataDepth == null)
                    {
                        pixelDataDepth = new byte[depthImageFrame.PixelDataLength];
                    }
                    depthImageFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    receiveData = true;
                }
                else
                {
                    //nu s-au primit date
                }
                if (receiveData)
                {
                    image2.Source = depthImageFrame.ToBitmapSource();
                    
                }
            }
        }
        
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            startedCapture = true;
            button1.Content = "Buffering...";
            button1.IsEnabled = false;
        }

        /** Handler pentru evenimentul creat de apasarea butonului "Comprima".
         * Acesta contine paralizarea compresiei fiecarui frame al stream-ului video
         * captat de la senzor.Dupa compresie se creaza un nou stream video din aceste
         * noi frameuri.
         **/
        private void button2_Click(object sender, RoutedEventArgs e)
        {
            button2.IsEnabled = false;
            Parallel.For(0, bufferedFrames.Count, i =>
            {
                compression(i);
            });
            createMovie("D:\\teste\\comprimat.avi", finalFrames);
        }

        /** Metoda care se foloseste de bibliotecile AviManager pentru a 
         * crea un stream video dintr-o lista de imagini format bitmap.
         **/
        private void createMovie(String fileName, List<Bitmap> images)
        {
            AviManager aviManager = new AviManager(fileName, false);
            VideoStream videoStream = aviManager.AddVideoStream(false, 30, images.ElementAt(0));
            foreach (var image in images)
            {
                if (finalFrames.IndexOf(image) != 0)
                {
                    videoStream.AddFrame(image);
                }
            }
            aviManager.Close();
        }

        /** Metoda pentru extragerea unui encoder dintr-o 
         * lista de encodere disponibile.
         **/
        private ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        /**
         * Metoda utilizata pentru compresia frameurilor
         **/
        private void compression(int i)
        {
            MemoryStream memoryStream = new MemoryStream();
            Bitmap bitmap = bufferedFrames.ElementAt(i);
                        
            try
            {
                bitmap.Save(memoryStream, jpgEncoder, encoderParams);
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                bitmap.Save("D:\\teste\\test"+i+".jpg",System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            catch (Exception)
            {
                Console.WriteLine("Eroare la compresia frame-ului: " + i);
            }
            finalFrames.Add(bitmap);
            memoryStream.Dispose();
        }
    }
}
