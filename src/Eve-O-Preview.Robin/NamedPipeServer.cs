// Eve-O-Preview.Robin is a companion program / sidekick (think Batman and Robin) to provide a native runtime for communicating with the client, such as permitting AllowSetForegroundWindow and limiting DirectX frames per minute.
// Copyright (C) 2026  Aura Asuna
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.using System.Diagnostics;

using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;
using static EveOPreview.Robin.DebugLogger;
using static EveOPreview.Robin.Global;

namespace EveOPreview.Robin;

public static unsafe class NamedPipeServer
{
    internal static bool IsNamedPipeRunning = true;
    private static NamedPipeServerStream _namedPipeServerStream = null!;
    private static readonly string EvoRobinPipeName = "EveoRobin_" + ThisClientsHandle;

    // Just making up a custom mini messaging protocol just randomly really.
    private const byte PIPE_FIRE_AND_FORGET_DIRECTION = 0xA3; // A3 is the first byte, if the caller wants something done quick and don't really care about the outcome.
    private const byte PIPE_SET_FOCUSED_COMMAND = 0xB1; // B1 is the second byte following a A3, to tell our process it needs to be ready to take focus and unthrottle the FPS ASAP.
    private const byte PIPE_PREDICT_FOCUS_COMMAND = 0xB3; // B3 is the second byte following a A3, to tell our process that focus might be on the way soon.

    private const byte PIPE_QUERY_DIRECTION = 0xA1; // A1 is the first byte, if the caller is requesting read only.
    private const byte PIPE_PING_REQUEST_CODE = 0xB2; // B2 is used for a simple ping, following an A1

    private const byte PIPE_UPDATE_DIRECTION = 0xA2; // A2 is the first byte, if the caller is asking us to update something.
    private const byte PIPE_FPS_PREFIX_BYTE_FOCUSED = 0xF1; // F1 is a prefix before an int (4 bytes) for the target focused FPS rate.
    private const byte PIPE_FPS_PREFIX_BYTE_BACKGROUND = 0xF2; // F2 is a prefix before an int (4 bytes) for the target background FPS rate.
    private const byte PIPE_FPS_PREFIX_BYTE_PREDICT = 0xF3; // F3 is a prefix before an int (4 bytes) for the target FPS rate when predicting focus is coming.
    private const byte PIPE_TAKE_OWNERSHIP_COMMAND = 0xB4; // 0xB4 is a prefix for the calling process to claim ownership of this.

    private const byte PIPE_SOUND_UNMUTE_ALL = 0xC1; // A2 C1 = Unmute all sounds. Reply with 0x01
    private const byte PIPE_SOUND_UNMUTE_LIST = 0xC2; // A2 C2 = Unmute a list of sounds. Receive (int) lengthOfList each{(uint) soundsEventId}. Reply with 0x01
    private const byte PIPE_SOUND_MUTE_LIST = 0xC3; // A2 C3 = Mute a list of sounds. Receive (int) lengthOfList each{(uint) soundsEventId}. Reply with 0x01
    private const byte PIPE_SOUND_GET_MUTED = 0xC4; // A1 C4 = Get a list of muted sounds. Send (int) lengthOfList each{(uint) soundsEventId}
    private const byte PIPE_SOUND_EVENT_HISTORY = 0xC5; // A1 C5 = Get a list of history. Send response (int) listLength each{(uint) eventId (ulong) gameObjectId (ulong) timestamp}

