using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.Wave;
using STTClient.Models;
using STTClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext.EventArgs;
using NAudio.Wave.Compression;

namespace BotPlzWork.Commands
{
    internal class RecordingCommandModule : BaseCommandModule
    {
        public STT sttClient { private get; set; }

        public DiscordClient discordClient { private get; set; }

        private RecordingManager recordingManager;

        [Command("start")]
        public async Task joinCommand(CommandContext ctx, DiscordChannel? channel = null)
        {

            //when starting the recording manaager, make sure the time provided to the constructor is
            //The time when the bot connects to the channel (just use task.wait methinks)

            channel ??= ctx.Member.VoiceState.Channel;

            if (channel == null)
            {
                await ctx.RespondAsync("Get in a voice channel first dummy");
            }
            var connection = await channel.ConnectAsync();

            if (connection != null)
            {
                //Gets the last 32 bits of the timestamp by bit comparing it to the max of a 32 bit int 
                int timeStamp = HelperMethods.getTime32();
                IReadOnlyList<DiscordMember> users = channel.Users;
                recordingManager = new RecordingManager(timeStamp, users, ctx);
                await recordingManager.StartQueuing();


               
                connection.VoiceReceived += onVoiceReceived;
                connection.UserJoined += onUserJoined;
                connection.UserLeft += onUserLeft;

            }
            else
            {
                Console.WriteLine("Connection is fucked yo");
            }


        }


        [Command("Stop")]
        public async Task stopCommand(CommandContext ctx)
        {

            await recordingManager.StopQueuing();

            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);

            if (connection == null)
            {
                Console.WriteLine("Tried to leave but shit is wack");
            }

            connection.UserJoined -= onUserJoined;
            connection.UserLeft -= onUserLeft;
            connection.VoiceReceived -= onVoiceReceived;

            connection.Disconnect();
            recordingManager.DisposeStreams(true);
        }

        [Command("record")]
        public async Task leaveCommand(CommandContext ctx)
        {
            //Disconnects to the voice channel
            var vnext = ctx.Client.GetVoiceNext();

            var connection = vnext.GetConnection(ctx.Guild);

            if (connection == null)
            {
                Console.WriteLine("Tried to leave but shit is wack");
            }

            connection.UserJoined -= onUserJoined;
            connection.UserLeft -= onUserLeft;
            connection.VoiceReceived -= onVoiceReceived;


            //await recordingManager.StopQueuing();

            //Saves a list of the users, then creates a list of tasks for the ffmpeg conversions
            List<StreamUser> users = recordingManager.users;

            //Creates an empty string for the inputs portion of the final ffmpeg command
            string inputs = "";
            int finishTime = HelperMethods.getTime32();
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].recordingStream.Length() < HelperMethods.maxLength)
                {
                    users[i].recordingStream.AddSilenceToStream(HelperMethods.maxLengthMS - ((int)users[i].recordingStream.Length() / HelperMethods.oneMillisecondLength));
                }
                else
                {
                    users[i].recordingStream.AddSilenceToStream(finishTime - users[i].lastPacketReceived);
                }
                //Saves a wav file with the same name as the user
                string fileName = $"C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\recordings\\{users[i].member.Username}.wav";
                inputs += $" -i {fileName}";
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                //Converts PCM data to a wav file
                //Even though Discord says the audio is stereo, it does not treat it as such
                int samplerate = connection.AudioFormat.SampleRate;
                var ffmpeg = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $@"-ac 1 -ar {samplerate} -f s16le -i pipe:0 -ac 1 -ar 44100 {fileName}",
                    RedirectStandardInput = true,
                    UseShellExecute = false

                });

                //byte[] recordingArray = users[i].recordingStream.ToArray();
                //Console.WriteLine("Check 1!");
                //SOOOOOOOOOOOOOO for whatever reason we are still gettting packets here so this results in a negative numbers
                byte[] recordingArray = users[i].recordingStream.orderStream();
                Task writing = ffmpeg.StandardInput.BaseStream.WriteAsync(recordingArray, 0, recordingArray.Length);
                await writing;
                
                ffmpeg.Dispose();

            }

            recordingManager.DisposeStreams(false);

            string fullRecording = "C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\recordings\\recording.wav";
            if (File.Exists(fullRecording))
            {
                File.Delete(fullRecording);
            }

            string arguments = $"{inputs} -filter_complex amix=inputs={users.Count}:duration={HelperMethods.getLongestStreamIndex(users)} {fullRecording}";
            var ffmpegFull = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardInput = true,
                UseShellExecute = false
            });

            //await recordingManager.StartQueuing();


            await ctx.TriggerTypingAsync();
            using (var fs = new FileStream(fullRecording, FileMode.Open, FileAccess.Read))
            {
                await new DiscordMessageBuilder().AddFile(fs).SendAsync(ctx.Channel);
            }

            connection.VoiceReceived += onVoiceReceived;
            connection.UserJoined += onUserJoined;
            connection.UserLeft += onUserLeft;

            ffmpegFull.Dispose();
            //recordingManager.DisposeStreams();
            //connection.Disconnect();
        }


        private async Task onVoiceReceived(VoiceNextConnection connection, VoiceReceiveEventArgs args)
        {
            //Checks to make sure the User value is not null, since for whatever reason the starting packet(s) don't have a user assocaited 
            //With them which breaks entire program
            if (args.User != null)
            {
                //For reasons unbeknownst to man, the program entirely breaks if a user is using the web version of discord. 
                //For this reason web users aren't allowed to use the bot. 
                AudioPacket packet = new AudioPacket();
                packet.PCMdata = args.PcmData;
                packet.User = args.User;
                packet.TimeStamp = HelperMethods.getTime32();
                recordingManager.AddPacketToQueue(packet);
            }


        }

        public async Task onUserJoined(VoiceNextConnection connection, VoiceUserJoinEventArgs args)
        {
            if (!args.User.IsBot)
            {
                Console.WriteLine("User Joined");
                recordingManager.AddUser((DiscordMember)args.User, HelperMethods.getTime32());

            }
        }

        public async Task onUserLeft(VoiceNextConnection connection, VoiceUserLeaveEventArgs args)
        {
            Console.WriteLine("User left");
            recordingManager.RemoveUser((DiscordMember)args.User);
        }
    }
}
