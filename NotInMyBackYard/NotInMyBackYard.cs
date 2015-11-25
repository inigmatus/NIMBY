﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using System.Reflection;

namespace NotInMyBackYard
{
    public class NotInMyBackYard : MonoBehaviour
    {
        protected ScreenSafeUIButton.ButtonPressCallback originalCallback;

        public List<Beacon> beacons = new List<Beacon>();

        public void Start()
        {
            //initialize beacons
            LoadBeacons(KSPUtil.ApplicationRootPath+"/GameData/NIMBY/Beacons.cfg", true);
        }

        public void LoadBeacons(string beaconFile, bool createIfNotExists = false)
        {
            beacons.Clear();
            if (System.IO.File.Exists(beaconFile))
            {
                ConfigNode beaconsNode = ConfigNode.Load(beaconFile);
                foreach (ConfigNode beacon in beaconsNode.GetNodes("Beacon"))
                {
                    beacons.Add(new Beacon(beacon));
                }
            }
            else if (createIfNotExists)
            {
                //Set the defaults and save the file
                Beacon KSC = new Beacon("KSC", SpaceCenter.Instance.Latitude, SpaceCenter.Instance.Longitude, 100000);
                beacons.Add(KSC);

                ConfigNode beaconsNode = new ConfigNode("Beacons");
                beaconsNode.AddNode(KSC.AsNode());

                beaconsNode.Save(beaconFile);
            }
        }

        protected void NewRecoveryFunction(Vessel vessel)
        {
            Debug.Log("!!Our recovery function is being called!");
            //check the distance to the KSC, if within 100km then recover, else pop up a message

            //We might be able to (temporarily) change the location of the KSC to the closest Beacon, which would also change the amount of funds we recover due to distance

            Beacon closestBeacon = null; //Used for if we're not in range of any beacons
            double shortestDistance = double.PositiveInfinity;

            foreach (Beacon beacon in beacons)
            {
                double distance = beacon.GreatCircleDistance(vessel);
                if (distance < beacon.range)
                {
                    originalCallback.Invoke();
                    return;
                }
                else
                {
                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        closestBeacon = beacon;
                    }
                }
            }

            //No beacons in range
            //popup "error"
            Debug.Log("!!Too far to recover!");

            PopupDialog.SpawnPopupDialog("Vessel Too Far", "Vessel is too far from any Recovery Beacons to recover. Closest Recovery Beacon is "+closestBeacon.name+" and is "+(shortestDistance/1000).ToString("N2")+"km away.", "OK", false, HighLogic.Skin);
        }
    }

    //Start in the Flight Scene only, every time it's loaded
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class NotInMyBackYard_Flight : NotInMyBackYard
    {
        //Called once when the scene starts
        new public void Start()
        {
            base.Start(); //Call Start() in the parent class
            //Get the MonoBehaviour that controls the buttons at the top of the screen
            AltimeterSliderButtons buttons = (AltimeterSliderButtons)FindObjectOfType(typeof(AltimeterSliderButtons));

            if (buttons != null)
            {
                //back up the original function. We'll call that when we're within range
                originalCallback = buttons.vesselRecoveryButton.OnPress;

                //Override the original function with ours, which checks the distance.
                buttons.vesselRecoveryButton.OnPress = new ScreenSafeUIButton.ButtonPressCallback(NewRecoveryFunctionFlight);
            }
        }

        private void NewRecoveryFunctionFlight()
        {
            NewRecoveryFunction(FlightGlobals.ActiveVessel);
        }
    }

    //Run in the Tracking Station, every time.
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class NotInMyBackYard_TrackingStation : NotInMyBackYard
    {
        //Called once when the scene starts
        new public void Start()
        {
            base.Start(); //Call Start() in the parent class
            //Get the MonoBehaviour that controls the buttons at the top of the screen
            SpaceTracking trackingStation = (SpaceTracking)FindObjectOfType(typeof(SpaceTracking));
            
            if (trackingStation != null)
            {
                //back up the original function. We'll call that when we're within range
                originalCallback = trackingStation.RecoverButton.OnPress;

                //Override the original function with ours, which checks the distance.
                trackingStation.RecoverButton.OnPress = new ScreenSafeUIButton.ButtonPressCallback(NewRecoveryFunctionTrackingStation);
            }
        }

        private void NewRecoveryFunctionTrackingStation()
        {
            Vessel selectedVessel = null;

            SpaceTracking trackingStation = (SpaceTracking)FindObjectOfType(typeof(SpaceTracking));

            foreach (FieldInfo f in trackingStation.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(Vessel))
                {
                    //FYI: the first one (0) is the currently selected vessel
                    //The second one (1) is the one that the mouse is hovering over
                    selectedVessel = f.GetValue(trackingStation) as Vessel;
                    break;
                }
            }

            if (selectedVessel == null)
            {
                Debug.Log("!!Error! No Vessel selected.");
                return;
            }

            NewRecoveryFunction(selectedVessel);
        }
    }

    //We should also consider the space center scene (you can recover there) except that's *probably* within range. Unless someone purposefully removes the KSC as a Beacon
}
