using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redadeg.lmuDataPlugin
{


    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class FrontLeft
    {
        public double centerTemperature { get; set; }
        public double leftTemperature { get; set; }
        public double load { get; set; }
        public double pressure { get; set; }
        public double rightTemperature { get; set; }
    }

    public class FrontRight
    {
        public double centerTemperature { get; set; }
        public double leftTemperature { get; set; }
        public double load { get; set; }
        public double pressure { get; set; }
        public double rightTemperature { get; set; }
    }

    public class RearLeft
    {
        public double centerTemperature { get; set; }
        public double leftTemperature { get; set; }
        public double load { get; set; }
        public double pressure { get; set; }
        public double rightTemperature { get; set; }
    }

    public class RearRight
    {
        public double centerTemperature { get; set; }
        public double leftTemperature { get; set; }
        public double load { get; set; }
        public double pressure { get; set; }
        public double rightTemperature { get; set; }
    }

    public class TireInfoClass
    {
        public FrontLeft frontLeft { get; set; }
        public FrontRight frontRight { get; set; }
        public RearLeft rearLeft { get; set; }
        public RearRight rearRight { get; set; }
        public string unitSystem { get; set; }
    }


    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class LiveStandingData
    {
        public double bestIndividualTimeSector1 { get; set; }
        public double bestIndividualTimeSector2 { get; set; }
        public double bestLapTime { get; set; }
        public double bestLapTimeS1 { get; set; }
        public double bestLapTimeS2 { get; set; }
        public List<string> compoundNames { get; set; }
        public string countLapFlag { get; set; }
        public double currentLapTimeS1 { get; set; }
        public double currentLapTimeS2 { get; set; }
        public int currentLapsCompleted { get; set; }
        public int currentSector { get; set; }
        public int dlcAppID { get; set; }
        public string driverName { get; set; }
        public string finishStatus { get; set; }
        public string gapToClassLeader { get; set; }
        public string gapToRelativeTxt { get; set; }
        public bool inGarage { get; set; }
        public bool inPits { get; set; }
        public double lapDist { get; set; }
        public int lapsBehindLeader { get; set; }
        public int lapsBehindNext { get; set; }
        public double lastLapTime { get; set; }
        public double lastLapTimeS1 { get; set; }
        public double lastLapTimeS2 { get; set; }
        public double localVelX { get; set; }
        public double localVelY { get; set; }
        public double localVelZ { get; set; }
        public int numPitstops { get; set; }
        public Penalties penalties { get; set; }
        public int pitState { get; set; }
        public bool player { get; set; }
        public double posX { get; set; }
        public double posY { get; set; }
        public double posZ { get; set; }
        public int position { get; set; }
        public int qualiPosition { get; set; }
        public bool slotHasFocus { get; set; }
        public int slotID { get; set; }
        public string steamID { get; set; }
        public string teamName { get; set; }
        public double timeBehindLeader { get; set; }
        public double timeBehindNext { get; set; }
        public double timeIntoLap { get; set; }
        public string tireCompoundNameFront { get; set; }
        public string tireCompoundNameRear { get; set; }
        public double totalRaceTime { get; set; }
        public string vehFilename { get; set; }
        public bool vehOwned { get; set; }
        public string vehicleClass { get; set; }
        public string vehicleName { get; set; }
        public string vehicleNumber { get; set; }
        public double virtualEnergy { get; set; }
    }

    public class Penalties
    {
        public int DT { get; set; }
        public int SG { get; set; }
    }

    public class StandingDataRoot
    {
        public string messageType { get; set; }
        public string topic { get; set; }
        public List<LiveStandingData> LiveStandingData { get; set; }
    }



    // Root myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(myJsonResponse);
    public class AttackMode
    {
        public int remainingCount { get; set; }
        public int timeRemaining { get; set; }
        public int totalCount { get; set; }
    }

    public class CarAcceleration
    {
        public double velocity { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class CarPosition
    {
        public int type { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class CarVelocity
    {
        public double velocity { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class StandingData
    {
        public AttackMode attackMode { get; set; }
        public double bestLapSectorTime1 { get; set; }
        public double bestLapSectorTime2 { get; set; }
        public double bestLapTime { get; set; }
        public double bestSectorTime1 { get; set; }
        public double bestSectorTime2 { get; set; }
        public CarAcceleration carAcceleration { get; set; }
        public string carClass { get; set; }
        public string carId { get; set; }
        public string carNumber { get; set; }
        public CarPosition carPosition { get; set; }
        public CarVelocity carVelocity { get; set; }
        public string countLapFlag { get; set; }
        public double currentSectorTime1 { get; set; }
        public double currentSectorTime2 { get; set; }
        public string driverName { get; set; }
        public bool drsActive { get; set; }
        public double estimatedLapTime { get; set; }
        public string finishStatus { get; set; }
        public string flag { get; set; }
        public bool focus { get; set; }
        public double fuelFraction { get; set; }
        public string fullTeamName { get; set; }
        public string gamePhase { get; set; }
        public bool hasFocus { get; set; }
        public bool headlights { get; set; }
        public int inControl { get; set; }
        public bool inGarageStall { get; set; }
        public double lapDistance { get; set; }
        public double lapStartET { get; set; }
        public double lapsBehindLeader { get; set; }
        public double lapsBehindNext { get; set; }
        public int lapsCompleted { get; set; }
        public double lastLapTime { get; set; }
        public double lastSectorTime1 { get; set; }
        public double lastSectorTime2 { get; set; }
        public double pathLateral { get; set; }
        public int penalties { get; set; }
        public string pitGroup { get; set; }
        public double pitLapDistance { get; set; }
        public string pitState { get; set; }
        public int pitstops { get; set; }
        public bool pitting { get; set; }
        public bool player { get; set; }
        public int position { get; set; }
        public int qualification { get; set; }
        public string sector { get; set; }
        public bool serverScored { get; set; }
        public int slotID { get; set; }
        public int steamID { get; set; }
        public double timeBehindLeader { get; set; }
        public double timeBehindNext { get; set; }
        public double timeIntoLap { get; set; }
        public double trackEdge { get; set; }
        public bool underYellow { get; set; }
        public string upgradePack { get; set; }
        public string vehicleFilename { get; set; }
        public string vehicleName { get; set; }
    }

}
