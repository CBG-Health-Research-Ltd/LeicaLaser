using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace LeicaLaserInterface
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //Gets surveyor info as saved by sample manager in MeasurementInfo.txt <qtr>+<MB>+<HHID>+<RespondentID> to be used in csv saving of data
            initialiseSurveyorInfo();
            try
            {
                //Open the DISTO connect service provided by leica to connect the bluetooth device.
                OpenLeicaService();
                
                //Minimise it, for some reason this doesn't seem to work. MAYBE launch from SM before launching from here, won't be any conflicts.
                MinimizeLeicaService();
                Keyboard.Focus(H1Measurement);


            }
            catch
            {
                //The attempt to open Leica service has faled, so it probbaly doesn't exist.
                MessageBox.Show("Could not find DISTO service for Bluetooth transfer.\n\n" +
                    "Please contact IT support letting them know DISTO Laser service is missing.");
                Application.Current.Shutdown();
            }
            //MonitorConnection(); Old fashioned polling method, keep it here just in case or for future reference.
            this.Topmost = true; //Set this window to be above the Leica service window
            this.Focus();
            Keyboard.Focus(H1Measurement);

            //Begins watching for the Leica laser connection. This continues as a background thread so we can watch for disconnections as well.
            StartBleDeviceWatcher();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);

        }

        //Below is the leica service that needs to be opened to connect. Device must already be paired.
        Process process = new Process();
        void OpenLeicaService()
        {

            string fileName = @"C:\Program Files (x86)\DISTO transfer 60\DistoTransfer.exe";
            
            process.StartInfo.Arguments = null;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.StartInfo.FileName = fileName;
            process.StartInfo.UseShellExecute = true;
            process.Start();

        }

        //Function attempts to minimise Leica service although it hasn't been successful. 
        private void MinimizeLeicaService()
        {
            
            WindowControl DistoTransfer = new WindowControl();
            DistoTransfer.AppName = "DistoTransfer.exe";
            DistoTransfer.Minimize();
        }

        //Focus on the first measurement textbox H1measurement to get the surveyor ready for measurement once 'CONNECTED' is displayed.
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
            Keyboard.Focus(H1Measurement);
            
        }

        //Button_click is the 'Done Measuring' button. 
        private void button_Click(object sender, RoutedEventArgs e)
        {
            decimal measurement1;
            decimal measurement2;
            //CSV conversion must go here with appropriate handling. Currently checking for decimal point at string position 2
            try
            {    //Scanning for a decimal point from the first two indexes as expected from Leica laser input. Length must always equal 5 for correct input.
                if (arrayMeasurements[1, 1].Substring(0, 2).Contains(".") && arrayMeasurements[2, 1].Substring(0, 2).Contains(".") && arrayMeasurements[1, 1].Length == 5 && arrayMeasurements[2, 1].Length == 5)
                {
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    if (CheckGreaterOnePercentDiff(measurement1, measurement2) == false)//Checking that there is a less than 1% difference between two measurements
                    {
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        WindowControl DistoTransfer = new WindowControl();
                        DistoTransfer.AppName = "DistoTransfer";
                        DistoTransfer.Close();
                        Application.Current.Shutdown();
                    }
                    else //There is a greater than 1% difference, therefore get a 3rd measurement by enabling third measurement box.
                    {
                        //Disable first two measurement boxes. Enable third measurement box, shift focus to third measurement, disable Done measuring Box, 
                        //enable submit final measurements.
                        MessageBox.Show("Third measurement required.\n\nPlease take 10 seconds to re-position yourself for re-taking measurement.\n\n" +
                        "3rd measurement will be enabled after 10 seconds of closing this message.");
                        Thread.Sleep(10000);
                        H1Measurement.IsEnabled = false;
                        H2Measurement.IsEnabled = false;
                        button.IsEnabled = false;
                        button.Visibility = Visibility.Hidden;
                        textBlock6.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        H3Measurement.Visibility = Visibility.Visible;
                        button1.Visibility = Visibility.Visible;
                        textBlock4_Copy1.Visibility = Visibility.Visible;
                        clear3.Visibility = Visibility.Visible;
                        H3Measurement.IsEnabled = true;
                        H3Measurement.Focus();
                    }

                }
                else
                {   //Data has been entered but it does not match the expected 1.000 format of the leica laser.
                    MessageBox.Show("Incorrect height format. \n\n Please ensure you've collected results using Bluetooth Laser.\n\n" +
                        "If you are entering manually, the format expected is 3 decimal places.\nFor Example 1.43 meters should be input as 1.430");
                }
            }
            catch
            {
                //Exception is thrown due to arrayMeasurements not being complete, and therefore one or both the text fields are empty.
                MessageBox.Show("Please enter some measurements.\n\nEnsure you are using Bluetooth Laser for measuring.\n\n" +
                    "If you are entering manually, the format expected is 3 decimal places.\nFor Example 1.43 meters should be input as 1.430");
            }
            
        }

        private void button1_Click(object sender, RoutedEventArgs e)//This is the button for thrid measurement submission
        {
            try
            {    //Run appropriate data check on the 3rd measurement. For proper measurement DP will always be in the same place.
                if (arrayMeasurements[3, 1].Substring(0, 2).Contains(".") && (arrayMeasurements[3,1].Length == 5))
                {
                    //Do appropriate CSV conversion for ALL measurements and save
                    string csv = ArrayToCsv(arrayMeasurements);
                    WriteCSVFile(csv);
                    WindowControl DistoTransfer = new WindowControl();
                    DistoTransfer.AppName = "DistoTransfer";
                    DistoTransfer.Close();
                    Application.Current.Shutdown();

                }
                else
                {   //Data has been entered but it does not match the expected 1.000 format of the leica laser.
                    MessageBox.Show("Incorrect height format. \n\n Please ensure you've collected results using Bluetooth Laser\n\n" +
                        "If you are entering manually, the format expected is 3 decimal places.\nFor Example 1.43 meters should be input as 1.430");
                }
            }
            catch
            {
                //Exception is thrown due to arrayMeasurements not being complete, and therefore one or both the text fields are empty.
                MessageBox.Show("Please enter some measurements.\n\nEnsure you are using Bluetooth Laser for measuring.\n\n" +
                    "If you are entering manually, the format expected is 3 decimal places.\nFor Example 1.43 meters should be input as 1.430");
            }
        }

        //Checkbox below determines whether manual measurement or not
        bool manualMeasurement = false;
        bool regexOverride = false;//allows usage of text box clear operations to delte old results by not having regex applied to user input
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            manualMeasurement = true;
            Application.Current.Dispatcher.Invoke(() => { H1Measurement.Clear(); H2Measurement.Clear(); H3Measurement.Clear(); });
            MessageBox.Show("You are now entering measurements manually.\n\n" +
                "Please ensure measurements are of 3 decimal place format\n\n" +
                "For example, 1.43 meters should be inout as 1.430.\n" +
                "1 meter should be input as 1.000");
            //////
            RunCleanUp();
            H1Measurement.Focus();
            ///////
            regexOverride = false;
           
        }

        //Clearing measurements from individual fields
        private void clear1_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => { H1Measurement.Clear(); });
            H1Measurement.Focus();
            regexOverride = false;
        }

        private void clear2_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => { H2Measurement.Clear(); });
            H2Measurement.Focus();
            regexOverride = false;
        }

        private void clear3_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            Application.Current.Dispatcher.Invoke(() => { H3Measurement.Clear(); });
            H3Measurement.Focus();
            regexOverride = false;
        }

        //checkbox unchecked is returning to bluetooth measruements
        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            manualMeasurement = false;
            Application.Current.Dispatcher.Invoke(() => { H1Measurement.Clear(); H2Measurement.Clear(); H3Measurement.Clear(); });
            MessageBox.Show("You are now entering measurements with Bluetooth.");
            //////
            RunCleanUp();
            H1Measurement.Focus();
            ////////
            regexOverride = false;
        }

        //Clean up to original state expecting first measurement
        public void RunCleanUp()
        {
            arrayMeasurements[1, 1] = null;
            arrayMeasurements[2, 1] = null;
            arrayMeasurements[3, 1] = null;
            arrayMeasurements[1, 6] = null;
            arrayMeasurements[2, 6] = null;
            arrayMeasurements[3, 6] = null;
            H1Measurement.IsEnabled = true;
            H2Measurement.IsEnabled = true;
            button.IsEnabled = true;
            button.Visibility = Visibility.Visible;
            textBlock6.Visibility = Visibility.Hidden;
            textBlock5.Visibility = Visibility.Hidden;
            H3Measurement.Visibility = Visibility.Hidden;
            button1.Visibility = Visibility.Hidden;
            textBlock4_Copy1.Visibility = Visibility.Hidden;
            clear3.Visibility = Visibility.Hidden;
            H3Measurement.IsEnabled = false;
            H1Measurement.Focus();
            previousInput = "";
            previousInput1 = "";
            previousInput2 = "";
        }

        //update UI to let usrveyor know connected
        public void updateConnectionStatus(string text)
        {
            if (text == "CONNECTED")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Green; });
            }
            if (text == "Disconnected")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Black; });
            }
        }

        public void updateH1Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { H1Measurement.Text = text; });
        }

        public void updateH2Text(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { H2Measurement.Text = text; });
        }

        //Below monitors all BT events on machine and handles appropriately. The updated event is the one to be concerned with regarding connecton status.
        //This code is modified from UWP Microsoft BT app.
        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            //deviceWatcher.Removed += DeviceWatcher_Removed;
            //deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            //deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();

            

            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }

        //Function pulled and edited from the UWP Bluetooth app provided by microsoft. 
        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await Task.Run(async () =>
            {
                lock (this)
                {

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });

        }

        //This function searches for the actual laser everytime bluetooth device enumeration (device watcher) is updated.
        //Code adapted from microsoft UWP BLE app
        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {

            
            await Task.Run(async () =>
            {
                lock (this)
                {
                   

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            DeviceInformation updatedDevice = bleDeviceDisplay.DeviceInformation;
                            //IsConnectable will be established once updated accordingly here. So function needs to be added that handles all devices.
                            if (bleDeviceDisplay.IsConnected && bleDeviceDisplay.Name.Contains("DISTO"))
                            {
                                updateConnectionStatus("CONNECTED");
                                
                            }
                            if (bleDeviceDisplay.IsConnected == false && bleDeviceDisplay.Name.Contains("DISTO"))
                            {
                                //This cna have a bit of a delay on the UI, maybe find out why.
                                updateConnectionStatus("Disconnected");
                            }

                            return;
                        }

                        //Just extra handling, not sure if completely neccessary.
                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });

        }

        //Retrieves ID of specific BT devices
        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        //Devices that are unkown, i.e. haven't been added to BT history
        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        public class BluetoothLEDeviceDisplay : INotifyPropertyChanged
        {
            public BluetoothLEDeviceDisplay(DeviceInformation deviceInfoIn)
            {
                DeviceInformation = deviceInfoIn;

            }

            public DeviceInformation DeviceInformation { get; private set; }

            public string Id => DeviceInformation.Id;
            public string Name => DeviceInformation.Name;
            public bool IsPaired => DeviceInformation.Pairing.IsPaired;
            public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
            public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

            public IReadOnlyDictionary<string, object> Properties => DeviceInformation.Properties;



            public event PropertyChangedEventHandler PropertyChanged;

            public void Update(DeviceInformationUpdate deviceInfoUpdate)
            {
                DeviceInformation.Update(deviceInfoUpdate);

                OnPropertyChanged("Id");
                OnPropertyChanged("Name");
                OnPropertyChanged("DeviceInformation");
                OnPropertyChanged("IsPaired");
                OnPropertyChanged("IsConnected");
                OnPropertyChanged("Properties");
                OnPropertyChanged("IsConnectable");

            }


            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        //Initialising all the needed fields for the 4x6 csv for logging measurements.
        string[,] arrayMeasurements = new string[4, 7];
        private void initialiseSurveyorInfo()
        {
            arrayMeasurements[0, 0] = "MeasureType";
            arrayMeasurements[0, 1] = "Measurement";
            arrayMeasurements[0, 2] = "Qtr";
            arrayMeasurements[0, 3] = "MB";
            arrayMeasurements[0, 4] = "HHID";
            arrayMeasurements[0, 5] = "RespondentID";
            arrayMeasurements[0, 6] = "MeasurementInputType";
            string[] respondentInfo = GetRespondentIdentifiers();
            arrayMeasurements[1, 2] = respondentInfo[0];
            arrayMeasurements[1, 3] = respondentInfo[1];
            arrayMeasurements[1, 4] = respondentInfo[2];
            arrayMeasurements[1, 5] = respondentInfo[3];
            arrayMeasurements[2, 2] = respondentInfo[0];
            arrayMeasurements[2, 3] = respondentInfo[1];
            arrayMeasurements[2, 4] = respondentInfo[2];
            arrayMeasurements[2, 5] = respondentInfo[3];
            arrayMeasurements[3, 2] = respondentInfo[0];
            arrayMeasurements[3, 3] = respondentInfo[1];
            arrayMeasurements[3, 4] = respondentInfo[2];
            arrayMeasurements[3, 5] = respondentInfo[3];


        }

        //reads the MeasurementInfo.txt file generated by SM containing relevant respondent info
        private string[] GetRespondentIdentifiers()
        {
            string respIDs = File.ReadLines(@"C:\NZHS\surveyinstructions\MeasurementInfo.txt").First();
            string[] respIDSplit = respIDs.Split('+');
            return respIDSplit;
        }


        string previousInput = "";
        private void H1Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // Permitting only numeric values and one decimal point
                Match m = r.Match(H1Measurement.Text);
                if (m.Success)
                {
                    previousInput = H1Measurement.Text;
                }
                else
                {
                    H1Measurement.Text = previousInput;
                }
            }

            if (H1Measurement.Text.Length == 5) //The correct laser values are of a length of 5, so whether auto or manual we can handle here
            {

                    //Added the actual mesurement to the array
                    string rounded = H1Measurement.Text.Substring(0, 5);
                    arrayMeasurements[1, 0] = "HT";
                    arrayMeasurements[1, 1] = rounded;
                    //updateH1Text(rounded.ToString());
                    if (manualMeasurement == false)
                        {
                        arrayMeasurements[1, 6] = "BluetoothInput";
                        }
                    else
                        {
                        arrayMeasurements[1, 6] = "ManualInput";
                        }
                MessageBox.Show("Please take 10 seconds to re-position yourself for re-taking measurement.\n\n" +
                    "2nd measurement will be enabled after 10 seconds.");
                Thread.Sleep(10000); //Enforces delay for surveyor to re-position. This may not be the best way to handle. Maybe disabling everything with a new timer better
                    
                    Keyboard.Focus(H2Measurement);               
            }
        }

        string previousInput1 = "";
        private void H2Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // Permitting only numeric values and one decimal point
                Match m = r.Match(H2Measurement.Text);
                if (m.Success)
                {
                    previousInput1 = H2Measurement.Text;
                }
                else
                {
                    H2Measurement.Text = previousInput1;
                }
            }



            if (H2Measurement.Text.Length == 5)//The correct laser values are of a length of 5, so whether auto or manual we can handle here
            {
                //Adding the second measurement to the array
                string rounded = H2Measurement.Text.Substring(0, 5);
                arrayMeasurements[2, 0] = "HT";
                arrayMeasurements[2, 1] = rounded;
                //updateH2Text(rounded.ToString());
                if (manualMeasurement == false)
                {
                    arrayMeasurements[2, 6] = "BluetoothInput";
                }
                else
                {
                    arrayMeasurements[2, 6] = "ManualInput";
                }
                //Keyboard.Focus(H1Measurement);
            }
        }

        string previousInput2 = "";
        private void H3Measurement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // Permitting only numeric values and one decimal point
                Match m = r.Match(H3Measurement.Text);
                if (m.Success)
                {
                    previousInput2 = H3Measurement.Text;
                }
                else
                {
                    H3Measurement.Text = previousInput2;
                }
            }

            if (H3Measurement.Text.Length == 5)//The correct laser values are of a length of 5, so whether auto or manual we can handle here
            {
                //Adding the second measurement to the array
                string rounded = H3Measurement.Text.Substring(0, 5);
                arrayMeasurements[3, 0] = "HT";
                arrayMeasurements[3, 1] = rounded;
                //updateH2Text(rounded.ToString());
                if (manualMeasurement == false)
                {
                    arrayMeasurements[3, 6] = "BluetoothInput";
                }
                else
                {
                    arrayMeasurements[3, 6] = "ManualInput";
                }
                //Keyboard.Focus(H1Measurement);
            }
        }

        private decimal ConvertStrToDec(string value)
        {
            decimal convert = Convert.ToDecimal(value);
            return convert;
        }

        //used in checking the final values are within 1% difference of eachother
        private bool CheckGreaterOnePercentDiff(decimal value1, decimal value2)
        {
            if ( value1 > value2 )
            {
                decimal percent = ((value1 / value2)*100);
                if(percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else if (value2 > value1)
            {
                decimal percent = ((value2 / value1) * 100);
                if (percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else
            {
                return false; // All other cases false as value1 and value2 will be equal
            }
        }

        //Changes any rectangular array, 2d array, to an appropriate csv string.
        static string ArrayToCsv(string[,] values)
        {
            // Get the bounds.
            int num_rows = values.GetUpperBound(0) + 1;
            int num_cols = values.GetUpperBound(1) + 1;

            // Convert the array into a CSV string.
            StringBuilder sb = new StringBuilder();
            for (int row = 0; row < num_rows; row++)
            {
                // Add the first field in this row.
                sb.Append(values[row, 0]);

                // Add the other fields in this row separated by commas.
                for (int col = 1; col < num_cols; col++)
                    sb.Append("," + values[row, col]);

                // Move to the next line.
                sb.AppendLine();
            }

            // Return the CSV format string.
            return sb.ToString();
        }

        //Height measurement specific csv file
        private void WriteCSVFile(string csvMeasurements)
        {

            System.IO.Directory.CreateDirectory(@"C:\BodyMeasurements\HeightMeasurements");
            string CSVFileName = @"C:\BodyMeasurements\HeightMeasurements\" + "HeightMeasurements_" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".csv";

            System.IO.File.WriteAllText(CSVFileName, csvMeasurements);
                    

        }

        //Window control for handling of external windows. 
        public class WindowControl
        {
            private string appName;  // the name field
            public string AppName    // the Name property
            {
                get
                {
                    return appName;
                }
                set
                {
                    appName = value;
                }
            }

            [DllImport("user32.dll")]
            private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            private const int ForceMinimize = 11;
            private const int RestoreMaximization = 9;
            public void Minimize()
            {
                Process[] processlist = Process.GetProcesses();

                foreach (Process process in processlist.Where(process => process.ProcessName == appName))
                {
                    ShowWindow(Process.GetProcessById(process.Id).MainWindowHandle, ForceMinimize);
                }
            }
            public void Restore()
            {
                Process[] processlist = Process.GetProcesses();

                foreach (Process process in processlist.Where(process => process.ProcessName == appName))
                {
                    ShowWindow(Process.GetProcessById(process.Id).MainWindowHandle, RestoreMaximization);
                }
            }

            public void Close()//Makes sure foxit is closed before launch so it can be launch full-screen mode.
            {
                if (Process.GetProcessesByName(appName).Length > 0)
                {
                    foreach (Process proc in Process.GetProcessesByName(appName))
                    {
                        proc.Kill();
                    }
                }
            }

        }

    }
}
