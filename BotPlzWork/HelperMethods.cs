using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.VoiceNext;
using DSharpPlus.CommandsNext;
using STTClient.Models;

namespace BotPlzWork
{
    internal static class HelperMethods
    {
        public const int oneMillisecondLength = 1920 / 20;
        public const int maxLengthMS = 15 * 1000;
        public const int maxLength = maxLengthMS * oneMillisecondLength;

        
        public static String convertTokensToString(TokenMetadata[] tokens)
        {
            string result = "";
            for (int i = 0; i < tokens.Length; i++)
            {
                result += tokens[i].Text;
                //Console.WriteLine(tokens[i].Text);
            }
            return result;
        }

        public static Stream convertAudioToPCM(string filePath, int channelCount)
        {
            var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $@"-i ""{filePath}"" -ac {channelCount} -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false

            });

            return ffmpeg.StandardOutput.BaseStream;
        }

        public static async Task sendAudioToChannel(CommandContext ctx, string filePath)
        {
            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);
            var transmit = connection.GetTransmitSink();

            //string joinSoundPath = "C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\AudioFiles\\wereLive.wav";
            Stream joinSound = convertAudioToPCM(filePath, 2);

            await joinSound.CopyToAsync(transmit);
            await joinSound.DisposeAsync();
        }

        public static int getTime32()
        {
            //So this ones maybe unneccesary but i like it. Basically, I need the time in milliseconds, easy way to do that is to use
            //The unixTime. Problem is, saving a long is a lot of data. Since we only want to know time in relation to other timestamps,
            //We bit compare this long with the maximum number for an Integer (converted to a long), which leaves us with just the last 
            //32 bits of a long. This allows for 25 days worth of audio, so im pretty sure it's fine
            return (int)(Convert.ToUInt64(DateTimeOffset.Now.ToUnixTimeMilliseconds()) & Convert.ToUInt64(int.MaxValue));
        }

        public static int getLongestStreamIndex(List<StreamUser> users)
        {
            int longest = 0;
            for (int i = 1; i < users.Count; i++)
            {
                if (users[i].recordingStream.Length() > users[i - 1].recordingStream.Length())
                {
                    longest = i;
                }
            }
            return longest;
        }
    }
}
