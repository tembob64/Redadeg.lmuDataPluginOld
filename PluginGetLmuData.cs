using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Text;  //For File Encoding
using System.Windows.Forms;
using System.Windows.Controls;
//using System.Linq; // Needed for Properties().OrderBy
using Newtonsoft.Json.Linq; // Needed for JObject
using System.IO;    // Need for read/write JSON settings file
using SimHub;
//using SimHub.Plugins.InputPlugins;
using System.Net;
using System.Collections.Generic;
using SimHub.Plugins.InputPlugins;   // Needed for Logging

using System.Collections.ObjectModel;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using SimHub.Plugins.Resources;
using WoteverCommon;
using WoteverCommon.Extensions;
using WoteverLocalization;
using static WoteverCommon.JsonExtensions;
using static System.Net.Mime.MediaTypeNames;
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System.Xml.Linq;
using System.Windows.Markup;
using SimHub.Plugins.DataPlugins.DataCore;
using System.Linq.Expressions;
using System.Windows.Documents;
using System.Net.Http;



namespace Redadeg.lmuDataPlugin
{
    [PluginName("Redadeg LMU Data plugin")]
    [PluginDescription("Plugin for Redadeg Dashboards \nWorks for LMU")]
    [PluginAuthor("Bobokhidze T.B.")]

    //the class name is used as the property headline name in SimHub "Available Properties"
    public class lmuDataPlugin : IPlugin, IDataPlugin, IWPFSettings
    {

        private Thread lmu_extendedThread;
        private Thread lmuCalculateConsumptionsThread;
        private Thread lmuGetJSonDataThread;

        private SettingsControl settingsControlwpf;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationTokenSource ctsGetJSonDataThread = new CancellationTokenSource();
        private CancellationTokenSource ctsCalculateConsumptionsThread = new CancellationTokenSource();

        public bool IsEnded { get; private set; }
        public bool GetJSonDataIsEnded { get; private set; }
        public bool CalculateConsumptionsIsEnded { get; private set; }

        public PluginManager PluginManager { get; set; }

        public bool StopUpdate;
        public int Priority => 1;

        //input variables
        private string curGame;
        private bool GameInMenu = true;
        private bool GameRunning = false;
        private bool GamePaused = true;
        //private JoystickManagerSlimDX gamepadinput;
        //private string CarModel = "";
        private Dictionary<string, string> frontABR;
        private Dictionary<string, string> rearABR;
      
        //private float[] TyreRPS = new float[] { 0f, 0f, 0f, 0f };
        int[] lapsForCalculate = new int[] { };
        //private JObject JSONdata_diameters;
        //private bool isHybrid = false;
        //private bool isHaveVirtualEnergy = false;
        //private bool isDamaged = false;
        //private bool isStopAndGo = false;
        //private bool haveDriverMenu = false;
        private Guid SessionId;
        //output variables
        private float[] TyreDiameter = new float[] { 0f, 0f, 0f, 0f };   // in meter - FL,FR,RL,RR
        private float[] LngWheelSlip = new float[] { 0f, 0f, 0f, 0f }; // Longitudinal Wheel Slip values FL,FR,RL,RR
        
        private List<float> LapTimes = new List<float>();
        private List<float> EnergyConsuptions = new List<float>();
        private List<float> ClearEnergyConsuptions = new List<float>();
        private List<float> FuelConsuptions = new List<float>();

        //private double energy_AverageConsumptionPer5Lap;
        //private int energy_LastLapEnergy = 0;
        private int energy_CurrentIndex = 0;
        //private int IsInPit = -1;
        //private Guid LastLapId = new Guid();
        
        //private int energyPerLastLapRealTime = 0;
        private TimeSpan outFromPitTime = TimeSpan.FromSeconds(0);
        private bool OutFromPitFlag = false;
        private TimeSpan InToPitTime = TimeSpan.FromSeconds(0);
        private bool InToPitFlag = false;
        private bool IsLapValid = true;
        private bool LapInvalidated = false;
        private int pitStopUpdatePause = -1;
        private double sesstionTimeStamp = 0; 
        private double lastLapTime = 0;
        private const  int updateDataDelayTimer = 10;
        private  int updateDataDelayCounter = 0;
        private  int updateConsuptionDelayCounter = 0;
        private bool updateConsuptionFlag = false;
        private bool NeedUpdateData = false;

        //JObject pitMenuJSONData;
   