    private const byte PIPE_SUCCESS_RESPONSE_CODE = 0x01; // 01 is the response we send at the end of a A2 request, like a success return code. 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize()
    {
        _namedPipeServerStream = CreateNamedPipeServer();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NamedPipeServerStream CreateNamedPipeServer()
    {
        Info($"Creating named pipe server: {EvoRobinPipeName}");

        if (OperatingSystem.IsWindows())
        {
            // Secure the named pipe to our current user only.
            var currentUser = WindowsIdentity.GetCurrent().User
                              ?? throw new InvalidOperationException("Cannot determine current user identity to configure Named Pipe ACLs.");

            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            return NamedPipeServerStreamAcl.Create(EvoRobinPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, pipeSecurity);
        }
        else
        {
            // Not offering linux support at the moment but let's make sure it's secure if anyone does try running this on linux just in case.
            var pipe = new NamedPipeServerStream(EvoRobinPipeName, PipeDirection.InOut, 1);
            string socketPath = Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{EvoRobinPipeName}");

            if (File.Exists(socketPath))
            {
                // 600: Read/Write for User, No Access for Group/Others
                File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            return pipe;
        }
    }

    internal static void StartPipeServer()
    {
        var failureCount = 0;
        while (IsNamedPipeRunning)
        {
            var cmdBytes = new byte[2];
            try
            {
                //Log($"Named pipe {EvoRobinPipeName} WaitForConnection()");
                _namedPipeServerStream.WaitForConnection();

                using (var reader =
                       new BinaryReader(_namedPipeServerStream, System.Text.Encoding.UTF8, leaveOpen: true))
                using (var writer =
                       new BinaryWriter(_namedPipeServerStream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    //lastPlace = "ReadByte directionByte";
                    cmdBytes[0] = reader.ReadByte();
                    cmdBytes[1] = reader.ReadByte();
                    switch (cmdBytes[0])
                    {
                        case PIPE_FIRE_AND_FORGET_DIRECTION:
                            switch (cmdBytes[1])
                            {
                                case PIPE_SET_FOCUSED_COMMAND:
                                    A3B1_SetFocusNow();
                                    break;
                                case PIPE_PREDICT_FOCUS_COMMAND:
                                    A3B3_PrepareToTakeFocusSoon(reader);
                                    break;
                            }

                            break;
                        case PIPE_UPDATE_DIRECTION:
                            switch (cmdBytes[1])
                            {
                                case PIPE_TAKE_OWNERSHIP_COMMAND:
                                    A2B4_ClaimProcessOwnership(reader);
                                    break;

                                case PIPE_FPS_PREFIX_BYTE_FOCUSED:
                                    A2F1_SetFpsTargets(reader);
                                    break;
                                case PIPE_SOUND_UNMUTE_ALL:
                                    A2C1_UnmuteAllSounds();
                                    break;
                                case PIPE_SOUND_UNMUTE_LIST:
                                    A2C2_UnmuteSounds(reader);
                                    break;
                                case PIPE_SOUND_MUTE_LIST:
                                    A2C3_MuteSounds(reader);
                                    break;
                            }

                            ReplySuccess(writer);

                            break;

                        case PIPE_QUERY_DIRECTION:
                            switch (cmdBytes[1])
                            {
                                case PIPE_QUERY_DIRECTION: // The default will be getting the FPS settings. 
                                    A1A1_QueryFpsSettings(writer);
                                    break;
                                case PIPE_PING_REQUEST_CODE:
                                    A1B2_Ping(writer);
                                    break;
                                case PIPE_SOUND_GET_MUTED:
                                    A1C4_SendAllMutedSounds(writer);
                                    break;
                                case PIPE_SOUND_EVENT_HISTORY:
                                    A1C5_SendSoundEventHistory(writer);
                                    break;
                            }

                            break;
                    }

                    writer.Flush();
                    writer.Close();
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Error(ex, $"Named pipe {_namedPipeServerStream} processing command {cmdBytes[0]:X} {cmdBytes[1]:X}");
                Thread.Sleep(10); // hopefully we never crash in a loop but if we do, save the cpu

                // Rather silent failure than crashing another process

                _namedPipeServerStream.Dispose();
                _namedPipeServerStream = CreateNamedPipeServer();

                failureCount++;

                if (failureCount > 100)
                {
                    // Something is very wrong. Let's just give up on everything.
                    IsNamedPipeRunning = false;
                    DxHook.IsFpsThrottleActive = false;
                    NativeMethods.MessageBox(IntPtr.Zero, $"Aborting FPS Limiter. Named pipe error: {ex}",
                        "FPS Limiter", 0);
                }
            }
            finally
            {
                if (_namedPipeServerStream.IsConnected)
                {
                    _namedPipeServerStream.Disconnect();
                }
            }
        }
    }

    private static void A2C3_MuteSounds(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        for (int i = 0; i < length; i++)
        {
            var nextId = reader.ReadUInt32();
            AudioMuteSystem.AddMutedId(nextId);
        }
    }

    private static void A2C2_UnmuteSounds(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        for (int i = 0; i < length; i++)
        {
            var nextId = reader.ReadUInt32();
            AudioMuteSystem.RemoveMutedId(nextId);
        }
    }

    private static void A2C1_UnmuteAllSounds()
    {
        AudioMuteSystem.ClearMutedIds();
    }

    private static void A1C5_SendSoundEventHistory(BinaryWriter writer)
    {
        var eventHistory = AudioLog.GetOrderedEventHistory();
        writer.Write(eventHistory.Count);
        foreach (var e in eventHistory)
        {
            writer.Write(e.EventID);
            writer.Write(e.GameObjectID);
            writer.Write(e.Timestamp);
        }
    }

    private static void A1C4_SendAllMutedSounds(BinaryWriter writer)
    {
        var allMutedSounds = AudioMuteSystem.GetMutedIds();
        writer.Write(allMutedSounds.Count);

        foreach (var id in allMutedSounds)
        {
            writer.Write(id);
        }
    }

    private static void A1B2_Ping(BinaryWriter writer)
    {
        writer.Write(PIPE_SUCCESS_RESPONSE_CODE);
    }

    private static void A1A1_QueryFpsSettings(BinaryWriter writer)
    {
        //lastPlace = "Write PipeFpsPrefixByteFocused";
        writer.Write(PIPE_FPS_PREFIX_BYTE_FOCUSED);
        //lastPlace = "Write _targetFpsInFocus";
        writer.Write(DxHook.TargetFpsInFocus);
        //lastPlace = "Write PipeFpsPrefixByteBackground";
        writer.Write(PIPE_FPS_PREFIX_BYTE_BACKGROUND);
        //lastPlace = "Write _targetFpsInBackground";
        writer.Write(DxHook.TargetFpsInBackground);
        //lastPlace = "Write PipeFpsPrefixBytePredict";
        writer.Write(PIPE_FPS_PREFIX_BYTE_PREDICT);
        //lastPlace = "Write _targetFpsInPredictFocus";
        writer.Write(DxHook.TargetFpsInPredictFocus);
        writer.Write(PIPE_SUCCESS_RESPONSE_CODE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReplySuccess(BinaryWriter writer)
    {
        writer.Write(PIPE_SUCCESS_RESPONSE_CODE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void A2F1_SetFpsTargets(BinaryReader reader)
    {
        // since we read the 0xF1 already we know the next 4 bytes are going to be an 32 bit int.
        //lastPlace = "ReadInt32 newTargetFpsFocus";
        var newTargetFpsFocus = reader.ReadInt32();

        if (newTargetFpsFocus < 1 || newTargetFpsFocus > 1000)
        {
            // anything that might look like it's just unthrottled, we will turn off the throttle.
            DxHook.TargetFpsInFocus = 0;
            DxHook.PerFrameTargetMsInFocus = 0;
        }
        else
        {
            DxHook.TargetFpsInFocus = newTargetFpsFocus;
            DxHook.PerFrameTargetMsInFocus = 1000.0 / newTargetFpsFocus;
        }

        //lastPlace = "ReadByte PipeFpsPrefixByteBackground";
        if (reader.ReadByte() == PIPE_FPS_PREFIX_BYTE_BACKGROUND)
        {
            // since we read the 0xF2 we know the next 4 bytes are going to be an 32 bit int.
            //lastPlace = "ReadInt32 newTargetFpsBackground";
            var newTargetFpsBackground = reader.ReadInt32();

            if (newTargetFpsBackground < 1 || newTargetFpsBackground > 1000)
            {
                // anything that might look like it's just unthrottled, we will turn off the throttle.
                DxHook.TargetFpsInBackground = 0;
                DxHook.PerFrameTargetMsInBackground = 0;
            }
            else
            {
                DxHook.TargetFpsInBackground = newTargetFpsBackground;
                DxHook.PerFrameTargetMsInBackground = 1000.0 / newTargetFpsBackground;
            }
        }

        //lastPlace = "ReadByte PipeFpsPrefixBytePredict";
        if (reader.ReadByte() == PIPE_FPS_PREFIX_BYTE_PREDICT)
        {
            // since we read the 0xF1 we know the next 4 bytes are going to be an 32 bit int.
            //lastPlace = "ReadInt32 newTargetFpsPredict";
            var newTargetFpsPredict = reader.ReadInt32();

            if (newTargetFpsPredict < 1 || newTargetFpsPredict > 1000)
            {
                // anything that might look like it's just unthrottled, we will turn off the throttle.
                DxHook.TargetFpsInPredictFocus = 0;
                DxHook.PerFrameTargetMsInPredictFocus = 0;
            }
            else
            {
                DxHook.TargetFpsInPredictFocus = newTargetFpsPredict;
                DxHook.PerFrameTargetMsInPredictFocus = 1000.0 / newTargetFpsPredict;
            }
        }

        // If both focus and background are 0, then disable the whole throttling.
        DxHook.IsFpsThrottleActive = DxHook.PerFrameTargetMsInBackground + DxHook.PerFrameTargetMsInFocus > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void A2B4_ClaimProcessOwnership(BinaryReader reader)
    {
        OwnerProcessId = reader.ReadInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void A3B3_PrepareToTakeFocusSoon(BinaryReader reader)
    {
        DxHook.SetOurWindowInFocus(FocusType.Predicted);
        DxHook.PredictedFocusTimeoutMs = reader.ReadInt32();
        // Log($"PipePredictFocusCommand processed");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void A3B1_SetFocusNow()
    {
        DxHook.SetOurWindowInFocus(FocusType.Foreground);
        // Log($"PipeSetFocusedCommand processed");
    }
}