using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Communication;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ArduinoWiring
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer timeout;
        private CoreDispatcher dispatcher;

        //left bridge
        byte leftEN = 12;
        byte leftInput1 = 10;
        byte leftInput2 = 11;
        byte rightServoPin = 3;
        byte leftServoPin = 9;
        byte upServoPin = 5;
        int upPosition = 0;
        int rightServoPos = 90;
        int leftServoPos = 0;
        public int LeftServoPos {
            get { return leftServoPos; }
            set
            {
                if (value > 90)
                {
                    leftServoPos = 90;
                }
                else if (value < 0)
                {
                    leftServoPos = 0;
                }
                else
                {
                    leftServoPos = value;
                }
            }
        }

        public int UpPosition
        {
            get { return upPosition; }
            set
            {
                if (value > 90)
                {
                    upPosition = 90;
                }
                else if (value < 0)
                {
                    upPosition = 0;
                }
                else
                {
                    upPosition = value;
                }
            }
        }

        public int RightServoPos {
            get { return rightServoPos; }

            set
            {
                if (value > 90)
                {
                    rightServoPos = 90;
                }
                else if (value < 0)
                {
                    rightServoPos = 0;
                }
                else
                {
                    rightServoPos = value;
                }
            }
        }
        public ushort Speed {
            get { return speed; }

            set
            {
                if (value > 255)
                {
                    speed = 255;
                }
                else if (speed < 0)
                {
                    speed = 0;
                }
                else
                {
                    speed = value;
                }
            } }

        // right bridge 
        byte rightEN = 7;
        byte rightInput1 = 6;
        byte rightInput2 = 4; //no pwm supported for reverse
        
        public ushort Position {
            get;

            set;
        }
        bool doWrite = false;  //to write only when there is communication between computer and device
        public ushort speed = 255;
        public byte reversePin = 3;
        public string greenPin = "A0";
        public string redPin = "A1";
        public string bluePin = "A2";
        public bool moveForward = true, moveBackward = true, moveLeft = true, moveRight= true;
        public bool movingForward = false, movingBackward = false;
        // stopwatch for tracking connection timing
        Stopwatch connectionStopwatch = new Stopwatch();
        public IStream connection;
        public RemoteDevice arduino;
        Task<DeviceInformationCollection> task;
        //CancellationTokenSource cancelTokenSource;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Speed = 0;
            this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            //cancelTokenSource = new CancellationTokenSource();
            //cancelTokenSource.Token
            task = BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>();
            if (true)
            {
                task.ContinueWith(listTask =>
                {
                    //store the result and populate the device list on the UI thread
                    var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                    {
                        Connections connections = new Connections();

                        var result = listTask.Result;
                        if (result == null || result.Count == 0)
                        {
                            DebugInfo.Text = "No items found.";
                            ConnectionList.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            foreach (DeviceInformation device in result)
                            {
                                connections.Add(new Connection(device.Name, device));
                            }
                            DebugInfo.Text = "Select an device and press \"Connect\" to connect.";
                        }

                        ConnectionList.ItemsSource = connections;
                    }));
                });
            }

        }

        private async void OnDeviceReady()
        {
            arduino.pinMode(leftEN, PinMode.OUTPUT);
            arduino.pinMode(leftInput1, PinMode.OUTPUT);
            arduino.pinMode(leftInput2, PinMode.OUTPUT);
            arduino.pinMode(rightEN, PinMode.OUTPUT);
            arduino.pinMode(rightInput1, PinMode.OUTPUT);
            arduino.pinMode(rightInput2, PinMode.OUTPUT);
            arduino.pinMode(13, PinMode.OUTPUT);
            arduino.pinMode(rightServoPin, PinMode.SERVO);
            arduino.pinMode(leftServoPin, PinMode.SERVO);
            arduino.pinMode(upServoPin, PinMode.SERVO);
            arduino.pinMode("A0", PinMode.OUTPUT);
            arduino.pinMode("A1", PinMode.OUTPUT);
            arduino.pinMode("A2", PinMode.OUTPUT);


            await DebugInfo.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() => {
                DebugInfo.Text = "Connected to Device";
            }));
            doWrite = true;
            arduino.analogWrite(leftServoPin, 0);
            arduino.analogWrite(rightServoPin, 90);
            arduino.analogWrite(upServoPin, 0);
        }

        //this async Task function will execute infinitely in the background

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            DebugInfo.Text = "Atempting to Connect";
            ConnectButton.IsEnabled = false;
            DeviceInformation device = null;
            if (ConnectionList.SelectedItem != null)
            {
                var selectedConnection = ConnectionList.SelectedItem as Connection;
                device = selectedConnection.Source as DeviceInformation;

                connection = new BluetoothSerial(device);
                arduino = new RemoteDevice(connection);
                arduino.DeviceReady += OnDeviceReady;
                arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
                arduino.DeviceConnectionLost += Arduino_DeviceConnectionLost;
                //using my Bluetooth device's baud rate, StandardFirmata configured to match
                connection.begin(115200, SerialConfig.SERIAL_8N1);

                //stop watch for timing
                connectionStopwatch.Reset();
                connectionStopwatch.Start();

                timeout = new DispatcherTimer();
                timeout.Interval = new TimeSpan(0, 0, 30);
                timeout.Tick += Timeout_Tick;
                timeout.Start();
            }
            else
            {
                DebugInfo.Text = "Please select a device";
            }
        }

        //Remember to Disable all other buttons when connection is lost.
        private async void Arduino_DeviceConnectionLost(string message)
        {
            doWrite = false;
            await ConnectButton.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>{
                DebugInfo.Text = "Connection Lost";
            }));
           
        }

        private void Timeout_Tick(object sender, object e)
        {
            var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                timeout.Stop();
                DebugInfo.Text = "Connection attempt timed out.";
                ConnectButton.IsEnabled = true;
                //Reset();
            }));
        }

        private void Arduino_DeviceConnectionFailed(string message)
        {
            doWrite = false;  // just to be more sure
            string info = "failed to connect";
            DebugInfo.Text = info + message;
            ConnectButton.IsEnabled = true;
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ForwardButton_Checked(object sender, RoutedEventArgs e)
        {
            if (doWrite) {
                arduino.digitalWrite(13, PinState.HIGH);
            }
        }

        private void ForwardButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (doWrite)
            {
                arduino.digitalWrite(13, PinState.LOW);
            }           
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void SpeedMeter_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Speed = (ushort)SpeedMeter.Value;
            if(doWrite)
            {
                //arduino.analogWrite(leftEN, speed);
            }
            
        }

        private void FrontButton_Checked(object sender, RoutedEventArgs e)
        {
            if (doWrite)
            {
                MoveForward();
            }
            else
            {
                FrontButton.IsChecked = false;
            }
            
        }

        private void FrontButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void ReverseButton_Checked(object sender, RoutedEventArgs e)
        {
            MoveBackWord();
        }

        private void ReverseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up
             && e.Key != Windows.System.VirtualKey.C
             && e.Key != Windows.System.VirtualKey.S
             && e.Key != Windows.System.VirtualKey.H
             && e.Key != Windows.System.VirtualKey.R)
                base.OnKeyDown(e);
            else
            {
                //implement code here
                if (e.Key == Windows.System.VirtualKey.Up)
                {
                    moveBackward = false;
                    ReverseButton.IsChecked = false;
                    FrontButton.IsChecked = true;
                }

                if (e.Key == Windows.System.VirtualKey.Down)
                {
                    moveForward = false;
                    FrontButton.IsChecked = false;
                    ReverseButton.IsChecked = true;
                }
                if (e.Key == Windows.System.VirtualKey.Left)
                {
                    moveRight = false;
                    moveLeft = true;
                    await Loop();
                }
                if (e.Key == Windows.System.VirtualKey.Right)
                {
                    moveLeft = false;
                    moveRight = true;
                    await Loop();
                }

                if (e.Key == Windows.System.VirtualKey.C)
                {
                    ListenVoiceCommand();
                }

                if (e.Key == Windows.System.VirtualKey.S)
                {
                    Stop();
                }

                if (e.Key == Windows.System.VirtualKey.H)
                {
                    Hold();
                }
                if (e.Key == Windows.System.VirtualKey.R)
                {
                    Release();
                }
            }

        }

        //Managing Keys
        private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up)
                base.OnKeyDown(e);
            else
            {
                //implement code here
                if (e.Key == Windows.System.VirtualKey.Up)
                {
                    //moveBackward = true;
                    //ReverseButton.IsChecked = false;
                    FrontButton.IsChecked = false;
                }

                if (e.Key == Windows.System.VirtualKey.Down)
                {
                   // moveForward = true;
                    //FrontButton.IsChecked = false;
                    ReverseButton.IsChecked = false;
                }
                if (e.Key == Windows.System.VirtualKey.Left)
                {
                    moveLeft = false;
                }
                if (e.Key == Windows.System.VirtualKey.Right)
                {
                    moveRight = false;
                }

            }

        }

        private void HoldButtonClick(object sender, RoutedEventArgs e)
        {
            Hold();
        }

        private void ReleaseButtonClick(object sender, RoutedEventArgs e)
        {
            Release();
        }


        //Controlling function

        private async Task Loop()
        {
            if (doWrite)
            {
                if (moveLeft)
                {
                    while (moveLeft)
                    {
                        Stop();
                        arduino.digitalWrite(leftEN, PinState.HIGH);
                        arduino.digitalWrite(leftInput2, PinState.HIGH);
                        arduino.digitalWrite(rightEN, PinState.HIGH);
                        arduino.digitalWrite(rightInput1, PinState.HIGH);
                        await Task.Delay(100);
                        Stop();
                    }
                }

                if (moveRight)
                {
                    while (moveRight)
                    {
                        Stop();
                        arduino.digitalWrite(leftEN, PinState.HIGH);
                        arduino.digitalWrite(leftInput1, PinState.HIGH);
                        arduino.digitalWrite(rightEN, PinState.HIGH);
                        arduino.digitalWrite(rightInput2, PinState.HIGH);
                        await Task.Delay(100);
                        Stop();
                    }
                }
            }
            else
            {
                DebugInfo.Text = "No connection to the device";
            }

        }

        public void MoveForward()
        {
            if (doWrite)
            {
                arduino.digitalWrite(leftEN, PinState.LOW);
                arduino.digitalWrite(rightEN, PinState.LOW);
                arduino.digitalWrite(leftInput2, PinState.LOW);
                arduino.digitalWrite(rightInput2, PinState.LOW);
                arduino.digitalWrite(leftInput1, PinState.HIGH);
                arduino.digitalWrite(rightInput1, PinState.HIGH);
                arduino.digitalWrite(leftEN, PinState.HIGH);
                arduino.digitalWrite(rightEN, PinState.HIGH);
                DebugInfo.Text = "Moving Forward";
            }
            else
            {
                DebugInfo.Text = "No connection to device";
            }
            
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection.end();

            }
            catch (Exception)
            {
                ;
            }
        }

        private void DownButtonClick(object sender, RoutedEventArgs e)
        {
            if (doWrite)
            {
                UpPosition += 5;
                arduino.analogWrite(upServoPin, (ushort) UpPosition);
            }
        }

        private void UpButtonClick(object sender, RoutedEventArgs e)
        {
            if (doWrite)
            {
                UpPosition -= 5;
                arduino.analogWrite(upServoPin, (ushort)UpPosition);
            }
        }

        private void LightsButtonChecked(object sender, RoutedEventArgs e)
        {
            LightsButton.Content = "ON";
            LightsOn();
        }

        private void LightsButtonUnchecked(object sender, RoutedEventArgs e)
        {
            LightsButton.Content = "OFF";
            LightsOff();
        }

        private void RedButtonClick(object sender, RoutedEventArgs e)
        {
            RedLight();
        }

        private void GreenButtonClick(object sender, RoutedEventArgs e)
        {
            GreenLight();
        }

        private void BlueButtonClick(object sender, RoutedEventArgs e)
        {
            BlueLight();
        }

        public void Stop() {
            if (doWrite)
            {
                arduino.digitalWrite(leftEN, PinState.LOW);
                arduino.digitalWrite(rightEN, PinState.LOW);
                arduino.digitalWrite(leftInput2, PinState.LOW);
                arduino.digitalWrite(rightInput2, PinState.LOW);
                arduino.digitalWrite(rightInput1, PinState.LOW);
                arduino.digitalWrite(rightInput1, PinState.LOW);
                DebugInfo.Text = "Stopped";
            }
            else
            {
                DebugInfo.Text = "Not connected to device";
            }
            
        }

        public void MoveBackWord()
        {
            if (doWrite)
            {
                arduino.digitalWrite(leftEN, PinState.LOW);
                arduino.digitalWrite(rightEN, PinState.LOW);
                arduino.digitalWrite(leftInput1, 0);
                arduino.digitalWrite(rightInput1, 0);
                arduino.digitalWrite(leftInput2, PinState.HIGH);
                arduino.digitalWrite(rightInput2, PinState.HIGH);
                arduino.digitalWrite(leftEN, PinState.HIGH);
                arduino.digitalWrite(rightEN, PinState.HIGH);
                DebugInfo.Text = "Moving Forward";
            }
            else
            {
                DebugInfo.Text = "No connection to device";
            }
        }

        public void Hold()
        {
            if (doWrite)
            {
                RightServoPos -= 5;
                LeftServoPos += 5;
                arduino.analogWrite(rightServoPin, (ushort)RightServoPos);
                arduino.analogWrite(leftServoPin, (ushort)LeftServoPos);
            }
        }

        public void Release()
        {
            if (doWrite)
            {
                RightServoPos += 5;
                LeftServoPos -= 5;
                arduino.analogWrite(rightServoPin, (ushort)RightServoPos);
                arduino.analogWrite(leftServoPin, (ushort)LeftServoPos);
            }
        }

    }
}


/*
 * Test for non directional keys and eliminate them
 
     if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up)
              base.OnKeyDown(e);

     
     
     */
