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
        static FuelTankList tankList = new FuelTankList();
        static bool hasCryoFuels = false;
        readonly double maxCoolerPower = 5;  //kW
        readonly double maxEfficiency = 10;
            
        [KSPField(isPersistant = true, guiActive = true, guiName = "Cooler Enabled: ")]
        public bool powerState = true;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Cooler Efficiency: ", guiFormat = "F3")]
        public double coolerEfficiency = 1;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Cooler Xfer: ", guiFormat ="F3", guiUnits ="kW")]
        public double heatFlux = 0;

        public void Start()
        {
            Debug.Log("[SCC] Starting CryoCooler " + Time.realtimeSinceStartup);
 //           electricResourceIndex = this.resHandler.inputResources.FindIndex(x => x.name == "ElectricCharge");
            tankModule = base.part.parent.FindModuleImplementing<RealFuels.Tanks.ModuleFuelTanks>();
            if (tankModule != null)
            {
                tankList = GetInstanceField(tankModule, "tankList") as FuelTankList;
                if (tankList != null)
                {
                    hasCryoFuels = CalculateLowestTankTemperature();
                    powerState = true;
                }
            }
            else
            {
                powerState = false;
                Debug.Log("[SCC] Error: Not Attached to RF tank. ");
            }

           // base.part.SetHighlightColor(Color.yellow);
        }

        public void FixedUpdate()
        {
            //base.FixedUpdate();
            if (tankModule != null)
            {
                if (hasCryoFuels)
                {
                    if (part.parent.temperature > lowestTankTemperature - .1 && part.temperature < part.maxTemp * .9 && powerState)
                    {

                        if (part.temperature > part.parent.temperature) //don't work as a generator
                        { 
                            double carnotEfficiency = part.parent.temperature / (part.temperature - part.parent.temperature);
                            coolerEfficiency = Math.Min(maxEfficiency,.2 * carnotEfficiency); // Very good for a small stirling
                        } else {
                            coolerEfficiency = maxEfficiency; 
                        }

                        double powerRequested = Math.Max(0,Math.Min(maxCoolerPower, maxCoolerPower * (1 + (part.parent.temperature - lowestTankTemperature) * 10 )));  //throttle to 0 at -.1 target
                        double energyUsed = part.RequestResource("ElectricCharge", TimeWarp.fixedDeltaTime * powerRequested, ResourceFlowMode.ALL_VESSEL);
                   
                        heatFlux = energyUsed * coolerEfficiency / TimeWarp.fixedDeltaTime;
                        part.parent.AddThermalFlux(-heatFlux);
                        part.AddThermalFlux(energyUsed / TimeWarp.fixedDeltaTime + heatFlux);
                        //part.parent.temperature = lowestTankTemperature - .1;
                        //part.parent.skinTemperature = lowestTankTemperature;
                        
                    }
                }
            }
        }
        private bool CalculateLowestTankTemperature()
        {
            bool result = false;
            lowestTankTemperature = 300;
            for (int i = tankList.Count - 1; i >= 0; --i)
            {
                RealFuels.Tanks.FuelTank tank = tankList[i];
                if (tank.maxAmount > 0d && (tank.vsp > 0.0 || tank.loss_rate > 0d))
                {
                    lowestTankTemperature = Math.Min(lowestTankTemperature, tank.temperature);
                    result = true;
                }
            }
            return result;
        }
        [KSPEvent(guiActive = true, guiName = "Toggle Cooling")]
        public void CoolingToggle()
        {
            powerState = !powerState;
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