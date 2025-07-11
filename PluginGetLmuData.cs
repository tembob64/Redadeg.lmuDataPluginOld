using GameReaderCommon;
using MahApps.Metro.Behaviours;
using Newtonsoft.Json.Linq; // Needed for JObject
using SimHub;
using SimHub.Plugins;
using SimHub.Plugins.DataPlugins.DataCore;
using SimHub.Plugins.OutputPlugins.GraphicalDash.Models.BuiltIn;
using System;
using System.Collections.Generic;
using System.IO;    // Need for read/write JSON settings file
using System.Linq;
using System.Net.Http;
using System.Text;  //For File Encoding
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shapes;
using WoteverCommon.Extensions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;



namespace Redadeg.lmuDataPlugin
{
    [PluginName("Redadeg LMU Data plugin")]
    [PluginDescription("Plugin for Redadeg Dashboards \nWorks for LMU")]
    [PluginAuthor("Bobokhidze T.B.")]

    //the class name is used as the property headline name in SimHub "Available Properties"
    public class lmuDataPlugin : IPlugin, IDataPlugin, IWPFSettings
    {

        private const string PLUGIN_CONFIG_FILENAME = "Redadeg.lmuDataPlugin.json";

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

        private Dictionary<string, List<string>> rearABRs;
        private Dictionary<string, List<string>> frontABRs;
        //    int[] lapsForCalculate = new int[] { };
        private Guid SessionId;

        //output variables

        private List<float> LapTimes = new List<float>();
        private List<float> EnergyConsuptions = new List<float>();
        private List<float> ClearEnergyConsuptions = new List<float>();
        private List<float> FuelConsuptions = new List<float>();
        private List<float> ClearFuelConsuptions = new List<float>();

        private int hystory_size = 5;
        //private double energy_AverageConsumptionPer5Lap;
        //private int energy_LastLapEnergy = 0;
        private int EnergyCurrentIndex = 1;
        private int ClearEnergyCurrentIndex = -1;
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
        private const int updateDataDelayTimer = 10;
        private int updateDataDelayCounter = 0;
        private int updateConsuptionDelayCounter = 0;
        private bool updateConsuptionFlag = false;
        //private bool TireManagementJSONdataInited = false;
        
        private bool NeedUpdateData = false;
        private bool GetDataThreadEndWork = false;
        JObject TireManagementJSONdata;

