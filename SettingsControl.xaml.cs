﻿using System;
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
using Newtonsoft.Json.Linq; // Needed for JObject 
using System.IO;    // Needed for read/write JSON settings file
using SimHub;   // Needed for Logging
using System.Net;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using MahApps.Metro.Controls;   // Needed for Logging
using System.Windows.Markup;
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System.Diagnostics.Eventing.Reader;

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
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
            }
            catch { }
            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
        }

   
        public  void Refresh(string _Key)
        {
            bool changedBind = false;
            string MessageText = "";

            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 20;
            }
            catch { }
            base.Dispatcher.InvokeAsync(delegate
            {
                
               
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

       

        private void refresh_button_Click(object sender, RoutedEventArgs e)
        {

            clock_format24.IsChecked = ButtonBindSettings.Clock_Format24;
            RealTimeClock.IsChecked = ButtonBindSettings.RealTimeClock;
            GetMemoryDataThreadTimeout.Value = ButtonBindSettings.GetMemoryDataThreadTimeout;
            DataUpdateThreadTimeout.Value = ButtonBindSettings.DataUpdateThreadTimeout;
            message_text.Text = "";
        }

        private void SaveSetting()
         {
            JObject JSONdata = new JObject(
                   new JProperty("Clock_Format24", ButtonBindSettings.Clock_Format24),
                   new JProperty("RealTimeClock", ButtonBindSettings.RealTimeClock),
                   new JProperty("GetMemoryDataThreadTimeout", ButtonBindSettings.GetMemoryDataThreadTimeout),
                   new JProperty("DataUpdateThreadTimeout", ButtonBindSettings.DataUpdateThreadTimeout));
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
        public static int DataUpdateThreadTimeout { get; set; }
        public static int GetMemoryDataThreadTimeout { get; set; }

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
