using System;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;
using System.Collections.Generic;

namespace MiniPlayer
{
    public static class AudioHelper
    {
        public static float AdjustVolumeRobust(string appName, string exePath, float delta)
        {
            float lastVol = -1f;
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    foreach (var device in devices)
                    {
                        try 
                        {
                            var sessionManager = device.AudioSessionManager;
                            if (sessionManager == null) continue;

                            var sessions = sessionManager.Sessions;
                            if (sessions == null) continue;

                            for (int i = 0; i < sessions.Count; i++)
                            {
                                using (var session = sessions[i])
                                {
                                    bool isMatch = false;
                                    string sessionId = session.GetSessionIdentifier;
                                    string dispName = session.DisplayName;

                                    if (!string.IsNullOrEmpty(sessionId) && 
                                        sessionId.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                                        isMatch = true;

                                    if (!isMatch && !string.IsNullOrEmpty(dispName) && 
                                        dispName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                                        isMatch = true;

                                    uint pid = session.GetProcessID;
                                    if (!isMatch && pid > 0)
                                    {
                                        try {
                                            using (var proc = Process.GetProcessById((int)pid)) {
                                                if (proc.ProcessName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    isMatch = true;
                                                else if (!string.IsNullOrEmpty(exePath) && string.Equals(proc.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                                                    isMatch = true;
                                            }
                                        } catch { }
                                    }

                                    if (isMatch)
                                    {
                                        float currentVol = session.SimpleAudioVolume.Volume;
                                        float newVol = currentVol + delta;
                                        if (newVol < 0f) newVol = 0f;
                                        if (newVol > 1f) newVol = 1f;
                                        
                                        session.SimpleAudioVolume.Volume = newVol;
                                        lastVol = newVol;
                                    }
                                }
                            }
                        }
                        catch { }
                        finally { device.Dispose(); }
                    }
                }
            }
            catch { }
            return lastVol;
        }

        public static string GetDebugSessionInfo()
        {
            var names = new List<string>();
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                    {
                        var sessionManager = device.AudioSessionManager;
                        var sessions = sessionManager.Sessions;
                        if (sessions != null)
                        {
                            for (int i = 0; i < sessions.Count; i++)
                            {
                                using (var s = sessions[i])
                                {
                                    string name = s.DisplayName;
                                    if (string.IsNullOrEmpty(name)) name = "P:" + s.GetProcessID;
                                    if (!names.Contains(name)) names.Add(name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { return "Err: " + ex.Message; }
            return string.Join(", ", names.Take(3));
        }
    }
}