        MappedBuffer<LMU_Extended> extendedBuffer = new MappedBuffer<LMU_Extended>(LMU_Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(LMU_Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        // MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(LMU_Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);

        LMU_Extended lmu_extended;
        //rF2Scoring scoring;
        //rF2Rules rules;
        bool lmu_extended_connected = false;
        bool rf2_score_connected = false;
        private HttpClient _httpClient;
        private bool loadSessionStaticInfoFromWS = true; // set to true, to force loading data if simhub is launch after the session
        private bool ReguiredVluesInited = false; // set to true, to force loading data if simhub is launch after the session
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            //curGame = pluginManager.GetPropertyValue("DataCorePlugin.CurrentGame").ToString();
            curGame = data.GameName;
            GameInMenu = data.GameInMenu;
            GameRunning = data.GameRunning;
            GamePaused = data.GamePaused;

            // When game is in menu, we setup flag to recall the pitmenu settings at the beginning of the session
            if (curGame == "LMU"
                    && data.GameRunning && data.GameInMenu && (!loadSessionStaticInfoFromWS)
                )
            {
                loadSessionStaticInfoFromWS = true;
                ReguiredVluesInited = false;
                //TireManagementJSONdataInited = false;
            }

            if (data.GameRunning && !data.GameInMenu && !data.GamePaused && !StopUpdate && (data.OldData != null))
            {
                //updateDataDelayCounter--;
                if (curGame == "LMU")   //TODO: check a record where the game was captured from startup on
                {

                    LMURepairAndRefuelData.IsInPit = data.OldData.IsInPit;
                    LMURepairAndRefuelData.CarClass = data.OldData.CarClass;
                    LMURepairAndRefuelData.CarModel = data.OldData.CarModel;
                    ReguiredVluesInited = true;
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

                            pluginManager.SetPropertyValue("Redadeg.lmu.fuelPerLast5Lap", this.GetType(), LMURepairAndRefuelData.fuelPerLast5Lap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.fuelPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.fuelPerLast5ClearLap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.fuelPerLastLap", this.GetType(), LMURepairAndRefuelData.fuelPerLastLap);

                           // pluginManager.SetPropertyValue("Redadeg.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

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

                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenalty1", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType1);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenalty2", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType2);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenalty3", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType3);

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
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY_INT", this.GetType(), LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT);
                            pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_REAR_ANTISWAY_INT", this.GetType(), LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT);


                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateDamage", this.GetType(), LMURepairAndRefuelData.PitstopEstimateDamage);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateDriverSwap", this.GetType(), LMURepairAndRefuelData.PitstopEstimateDriverSwap);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateFuel", this.GetType(), LMURepairAndRefuelData.PitstopEstimateFuel);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateVE", this.GetType(), LMURepairAndRefuelData.PitstopEstimateVE);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimatePenalties", this.GetType(), LMURepairAndRefuelData.PitstopEstimatePenalties);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateTires", this.GetType(), LMURepairAndRefuelData.PitstopEstimateTires);
                            pluginManager.SetPropertyValue("Redadeg.lmu.PitstopEstimateTotal", this.GetType(), LMURepairAndRefuelData.PitstopEstimateTotal);


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



        private void setPropertiesInSimhub(PluginManager pluginManager)
        {
            try
            {
            }
            catch (Exception ex)
            {
                Logging.Current.Info("Plugin Redadeg.lmuDataPlugin Update parameters: " + ex.ToString());
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
                if (lmu_extended_connected) this.extendedBuffer.Disconnect();
                //  if (lmu_extended_connected) this.rulesBuffer.Disconnect();

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

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
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
        private void AddOrUpdateCircularList<T>(List<T> list, ref int index, T value)
        {
            if (list.Count < hystory_size)
            {
                list.Add(value);
                index = list.Count - 1;
            }
            else
            {
                index = (index + 1) % hystory_size;
                list[index] = value;
            }
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

                            //GetDataThreadEndWork wait end work, to avoid overlapping data requests
                            if (updateConsuptionDelayCounter < 0 && GetDataThreadEndWork)
                            {

                                //JObject SetupJSONdata = JObject.Parse(wc_calc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                                JObject TireManagementJSONdata = JObject.Parse(await FetchTireManagementJSONdata());

                                JObject expectedUsage = JObject.Parse(TireManagementJSONdata["expectedUsage"].ToString());

                                float fuelConsumption = expectedUsage["fuelConsumption"] != null ? (float)expectedUsage["fuelConsumption"] : 0;
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
                                LMURepairAndRefuelData.fuelPerLastLap = fuelConsumption;

                                AddOrUpdateCircularList(EnergyConsuptions, ref EnergyCurrentIndex, virtualEnergyConsumption);
                                AddOrUpdateCircularList(FuelConsuptions, ref EnergyCurrentIndex, fuelConsumption);

                                //if (EnergyConsuptions.Count < 5)
                                //{
                                //    energy_CurrentIndex++;
                                //    EnergyConsuptions.Add(virtualEnergyConsumption);
                                //}
                                //else if (EnergyConsuptions.Count == 5)
                                //{
                                //    energy_CurrentIndex++;
                                //    if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                //    EnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                //}

                                if (IsLapValid && !LapInvalidated && !OutFromPitFlag && !InToPitFlag && LMURepairAndRefuelData.IsInPit == 0)
                                {
                                    AddOrUpdateCircularList(LapTimes, ref ClearEnergyCurrentIndex, (float)lastLapTime);
                                    AddOrUpdateCircularList(ClearEnergyConsuptions, ref ClearEnergyCurrentIndex, virtualEnergyConsumption);
                                    AddOrUpdateCircularList(ClearFuelConsuptions, ref ClearEnergyCurrentIndex, fuelConsumption);




                                    //if (LapTimes.Count < 5)
                                    //{
                                    //    energy_CurrentIndex++;
                                    //    ClearEnergyConsuptions.Add(virtualEnergyConsumption);
                                    //    FuelConsuptions.Add(fuelConsumption);
                                    //    LapTimes.Add((float)lastLapTime);

                                    //}
                                    //else if (LapTimes.Count == 5)
                                    //{
                                    //    energy_CurrentIndex++;
                                    //    if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                    //    LapTimes[energy_CurrentIndex] = (float)lastLapTime;
                                    //    ClearEnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                    //    FuelConsuptions[energy_CurrentIndex] = fuelConsumption;
                                    //}
                                }
                                // Logging.Current.Info("Last Lap: " + lastLapTime.ToString() + " virtualEnergyConsumption: " + virtualEnergyConsumption.ToString() + " Raw: " + (expectedUsage["virtualEnergyConsumption"] != null ? (float)(double)expectedUsage["virtualEnergyConsumption"] : 0).ToString());
                                if (EnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = (float)EnergyConsuptions.Average();
                                    LMURepairAndRefuelData.fuelPerLast5Lap = (float)FuelConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = 0;
                                }

                                if (ClearEnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5ClearLap = (float)ClearEnergyConsuptions.Average();
                                    LMURepairAndRefuelData.fuelPerLast5ClearLap = (float)ClearFuelConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
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
                    await Task.Delay(ButtonBindSettings.DataUpdateThreadTimeout, ctsGetJSonDataThread.Token);
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
            //try
            //{
                Task.Delay(500, ctsGetJSonDataThread.Token).Wait();
                while (!IsEnded)
                {

                    if (GameRunning && !GameInMenu && !GamePaused && curGame == "LMU" && ReguiredVluesInited)
                    {
                        //if (updateDataDelayCounter < 0)
                        //{
                        GetDataThreadEndWork = false;
                        //try
                        //{

                    
                            if (loadSessionStaticInfoFromWS)
                            {
                                JObject SetupJSONdata = JObject.Parse(await FetchCarSetupOverviewJSONdata());
                            
                                JObject carSetup = JObject.Parse(SetupJSONdata["carSetup"].ToString());
                                JObject garageValues = JObject.Parse(carSetup["garageValues"].ToString());
                                //  JObject pitRecommendations = JObject.Parse(JSONdata["pitRecommendations"].ToString());
                                if (garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"].ToString().Length > 1)
                                {
                                    if ((garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"].ToString()).Contains("N/A"))
                                    {
                                        LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"].ToString().Trim();
                                    }
                                    else
                                    {
                                        LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"].ToString().Substring(0, 2).Trim();
                                    }
                                    
                                }
                                LMURepairAndRefuelData.VM_BRAKE_BALANCE = garageValues["VM_BRAKE_BALANCE"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_BRAKE_MIGRATION = garageValues["VM_BRAKE_MIGRATION"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = garageValues["VM_ENGINE_BRAKEMAP"]?["stringValue"].ToString();

                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = garageValues["VM_ELECTRIC_MOTOR_MAP"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = garageValues["VM_ENGINE_MIXTURE"]?["stringValue"].ToString();

                                LMURepairAndRefuelData.VM_REGEN_LEVEL = garageValues["VM_REGEN_LEVEL"]?["stringValue"].ToString();

                                LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = garageValues["VM_TRACTIONCONTROLMAP"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = garageValues["VM_TRACTIONCONTROLPOWERCUTMAP"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = garageValues["VM_TRACTIONCONTROLSLIPANGLEMAP"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_FRONT_ANTISWAY = garageValues["VM_FRONT_ANTISWAY"]?["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_REAR_ANTISWAY = garageValues["VM_REAR_ANTISWAY"]?["stringValue"].ToString();
                        //LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT];
                        //LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT];

                        loadSessionStaticInfoFromWS = false;
                                await Task.Delay(ButtonBindSettings.AntiFlickPitMenuTimeout, ctsGetJSonDataThread.Token);

                                
                            }

                            else
                            {
                                if (LMURepairAndRefuelData.mChangedParamType > -1)
                                {
                                    switch (LMURepairAndRefuelData.mChangedParamType)
                                    {
                                        case 3:
                                            LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = LMURepairAndRefuelData.mChangedParamValue.Trim();
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
                                                if (!LMURepairAndRefuelData.CarModel.Equals("Floyd Vanwall Racing Team") && LMURepairAndRefuelData.CarClass.Equals("Hyper"))
                                                {
                                                    LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT];
                                                    LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT];
                                                }
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
                                    //if (LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse 2024") || LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse"))
                                    //{ LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["F" + LMURepairAndRefuelData.mChangedParamValue]; }
                                    //else if (LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies 2024") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport 2024") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing 2024") || LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing"))
                                    //{ LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["P" + LMURepairAndRefuelData.mChangedParamValue]; }
                                    //else if (LMURepairAndRefuelData.CarModel.Equals("Glickenhaus Racing"))
                                    //{ LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["G" + LMURepairAndRefuelData.mChangedParamValue]; }
                                    //else
                                    //{ LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR[LMURepairAndRefuelData.mChangedParamValue]; }
                                        if (!LMURepairAndRefuelData.CarModel.Equals("Floyd Vanwall Racing Team") && LMURepairAndRefuelData.CarClass.Equals("Hyper"))
                                        {
                                            LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT];
                                            LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABRs[LMURepairAndRefuelData.CarModel][LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT];
                                        }

                                            break;
                                        default:
                                            // code block
                                            break;
                                    }
                                }
                               
                               
                            }
                        //}
                        //catch
                        //{

                        //}


                        try
                        {
                            // call LMMU Webservice and wait to calm down the api throttle and fix Menu Flickering
                            JObject RepairAndRefuelJSONdata = JObject.Parse(await FetchRepairAndRefuelJSONdata());
                            await Task.Delay(ButtonBindSettings.AntiFlickPitMenuTimeout, ctsGetJSonDataThread.Token);

                            JObject GameStateJSONdata = JObject.Parse(await FetchGetGameStateJSONdata());
                            await Task.Delay(ButtonBindSettings.AntiFlickPitMenuTimeout, ctsGetJSonDataThread.Token);

                            JObject TireMagagementJSONdata = JObject.Parse(await FetchTireManagementJSONdata());
                            await Task.Delay(ButtonBindSettings.AntiFlickPitMenuTimeout, ctsGetJSonDataThread.Token);

                            JObject PitstopEstimateJSONdata = JObject.Parse(await FetchPitstopEstimateJSONdata());
                            await Task.Delay(ButtonBindSettings.AntiFlickPitMenuTimeout, ctsGetJSonDataThread.Token);   

                            //TireManagementJSONdataInited = true;

                            JObject fuelInfo = JObject.Parse(RepairAndRefuelJSONdata["fuelInfo"].ToString());
                            JObject pitStopLength = JObject.Parse(RepairAndRefuelJSONdata["pitStopLength"].ToString());
                            JObject pitMenuJSONData = JObject.Parse(RepairAndRefuelJSONdata["pitMenu"].ToString()); 

                            JObject tireInventory = JObject.Parse(TireMagagementJSONdata["tireInventory"].ToString());

                            LMURepairAndRefuelData.maxAvailableTires = tireInventory["maxAvailableTires"] != null ? (int)tireInventory["maxAvailableTires"] : 0;
                            LMURepairAndRefuelData.newTires = tireInventory["newTires"] != null ? (int)tireInventory["newTires"] : 0;

                            LMURepairAndRefuelData.currentBattery = fuelInfo["currentBattery"] != null ? (int)fuelInfo["currentBattery"] : 0;
                            LMURepairAndRefuelData.currentFuel = fuelInfo["currentFuel"] != null ? (int)fuelInfo["currentFuel"] : 0;
                            LMURepairAndRefuelData.timeOfDay = GameStateJSONdata["timeOfDay"] != null ? (double)GameStateJSONdata["timeOfDay"] : 0;


                            LMURepairAndRefuelData.PitstopEstimateDamage = PitstopEstimateJSONdata["damage"] != null ? (float)PitstopEstimateJSONdata["damage"] : 0;
                            LMURepairAndRefuelData.PitstopEstimateDriverSwap = PitstopEstimateJSONdata["driverSwap"] != null ? (float)PitstopEstimateJSONdata["driverSwap"] : 0;
                            LMURepairAndRefuelData.PitstopEstimatePenalties = PitstopEstimateJSONdata["penalties"] != null ? (float)PitstopEstimateJSONdata["penalties"] : 0;
                            LMURepairAndRefuelData.PitstopEstimateFuel = PitstopEstimateJSONdata["fuel"] != null ? (float)Math.Round((float)PitstopEstimateJSONdata["fuel"],3) : 0;
                            LMURepairAndRefuelData.PitstopEstimateVE = PitstopEstimateJSONdata["ve"] != null ? (float)Math.Round((float)PitstopEstimateJSONdata["ve"] , 3): 0;
                            LMURepairAndRefuelData.PitstopEstimateTires = PitstopEstimateJSONdata["tires"] != null ? (float)PitstopEstimateJSONdata["tires"] : 0;
                            LMURepairAndRefuelData.PitstopEstimateTotal = PitstopEstimateJSONdata["total"] != null ? (float)Math.Round((float)PitstopEstimateJSONdata["total"],3) : 0;
                            

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
                                   
                                }

                                if ((int)PMCs["PMC Value"] == 1)
                                {
                                    if (idx == 0)
                                    {
                                        //                                       isStopAndGo = false;
                                        LMURepairAndRefuelData.passStopAndGo = "";
                                    }
                                    LMURepairAndRefuelData.RepairDamage = PMCs["settings"][(int)PMCs["currentSetting"]]["text"].ToString();

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
                    GetDataThreadEndWork = true;
                    await Task.Delay(ButtonBindSettings.DataUpdateThreadTimeout, ctsGetJSonDataThread.Token);
                }
            //}
            //catch (AggregateException)
            //{
            //    Logging.Current.Info(("AggregateException"));
            //}
            //catch (TaskCanceledException)
            //{
            //    Logging.Current.Info(("TaskCanceledException"));
            //}
        }

        private async void lmu_extendedReadThread()
        {
            try
            {
                Task.Delay(500, cts.Token).Wait();

                while (!IsEnded)
                {
                    if (GameRunning && curGame == "LMU" && ReguiredVluesInited)
                    {
                        if (!this.lmu_extended_connected)
                        {
                            try
                            {
                                // Extended buffer is the last one constructed, so it is an indicator RF2SM is ready.
                                this.extendedBuffer.Connect();
                                // this.rulesBuffer.Connect();

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
                                LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT = 0;
                                LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT = 0;
                                this.lmu_extended_connected = false;
                                // Logging.Current.Info("Extended data update service not connectded.");
                            }
                        }
                        else
                        {
                            extendedBuffer.GetMappedData(ref lmu_extended);
                            LMURepairAndRefuelData.Cuts = lmu_extended.mCuts;
                            LMURepairAndRefuelData.CutsMax = lmu_extended.mCutsPoints;
                            LMURepairAndRefuelData.PenaltyLeftLaps = lmu_extended.mPenaltyLeftLaps;
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
                            LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT = lmu_extended.mFront_ABR;
                            LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT = lmu_extended.mRear_ABR;



                            // Logging.Current.Info(("Extended data update service connectded. " +  lmu_extended.mCutsPoints.ToString() + " Penalty laps" + lmu_extended.mPenaltyLeftLaps).ToString());
                        }
                    }
                    else  //game not runned clead stream values and try disconnect
                    {
                        try
                        {
                            //try disconnect if game not runned
                            this.extendedBuffer.Disconnect(); 
                        }
                        catch 
                        { }
                      

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
                        LMURepairAndRefuelData.VM_FRONT_ANTISWAY_INT = 0;
                        LMURepairAndRefuelData.VM_REAR_ANTISWAY_INT = 0;
                        this.lmu_extended_connected = false;
                    }

                    // if we are connected we wait a short time before read again the memory
                    if (this.lmu_extended_connected)
                    {
                        await Task.Delay(ButtonBindSettings.GetMemoryDataThreadTimeout, cts.Token);
                    }
                    // if we are not connected we wait 5 secondes before attempted a new connection 
                    else
                    {
                        await Task.Delay(1000, cts.Token);
                    }

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

        private async Task<string> FetchPitstopEstimateJSONdata()
        {
            try
            {
                var urlPitstopEstimate = "http://localhost:6397/rest/strategy/pitstop-estimate";
                var responsePitstopEstimate = await _httpClient.GetStringAsync(urlPitstopEstimate);
                return responsePitstopEstimate;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Failed to fetch Pitstop Estimate data: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
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
            List<rF2VehicleScoring> playersVehScoring = new List<rF2VehicleScoring>();
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
                   new JProperty("DataUpdateThreadTimeout", ButtonBindSettings.DataUpdateThreadTimeout),
                   new JProperty("AntiFlickPitMenuTimeout", ButtonBindSettings.AntiFlickPitMenuTimeout));
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

            _httpClient = new HttpClient();
            LapTimes = new List<float>();
            EnergyConsuptions = new List<float>();
            ClearEnergyConsuptions = new List<float>();
            FuelConsuptions = new List<float>();
            // set path/filename for settings file
            LMURepairAndRefuelData.path = PluginManager.GetCommonStoragePath(PLUGIN_CONFIG_FILENAME);
            //string path_data = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.data.json");
            //List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
            // try to read settings file
            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
                ButtonBindSettings.AntiFlickPitMenuTimeout = JSONSettingsdata["AntiFlickPitMenuTimeout"] != null ? (int)JSONSettingsdata["AntiFlickPitMenuTimeout"] : 10;
            }
            catch { }



            LoadSettings(pluginManager);
            lmu_extendedThread = new Thread(lmu_extendedReadThread);
            lmu_extendedThread.Name = "GetJSonDataThread";
            lmu_extendedThread.Start();

            lmuGetJSonDataThread = new Thread(lmu_GetJSonDataThread);
            lmuGetJSonDataThread.Name = "ExtendedDataUpdateThread";
            lmuGetJSonDataThread.Start();


            lmuCalculateConsumptionsThread = new Thread(lmu_CalculateConsumptionsThread);
            lmuCalculateConsumptionsThread.Name = "CalculateConsumptionsThread";
            lmuCalculateConsumptionsThread.Start();

            //***** Init Properties and Data SimHUB
            addPropertyToSimHUB(pluginManager);
            initFrontABRDict();
            initBackABRDict();
        }

        private void addPropertyToSimHUB(PluginManager pluginManager)
        {
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.energyPerLast5ClearLap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
            pluginManager.AddProperty("Redadeg.lmu.fuelPerLast5Lap", this.GetType(), LMURepairAndRefuelData.fuelPerLast5Lap);
            pluginManager.AddProperty("Redadeg.lmu.fuelPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.fuelPerLast5ClearLap);
            pluginManager.AddProperty("Redadeg.lmu.fuelPerLastLap", this.GetType(), LMURepairAndRefuelData.fuelPerLastLap);

            //pluginManager.AddProperty("Redadeg.lmu.energyPerLastLapRealTime", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.energyLapsRealTimeElapsed", this.GetType(), 0);

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

            pluginManager.AddProperty("Redadeg.lmu.Extended.Cuts", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.CutsMax", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyLeftLaps", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyType", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyCount", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenalty1", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenalty2", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenalty3", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.MotorMap", this.GetType(), "None");
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamType", this.GetType(), -1);
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), "None");


            pluginManager.AddProperty("Redadeg.lmu.mMessage", this.GetType(), "");

            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ANTILOCKBRAKESYSTEMMAP", this.GetType(), "");
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
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY_INT", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_REAR_ANTISWAY_INT", this.GetType(), 0);

            //Estimate pitsstop time in seconds
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateDamage", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateDriverSwap", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateFuel", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateVE", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimatePenalties", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateTires", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.PitstopEstimateTotal", this.GetType(), 0);


            //{ "damage":133.99281311035156,"driverSwap":0.0,"fuel":16.07479476928711,"penalties":0.0,"tires":0.0,"total":160.39280700683594,"ve":26.399999618530273}
        }

        private void initFrontABRDict()
        {
            frontABRs = new Dictionary<string, List<string>>();
            try
            {


                //BMW 2024
                List<string> BMWFABRs = new List<string>();
                BMWFABRs.Add("Detached");
                BMWFABRs.Add("P1");
                BMWFABRs.Add("P2");
                BMWFABRs.Add("P3");
                BMWFABRs.Add("P4");
                BMWFABRs.Add("P5");
                frontABRs.Add("BMW M Team WRT 2024", BMWFABRs);

                //alpine 2024
                List<string> alipineFABRs = new List<string>();
                alipineFABRs.Add("Detached");
                alipineFABRs.Add("P1");
                alipineFABRs.Add("P2");
                alipineFABRs.Add("P3");
                alipineFABRs.Add("P4");
                alipineFABRs.Add("P5");
                alipineFABRs.Add("P6");
                alipineFABRs.Add("P7");
                alipineFABRs.Add("P8");
                alipineFABRs.Add("P9");
                alipineFABRs.Add("P10");
                alipineFABRs.Add("P11");
                alipineFABRs.Add("P12");
                alipineFABRs.Add("P13");
                alipineFABRs.Add("P14");

                frontABRs.Add("Alpine Endurance Team 2024", alipineFABRs);

                //Lambo 2024
                List<string> lamboFABRs = new List<string>();
                lamboFABRs.Add("Detached");
                lamboFABRs.Add("14.5-TK 0deg");
                lamboFABRs.Add("14.5-TK 30deg");
                lamboFABRs.Add("14.5-TK 45deg");
                lamboFABRs.Add("14.5-TK 60deg");
                lamboFABRs.Add("14.5-TK 90deg");
                lamboFABRs.Add("16-TK 0deg");
                lamboFABRs.Add("16-TK 30deg");
                lamboFABRs.Add("16-TK 45deg");
                lamboFABRs.Add("16-TK 60deg");
                lamboFABRs.Add("16-TK 90deg");
                lamboFABRs.Add("17.5-TK 0deg");
                lamboFABRs.Add("17.5-TK 30deg");
                lamboFABRs.Add("17.5-TK 45deg");
                lamboFABRs.Add("17.5-TK 60deg");
                lamboFABRs.Add("17.5-TK 90deg");
                lamboFABRs.Add("20.5-TK 0deg");
                lamboFABRs.Add("20.5-TK 30deg");
                lamboFABRs.Add("20.5-TK 45deg");
                lamboFABRs.Add("20.5-TK 60deg");
                lamboFABRs.Add("20.5-TK 90deg");

                frontABRs.Add("Lamborghini Iron Lynx 2024", lamboFABRs);


                //Cadillac 2024
                List<string> CadillacFABRs = new List<string>();
                CadillacFABRs.Add("Detached");
                CadillacFABRs.Add("P1");
                CadillacFABRs.Add("P2");
                CadillacFABRs.Add("P3");
                CadillacFABRs.Add("P4");
                CadillacFABRs.Add("P5");
                frontABRs.Add("Cadillac Racing 2024", CadillacFABRs);
                frontABRs.Add("Cadillac Racing", CadillacFABRs);

                //Ferrary 2024
                List<string> FerraryFABRs = new List<string>();
                FerraryFABRs.Add("Detached");
                FerraryFABRs.Add("A-P1");
                FerraryFABRs.Add("A-P2");
                FerraryFABRs.Add("A-P3");
                FerraryFABRs.Add("A-P4");
                FerraryFABRs.Add("A-P5");

                FerraryFABRs.Add("B-P1");
                FerraryFABRs.Add("B-P2");
                FerraryFABRs.Add("B-P3");
                FerraryFABRs.Add("B-P4");
                FerraryFABRs.Add("B-P5");

                FerraryFABRs.Add("C-P1");
                FerraryFABRs.Add("C-P2");
                FerraryFABRs.Add("C-P3");
                FerraryFABRs.Add("C-P4");
                FerraryFABRs.Add("C-P5");

                FerraryFABRs.Add("D-P1");
                FerraryFABRs.Add("D-P2");
                FerraryFABRs.Add("D-P3");
                FerraryFABRs.Add("D-P4");
                FerraryFABRs.Add("D-P5");

                FerraryFABRs.Add("E-P1");
                FerraryFABRs.Add("E-P2");
                FerraryFABRs.Add("E-P3");
                FerraryFABRs.Add("E-P4");
                FerraryFABRs.Add("E-P5");
                frontABRs.Add("Ferrari AF Corse 2024", FerraryFABRs);
                frontABRs.Add("Ferrari AF Corse", FerraryFABRs);

                //Porsche, Pegeout,Glickenhaus
                List<string> PorscheFABRs = new List<string>();
                PorscheFABRs.Add("Detached");
                PorscheFABRs.Add("P1");
                PorscheFABRs.Add("P2");
                PorscheFABRs.Add("P3");
                PorscheFABRs.Add("P4");
                PorscheFABRs.Add("P5");

                PorscheFABRs.Add("P6");
                PorscheFABRs.Add("P7");
                PorscheFABRs.Add("P8");
                PorscheFABRs.Add("P9");
                PorscheFABRs.Add("P10");

                PorscheFABRs.Add("P11");
                PorscheFABRs.Add("P12");
                PorscheFABRs.Add("P13");
                PorscheFABRs.Add("P14");
                PorscheFABRs.Add("P15");
                frontABRs.Add("Porsche Penske Motorsport 2024", PorscheFABRs);
                frontABRs.Add("Porsche Penske Motorsport", PorscheFABRs);
                frontABRs.Add("Peugeot TotalEnergies 2024", PorscheFABRs);
                frontABRs.Add("Peugeot TotalEnergies", PorscheFABRs);
                frontABRs.Add("Toyota Gazoo Racing 2024", PorscheFABRs);
                frontABRs.Add("Toyota Gazoo Racing", PorscheFABRs);
                frontABRs.Add("Glickenhaus Racing", PorscheFABRs);


                //Isotta TIPO6 2024
                List<string> IsottaFABRs = new List<string>();
                IsottaFABRs.Add("Detached");
                IsottaFABRs.Add("P1");
                IsottaFABRs.Add("P2");
                IsottaFABRs.Add("P3");
                IsottaFABRs.Add("P4");
                IsottaFABRs.Add("P5");
                IsottaFABRs.Add("P6");
                IsottaFABRs.Add("P7");
                frontABRs.Add("Isotta TIPO6 2024", IsottaFABRs);

            }
            catch { }
        }

        private void initBackABRDict()
        {
            rearABRs = new Dictionary<string, List<string>>();
            try
            {
                //BMW 2024
                List<string> BMWRABRs = new List<string>();
                BMWRABRs.Add("Detached");
                BMWRABRs.Add("P1");
                BMWRABRs.Add("P2");
                BMWRABRs.Add("P3");
                BMWRABRs.Add("P4");
                BMWRABRs.Add("P5");
                rearABRs.Add("BMW M Team WRT 2024", BMWRABRs);

                //alpine 2024
                List<string> alipineRABRs = new List<string>();
                alipineRABRs.Add("Detached");
                alipineRABRs.Add("P1");
                alipineRABRs.Add("P2");
                alipineRABRs.Add("P3");
                alipineRABRs.Add("P4");
                alipineRABRs.Add("P5");
                alipineRABRs.Add("P6");
                alipineRABRs.Add("P7");
                alipineRABRs.Add("P8");
                alipineRABRs.Add("P9");
                alipineRABRs.Add("P10");
                alipineRABRs.Add("P11");
                alipineRABRs.Add("P12");

                rearABRs.Add("Alpine Endurance Team 2024", alipineRABRs);

                //Lambo 2024
                List<string> lamboRABRs = new List<string>();
                lamboRABRs.Add("Detached");
                lamboRABRs.Add("14.5-TN 0deg");
                lamboRABRs.Add("14.5-TN 30deg");
                lamboRABRs.Add("14.5-TN 60deg");
                lamboRABRs.Add("14.5-TN 90deg");
                lamboRABRs.Add("16-TK 0deg");

                lamboRABRs.Add("16-TK 30deg");
                lamboRABRs.Add("16-TK 60deg");
                lamboRABRs.Add("16-TK 90deg");

                lamboRABRs.Add("17.5-TK 0deg");
                lamboRABRs.Add("17.5-TK 30deg");
                lamboRABRs.Add("17.5-TK 60deg");
                lamboRABRs.Add("17.5-TK 90deg");
                lamboRABRs.Add("20.5-TK 0deg");
                lamboRABRs.Add("20.5-TK 30deg");
                lamboRABRs.Add("20.5-TK 60deg");
                lamboRABRs.Add("20.5-TK 90deg");

                rearABRs.Add("Lamborghini Iron Lynx 2024", lamboRABRs);

                //Cadillac 2024
                List<string> CadillacRABRs = new List<string>();
                CadillacRABRs.Add("Detached");
                CadillacRABRs.Add("P1");
                CadillacRABRs.Add("P2");
                CadillacRABRs.Add("P3");
                CadillacRABRs.Add("P4");
                CadillacRABRs.Add("P5");
                rearABRs.Add("Cadillac Racing 2024", CadillacRABRs);
                rearABRs.Add("Cadillac Racing", CadillacRABRs);


                //Ferrary 2024
                List<string> FerraryRABRs = new List<string>();
                FerraryRABRs.Add("Detached");
                FerraryRABRs.Add("A-P1");
                FerraryRABRs.Add("A-P2");
                FerraryRABRs.Add("A-P3");
                FerraryRABRs.Add("A-P4");
                FerraryRABRs.Add("A-P5");

                FerraryRABRs.Add("B-P1");
                FerraryRABRs.Add("B-P2");
                FerraryRABRs.Add("B-P3");
                FerraryRABRs.Add("B-P4");
                FerraryRABRs.Add("B-P5");

                FerraryRABRs.Add("C-P1");
                FerraryRABRs.Add("C-P2");
                FerraryRABRs.Add("C-P3");
                FerraryRABRs.Add("C-P4");
                FerraryRABRs.Add("C-P5");

                FerraryRABRs.Add("D-P1");
                FerraryRABRs.Add("D-P2");
                FerraryRABRs.Add("D-P3");
                FerraryRABRs.Add("D-P4");
                FerraryRABRs.Add("D-P5");

                FerraryRABRs.Add("E-P1");
                FerraryRABRs.Add("E-P2");
                FerraryRABRs.Add("E-P3");
                FerraryRABRs.Add("E-P4");
                FerraryRABRs.Add("E-P5");
                rearABRs.Add("Ferrari AF Corse 2024", FerraryRABRs);
                rearABRs.Add("Ferrari AF Corse", FerraryRABRs);

                //Porsche, Pegeout,Glickenhaus
                List<string> PorscheRABRs = new List<string>();
                PorscheRABRs.Add("Detached");
                PorscheRABRs.Add("P1");
                PorscheRABRs.Add("P2");
                PorscheRABRs.Add("P3");
                PorscheRABRs.Add("P4");
                PorscheRABRs.Add("P5");

                PorscheRABRs.Add("P6");
                PorscheRABRs.Add("P7");
                PorscheRABRs.Add("P8");
                PorscheRABRs.Add("P9");
                PorscheRABRs.Add("P10");

                PorscheRABRs.Add("P11");
                PorscheRABRs.Add("P12");
                PorscheRABRs.Add("P13");
                PorscheRABRs.Add("P14");
                PorscheRABRs.Add("P15");
                rearABRs.Add("Porsche Penske Motorsport 2024", PorscheRABRs);
                rearABRs.Add("Porsche Penske Motorsport", PorscheRABRs);
                rearABRs.Add("Peugeot TotalEnergies 2024", PorscheRABRs);
                rearABRs.Add("Peugeot TotalEnergies", PorscheRABRs);
                rearABRs.Add("Toyota Gazoo Racing 2024", PorscheRABRs);
                rearABRs.Add("Toyota Gazoo Racing", PorscheRABRs);
                rearABRs.Add("Glickenhaus Racing", PorscheRABRs);

                //Isotta TIPO6 2024
                List<string> IsottaRABRs = new List<string>();
                IsottaRABRs.Add("Detached");
                IsottaRABRs.Add("P1");
                IsottaRABRs.Add("P2");
                IsottaRABRs.Add("P3");
                IsottaRABRs.Add("P4");
                IsottaRABRs.Add("P5");
                IsottaRABRs.Add("P6");
                IsottaRABRs.Add("P7");
                rearABRs.Add("Isotta TIPO6 2024", IsottaRABRs);
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
            public static float fuelPerLastLap { get; set; }
            public static float energyPerLast5Lap { get; set; }
        public static float fuelPerLast5Lap { get; set; }
        public static float energyPerLast5ClearLap { get; set; }
        public static float fuelPerLast5ClearLap { get; set; }
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
            public static int VM_REAR_ANTISWAY_INT { get; set; }
            public static int VM_FRONT_ANTISWAY_INT { get; set; }
            public static string CarClass { get; set; }
            public static string CarModel { get; set; }
            public static string SessionTypeName { get; set; }
            public static int IsInPit { get; set; }
            public static float PitstopEstimateDamage { get; set; }
            public static float PitstopEstimateDriverSwap { get; set; }
            public static float PitstopEstimateFuel { get; set; }
            public static float PitstopEstimateVE { get; set; }
            public static float PitstopEstimatePenalties { get; set; }
            public static float PitstopEstimateTires { get; set; }
            public static float PitstopEstimateTotal { get; set; }


    }

    }


