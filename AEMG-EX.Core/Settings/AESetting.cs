﻿using Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using EMM.Core;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Linq;
using EMM.Core.Extension;

namespace AEMG_EX.Core
{
    public class AESetting
    {
        public AESetting()
        {
            Load();
        }

        private static readonly string fileName = Path.Combine(Environment.CurrentDirectory, "Setting", "AEMGSettings");

        private static AESetting settings;

        private static object sync = new object();

        #region General settings

        /// <summary>
        /// true if auto update at start up enable
        /// </summary>
        public bool IsAutoUpdateEnable { get; set; } = true;

        /// <summary>
        /// The location of Nox's record file
        /// </summary>
        public string NoxScriptLocation { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nox", "record");

        /// <summary>
        /// The location to memu script folder
        /// </summary>
        public string MemuScriptLocation { get; set; } = Path.Combine(UsefulStaticMethod.GetInstallationPath("MEmu"), "MEmu", "scripts");

        #endregion

        #region Convert settings

        /// <summary>
        /// Number of pixels to randomize
        /// </summary>
        public int Randomize { get; set; } = GlobalData.Randomize;

        /// <summary>
        /// Custom x resolution
        /// </summary>
        public int CustomX { get; set; } = GlobalData.CustomX;

        /// <summary>
        /// Custom y resolution
        /// </summary>
        public int CustomY { get; set; } = GlobalData.CustomY;

        /// <summary>
        /// The selected emulator
        /// </summary>
        public Emulator SelectedEmulator { get; set; } = Emulator.Nox;

        #endregion

        #region Methods

        /// <summary>
        /// Persist setting change
        /// </summary>
        public void Save()
        {
            //Check if file exists
            if (!File.Exists(fileName))
            {
                (new FileInfo(fileName)).Directory.Create();
                File.Create(fileName).Close();
            }

            string settingText = JsonConvert.SerializeObject(this, Formatting.Indented);

            File.WriteAllText(fileName, settingText);
        }

        /// <summary>
        /// Load the settings
        /// </summary>
        public void Load()
        {
            if (!File.Exists(fileName))
            {
                Save();
            }

            JObject settingJObject;

            try
            {
                settingJObject = JObject.Parse(File.ReadAllText(fileName));
            }
            catch
            {
                this.RestoreDefaultSettings();
                settingJObject = JObject.Parse(File.ReadAllText(fileName));
            }

            var serializer = new JsonSerializer();
            serializer.Populate(settingJObject.CreateReader(), this);
        }

        /// <summary>
        /// Restore settings to the default value
        /// </summary>
        public void RestoreDefaultSettings()
        {
            if (!File.Exists(fileName))
                return;
            try
            {
                File.Delete(fileName);
            }
            catch
            {
                //TO DO: Add some handler here
            }

            this.Save();
        }

        #endregion

        #region Helpers

        public static AESetting Default()
        {
            lock(sync)
            {
                return settings ?? (settings = new AESetting());
            }          
        }

        #endregion
    }
}