
// Do we record if a navigation button has been pressed
//   we don't do anything with this, so for now just ignore related code for now
//#define NAVIGATION_KEYS

// Do we know when the same button has been pressed multiple times in a row
//   we don't do anything with this information, so just ignore related code for now
//#define DUPLICATE_KEYS


using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using MediaPortal.Common.Utils;
using MediaPortal.GUI.Library;
using MediaPortal.InputDevices;
using MediaPortal.Configuration;
using REMOTESERVICELib;
using System.IO;
[assembly: CompatibleVersion("1.2.0.0","1.1.3.0")]
[assembly: UsesSubsystem("MP.SkinEngine")]
[assembly: UsesSubsystem("MP.Config")]
[assembly: UsesSubsystem("MP.Input")]
[assembly: UsesSubsystem("MP.Input.Mapping")]

namespace AVerRMKV
{
    /// <summary>
    /// AVerMedia AVerRemote RM-KV remote control plugiin (with service AverRemote.exe 1.0.1.4)
    /// </summary>
    [PluginIcons("AVerRMKV.iconoon.png", "AVerRMKV.iconooff.png")]
    public class AVerRemote : ISetupForm, IPlugin
    {
        #region Class Variables

        const string        mappingfile             = "AVerRMKV";          // XML file containing button to action mappings
        private RemoteClass rc;
        private InputHandler inputhandler;
        private double      interKeyDelay           = 200;              // inter-key delay in ms
        private DateTime    lastTimeActionPerformed;                    // records the time an action was performed
        private DateTime    lastTimeButtonPushed;                       // records the time a button was pushed
        private uint        lastnKeyFunPressed      = 0x00000000;
        #if DUPLICATE_KEYS
        private int         sameButtonPushedCount   = 0;                // tracks the number of times the same button was pushed in a row
        #endif // DUPLICATE_KEYS

        #endregion // Class Variables

        #region Constructor

        public AVerRemote()
        {
            // do nothing
        }

        #endregion // Constructor

        #region Callback Function

        public void ReceiveData(uint nKeyFun, uint nKey, uint dwKeyCode)
        {
            TimeSpan timeSinceActionTaken;    // timespan since the last action was performed
            TimeSpan timeSinceButtonPushed;   // timespan since the last button was pushed
            double timeSinceActionTakenInMS;  // timeSinceActionTaken in milliseconds
            double timeSinceButtonPushedInMS; // timeSinceButtonPushed in milliseconds
            DateTime now = DateTime.Now;      // the time now

            //ignore message: 0x000004ca. Messages come in pairs. This code is 1226
            //if (nKey == 0x000004c9 && inputhandler != null && inputhandler.IsLoaded)
            if (nKey != 0x000004ca && inputhandler != null && inputhandler.IsLoaded)
            {
                // calculate the time passed since a button was pressed
                timeSinceButtonPushed = now - lastTimeButtonPushed;
                timeSinceButtonPushedInMS = timeSinceButtonPushed.TotalMilliseconds;

                // calculate the time since an action was performed
                timeSinceActionTaken = now - lastTimeActionPerformed;
                timeSinceActionTakenInMS = timeSinceActionTaken.TotalMilliseconds;

                #if DUPLICATE_KEYS
                // increase the counter for the number of times the same key has been pressed
                //if (nKeyFun == lastnKeyFunPressed && (totalKeyPressedTimeSpan == 0 || keyPressedTimeSpan == 156 || totalKeyPressedTimeSpan == 187.5 || totalKeyPressedTimeSpan == 203.125)) // keyPressedTimeSpan == 140 
                if (nKeyFun == lastnKeyFunPressed)
                {
                    sameButtonPushedCount++;
                }
                else
                {
                    sameButtonPushedCount = 0;
                }
                #endif

                #if NAVIGATION_KEYS
                // nKeyFun.ToString() - codes for keys
                // up       = 15
                // down     = 16
                // right    = 17
                // left     = 18
                // channel+ = 29
                // channel- = 30
                Boolean navigationKeyPressed = (
                    nKeyFun == 15 ||
                    nKeyFun == 16 ||
                    nKeyFun == 17 ||
                    nKeyFun == 18 ||
                    nKeyFun == 29 ||
                    nKeyFun == 30
                );
                #endif // NAVIGATION_KEYS

                // log some useful internal information - for debugging purposes
                Log.Debug("AverRMKV: " +
                    #if DUPLICATE_KEYS
                    "button pressed count: " + sameButtonPushedCount + ": " +
                    #endif
                    "time since action taken: " + timeSinceButtonPushedInMS + "ms, " +
                    "time since button pushed: " + timeSinceActionTakenInMS + "ms, " +
                    "Data Received: " + nKey.ToString() + " " + nKeyFun.ToString() 
                    #if NAVIGATION_KEYS
                    + " " + navigationKeyPressed
                    #endif // NAVIGATION_KEYS
                );

                // perform the action 
                // if (now - last_time_action_performed) > timeout_period, then perform action
                if (timeSinceActionTakenInMS > (interKeyDelay + 10))
                {
                    Log.Debug("AverRMKV:   KEY ACTIONED: " + nKeyFun.ToString());
                    inputhandler.MapAction((int)nKeyFun);
                    lastTimeActionPerformed = DateTime.Now;
                }
                else
                {
                    Log.Debug("AverRMKV:   KEY IGNORED: " + nKeyFun.ToString());
                }
                lastTimeButtonPushed = DateTime.Now;
                lastnKeyFunPressed = nKeyFun;
            }
        }

