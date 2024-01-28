using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Camera_callbacks
{
    public partial class MainWindow : Window
    {
        public Camera _camera;
        public LibVLC _libVLC;
        public LibVLCSharp.Shared.MediaPlayer _mediaPlayer;

        public const uint width = 2560;
        public const uint height = 1440;
        public const uint pitch = width*2;
        public MemoryMappedFile? CurrentMappedFile;
        public MemoryMappedViewAccessor? CurrentMappedViewAccessor;
        public ConcurrentQueue<(MemoryMappedFile? file, MemoryMappedViewAccessor? accessor)> FramesToProcess = new ConcurrentQueue<(MemoryMappedFile? file, MemoryMappedViewAccessor? accessor)>();
        public int FrameCounter = 0;
        public CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Core.Initialize();
                _camera = new Camera("Camera", "192.168.1.188", 554, "admin", "123456", "stream");
                _libVLC = new LibVLC("--verbose=2", "--avcodec-hw=none");
                _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC) { EnableHardwareDecoding = false };

                _libVLC.Log += (sender, e) => Debug.WriteLine($"[{e.Level}] {e.Module}:{e.Message}");
                var media = new Media(_libVLC, _camera.GetStreamUrl(), FromType.FromLocation);

                media.AddOption(":no-audio");
                // Set the Media to the MediaPlayer
                _mediaPlayer.Media = media;

                _mediaPlayer.SetVideoFormat("I420", width, height, pitch);

                _mediaPlayer.SetVideoCallbacks(Lock, null, Display);
                _mediaPlayer.Stopped += (s, e) => cancellationTokenSource.CancelAfter(1);
                
                VideoView.MediaPlayer = _mediaPlayer;
                VideoView.MediaPlayer.Play();
                this.Closed += MainWindow_Closed;
                Task.Run(() => ProcessFrames(cancellationTokenSource.Token), cancellationTokenSource.Token);
            }
            catch(Exception ex) {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                Debug.WriteLine(ex.Message);
            }
        }

        private void ProcessFrames(CancellationToken cancellationToken)
        {
            MemoryMappedFile? file = null;
            MemoryMappedViewAccessor? accessor = null;
            int frameNumber = 0;
            var startTime = DateTime.Now;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!FramesToProcess.TryDequeue(out var frame))
                    {
                        //Use BlockingCollection.TryTake() instead of ConcurrentQueue.TryDequeue() if you
                        //want to block the thread until an item is available and not randomly sleep
                        // The queue is empty, put the thread to sleep for a short period of time
                        Thread.Sleep(100);
                        //Debug.WriteLine("Queue is empty");
                        continue;
                    }
                    else
                    {
                        (file, accessor) = frame;

                        frameNumber++;
                        // Calculate the fps
                        var elapsedTime = DateTime.Now - startTime;
                        var fps = frameNumber / elapsedTime.TotalSeconds;

                        // Process the frame
                        //Debug.WriteLine($"Frame {frameNumber} dequeued at {DateTime.Now},  FPS: {fps}");
                        // Dispose of the memory-mapped file and view accessor
                        accessor?.Dispose();
                        file?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    // Handle the exception
                    Debug.WriteLine(ex.Message);
                }
            
            }
                
        }

        private IntPtr Lock(IntPtr opaque, IntPtr planes)
        {
            //Debug.WriteLine("Lock is called");

            Dispatcher.Invoke(() =>
            {
                lockTextBox.Text = $"Lock method called {FrameCounter}";
            });

            // Create a new memory-mapped file and view accessor for the frame
            CurrentMappedFile = MemoryMappedFile.CreateNew(null, pitch * height);
            CurrentMappedViewAccessor = CurrentMappedFile.CreateViewAccessor();

            // Write the pointer of the memory-mapped file to planes
            Marshal.WriteIntPtr(planes, CurrentMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle());

            return IntPtr.Zero;
        }

        private void Display(IntPtr opaque, IntPtr picture)
        {
            //Debug.WriteLine($"Debug is called with framecounter= {FrameCounter}");
            Dispatcher.Invoke(() =>
            {
                displayTextBox.Text = $"Display method called {FrameCounter}";
            });

            if (FrameCounter  == 5)
            {
                //Enqueued frames to be disposed in the processing thread
                FramesToProcess.Enqueue((CurrentMappedFile, CurrentMappedViewAccessor));
                CurrentMappedFile = null;
                CurrentMappedViewAccessor = null;
                FrameCounter = 0;//reset counter
            }
            else
            {
                if (CurrentMappedViewAccessor != null && CurrentMappedFile != null)
                {
                    CurrentMappedViewAccessor.Dispose();
                    CurrentMappedFile.Dispose();
                }
                
                CurrentMappedFile = null;
                CurrentMappedViewAccessor = null;
                FrameCounter++;
            }
        }
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _libVLC.Dispose();
        }
    }
}