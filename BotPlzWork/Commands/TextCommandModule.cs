using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using DSharpPlus.VoiceNext.EventArgs;
using Microsoft.VisualBasic;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NAudio.Utils;
using STTClient;
using STTClient.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using BotPlzWork;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace BotPlzWork.Commands
{

    //Things to fix Today
    //Doesnt work if there are long peiods of silence (overwrites) 
    //Memory Compounding. 
    public class TextCommandModule : BaseCommandModule
    {
        public STT sttClient { private get; set; }

        private STTStream? sttStream;

        public DiscordClient discordClient { private get; set; }

        private WaveFormat sttFormat = new WaveFormat(16000, 16, 1);
        private WaveFormat inputFormat = new WaveFormat(48000, 16, 2);
        private WaveFormat outputFormat = new WaveFormat(44100, 16, 2);

        private RecordingManager recordingManager;

        //private MemoryStream recordingStream = new MemoryStream();
        //int packetLength;

        [Command("just")]
        public async Task sendSpecificAudio(CommandContext ctx, DiscordMember member)
        {
            string fileName = $"C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\recordings\\{member.Username}.wav";
            if (File.Exists(fileName))
            {
                await ctx.TriggerTypingAsync();
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    await new DiscordMessageBuilder().AddFile(fs).SendAsync(ctx.Channel);
                }
            }
        }

        [Command("testDecode")]
        public async Task testing(CommandContext ctx, string fileName)
        {
             
            

            
            WaveFileReader wave = new WaveFileReader($"AudioFiles\\{fileName}.wav");
            WaveBuffer buffer = new WaveBuffer((int)wave.Length);
            wave.Read(buffer, 0, (int)wave.Length);


            using (STT sttClient = new STT("model.tflite"))
            {
                sttClient.EnableExternalScorer("large_vocabulary.scorer");
                Metadata metaData = sttClient.SpeechToTextWithMetadata(buffer.ShortBuffer, Convert.ToUInt32(buffer.MaxSize / 2), 1);
                CandidateTranscript transcript = metaData.Transcripts[0];
                TokenMetadata[] tokens = transcript.Tokens;
                
                string result = HelperMethods.convertTokensToString(tokens);

                Console.WriteLine(result);
            }
            

            //Console.WriteLine(sttClient.SpeechToText(buffer.ShortBuffer, Convert.ToUInt32(buffer.MaxSize / 2)));
            
        }

        [Command("bojangles")]
        public async Task bojCommand(CommandContext ctx)
        {
            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);

            var transmit = connection.GetTransmitSink();

            var pcm = HelperMethods.convertAudioToPCM("C:\\Users\\cojac\\source\\repos\\botTesting\\botTesting\\bin\\x64\\Debug\\net6.0\\WAVFiles\\bojangles.wav", 1);
            await pcm.CopyToAsync(transmit);
            await pcm.DisposeAsync();

        }

        [Command("status")]
        public async Task statusCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Im alive, bitch");
        }

}

}






