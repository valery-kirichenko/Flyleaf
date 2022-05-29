﻿using System;
using System.Collections.ObjectModel;

using SharpGen.Runtime;
using SharpGen.Runtime.Win32;

using Vortice.MediaFoundation;
using static Vortice.XAudio2.XAudio2;

namespace FlyleafLib
{
    public class AudioEngine : CallbackBase, IMMNotificationClient//, INotifyPropertyChanged
    {
        /* TODO
         * 
         * 1) Master/Session Volume/Mute probably not required
         *      Possible only to ensure that we have session's volume unmuted and to 1? (probably the defaults?)
         */

        #region Properties (Public)
        /// <summary>
        /// Default audio device name
        /// </summary>
        public string       DefaultDeviceName   { get; private set; } = "Default";

        /// <summary>
        /// Default audio device id
        /// </summary>
        public string       DefaultDeviceId     { get; private set; } = "0";

        /// <summary>
        /// Whether no audio devices were found or audio failed to initialize
        /// </summary>
        public bool         Failed              { get; private set; }

        public string       CurrentDeviceName   { get; private set; } = "Default";
        public string       CurrentDeviceId     { get; private set; } = "0";

        /// <summary>
        /// List of Audio Capture Devices
        /// </summary>
        public ObservableCollection<string> CapDevices { get; private set; } = new ObservableCollection<string>();

        /// <summary>
        /// List of Audio Devices
        /// </summary>
        public ObservableCollection<string> Devices { get; private set; } = new ObservableCollection<string>();

        public string GetDeviceId(string deviceName)
        {
            if (deviceName == DefaultDeviceName)
                return DefaultDeviceId;

            foreach(var device in deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceStates.Active))
            {
                if (device.FriendlyName.ToLower() != deviceName.ToLower())
                    continue;

                return device.Id;
            }

            throw new Exception("The specified audio device doesn't exist");
        }
        public string GetDeviceName(string deviceId)
        {
            if (deviceId == DefaultDeviceId)
                return DefaultDeviceName;

            foreach(var device in deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceStates.Active))
            {
                if (device.Id.ToLower() != deviceId.ToLower())
                    continue;

                return device.FriendlyName;
            }

            throw new Exception("The specified audio device doesn't exist");
        }
        #endregion

        IMMDeviceEnumerator  deviceEnum;
        private object locker = new object();
        public AudioEngine()
        {
            if (Engine.Config.DisableAudio)
            {
                Failed = true;
                return;
            }

            try
            {
                deviceEnum = new IMMDeviceEnumerator();
                var defaultDevice = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (defaultDevice == null)
                {
                    Failed = true;
                    return;
                }

                Devices.Clear();
                Devices.Add(DefaultDeviceName);
                foreach(var device in deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceStates.Active))
                    Devices.Add(device.FriendlyName);

                CurrentDeviceId     = defaultDevice.Id;
                CurrentDeviceName   = defaultDevice.FriendlyName;

                if (Logger.CanInfo)
                {
                    string dump = "";
                    foreach (var device in deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceStates.Active))
                        dump += $"{device.Id} | {device.FriendlyName} {(defaultDevice.Id == device.Id ? "*" : "")}\r\n";
                    Engine.Log.Info($"Audio Devices\r\n{dump}");
                }

                var xaudio2 = XAudio2Create();

                if (xaudio2 == null)
                    Failed = true;
                else
                    xaudio2.Dispose();

                deviceEnum.RegisterEndpointNotificationCallback(this);

            } catch { Failed = true; }
        }

        private void RefreshDevices()
        {
            // Refresh Devices and initialize audio players if requried
            lock (locker)
            {

                Utils.UI(() =>
                {
                    Devices.Clear();
                    Devices.Add(DefaultDeviceName);
                    foreach(var device in deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceStates.Active))
                        Devices.Add(device.FriendlyName);

                    foreach(var player in Engine.Players)
                    {
                        if (!Devices.Contains(player.Audio.Device))
                            player.Audio.Device = DefaultDeviceName;
                        else
                            player.Audio.RaiseDevice();
                    }
                });

                var defaultDevice =  deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (defaultDevice != null)
                {
                    CurrentDeviceId     = defaultDevice.Id;
                    CurrentDeviceName   = defaultDevice.FriendlyName;
                }
            }
        }

        public void OnDeviceStateChanged(string pwstrDeviceId, int newState) { RefreshDevices(); }
        public void OnDeviceAdded(string pwstrDeviceId) { RefreshDevices(); }
        public void OnDeviceRemoved(string pwstrDeviceId) { RefreshDevices(); }
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string pwstrDefaultDeviceId) { RefreshDevices(); }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}