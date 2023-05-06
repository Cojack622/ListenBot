using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BotPlzWork
{
    internal class StreamUser
    {
        //public MemoryStream recordingStream;
        //public MemoryStream triggerStream;

        public PacketStream recordingStream;
        public PacketStream triggerStream;
        public PacketStream commandStream;

        public DiscordMember member;
        public int startTime, lastPacketReceived;

        public UserStates state;

        //private const int oneMillisecondLength = 1920 / 20;
        //private const int maxLengthMS = 15 * 1000;
        //private const int maxLengthTriggerMS = 4 * 1000;

        public string runningTranscript = "**";

        //Make this a field of streamUser, should be universal tho
        //private int maxLength = maxLengthMS * oneMillisecondLength;

        public StreamUser(DiscordMember member, int startTime)
        {
            this.member = member;
            this.startTime = startTime;
            this.lastPacketReceived = startTime;

            recordingStream = new PacketStream(15, 48000);
            triggerStream = new PacketStream(2, 16000);
            commandStream = new PacketStream(8, 16000);

            commandStream.FillStreamWithSilence();
            triggerStream.FillStreamWithSilence();

            //Change this to check for a user role, for now all users are able to use STT
            state = UserStates.WAITING_FOR_TRIGGER;

        }
    }
}