using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RealFuels.Tanks;

namespace CryoCooler
{
    public class ModuleCryoCooler : PartModule
    {
        static double lowestTankTemperature;
        static ModuleFuelTanks tankModule;
        static int noCatchup = 0;
        static FuelTankList tankList = new FuelTankList();
        readonly double maxCoolerPower = 5;  //kW
        readonly double maxEfficiency = 10;
            
        [KSPField(isPersistant = true, guiActive = true, guiName = "Cooler Enabled: ")]
        public bool isPoweredOn = true;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Cooler Efficiency: ", guiFormat = "F3")]
        public double coolerEfficiency = 1;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Cooler Xfer: ", guiFormat ="F3", guiUnits ="kW")]
        public double heatFlux = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public double lastTankTemperature = 0;
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (part.parent != null)
            {
                Debug.Log("[SCC] Starting CryoCooler " + Time.realtimeSinceStartup);
                tankModule = base.part.parent.FindModuleImplementing<RealFuels.Tanks.ModuleFuelTanks>();
                if (tankModule != null)
                {
                    tankList = GetInstanceField(tankModule, "tankList") as FuelTankList;
                    if (tankList != null)
                    {
                        CalculateLowestTankTemperature();
                        if (isPoweredOn && lastTankTemperature > 0)
                        {
                            noCatchup = 10;

                            //part.parent.skinTemperature = lowestTankTemperature;
                        }
                    }
                }
                else
                {
                    isPoweredOn = false;
                    Debug.Log("[SCC] Error: Not Attached to RF tank. ");
                }
            }

            // base.part.SetHighlightColor(Color.yellow);
        }
        

        public void FixedUpdate()
        { 
            if (tankModule != null && HighLogic.LoadedSceneIsFlight && part.parent != null)
            {
                if (part.parent.temperature > lowestTankTemperature - .1 && part.temperature < part.maxTemp * .9 && isPoweredOn)
                {

                    if (part.temperature > part.parent.temperature) //don't work as a generator
                    { 
                        double carnotEfficiency = part.parent.temperature / (part.temperature - part.parent.temperature);
                        coolerEfficiency = Math.Min(maxEfficiency,.2 * carnotEfficiency); // Very good for a small stirling
                    } else {
                        coolerEfficiency = maxEfficiency; 
                    }
                    if (noCatchup > 0) {
                        part.parent.temperature = lastTankTemperature;
                        noCatchup--;
                    }
                    double powerRequested = Math.Max(0,Math.Min(maxCoolerPower, maxCoolerPower * (1 + (part.parent.temperature - lowestTankTemperature) * 10 )));  //throttle to 0 at -.1 target
                    double energyUsed = part.RequestResource("ElectricCharge", TimeWarp.fixedDeltaTime * powerRequested, ResourceFlowMode.ALL_VESSEL);
                   
                    heatFlux = energyUsed * coolerEfficiency / TimeWarp.fixedDeltaTime;
                    part.parent.AddThermalFlux(-heatFlux);
                    part.AddThermalFlux(energyUsed / TimeWarp.fixedDeltaTime + heatFlux);
                    lastTankTemperature = part.parent.temperature;

                }
            }
        }
        private void CalculateLowestTankTemperature()
        {
            lowestTankTemperature = 500;
            for (int i = tankList.Count - 1; i >= 0; --i)
            {
                RealFuels.Tanks.FuelTank tank = tankList[i];
                if (tank.maxAmount > 0d && (tank.vsp > 0.0 || tank.loss_rate > 0d))
                {
                    lowestTankTemperature = Math.Min(lowestTankTemperature, tank.temperature);
                }
            }
        }
        [KSPEvent(guiActive = true, guiName = "Toggle Cooling")]
        public void CoolingToggle()
        {
            isPoweredOn = !isPoweredOn;
        }

        internal static object GetInstanceField(object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            
            FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
    }
}