        #endregion // Callback Fuction


        #region IPlugin Members

        /// <summary>
        /// Create and start this instance of the remote control.
        /// </summary>
        void IPlugin.Start()
        {
            try
            {
                rc = new RemoteClass();
                rc.Initialize();
                rc.OnRemoteData += ReceiveData;
                string mediaportalpath = System.Reflection.Assembly.GetEntryAssembly().FullName;
                rc.SwitchBeginAP(mediaportalpath);
                Log.Info("AverRMKV Plugin: Started by " + mediaportalpath);
            }
            catch (Exception e)
            {
                Log.Error("AverRMKV Plugin: AverRemote.exe not responding");
                Log.Error("AverRMKV Plugin: Exception: " + e);
            }

            inputhandler = new InputHandler(mappingfile);
            if (inputhandler == null || !inputhandler.IsLoaded)
            {
                Log.Error("AverRMKV Plugin: File " + mappingfile + " not loaded.");
            }
            lastTimeActionPerformed = DateTime.Now;
            lastTimeButtonPushed    = DateTime.Now;
            Log.Info("AverRMKV Plugin: Started.");
        }

        /// <summary>
        /// Stop and destroy this instance of the remote control.
        /// </summary>
        void IPlugin.Stop()
        {
            try
            {
                if (rc != null)
                {
                    rc.OnRemoteData -= ReceiveData;
                    rc.Uninitialize();
                }
            }
            catch { }
            Log.Info("AverRMKV Plugin: Stopped.");
        }

        #endregion // IPlugin Members

        #region ISetupForm Members

        // Returns the name of the plugin which is shown in the plugin menu
        public string PluginName()
        {
            return "AverRMKV";
        }

        // Returns the description of the plugin is shown in the plugin menu
        public string Description()
        {
            return "Remote control plug-in for the AVerMedia AV-KV remote control";
        }

        // Returns the author of the plugin which is shown in the plugin menu
        public string Author()
        {
            return "Mic Tuton (original code written by Pantav)";
        }

        // show the setup dialog
        public void ShowPlugin()
        {
            InputMappingForm conf = new InputMappingForm(mappingfile);
            conf.ShowDialog();
        }

        // Indicates whether plugin can be enabled/disabled
        public bool CanEnable()
        {
            return true;
        }

        // Get Windows-ID
        public int GetWindowId()
        {
            return -1;
        }

        // Indicates if plugin is enabled by default;
        public bool DefaultEnabled()
        {
            return true;
        }

        // indicates if a plugin has it's own setup screen
        public bool HasSetup()
        {
            return true;
        }
        
        /// <summary>
        /// If the plugin should have it's own button on the main menu of MediaPortal then it
        /// should return true to this method, otherwise if it should not be on home
        /// it should return false
        /// </summary>
        /// <param name="strButtonText">text the button should have</param>
        /// <param name="strButtonImage">image for the button, or empty for default</param>
        /// <param name="strButtonImageFocus">image for the button, or empty for default</param>
        /// <param name="strPictureImage">subpicture for the button or empty for none</param>
        /// <returns>true : plugin needs it's own button on home
        /// false : plugin does not need it's own button on home</returns>

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
        {
            strButtonText       = String.Empty;
            strButtonImage      = String.Empty;
            strButtonImageFocus = String.Empty;
            strPictureImage     = String.Empty;
            return false;
        }

        #endregion // ISetupForm Members
    }
}
