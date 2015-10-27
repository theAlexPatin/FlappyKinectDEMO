using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApplication1
{
    /// <summary>Interaction logic for MainWindow.xaml</summary>
    public partial class MainWindow : Window
    {
        // Declare variables that will be used throughout the app
        KinectSensor sensor;
        BodyFrameReader bodyFrameReader;
        ColorFrameReader colorFrameReader;
        WriteableBitmap colorBitmap;
        Body[] bodies;
        DrawingGroup drawingGroup;
        double birdHeight;
        double prevRightHandHeight;
        double prevLeftHandHeight;
        double pipeX;
        double pipeGapY;
        double pipeGapLength;
        Random randomGenerator;
        
        public MainWindow()
        {
            // Get the sensor
            sensor = KinectSensor.GetDefault();
            sensor.Open();

            // Setup readers for each source of data we want to use
            colorFrameReader = sensor.ColorFrameSource.OpenReader();
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();

            // Setup event handlers that use what we get from the readers
            colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            // Get ready to draw graphics
            drawingGroup = new DrawingGroup();

            // Initialize the components (controls) of the window
            InitializeComponent();

            // Initialize color components

            // create the bitmap to display
            colorBitmap = new WriteableBitmap(1920, 1080, 96.0, 96.0, PixelFormats.Bgr32, null);
            ColorImage.Source = colorBitmap;

            // Initialize the game components
            birdHeight = this.Height / 2; // put the bird in the middle of the screen
            prevRightHandHeight = 0;
            prevLeftHandHeight = 0;
            pipeX = -1;
            pipeGapY = 250;
            pipeGapLength = 170;
            randomGenerator = new Random();
        }

        void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // Get the current image frame in a memory-safe manner
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                // Defensive programming: Just in case the sensor frame is no longer valid, exit the function
                if (colorFrame == null)
                {
                    return;
                }
                
                using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                {
                    // Put a thread-safe lock on this data so it doesn't get modified elsewhere
                    colorBitmap.Lock();

                    // Let the application know where the image is being stored
                    colorFrame.CopyConvertedFrameDataToIntPtr(
                        colorBitmap.BackBuffer,
                        (uint)(1920 * 1080 * 4), // Width * Height * BytesPerPixel
                        ColorImageFormat.Bgra);

                    // Let the application know that it needs to redraw the screen in this area (the whole image)
                    colorBitmap.AddDirtyRect(new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight));

                    // Remove the thread-safe lock on this data
                    colorBitmap.Unlock();
                }
            }
        }

        void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                // Defensive programming: Just in case the sensor skips a frame, exit the function
                if (bodyFrame == null)
                {
                    return;
                }

                if (bodies == null)
                {
                    // Create an array of the bodies in the scene and update it
                    bodies = new Body[bodyFrame.BodyCount];
                }
                bodyFrame.GetAndRefreshBodyData(bodies);

                // For each body in the scene
                foreach (Body body in bodies)
                {
                    if (body.IsTracked)
                    {
                        var joints = body.Joints; // Get all of the joints in that body
                        if (joints[JointType.HandRight].TrackingState == TrackingState.Tracked
                            && joints[JointType.HandLeft].TrackingState == TrackingState.Tracked)
                        {
                            var rightHandFlap = Math.Max(0, prevRightHandHeight - joints[JointType.HandRight].Position.Y);
                            var leftHandFlap = Math.Max(0, prevLeftHandHeight - joints[JointType.HandLeft].Position.Y);
                            birdHeight -= 100 * (rightHandFlap + leftHandFlap); // Move the bird up

                            // Save the current hand heights for next time
                            prevRightHandHeight = joints[JointType.HandRight].Position.Y;
                            prevLeftHandHeight = joints[JointType.HandLeft].Position.Y;
                        }
                    }
                }
            }

            // Move the bird
            birdHeight += 4; // Gravity
            birdHeight = Math.Max(0, Math.Min(birdHeight, this.Height - 20)); // So the bird doesn't fall of the screen

            // Move the pipes
            pipeX -= 4;
            if (pipeX < 0)
            {
                // Reset the pipe location
                pipeX = this.Width - 100;
                pipeGapY = randomGenerator.Next(100, 200);
            }

            // Draw the bird and pipes
            using (var canvas = drawingGroup.Open())
            {
                // Set the dimensions of the drawing to cover the screen
                canvas.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));

                // Draw the bird
                canvas.DrawEllipse(Brushes.Blue, null, new Point(Width / 2, birdHeight), 20, 20);

                // Draw the top pipe
                canvas.DrawRectangle(Brushes.Green, null, new Rect(pipeX, 0, 100, pipeGapY));

                // Draw the bottom pipe
                canvas.DrawRectangle(Brushes.Green, null,
                    new Rect(pipeX, pipeGapY + pipeGapLength, 100, Height - (pipeGapY + pipeGapLength)));

                // Show the drawing on the screen
                GameImage.Source = new DrawingImage(drawingGroup);
            }
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close the sensor when we close the window (and the application)
            sensor.Close();
        }
    }
}