        MappedBuffer<LMU_Extended> extendedBuffer = new MappedBuffer<LMU_Extended>(LMU_Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(LMU_Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(LMU_Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);

        LMU_Extended lmu_extended;
      //  rF2Scoring scoring;
        rF2Rules rules;
        //WebClient wc = new WebClient();
        //WebClient wc_calc = new WebClient();
        bool lmu_extended_connected = false;
        bool rf2_score_connected = false;
        private HttpClient _httpClient;

        //        private void ComputeEnergyData(int CurrentLap, double CurrentLapTime, int pitState ,bool IsLapValid, PluginManager pluginManager)
        //        {
        //           // pluginManager.SetPropertyValue("georace.lmu.NewLap", this.GetType(), CurrentLap + " - PitState " + pitState);


        //            //if (pitState > 0)
        //            //{
        //            //    energy_LastLapEnergy = currentVirtualEnergy;
        //            // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), energy_LastLapEnergy + " - 112" + LMURepairAndRefuelData.currentVirtualEnergy);
        //            //}

        //            if (energy_LastLapEnergy > LMURepairAndRefuelData.currentVirtualEnergy)
        //            {
        //                int energyPerLastLapRaw = energy_LastLapEnergy - LMURepairAndRefuelData.currentVirtualEnergy;

        //                if (OutFromPitFlag) energyPerLastLapRaw = energyPerLastLapRealTime;
        //;

        //                //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(),  energyPerLastLapRaw);

        //                if ((pitState != CurrentLap && IsLapValid) || OutFromPitFlag || InToPitFlag)
        //                {
        //                    IsInPit = -1;
        //                    if (LapTimes.Count < 5)
        //                    {
        //                        energy_CurrentIndex++;
        //                        LapTimes.Add(CurrentLapTime);
        //                        EnergyConsuptions.Add(energyPerLastLapRaw);

        //                    }
        //                    else if (LapTimes.Count == 5)
        //                    {
        //                        energy_CurrentIndex++;
        //                        if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
        //                        LapTimes[energy_CurrentIndex] = CurrentLapTime;
        //                        EnergyConsuptions[energy_CurrentIndex] = energyPerLastLapRaw;
        //                    }
        //                }
        //                LMURepairAndRefuelData.energyPerLastLap = (double)(energyPerLastLapRaw);
        //                LMURepairAndRefuelData.energyPerLast5Lap = EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy;

        //                    //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), LapTimes.Average() + " - " + EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy);
        //                }


        //            energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
        //        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            //curGame = pluginManager.GetPropertyValue("DataCorePlugin.CurrentGame").ToString();
            curGame = data.GameName;
            GameInMenu = data.GameInMenu;
            GameRunning = data.GameRunning;
            GamePaused = data.GamePaused;

            if (data.GameRunning && !data.GameInMenu && !data.GamePaused && !StopUpdate)
            {
                //updateDataDelayCounter--;
                if (curGame == "LMU")   //TODO: check a record where the game was captured from startup on
                {

                    LMURepairAndRefuelData.IsInPit = data.OldData.IsInPit;
                    LMURepairAndRefuelData.CarClass = data.OldData.CarClass;
                    LMURepairAndRefuelData.CarModel = data.OldData.CarModel;

                    //detect out from pit
                    if (data.OldData.IsInPit > data.NewData.IsInPit)
                    {
                        OutFromPitFlag = true;
                        outFromPitTime = data.NewData.CurrentLapTime;
                        //   pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), outFromPitTime.ToString() + " SetPit Out " + data.NewData.IsInPit.ToString());
                    }

                    //detect in to pit
                    if (data.OldData.IsInPit < data.NewData.IsInPit)
                    {
                        InToPitFlag = true;
                        InToPitTime = data.NewData.CurrentLapTime;
                        //  pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), InToPitTime + " SetPit Int " + data.NewData.IsInPit.ToString());
                    }


                    //Clear data if session restart
                    if (data.OldData.SessionTypeName != data.NewData.SessionTypeName || data.OldData.IsSessionRestart != data.NewData.IsSessionRestart || !data.SessionId.Equals(SessionId))
                    {
                        SessionId = data.SessionId;
                        lastLapTime = 0;
                        sesstionTimeStamp = data.OldData.SessionTimeLeft.TotalSeconds;
                        LMURepairAndRefuelData.energyPerLastLap = 0;
                        LMURepairAndRefuelData.energyPerLast5Lap = 0;
                        LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
                        EnergyConsuptions.Clear();
                        ClearEnergyConsuptions.Clear();
                        FuelConsuptions.Clear();
                        LapTimes.Clear();
                    }

                    //Detect new lap
                    if (data.OldData.CurrentLap < data.NewData.CurrentLap || (LMURepairAndRefuelData.energyPerLastLap == 0 && !updateConsuptionFlag))
                    {
                        // Calculate last lap time
                        lastLapTime = sesstionTimeStamp - data.OldData.SessionTimeLeft.TotalSeconds;
                        sesstionTimeStamp = data.OldData.SessionTimeLeft.TotalSeconds;
                        // Calculate last lap time end

                        updateConsuptionFlag = true;
                        updateConsuptionDelayCounter = 10;

                        IsLapValid = data.OldData.IsLapValid;
                        LapInvalidated = data.OldData.LapInvalidated;
                    }
                    //Detect new lap end

                    //Calculate Energy consumption
                    //EnergyCalculate Delay counter elabsed "updateConsuptionDelayCounter" It is necessary because the data in the WEB API does not have time to update.
                    //Calculate Energy consumption END


                    //Update data
                    //f Data update Delay counter elabsed "updateDataDelayCounter" 
                    if (NeedUpdateData)
                    { 
                        try 
                        { 
                            pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.energyPerLast5ClearLap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

                            pluginManager.SetPropertyValue("Redadeg.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);
                            pluginManager.SetPropertyValue("Redadeg.lmu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
                            pluginManager.SetPropertyValue("Redadeg.lmu.rainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
                            pluginManager.SetPropertyValue("Redadeg.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
                            pluginManager.SetPropertyValue("Redadeg.lmu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
                            pluginManager.SetPropertyValue("Redadeg.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);
                            pluginManager.SetPropertyValue("Redadeg.lmu.addFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
                            pluginManager.SetPropertyValue("Redadeg.lmu.addVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);

                            pluginManager.SetPropertyValue("Redadeg.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
                            pluginManager.SetPropertyValue("Redadeg.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

                            pluginManager.SetPropertyValue("Redadeg.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
                            pluginManager.SetPropertyValue("Redadeg.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
                            pluginManager.SetPropertyValue("Redadeg.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

                            pluginManager.SetPropertyValue("Redadeg.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
                            pluginManager.SetPropertyValue("Redadeg.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
                            pluginManager.SetPropertyValue("Redadeg.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
                            pluginManager.SetPropertyValue("Redadeg.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

                            pluginManager.SetPropertyValue("Redadeg.lmu.fl_TyrePressure", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
                            pluginManager.SetPropertyValue("Redadeg.lmu.fr_TyrePressure", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
                            pluginManager.SetPropertyValue("Redadeg.lmu.rl_TyrePressure", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
                            pluginManager.SetPropertyValue("Redadeg.lmu.rr_TyrePressure", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);
                            pluginManager.SetPropertyValue("Redadeg.lmu.replaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

                            pluginManager.SetPropertyValue("Redadeg.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
                            pluginManager.SetPropertyValue("Redadeg.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
                            pluginManager.SetPropertyValue("Redadeg.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);
                            pluginManager.AddProperty("Redadeg.lmu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.VirtualEnergy);

                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.Cuts", this.GetType(), LMURepairAndRefuelData.Cuts);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.CutsMax", this.GetType(), LMURepairAndRefuelData.CutsMax);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyLeftLaps", this.GetType(), LMURepairAndRefuelData.PenaltyLeftLaps);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyType", this.GetType(), LMURepairAndRefuelData.PenaltyType);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyCount", this.GetType(), LMURepairAndRefuelData.PenaltyCount);

                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType1", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType1);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType2", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType2);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType3", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType3);

                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.TractionControl", this.GetType(), LMURepairAndRefuelData.mpTractionControl);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.BrakeMigration", this.GetType(), LMURepairAndRefuelData.mpBrakeMigration);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.BrakeMigrationMax", this.GetType(), LMURepairAndRefuelData.mpBrakeMigrationMax);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.MotorMap", this.GetType(), LMURepairAndRefuelData.mpMotorMap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), LMURepairAndRefuelData.mChangedParamValue);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.ChangedParamType", this.GetType(), LMURepairAndRefuelData.mChangedParamType);

                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ANTILOCKBRAKESYSTEMMAP", this.GetType(), LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_BRAKE_BALANCE", this.GetType(), LMURepairAndRefuelData.VM_BRAKE_BALANCE);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_BRAKE_MIGRATION", this.GetType(), LMURepairAndRefuelData.VM_BRAKE_MIGRATION);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ENGINE_BRAKEMAP", this.GetType(), LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ELECTRIC_MOTOR_MAP", this.GetType(), LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ENGINE_MIXTURE", this.GetType(), LMURepairAndRefuelData.VM_ENGINE_MIXTURE);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_REGEN_LEVEL", this.GetType(), LMURepairAndRefuelData.VM_REGEN_LEVEL);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLPOWERCUTMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLSLIPANGLEMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY", this.GetType(), LMURepairAndRefuelData.VM_FRONT_ANTISWAY);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_REAR_ANTISWAY", this.GetType(), LMURepairAndRefuelData.VM_REAR_ANTISWAY);
                            NeedUpdateData = false;
                        }
                        catch (Exception ex)
                        {
                            Logging.Current.Info("Plugin Redadeg.lmuDataPlugin Update parameters: " + ex.ToString());
                        }
                    }
                }
            }
            else
            {
                LMURepairAndRefuelData.mChangedParamType = -1;
                LMURepairAndRefuelData.mChangedParamValue = "";
            }
            
        }





        /// <summary>
        /// Called at plugin manager stop, close/displose anything needed here !
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            IsEnded = true;
            CalculateConsumptionsIsEnded = true;
            GetJSonDataIsEnded = true;
            cts.Cancel();
            ctsGetJSonDataThread.Cancel();
            ctsCalculateConsumptionsThread.Cancel();
            lmu_extendedThread.Join();
            lmuGetJSonDataThread.Join();
            lmuCalculateConsumptionsThread.Join();
            // try to read complete data file from disk, compare file data with new data and write new file if there are diffs
            try
            {
                if (rf2_score_connected) this.scoringBuffer.Disconnect();
                if(lmu_extended_connected) this.extendedBuffer.Disconnect();
                if (lmu_extended_connected) this.rulesBuffer.Disconnect();
               
                //WebClient wc = new WebClient();
                //JObject JSONcurGameData = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));

            }
            // if there is not already a settings file on disk, create new one and write data for current game
            catch (FileNotFoundException)
            {
                // try to write data file
               
            }
            // other errors like Syntax error on JSON parsing, data file will not be saved
            catch (Exception ex)
            {
                Logging.Current.Info("Plugin Redadeg.lmuDataPlugin - data file not saved. The following error occured: " + ex.Message);
            }
        }

        /// <summary>
        /// Return you winform settings control here, return null if no settings control
        /// 
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Forms.Control GetSettingsControl(PluginManager pluginManager)
        {
            return null;
        }

        public  System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            if (settingsControlwpf == null)
            {
                settingsControlwpf = new SettingsControl();
            }

            return settingsControlwpf;
        }

        private void LoadSettings(PluginManager pluginManager)
        {
            //IL_006a: Unknown result type (might be due to invalid IL or missing references)
            //IL_006f: Unknown result type (might be due to invalid IL or missing references)
            //IL_007c: Unknown result type (might be due to invalid IL or missing references)
            //IL_008e: Expected O, but got Unknown
           
        }

        private async void lmu_CalculateConsumptionsThread()

        {
            try
            {
                Task.Delay(500, ctsCalculateConsumptionsThread.Token).Wait();
              
                while (!IsEnded)
                {
                    if (GameRunning && !GameInMenu && !GamePaused && curGame == "LMU")
                    {
                        if (updateConsuptionFlag)
                        {

                           
                            if (updateConsuptionDelayCounter < 0)
                            {

                                //JObject SetupJSONdata = JObject.Parse(wc_calc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                                JObject TireManagementJSONdata = JObject.Parse(await FetchTireManagementJSONdata());
                                JObject expectedUsage = JObject.Parse(TireManagementJSONdata["expectedUsage"].ToString());

                                float fuelConsumption = expectedUsage["expectedUsage"] != null ? (float)expectedUsage["expectedUsage"] : 0;
                                double fuelFractionPerLap = expectedUsage["fuelFractionPerLap"] != null ? (double)expectedUsage["fuelFractionPerLap"] : 0;
                                float virtualEnergyConsumption = expectedUsage["virtualEnergyConsumption"] != null ? (float)((double)expectedUsage["virtualEnergyConsumption"] / (double)LMURepairAndRefuelData.maxVirtualEnergy * 100) : (float)0.0;
                                double virtualEnergyFractionPerLap = expectedUsage["virtualEnergyFractionPerLap"] != null ? (double)expectedUsage["virtualEnergyFractionPerLap"] : 0;
                                //JObject raceHistory = JObject.Parse(SetupJSONdata["raceHistory"].ToString());
                                //double LastLapConsumption = 0;
                                //int lapsCompletedCount = 0;

                                //EnergyConsuptions.Clear();
                                //FuelConsuptions.Clear();
                                //LapTimes.Clear();
                                LMURepairAndRefuelData.energyPerLastLap = virtualEnergyConsumption;

                                if (EnergyConsuptions.Count < 5)
                                {
                                    energy_CurrentIndex++;
                                    EnergyConsuptions.Add(virtualEnergyConsumption);
                                }
                                else if (EnergyConsuptions.Count == 5)
                                {
                                    energy_CurrentIndex++;
                                    if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                    EnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                }

                                if (IsLapValid && !LapInvalidated && !OutFromPitFlag && !InToPitFlag && LMURepairAndRefuelData.IsInPit == 0)
                                {
                                    if (LapTimes.Count < 5)
                                    {
                                        energy_CurrentIndex++;
                                        ClearEnergyConsuptions.Add(virtualEnergyConsumption);
                                        FuelConsuptions.Add(fuelConsumption);
                                        LapTimes.Add((float)lastLapTime);

                                    }
                                    else if (LapTimes.Count == 5)
                                    {
                                        energy_CurrentIndex++;
                                        if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                        LapTimes[energy_CurrentIndex] = (float)lastLapTime;
                                        ClearEnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                        FuelConsuptions[energy_CurrentIndex] = fuelConsumption;
                                    }
                                }
                               // Logging.Current.Info("Last Lap: " + lastLapTime.ToString() + " virtualEnergyConsumption: " + virtualEnergyConsumption.ToString() + " Raw: " + (expectedUsage["virtualEnergyConsumption"] != null ? (float)(double)expectedUsage["virtualEnergyConsumption"] : 0).ToString());
                                if (EnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = (float)EnergyConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = 0;
                                }

                                updateConsuptionFlag = false;
                                updateConsuptionDelayCounter = 10;
                            }
                           // Logging.Current.Info("Last Lap: " + lastLapTime.ToString() + " updateConsuptionDelayCounter: " + updateConsuptionDelayCounter.ToString() + " virtualEnergyConsumption: " + virtualEnergyConsumption.ToString());

                            updateConsuptionDelayCounter--;
                        }
                        OutFromPitFlag = false;
                        InToPitFlag = false;
                    }
                    Thread.Sleep(ButtonBindSettings.DataUpdateThreadTimeout);
                }
                
               

            }
            catch (AggregateException)
            {
                Logging.Current.Info(("AggregateException"));
            }
            catch (TaskCanceledException)
            {
                Logging.Current.Info(("TaskCanceledException"));
            }
        }

        private async void lmu_GetJSonDataThread()
            {
            try
            {
                Task.Delay(500, ctsGetJSonDataThread.Token).Wait();
                while (!IsEnded)
                {

                    if (GameRunning && !GameInMenu && !GamePaused && curGame == "LMU")
                    {
                        //if (updateDataDelayCounter < 0)
                        //{
                        //    wc = new WebClient();
                        try
                        {
                            JObject SetupJSONdata = JObject.Parse(await FetchCarSetupOverviewJSONdata()); // JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/CarSetupOverview"));
                            JObject carSetup = JObject.Parse(SetupJSONdata["carSetup"].ToString());
                            JObject garageValues = JObject.Parse(carSetup["garageValues"].ToString());
                            //  JObject pitRecommendations = JObject.Parse(JSONdata["pitRecommendations"].ToString());
                            JObject VM_ANTILOCKBRAKESYSTEMMAP = JObject.Parse(garageValues["VM_ANTILOCKBRAKESYSTEMMAP"].ToString());
                            JObject VM_BRAKE_BALANCE = JObject.Parse(garageValues["VM_BRAKE_BALANCE"].ToString());
                            JObject VM_BRAKE_MIGRATION = JObject.Parse(garageValues["VM_BRAKE_MIGRATION"].ToString());
                            JObject VM_ENGINE_BRAKEMAP = JObject.Parse(garageValues["VM_ENGINE_BRAKEMAP"].ToString());

                            JObject VM_ELECTRIC_MOTOR_MAP = JObject.Parse(garageValues["VM_ELECTRIC_MOTOR_MAP"].ToString());
                            JObject VM_ENGINE_MIXTURE = JObject.Parse(garageValues["VM_ENGINE_MIXTURE"].ToString());

                            JObject VM_REGEN_LEVEL = JObject.Parse(garageValues["VM_REGEN_LEVEL"].ToString());

                            JObject VM_TRACTIONCONTROLMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLMAP"].ToString());
                            JObject VM_TRACTIONCONTROLPOWERCUTMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLPOWERCUTMAP"].ToString());
                            JObject VM_TRACTIONCONTROLSLIPANGLEMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLSLIPANGLEMAP"].ToString());
                            JObject VM_REAR_ANTISWAY = JObject.Parse(garageValues["VM_REAR_ANTISWAY"].ToString());
                            JObject VM_FRONT_ANTISWAY = JObject.Parse(garageValues["VM_FRONT_ANTISWAY"].ToString());



                            if (LMURepairAndRefuelData.mChangedParamType == -1)
                            {
                                LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = VM_ANTILOCKBRAKESYSTEMMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_BRAKE_BALANCE = VM_BRAKE_BALANCE["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_BRAKE_MIGRATION = VM_BRAKE_MIGRATION["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = VM_ENGINE_BRAKEMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = VM_ELECTRIC_MOTOR_MAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = VM_ENGINE_MIXTURE["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_REGEN_LEVEL = VM_REGEN_LEVEL["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = VM_TRACTIONCONTROLMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = VM_TRACTIONCONTROLPOWERCUTMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = VM_TRACTIONCONTROLSLIPANGLEMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_REAR_ANTISWAY = VM_REAR_ANTISWAY["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_FRONT_ANTISWAY = VM_FRONT_ANTISWAY["stringValue"].ToString();
                            }
                            else
                            {
                                switch (LMURepairAndRefuelData.mChangedParamType)
                                {
                                    case 3:
                                        LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 10:
                                        LMURepairAndRefuelData.VM_BRAKE_BALANCE = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 15:
                                        LMURepairAndRefuelData.VM_BRAKE_MIGRATION = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 9:
                                        if (LMURepairAndRefuelData.mChangedParamValue.Contains("kW") || LMURepairAndRefuelData.mChangedParamValue.Contains("Off") || LMURepairAndRefuelData.mChangedParamValue.Contains("Safety-car") || LMURepairAndRefuelData.mChangedParamValue.Contains("Race"))
                                        {
                                            if (LMURepairAndRefuelData.CarClass.Contains("Hyper"))
                                            {
                                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = LMURepairAndRefuelData.mChangedParamValue;
                                            }
                                            else
                                            {
                                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = LMURepairAndRefuelData.mChangedParamValue;
                                            }
                                        }
                                        else
                                        {
                                            if (LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse 2024") || LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["F" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else if (LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies 2024") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport 2024") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing 2024") || LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["P" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else if (LMURepairAndRefuelData.CarModel.Equals("Glickenhaus Racing"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["G" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR[LMURepairAndRefuelData.mChangedParamValue]; }
                                        }
                                        break;
                                    case 11:
                                        LMURepairAndRefuelData.VM_REGEN_LEVEL = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 7:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 6:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 2:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 8:
                                        if (LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse 2024") || LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse"))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["F" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else if (LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies 2024") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport 2024") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing 2024") || LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing"))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["P" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else if (LMURepairAndRefuelData.CarModel.Equals("Glickenhaus Racing"))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["G" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR[LMURepairAndRefuelData.mChangedParamValue]; }

                                        break;
                                    default:
                                        // code block
                                        break;
                                }
                            }
                        }
                        catch
                        {

                        }


                        try
                        {
                            JObject RepairAndRefuelJSONdata = JObject.Parse(await FetchRepairAndRefuelJSONdata());
                            //Thread.Sleep(5);
                            JObject TireMagagementJSONdata = JObject.Parse(await FetchTireManagementJSONdata());
                            //Thread.Sleep(5);
                            JObject GameStateJSONdata = JObject.Parse(await FetchGetGameStateJSONdata());
                            //Thread.Sleep(5);
                            JObject fuelInfo = JObject.Parse(RepairAndRefuelJSONdata["fuelInfo"].ToString());
                            JObject pitStopLength = JObject.Parse(RepairAndRefuelJSONdata["pitStopLength"].ToString());
                            JObject pitMenuJSONData =  JObject.Parse(RepairAndRefuelJSONdata["pitMenu"].ToString()); // JObject.Parse(await FetchPitMenuJSONdata());
                            //if (pitStopUpdatePause == -1)
                            //{
                                
                            //}
                            //else
                            //{
                            //    if (pitStopUpdatePause == 0) // Update pit data if pitStopUpdatePauseCounter is 0
                            //    {
                            //       pitStopUpdatePause = -1;
                            //    }
                            //    pitStopUpdatePause--;
                            //}

                            JObject tireInventory = JObject.Parse(TireMagagementJSONdata["tireInventory"].ToString());

                            LMURepairAndRefuelData.maxAvailableTires = tireInventory["maxAvailableTires"] != null ? (int)tireInventory["maxAvailableTires"] : 0;
                            LMURepairAndRefuelData.newTires = tireInventory["newTires"] != null ? (int)tireInventory["newTires"] : 0;

                            LMURepairAndRefuelData.currentBattery = fuelInfo["currentBattery"] != null ? (int)fuelInfo["currentBattery"] : 0;
                            LMURepairAndRefuelData.currentFuel = fuelInfo["currentFuel"] != null ? (int)fuelInfo["currentFuel"] : 0;
                            LMURepairAndRefuelData.timeOfDay = GameStateJSONdata["timeOfDay"] != null ? (double)GameStateJSONdata["timeOfDay"] : 0;

                            JObject InfoForEventJSONdata = JObject.Parse(await FetchInfoForEventJSONdata());
                            JObject scheduledSessions = JObject.Parse(InfoForEventJSONdata.ToString());

                            foreach (JObject Sesstions in scheduledSessions["scheduledSessions"])
                            {
                                if (Sesstions["name"].ToString().ToUpper().Equals(LMURepairAndRefuelData.SessionTypeName)) LMURepairAndRefuelData.rainChance = Sesstions["rainChance"] != null ? (int)Sesstions["rainChance"] : 0;

                            }

                            LMURepairAndRefuelData.maxVirtualEnergy = fuelInfo["maxVirtualEnergy"] != null ? (int)fuelInfo["maxVirtualEnergy"] : 0;
                            LMURepairAndRefuelData.currentVirtualEnergy = fuelInfo["currentVirtualEnergy"] != null ? (int)fuelInfo["currentVirtualEnergy"] : 0;

                            LMURepairAndRefuelData.maxBattery = fuelInfo["maxBattery"] != null ? (int)fuelInfo["maxBattery"] : 0;
                            LMURepairAndRefuelData.maxFuel = fuelInfo["maxFuel"] != null ? (int)fuelInfo["maxFuel"] : 0;

                            LMURepairAndRefuelData.pitStopLength = pitStopLength["timeInSeconds"] != null ? (int)pitStopLength["timeInSeconds"] : 0;

                            //isStopAndGo = false;
                            //isDamaged = false;



                            //pitStopUpdatePause area

                            //pitStopUpdatePause area
                            int idx = 0;
                            int Virtual_Energy = 0;
                            foreach (JObject PMCs in pitMenuJSONData["pitMenu"])
                            {



                                if ((int)PMCs["PMC Value"] == 0)
                                {
                                    LMURepairAndRefuelData.passStopAndGo = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
 //                                   isStopAndGo = true;
                                 //   PitStopDataIndexes.Add(new PitStopDataIndexesClass(idx, (int)PMCs["settings"].Children().Count() - 1, PMCs["name"].ToString()));
                               }

                                if ((int)PMCs["PMC Value"] == 1)
                                {
                                    if (idx == 0)
                                    {
 //                                       isStopAndGo = false;
                                        LMURepairAndRefuelData.passStopAndGo = "";
                                    }
                                    LMURepairAndRefuelData.RepairDamage = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
//                                    if (LMURepairAndRefuelData.RepairDamage.Equals("N/A"))
//                                    { isDamaged = false; }
//                                    else
//                                    {
//                                        isDamaged = true;
///                                    }

                                }
                                if ((int)PMCs["PMC Value"] == 4)
                                {
                                    LMURepairAndRefuelData.Driver = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    //haveDriverMenu = true;
                                }
                                try
                                {

                                    
                                    if ((int)PMCs["PMC Value"] == 5)
                                    {
                                        LMURepairAndRefuelData.addVirtualEnergy = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                        Virtual_Energy = (int)PMCs["currentSetting"];
                                        //pluginManager.SetPropertyValue("georace.lmu.Virtual_Energy", this.GetType(), Virtual_Energy);
                                    }
                                    if ((int)PMCs["PMC Value"] == 6)
                                    {
                                        if (PMCs["name"].ToString().Equals("FUEL:"))
                                        {
                                            LMURepairAndRefuelData.addFuel = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                        }
                                        else
                                        {
                                            LMURepairAndRefuelData.FuelRatio = (double)PMCs["settings"][(int)PMCs["currentSetting"]]["text"];
                                            LMURepairAndRefuelData.addFuel = string.Format("{0:f1}", LMURepairAndRefuelData.FuelRatio * Virtual_Energy) + "L" + LMURepairAndRefuelData.addVirtualEnergy.Split('%')[1];
                                        }
                                    }
                                }
                                catch
                                {
                                    LMURepairAndRefuelData.FuelRatio = 0;
                                }


                                try
                                {
                                    if ((int)PMCs["PMC Value"] == 21)
                                    {
                                        LMURepairAndRefuelData.Grille = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    }
                                    if ((int)PMCs["PMC Value"] == 19)
                                    {
                                        LMURepairAndRefuelData.Wing = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                    }
                                }
                                catch
                                {
                                }

                                if ((int)PMCs["PMC Value"] == 12)
                                {
                                    LMURepairAndRefuelData.fl_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 13)
                                {
                                    LMURepairAndRefuelData.fr_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 14)
                                {
                                    LMURepairAndRefuelData.rl_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 15)
                                {
                                    LMURepairAndRefuelData.rr_TyreChange = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }

                                if ((int)PMCs["PMC Value"] == 24)
                                {
                                    LMURepairAndRefuelData.fl_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 25)
                                {
                                    LMURepairAndRefuelData.fr_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 26)
                                {
                                    LMURepairAndRefuelData.rl_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                if ((int)PMCs["PMC Value"] == 27)
                                {
                                    LMURepairAndRefuelData.rr_TyrePressure = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }

                                if ((int)PMCs["PMC Value"] == 32)
                                {
                                    LMURepairAndRefuelData.replaceBrakes = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();
                                }
                                idx++;
                            }
                            //pitStopUpdatePause area
                        }
                        catch (Exception ex2)
                        {
                            Logging.Current.Info("currentVirtualEnergy: " + ex2.ToString());
                            LMURepairAndRefuelData.currentVirtualEnergy = 0;
                            LMURepairAndRefuelData.maxVirtualEnergy = 0;
                        }



                        try
                        {


                            if (ClearEnergyConsuptions.Count() > 0 && LapTimes.Count() > 0 && LMURepairAndRefuelData.maxVirtualEnergy > 0)
                            {
                                float virtualErg = (float)LMURepairAndRefuelData.currentVirtualEnergy / (float)LMURepairAndRefuelData.maxVirtualEnergy * 100;
                                LMURepairAndRefuelData.energyTimeElapsed = (LapTimes.Average() * virtualErg / ClearEnergyConsuptions.Average()) / 60;
                                LMURepairAndRefuelData.VirtualEnergy = virtualErg;
                                //LTime ConsumAvg
                                //      Energy    
                            }

                            if (EnergyConsuptions.Count() > 0)
                            {
                                LMURepairAndRefuelData.energyPerLast5Lap = (float)EnergyConsuptions.Average();
                            }
                            else
                            {
                                LMURepairAndRefuelData.energyPerLast5Lap = 0;
                            }

                            if (ClearEnergyConsuptions.Count() > 0)
                            {
                                LMURepairAndRefuelData.energyPerLast5ClearLap = (float)ClearEnergyConsuptions.Average();
                            }
                            else
                            {
                                LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Current.Info("SectorChange: " + ex.ToString());

                        }
                        //updateDataDelayCounter = ButtonBindSettings.UpdateDataCounter;
                        NeedUpdateData = true;
                    }

                    Thread.Sleep(ButtonBindSettings.DataUpdateThreadTimeout);
                }
            }
              catch (AggregateException)
            {
                Logging.Current.Info(("AggregateException"));
            }
            catch (TaskCanceledException)
            {
                Logging.Current.Info(("TaskCanceledException"));
            }
        }

        private void lmu_extendedReadThread()
        {
            try
            {
                Task.Delay(500, cts.Token).Wait();
            
                while (!IsEnded)
                {
                    if (!this.lmu_extended_connected)
                    {
                        try
                        {
                            // Extended buffer is the last one constructed, so it is an indicator RF2SM is ready.
                            this.extendedBuffer.Connect();
                            this.rulesBuffer.Connect();
                            
                            this.lmu_extended_connected = true; 
                        }
                        catch (Exception)
                        {
                            LMURepairAndRefuelData.Cuts = 0;
                            LMURepairAndRefuelData.CutsMax = 0;
                            LMURepairAndRefuelData.PenaltyLeftLaps = 0;
                            LMURepairAndRefuelData.PenaltyType = 0;
                            LMURepairAndRefuelData.PenaltyCount = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType1 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType2 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType3 = 0;
                            LMURepairAndRefuelData.mpBrakeMigration = 0;
                            LMURepairAndRefuelData.mpBrakeMigrationMax = 0;
                            LMURepairAndRefuelData.mpTractionControl = 0;
                            LMURepairAndRefuelData.mpMotorMap = "None";
                            LMURepairAndRefuelData.mChangedParamValue = "None";
                            LMURepairAndRefuelData.mChangedParamType = 0;
                            LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = "N/A";
                            LMURepairAndRefuelData.VM_BRAKE_BALANCE = "N/A";
                            LMURepairAndRefuelData.VM_BRAKE_MIGRATION = "N/A";
                            LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = "N/A";
                            LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = "N/A";
                            LMURepairAndRefuelData.VM_REGEN_LEVEL = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = "N/A";
                            LMURepairAndRefuelData.VM_FRONT_ANTISWAY = "N/A";
                            LMURepairAndRefuelData.VM_REAR_ANTISWAY = "N/A";
                            this.lmu_extended_connected = false;
                           // Logging.Current.Info("Extended data update service not connectded.");
                        }
                    }
                    else
                    {
                        extendedBuffer.GetMappedData(ref lmu_extended);
                        rulesBuffer.GetMappedData(ref rules);
                        LMURepairAndRefuelData.Cuts = lmu_extended.mCuts;
                        LMURepairAndRefuelData.CutsMax = lmu_extended.mCutsPoints;
                        LMURepairAndRefuelData.PenaltyLeftLaps  = lmu_extended.mPenaltyLeftLaps;
                        LMURepairAndRefuelData.PenaltyType = lmu_extended.mPenaltyType;
                        LMURepairAndRefuelData.PenaltyCount = lmu_extended.mPenaltyCount;
                        LMURepairAndRefuelData.mPendingPenaltyType1 = lmu_extended.mPendingPenaltyType1;
                        LMURepairAndRefuelData.mPendingPenaltyType2 = lmu_extended.mPendingPenaltyType2;
                        LMURepairAndRefuelData.mPendingPenaltyType3 = lmu_extended.mPendingPenaltyType3;
                        LMURepairAndRefuelData.mpBrakeMigration = lmu_extended.mpBrakeMigration;
                        LMURepairAndRefuelData.mpBrakeMigrationMax = lmu_extended.mpBrakeMigrationMax;
                        LMURepairAndRefuelData.mpTractionControl = lmu_extended.mpTractionControl;
                        LMURepairAndRefuelData.mpMotorMap = GetStringFromBytes(lmu_extended.mpMotorMap);
                        string mChangedParamValue = GetStringFromBytes(lmu_extended.mChangedParamValue).Trim();
                        if (lmu_extended.mChangedParamType == 0 && mChangedParamValue.Equals(""))
                        {
                            LMURepairAndRefuelData.mChangedParamType = -1;
                            LMURepairAndRefuelData.mChangedParamValue = "";
                        }
                        else 
                        {
                            LMURepairAndRefuelData.mChangedParamType = lmu_extended.mChangedParamType;
                            LMURepairAndRefuelData.mChangedParamValue = mChangedParamValue;
                        }


                       // Logging.Current.Info(("Extended data update service connectded. " +  lmu_extended.mCutsPoints.ToString() + " Penalty laps" + lmu_extended.mPenaltyLeftLaps).ToString());
                    }





                 Thread.Sleep(ButtonBindSettings.GetMemoryDataThreadTimeout);

                
                }

            }
            catch (AggregateException)
            {
                Logging.Current.Info(("AggregateException"));
            }
            catch (TaskCanceledException)
            {
                Logging.Current.Info(("TaskCanceledException"));
            }
        }

        private async Task<string> FetchTireManagementJSONdata()
        {
            try
            {
                var urlTireManagement = "http://localhost:6397/rest/garage/UIScreen/TireManagement";
                var responseTireManagement = await _httpClient.GetStringAsync(urlTireManagement);
                return responseTireManagement;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch TireManagement data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchRepairAndRefuelJSONdata()
        {
            try
            {
                var urlRepairAndRefuel = "http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel";
                var responseRepairAndRefuel = await _httpClient.GetStringAsync(urlRepairAndRefuel);
                return responseRepairAndRefuel;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch RepairAndRefuel data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchGetGameStateJSONdata()
        {
            try
            {
                var urlGetGameState = "http://localhost:6397/rest/sessions/GetGameState";
                var responseGetGameState = await _httpClient.GetStringAsync(urlGetGameState);
                return responseGetGameState;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch GetGameState data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchInfoForEventJSONdata()
        {
            try
            {
                var urlInfoForEvent = "http://localhost:6397/rest/sessions/GetSessionsInfoForEvent";
                var responseInfoForEvent = await _httpClient.GetStringAsync(urlInfoForEvent);
                return responseInfoForEvent;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch InfoForEvent data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchRaceHistoryJSONdata()
        {
            try
            {
                var urlRaceHistory = "http://localhost:6397/rest/garage/UIScreen/RaceHistory";
                var responseRaceHistory = await _httpClient.GetStringAsync(urlRaceHistory);
                return responseRaceHistory;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch RaceHistory data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchPitMenuJSONdata()
        {
            try
            {
                var urlPitMenu = "http://localhost:6397/rest/garage/PitMenu/receivePitMenu";
                var responsePitMenu = await _httpClient.GetStringAsync(urlPitMenu);
                return responsePitMenu;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch PitMenu data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private async Task<string> FetchCarSetupOverviewJSONdata()
        {
            try
            {
                var urlCarSetupOverview = "http://localhost:6397/rest/garage/UIScreen/CarSetupOverview";
                var responseCarSetupOverview = await _httpClient.GetStringAsync(urlCarSetupOverview);
                return responseCarSetupOverview;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch CarSetupOverview data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
        }
        private float GetPMCValue(JArray pitMenuJSONData, int pmcValue)
        {
            JToken item = pitMenuJSONData?.FirstOrDefault(x => (int?)x["PMC Value"] == pmcValue);

            if (item != null && item["currentSetting"] != null)
            {
                float currentSetting = (float)item["currentSetting"];

                return currentSetting;
            }
            return 0;
        }
        private string GetPMCText(JArray pitMenuJSONData, int pmcValue, string defaultValue = "Unknown")
        {
            JToken item = pitMenuJSONData?.FirstOrDefault(x => (int?)x["PMC Value"] == pmcValue);

            if (item != null && item["currentSetting"] != null)
            {
                int currentSetting = (int)item["currentSetting"];
                JToken setting = item["settings"]?[currentSetting];

                return setting?["text"]?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        private static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes == null)
                return "null";

            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);


        }

        public static rF2VehicleScoring GetPlayerScoring(ref rF2Scoring scoring)
        {
            var playerVehScoring = new rF2VehicleScoring();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        if (vehicle.mIsPlayer == 1)
                            playerVehScoring = vehicle;

                        break;

                    default:
                        continue;
                }

                if (playerVehScoring.mIsPlayer == 1)
                    break;
            }

            return playerVehScoring;
        }

        public static List<rF2VehicleScoring> GetOpenentsScoring(ref rF2Scoring scoring)
        {
            List<rF2VehicleScoring> playersVehScoring  = new List<rF2VehicleScoring>();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);
                        break;
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);

                        break;

                    default:
                        continue;
                }

             }

            return playersVehScoring;
        }

        private void SaveJSonSetting()
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
                //Logging.Current.Info("Plugin Viper.PluginCalcLngWheelSlip - Settings file saved to : " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);
            }
            catch
            {
                //A MessageBox creates graphical glitches after closing it. Search another way, maybe using the Standard Log in SimHub\Logs
                //MessageBox.Show("Cannot create or write the following file: \n" + System.Environment.CurrentDirectory + "\\" + AccData.path, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //Logging.Current.Error("Plugin Viper.PluginCalcLngWheelSlip - Cannot create or write settings file: " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);


            }
        }
      
        public void Init(PluginManager pluginManager)
        {
            //wc = new WebClient();
            //wc_calc = new WebClient();
            _httpClient = new HttpClient();
            LapTimes = new List<float>();
            EnergyConsuptions = new List<float>();
            ClearEnergyConsuptions = new List<float>();
            FuelConsuptions = new List<float>();
        // set path/filename for settings file
            LMURepairAndRefuelData.path = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.json");
            string path_data = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.data.json");
            //List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
            // try to read settings file




            LoadSettings(pluginManager);
            lmu_extendedThread = new Thread(lmu_extendedReadThread)
            {
                Name = "GetJSonDataThread"
            };
            lmu_extendedThread.Start();

            lmuGetJSonDataThread = new Thread(lmu_GetJSonDataThread)
            {
                Name = "ExtendedDataUpdateThread"
            };
            lmuGetJSonDataThread.Start();
            lmuCalculateConsumptionsThread = new Thread(lmu_CalculateConsumptionsThread)
            {
                Name = "CalculateConsumptionsThread"
            };
            lmuCalculateConsumptionsThread.Start();


            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
            }
            catch { }

          
            
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.energyPerLast5ClearLap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLastLapRealTime", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.energyLapsRealTimeElapsed", this.GetType(), 0);

            pluginManager.AddProperty("Redadeg.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

            pluginManager.AddProperty("Redadeg.lmu.NewLap", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.rainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
            pluginManager.AddProperty("Redadeg.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
            pluginManager.AddProperty("Redadeg.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);
            pluginManager.AddProperty("Redadeg.lmu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
            pluginManager.AddProperty("Redadeg.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
            pluginManager.AddProperty("Redadeg.lmu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
            pluginManager.AddProperty("Redadeg.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);
            pluginManager.AddProperty("Redadeg.lmu.addFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
            pluginManager.AddProperty("Redadeg.lmu.addVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
            pluginManager.AddProperty("Redadeg.lmu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);
            pluginManager.AddProperty("Redadeg.lmu.Virtual_Energy", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
            pluginManager.AddProperty("Redadeg.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.VirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

            pluginManager.AddProperty("Redadeg.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
            pluginManager.AddProperty("Redadeg.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

            pluginManager.AddProperty("Redadeg.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
            pluginManager.AddProperty("Redadeg.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
            pluginManager.AddProperty("Redadeg.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
            pluginManager.AddProperty("Redadeg.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

            pluginManager.AddProperty("Redadeg.lmu.fl_TyrePressure", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.fr_TyrePressure", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.rl_TyrePressure", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.rr_TyrePressure", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.replaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

            pluginManager.AddProperty("Redadeg.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
            pluginManager.AddProperty("Redadeg.lmu.selectedMenuIndex", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
            pluginManager.AddProperty("Redadeg.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);

            //pluginManager.AddProperty("Redadeg.lmu.isStopAndGo", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.isDamage", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.haveDriverMenu", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.isHyper", this.GetType(), 0);

            pluginManager.AddProperty("Redadeg.lmu.Extended.Cuts", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.CutsMax", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyLeftLaps", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyType", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyCount", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType1", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType2", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType3", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.TractionControl", this.GetType(),0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.MotorMap", this.GetType(), "None");
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamType", this.GetType(), -1);
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), "None");
            pluginManager.AddProperty("Redadeg.lmu.Extended.BrakeMigration", this.GetType(),0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.BrakeMigrationMax", this.GetType(), 0);

            pluginManager.AddProperty("Redadeg.lmu.mMessage", this.GetType(), "");

            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ANTILOCKBRAKESYSTEMMAP", this.GetType(),"");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_BRAKE_BALANCE", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_BRAKE_MIGRATION", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ENGINE_BRAKEMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ELECTRIC_MOTOR_MAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ENGINE_MIXTURE", this.GetType(), ""); 
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_REGEN_LEVEL", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLPOWERCUTMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLSLIPANGLEMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_REAR_ANTISWAY", this.GetType(), "");

            frontABR = new Dictionary<string, string>();
            rearABR = new Dictionary<string, string>();
            try
            {
                //Add front ABR
                frontABR.Add("Detached", "Detached");
                frontABR.Add("866 N/mm", "P1");
                frontABR.Add("1069 N/mm", "P2");
                frontABR.Add("1271 N/mm", "P3");
                frontABR.Add("1473 N/mm", "P4");
                frontABR.Add("1676 N/mm", "P5");

                //ferrary
                frontABR.Add("FDetached", "Detached");
                frontABR.Add("F94 N/mm", "A-P1");
                frontABR.Add("F107 N/mm", "A-P2");
                frontABR.Add("F133 N/mm", "A-P3");
                frontABR.Add("F172 N/mm", "A-P4");
                frontABR.Add("F254 N/mm", "A-P5");

                frontABR.Add("F232 N/mm", "B-P1");
                frontABR.Add("F262 N/mm", "B-P2");
                frontABR.Add("F307 N/mm", "B-P3");
                frontABR.Add("F364 N/mm", "B-P4");
                frontABR.Add("F440 N/mm", "B-P5");

                frontABR.Add("F312 N/mm", "C-P1");
                frontABR.Add("F332 N/mm", "C-P2");
                frontABR.Add("F365 N/mm", "C-P3");
                frontABR.Add("F403 N/mm", "C-P4");
                frontABR.Add("F450 N/mm", "C-P5");

                frontABR.Add("F426 N/mm", "D-P1");
                frontABR.Add("F469 N/mm", "D-P2");
                frontABR.Add("F530 N/mm", "D-P3");
                frontABR.Add("F599 N/mm", "D-P4");
                frontABR.Add("F685 N/mm", "D-P5");

                frontABR.Add("F632 N/mm", "E-P1");
                frontABR.Add("F748 N/mm", "E-P2");
                frontABR.Add("F929 N/mm", "E-P3");
                frontABR.Add("F1152 N/mm", "E-P4");
                frontABR.Add("F1473 N/mm", "E-P5");


                //pegeout
                frontABR.Add("PDetached", "Detached");
                frontABR.Add("P428 N/mm", "P1");
                frontABR.Add("P487 N/mm", "P2");
                frontABR.Add("P559 N/mm", "P3");
                frontABR.Add("P819 N/mm", "P4");
                frontABR.Add("P932 N/mm", "P5");

                frontABR.Add("P1069 N/mm", "P6");
                frontABR.Add("P1545 N/mm", "P7");
                frontABR.Add("P1758 N/mm", "P8");
                frontABR.Add("P2018 N/mm", "P9");
                frontABR.Add("P2689 N/mm", "P10");

                frontABR.Add("P3059 N/mm", "P11");
                frontABR.Add("P3512 N/mm", "P12");
                frontABR.Add("P3889 N/mm", "P13");
                frontABR.Add("P4425 N/mm", "P14");
                frontABR.Add("P5080 N/mm", "P15");

                //Glickenhaus Racing
                frontABR.Add("GDetached", "Detached");
                frontABR.Add("G86 N/mm", "P1");
                frontABR.Add("G97 N/mm", "P2");
                frontABR.Add("G112 N/mm", "P3");
                frontABR.Add("G164 N/mm", "P4");
                frontABR.Add("G186 N/mm", "P5");

                frontABR.Add("G214 N/mm", "P6");
                frontABR.Add("G309 N/mm", "P7");
                frontABR.Add("G352 N/mm", "P8");
                frontABR.Add("G404 N/mm", "P9");
                frontABR.Add("G538 N/mm", "P10");

                frontABR.Add("G612 N/mm", "P11");
                frontABR.Add("G702 N/mm", "P12");
                frontABR.Add("G778 N/mm", "P13");
                frontABR.Add("G885 N/mm", "P14");
                frontABR.Add("G1016 N/mm", "P15");

                //add rear abr
                rearABR.Add("Detached", "Detached");
                rearABR.Add("492 N/mm", "P1");
                rearABR.Add("638 N/mm", "P2");
                rearABR.Add("784 N/mm", "P3");
                rearABR.Add("930 N/mm", "P4");
                rearABR.Add("1077 N/mm", "P5");
                //ferrary
                rearABR.Add("FDetached", "Detached");
                rearABR.Add("F98 N/mm", "A-P1");
                rearABR.Add("F120 N/mm", "A-P2");
                rearABR.Add("F142 N/mm", "A-P3");
                rearABR.Add("F166 N/mm", "A-P4");
                rearABR.Add("F184 N/mm", "A-P5");

                rearABR.Add("F171 N/mm", "B-P1");
                rearABR.Add("F211 N/mm", "B-P2");
                rearABR.Add("F253 N/mm", "B-P3");
                rearABR.Add("F299 N/mm", "B-P4");
                rearABR.Add("F344 N/mm", "B-P5");

                rearABR.Add("F275 N/mm", "C-P1");
                rearABR.Add("F306 N/mm", "C-P2");
                rearABR.Add("F330 N/mm", "C-P3");
                rearABR.Add("F355 N/mm", "C-P4");
                rearABR.Add("F368 N/mm", "C-P5");

                rearABR.Add("F317 N/mm", "D-P1");
                rearABR.Add("F357 N/mm", "D-P2");
                rearABR.Add("F393 N/mm", "D-P3");
                rearABR.Add("F428 N/mm", "D-P4");
                rearABR.Add("F452 N/mm", "D-P5");

                rearABR.Add("F435 N/mm", "E-P1");
                rearABR.Add("F514 N/mm", "E-P2");
                rearABR.Add("F590 N/mm", "E-P3");
                rearABR.Add("F668 N/mm", "E-P4");
                rearABR.Add("F736 N/mm", "E-P5");

                //pegeout
                rearABR.Add("PDetached", "Detached");
                rearABR.Add("P119 N/mm", "P1");
                rearABR.Add("P144 N/mm", "P2");
                rearABR.Add("P178 N/mm", "P3");
                rearABR.Add("P206 N/mm", "P4");
                rearABR.Add("P250 N/mm", "P5");

                rearABR.Add("P308 N/mm", "P6");
                rearABR.Add("P393 N/mm", "P7");
                rearABR.Add("P476 N/mm", "P8");
                rearABR.Add("P587 N/mm", "P9");
                rearABR.Add("P732 N/mm", "P10");

                rearABR.Add("P886 N/mm", "P11");
                rearABR.Add("P1094 N/mm", "P12");
                rearABR.Add("P1330 N/mm", "P13");
                rearABR.Add("P1610 N/mm", "P14");
                rearABR.Add("P1987 N/mm", "P15");

                //Glickenhaus Racing
                rearABR.Add("GDetached", "Detached");
                rearABR.Add("G48 N/mm", "P1");
                rearABR.Add("G58 N/mm", "P2");
                rearABR.Add("G71 N/mm", "P3");
                rearABR.Add("G82 N/mm", "P4");
                rearABR.Add("G100 N/mm", "P5");

                rearABR.Add("G123 N/mm", "P6");
                rearABR.Add("G157 N/mm", "P7");
                rearABR.Add("G190 N/mm", "P8");
                rearABR.Add("G235 N/mm", "P9");
                rearABR.Add("G293 N/mm", "P10");

                rearABR.Add("G354 N/mm", "P11");
                rearABR.Add("G437 N/mm", "P12");
                rearABR.Add("G532 N/mm", "P13");
                rearABR.Add("G644 N/mm", "P14");
                rearABR.Add("G795 N/mm", "P15");
            }
            catch { }
        }
    }

    //public class for exchanging the data with the main cs file (Init and DataUpdate function)
    public class LMURepairAndRefuelData
    {

        public static double mPlayerBestLapTime { get; set; }
        public static double mPlayerBestLapSector1 { get; set; }
        public static double mPlayerBestLapSector2 { get; set; }
        public static double mPlayerBestLapSector3 { get; set; }

        public static double mPlayerBestSector1 { get; set; }
        public static double mPlayerBestSector2 { get; set; }
        public static double mPlayerBestSector3 { get; set; }

        public static double mPlayerCurSector1 { get; set; }
        public static double mPlayerCurSector2 { get; set; }
        public static double mPlayerCurSector3 { get; set; }

        public static double mSessionBestSector1 { get; set; }
        public static double mSessionBestSector2 { get; set; }
        public static double mSessionBestSector3 { get; set; }


        //public static string PIT_RECOM_FL_TIRE { get; set; }
        //public static string PIT_RECOM_FR_TIRE { get; set; }
        //public static string PIT_RECOM_RL_TIRE { get; set; }
        //public static string PIT_RECOM_RR_TIRE { get; set; }

        //public static string PIT_RECOM_TIRES { get; set; }
        //public static string PIT_RECOM_fuel { get; set; }
        //public static string PIT_RECOM_virtualEnergy { get; set; }

        public static int mpBrakeMigration { get; set; }
        public static int mpBrakeMigrationMax { get; set; }
        public static int mpTractionControl { get; set; }
        public static string mpMotorMap { get; set; }
        public static int mChangedParamType { get; set; }
        public static string mChangedParamValue { get; set; }

        public static float Cuts { get; set; }
        public static int CutsMax { get; set; }
        public static int PenaltyLeftLaps { get; set; }
        public static int PenaltyType { get; set; }
        public static int PenaltyCount { get; set; }
        public static int mPendingPenaltyType1 { get; set; }
        public static int mPendingPenaltyType2 { get; set; }
        public static int mPendingPenaltyType3 { get; set; }
        public static float energyTimeElapsed { get; set; }
        public static float energyPerLastLap { get; set; }
        public static float energyPerLast5Lap { get; set; }
        public static float energyPerLast5ClearLap { get; set; }
        public static double currentFuel { get; set; }
        public static int currentVirtualEnergy { get; set; }
        public static int currentBattery { get; set; }
        public static int maxBattery { get; set; }
        public static int maxFuel { get; set; }
        public static int maxVirtualEnergy { get; set; }
        public static string RepairDamage { get; set; }
        public static string passStopAndGo { get; set; }
        public static string Driver { get; set; }
        public static float VirtualEnergy { get; set; }

        public static string addVirtualEnergy { get; set; }
        public static string addFuel { get; set; }

        public static string Wing { get; set; }
        public static string Grille { get; set; }

        public static int maxAvailableTires { get; set; }
        public static int newTires { get; set; }
        public static string fl_TyreChange { get; set; }
        public static string fr_TyreChange { get; set; }
        public static string rl_TyreChange { get; set; }
        public static string rr_TyreChange { get; set; }

        public static string fl_TyrePressure { get; set; }
        public static string fr_TyrePressure { get; set; }
        public static string rl_TyrePressure { get; set; }
        public static string rr_TyrePressure { get; set; }
        public static string replaceBrakes { get; set; }
        public static double FuelRatio { get; set; }
        public static double pitStopLength { get; set; }
        public static string path { get; set; }
        public static double timeOfDay { get; set; }
        public static int rainChance { get; set; }

        public static string VM_ANTILOCKBRAKESYSTEMMAP { get; set; }
        public static string VM_BRAKE_BALANCE { get; set; }
        public static string VM_BRAKE_MIGRATION { get; set; }
        public static string VM_ENGINE_BRAKEMAP { get; set; }
        public static string VM_ELECTRIC_MOTOR_MAP { get; set; }
        public static string VM_ENGINE_MIXTURE { get; set; }
        public static string VM_REGEN_LEVEL { get; set; }
        public static string VM_TRACTIONCONTROLMAP { get; set; }
        public static string VM_TRACTIONCONTROLPOWERCUTMAP { get; set; }
        public static string VM_TRACTIONCONTROLSLIPANGLEMAP { get; set; }
        public static string VM_REAR_ANTISWAY { get; set; }
        public static string VM_FRONT_ANTISWAY { get; set; }
        public static string CarClass { get; set; }
        public static string CarModel { get; set; }
        public static string SessionTypeName { get; set; }
        public static int IsInPit { get; set; }


    }
}
