using MahApps.Metro.Controls;   // Needed for Logging
using Newtonsoft.Json.Linq; // Needed for JObject 
using SimHub;   // Needed for Logging
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;    // Needed for read/write JSON settings file
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Redadeg.lmuDataPlugin
{
    /// <summary>
    /// Logique d'interaction pour SettingsControlDemo.xaml
    /// </summary>

    public partial class SettingsControl : UserControl, IComponentConnector
    {


        //public void InitializeComponent()
        //{
        //    if (!_contentLoaded)
        //    {
        //        _contentLoaded = true;
        //        Uri resourceLocator = new Uri("/SimHub.Plugins;component/inputplugins/joystick/joystickpluginsettingscontrolwpf.xaml", UriKind.Relative);
        //        Application.LoadComponent(this, resourceLocator);
        //    }
        //}

        internal Delegate _CreateDelegate(Type delegateType, string handler)
        {
            return Delegate.CreateDelegate(delegateType, this, handler);
        }

        //void IComponentConnector.Connect(int connectionId, object target)
        //{
        //    if (connectionId == 1)
        //    {
        //        ((Button)target).Click += clearLogging_Click;
        //    }
        //    else
        //    {
        //        _contentLoaded = true;
        //    }
        //}
        public SettingsControl()
        {
            InitializeComponent();


        }

        //private bool value_changed = false;

        //private delegate void UpdateDataThreadSafeDelegate<TResult>(void Refresh);

        //public static void UpdateDataThreadSafe<TResult>(this Control @this)
        //{
        //   @this.Update;
        //}


        void OnLoad(object sender, RoutedEventArgs e)
        {
            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                
                ButtonBindSettings.WriteStandingsJSON = JSONSettingsdata["WriteStandingsJSON"] != null ? (bool)JSONSettingsdata["WriteStandingsJSON"] : false;
                ButtonBindSettings.WriteStandingsJSONToParameter = JSONSettingsdata["WriteStandingsJSONToParameter"] != null ? (bool)JSONSettingsdata["WriteStandingsJSONToParameter"] : false;
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
                ButtonBindSettings.AntiFlickPitMenuTimeout = JSONSettingsdata["AntiFlickPitMenuTimeout"] != null ? (int)JSONSettingsdata["AntiFlickPitMenuTimeout"] : 10;
                ButtonBindSettings.LMU_PluginInstallDir = JSONSettingsdata["LMU_PluginInstallDir"] != null ? ((string)JSONSettingsdata["LMU_PluginInstallDir"]).Equals(string.Empty) ? "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Le Mans Ultimate\\Plugins" : (string)JSONSettingsdata["LMU_PluginInstallDir"] : "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Le Mans Ultimate\\Plugins";
                SaveSetting();
            }
            catch { }
            WriteStandingsJSON.IsChecked = ButtonBindSettings.WriteStandingsJSON;
            WriteStandingsJSONToParameter.IsChecked = ButtonBindSettings.WriteStandingsJSONToParameter;
            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
            AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;
            pluginIstallPath.Text = ButtonBindSettings.LMU_PluginInstallDir;

            CheckUpdateLMUPlugin(pluginIstallPath.Text);
        }

   
        public  void Refresh(string _Key)
        {
           // bool changedBind = false;
            string MessageText = "";

            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.WriteStandingsJSON = JSONSettingsdata["WriteStandingsJSON"] != null ? (bool)JSONSettingsdata["WriteStandingsJSON"] : false;
                ButtonBindSettings.WriteStandingsJSONToParameter = JSONSettingsdata["WriteStandingsJSONToParameter"] != null ? (bool)JSONSettingsdata["WriteStandingsJSONToParameter"] : false;
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
                ButtonBindSettings.AntiFlickPitMenuTimeout = JSONSettingsdata["AntiFlickPitMenuTimeout"] != null ? (int)JSONSettingsdata["AntiFlickPitMenuTimeout"] : 10;
                ButtonBindSettings.LMU_PluginInstallDir = JSONSettingsdata["LMU_PluginInstallDir"] != null ? (string)JSONSettingsdata["LMU_PluginInstallDir"] : "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Le Mans Ultimate\\Plugins";
            }
            catch { }
            base.Dispatcher.InvokeAsync(delegate
            {

                lock (WriteStandingsJSON)
                {
                    WriteStandingsJSON.IsChecked = ButtonBindSettings.WriteStandingsJSON;

                }
                lock (WriteStandingsJSONToParameter)
                {
                    WriteStandingsJSONToParameter.IsChecked = ButtonBindSettings.WriteStandingsJSONToParameter;

                }

                lock (clock_format24)
                {
                    clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;

                }
                lock (RealTimeClock)
                {
                    RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;

                }

                lock (DataUpdateThreadTimeout)
                {
                    DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;

                }

                lock (GetMemoryDataThreadTimeout)
                {
                    GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;

                }

                lock (AntiFlickPitMenuTimeout)
                {
                    AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;

                }

                lock (AntiFlickPitMenuTimeout)
                {
                    pluginIstallPath.Text = ButtonBindSettings.LMU_PluginInstallDir;

                }

                lock (message_text)
                {
                    message_text.Text = MessageText;

                }
            }
        );
       }

        private void SHSection_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //Trigger for saving JSON file. Event is fired if you enter or leave the Plugin Settings View or if you close SimHub

            //Saving on leaving Settings View only
            if (!SHSectionPluginOptions.IsVisible)
            {
                try
                {
           
                  
                }
                catch (Exception ext)
                {
                    Logging.Current.Info("INNIT ERROR: " + ext.ToString());
                }


            }
        }



        internal void UpdateLMUPlugin(string LMU_PluginPath)
        {
            message_text.Text = "";
            string owner = "tembob64"; // Замените на владельца репозитория
            string repo = "LMU_SharedMemoryMapPlugin"; // Замените на название репозитория
            string releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LMU_Plugin_update_service");

                try
                {
                    var response = client.GetAsync(releasesUrl).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var jsonString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var release = JObject.Parse(jsonString);

                    string AvailablePluginVersion = "Unknown";
                    string InstalledPluginVersion = "1.1.1.1" ;
                    string InstalledPluginversionFileName = System.IO.Path.Combine(LMU_PluginPath, "LMU_SharedMemoryMapPlugin64.dll");


                    if (File.Exists(InstalledPluginversionFileName))
                    {
                        try
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(InstalledPluginversionFileName);
                            if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                            {
                                InstalledPluginVersion = versionInfo.FileVersion;
                            }
                           
                        }
                        catch (Exception ex2)
                        {
                            message_text.Text += $"Error: {ex2.ToString()}\r\n";
                        }
                    }

                    Match match = Regex.Match(release["tag_name"].ToString(), @"\d+\.\d+\.\d+\.\d+");
                    if (match.Success)
                    {
                        AvailablePluginVersion = match.Value;
                    }

                   

                    if (!AvailablePluginVersion.Equals("Unknown"))
                    {

                        if (!InstalledPluginVersion.Equals(AvailablePluginVersion))
                        {
                            
                            message_text.Text += $"Update available. Installed: {InstalledPluginVersion} , Available: {AvailablePluginVersion}\r\n";
                            message_text.Text += $"Update plugin in progress.\r\n";
                            var zipAsset = release["assets"]
                      .FirstOrDefault(a => a["name"].ToString().EndsWith(".zip"));

                            if (zipAsset == null)
                            {
                                message_text.Text += "ZIP file cannot find.\r\n";
                            }

                            string zipUrl = zipAsset["browser_download_url"].ToString();

                            // Скачиваем ZIP-файл
                            string zipFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), zipAsset["name"].ToString());
                            using (var zipResponse = client.GetAsync(zipUrl).GetAwaiter().GetResult())
                            {
                                zipResponse.EnsureSuccessStatusCode();
                                using (var fs = new FileStream(zipFilePath, FileMode.Create))
                                {
                                    zipResponse.Content.CopyToAsync(fs).Wait();
                                }
                            }

                            // Распаковываем архив
                            // string extractPath = Path.Combine(Directory.GetCurrentDirectory(), "Extracted");
                            // Directory.CreateDirectory(LMU_PluginPath);
                            string dllfilename = System.IO.Path.Combine(LMU_PluginPath, "LMU_SharedMemoryMapPlugin64.dll");
                            if (File.Exists(dllfilename))
                            {
                                File.Delete(dllfilename);
                            }
                            ZipFile.ExtractToDirectory(zipFilePath, LMU_PluginPath);

                            message_text.Text += $"Update plugin successful. Installed version {AvailablePluginVersion}\r\n";
                        }
                        else
                        {
                            message_text.Text += $"Plugin is up to date. Installed: {InstalledPluginVersion} , Available: {AvailablePluginVersion}\r\n";
                        }

                    }


                    //using (StreamWriter writer = new StreamWriter(InstalledPluginversionFileName))
                    //{
                    //    writer.WriteLine(AvailablePluginVersion);
                    //    writer.Close();
                    //}
 

                 
                }
                catch (Exception ex)
                {
                    message_text.Text += $"Error: {ex.ToString()}\r\n";
                }
            }

        }

        internal void CheckUpdateLMUPlugin(string LMU_PluginPath)
        {
            message_text.Text = "";
            string owner = "tembob64"; // Замените на владельца репозитория
            string repo = "LMU_SharedMemoryMapPlugin"; // Замените на название репозитория
            string releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LMU_Plugin_update_service");

                try
                {
                    var response = client.GetAsync(releasesUrl).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var jsonString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var release = JObject.Parse(jsonString);

                    string AvailablePluginVersion = "Unknown";
                    string InstalledPluginVersion = "1.1.1.1";
                    string InstalledPluginversionFileName = System.IO.Path.Combine(LMU_PluginPath, "LMU_SharedMemoryMapPlugin64.dll");


                    if (File.Exists(InstalledPluginversionFileName))
                    {
                        try
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(InstalledPluginversionFileName);
                            if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                            {
                                InstalledPluginVersion = versionInfo.FileVersion;
                            }

                        }
                        catch (Exception ex2)
                        {
                            message_text.Text += $"Error: {ex2.ToString()}\r\n";
                        }
                    }

                    Match match = Regex.Match(release["tag_name"].ToString(), @"\d+\.\d+\.\d+\.\d+");
                    if (match.Success)
                    {
                        AvailablePluginVersion = match.Value;
                    }



                    if (!AvailablePluginVersion.Equals("Unknown"))
                    {

                        if (!InstalledPluginVersion.Equals(AvailablePluginVersion))
                        {
                             message_text.Text += $"Plugin is need update! Installed: {InstalledPluginVersion} , Available: {AvailablePluginVersion}\r\n";
                        }
                        else
                        {
                            message_text.Text += $"Plugin is up to date. Installed: {InstalledPluginVersion} , Available: {AvailablePluginVersion}\r\n";
                        }

                    }


                    //using (StreamWriter writer = new StreamWriter(InstalledPluginversionFileName))
                    //{
                    //    writer.WriteLine(AvailablePluginVersion);
                    //    writer.Close();
                    //}



                }
                catch (Exception ex)
                {
                    message_text.Text += $"Error: {ex.ToString()}\r\n";
                }
            }

        }

        private void Click_SelectLmuPluginFolder(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderBrowserDialog.SelectedPath = pluginIstallPath.Text;
            folderBrowserDialog.Description = "Select LMU_Plugins folder location";
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = folderBrowserDialog.SelectedPath;
                pluginIstallPath.Text = selectedPath;
                ButtonBindSettings.LMU_PluginInstallDir = pluginIstallPath.Text;
                SaveSetting();
            }
        }

        private void btn_CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateLMUPlugin(pluginIstallPath.Text);
        }

        private void refresh_button_Click(object sender, RoutedEventArgs e)
        {

            WriteStandingsJSON.IsChecked = ButtonBindSettings.WriteStandingsJSON;
            WriteStandingsJSONToParameter.IsChecked = ButtonBindSettings.WriteStandingsJSONToParameter;
            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
            AntiFlickPitMenuTimeout.Value = ButtonBindSettings.AntiFlickPitMenuTimeout;
            pluginIstallPath.Text = ButtonBindSettings.LMU_PluginInstallDir;
            //message_text.Text = "";
        }

        private void SaveSetting()
         {
            JObject JSONdata = new JObject(
                   new JProperty("Clock_Format24", ButtonBindSettings.Clock_Format24),
                   new JProperty("RealTimeClock", ButtonBindSettings.RealTimeClock),
                   new JProperty("GetMemoryDataThreadTimeout", ButtonBindSettings.GetMemoryDataThreadTimeout),
                   new JProperty("DataUpdateThreadTimeout", ButtonBindSettings.DataUpdateThreadTimeout),
                   new JProperty("AntiFlickPitMenuTimeout", ButtonBindSettings.AntiFlickPitMenuTimeout),
                   new JProperty("WriteStandingsJSON", ButtonBindSettings.WriteStandingsJSON),
                   new JProperty("WriteStandingsJSONToParameter", ButtonBindSettings.WriteStandingsJSONToParameter),
                   new JProperty("LMU_PluginInstallDir", ButtonBindSettings.LMU_PluginInstallDir));
            //string settings_path = AccData.path;
            try
            {
                // create/write settings file
                File.WriteAllText(LMURepairAndRefuelData.path, JSONdata.ToString());
                Logging.Current.Info("Plugin georace.lmuDataPlugin - Settings file saved to : " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);
            }
            catch
            {
                //A MessageBox creates graphical glitches after closing it. Search another way, maybe using the Standard Log in SimHub\Logs
                //MessageBox.Show("Cannot create or write the following file: \n" + System.Environment.CurrentDirectory + "\\" + AccData.path, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logging.Current.Error("Plugin georace.lmuDataPlugin - Cannot create or write settings file: " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);


            }
        }



        private void clock_format24_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.Clock_Format24 = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void clock_format24_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.Clock_Format24 = false;
            message_text.Text = "";
            SaveSetting();
        }


        private void WriteStandingsJSONToParameter_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.WriteStandingsJSONToParameter = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void WriteStandingsJSONToParameter_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.WriteStandingsJSONToParameter = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void WriteStandingsJSON_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.WriteStandingsJSON = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void WriteStandingsJSON_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.WriteStandingsJSON = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void RealTimeClock_Checked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.RealTimeClock = true;
            message_text.Text = "";
            SaveSetting();
        }
        private void RealTimeClock_unChecked(object sender, RoutedEventArgs e)
        {
            ButtonBindSettings.RealTimeClock = false;
            message_text.Text = "";
            SaveSetting();
        }

        private void GetMemoryDataThreadTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.GetMemoryDataThreadTimeout = (int)GetMemoryDataThreadTimeout.Value;
            SaveSetting();
        }
        
        private void DataUpdateThreadTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.DataUpdateThreadTimeout = (int)DataUpdateThreadTimeout.Value;
            SaveSetting();
        }

        private void AntiFlickPitMenuTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            ButtonBindSettings.AntiFlickPitMenuTimeout = (int)AntiFlickPitMenuTimeout.Value;
            SaveSetting();
        }

        private void pluginIstallPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Directory.Exists(pluginIstallPath.Text))
            {
                ButtonBindSettings.LMU_PluginInstallDir = pluginIstallPath.Text;
                SaveSetting();
                pluginIstallPath.Foreground = Brushes.White;
            }
            else
            {
                pluginIstallPath.Foreground = Brushes.Red;
            }
        }
    }


    public class LMU_EnegryAndFuelCalculation
    {
        public static double lastLapEnegry { get; set; }
        public static int lapIndex = 0;
        public static bool runned = false;
        public static double LastLapUsed = 0;
        public static bool inPit = true;
        public static double AvgOfFive = 0;

    }
    public class ButtonKeyValues
    {
         string _key { get; set; }
         string _value { get; set; }
    }


        public class ButtonBindSettings
    {
        public static bool RealTimeClock { get; set; }
        public static bool Clock_Format24 { get; set; }
        public static bool WriteStandingsJSON { get; set; }
        public static bool WriteStandingsJSONToParameter { get; set; }
        public static int DataUpdateThreadTimeout { get; set; }
        public static int GetMemoryDataThreadTimeout { get; set; }
        public static int AntiFlickPitMenuTimeout{ get; set; }
        public static string LMU_PluginInstallDir { get; set; }

    }


    /*public class AccSpeed - old way
    {*/
    /*private static int Speed = 20;
    public static int Value
    {
        get { return Speed; }
        set { Speed = value; }
    }*/
    /*public static int Value { get; set; }
}*/
}
