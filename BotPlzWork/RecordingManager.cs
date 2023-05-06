using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using NAudio.Wave;
using NAudio.Wave.Compression;
using STTClient;
using STTClient.Models;

namespace BotPlzWork
{
    internal class RecordingManager
    {
        public STT sttClient;

        private Queue<AudioPacket> packetQueue;

        //Change this to be a specific guild maybe. ORRRRRRRR just add a guild variable. 
        private CommandContext context;

        private AcmStream resampleStream;
        public List<StreamUser> users { get; private set; }

        //private const int oneMillisecondLength = 1920 / 20;
        public int globalStart { get; private set; }

        private CancellationTokenSource cancellationTokenSourceRecordingQueue;
        private CancellationTokenSource cancellationTokenSourceSTTManager;

        public WaveFormat sttFormat = new WaveFormat(16000, 16, 1);
        public WaveFormat inputFormat = new WaveFormat(48000, 16, 1);

        private int attempts;
        private bool triggered;
        private StreamUser commandUser;

        public RecordingManager(int startTime, IReadOnlyList<DiscordMember> startingUsers, CommandContext ctx)
        {

            this.context = ctx;
            packetQueue = new Queue<AudioPacket>();
            resampleStream = new AcmStream(inputFormat, sttFormat);

            attempts = 1;
            triggered = false;


            sttClient = new STT("model.tflite");
            sttClient.EnableExternalScorer("large_vocabulary.scorer");
            //sttClient.SetModelBeamWidth(500);
            sttClient.AddHotWord("discord", 10.0f);
            sttClient.AddHotWord("hey", 10.0f);
            sttClient.AddHotWord("record", 10.0f);
            sttClient.AddHotWord("that", 10.0f);

            users = new List<StreamUser>();
            for (int i = 0; i < startingUsers.Count; i++)
            {
                //Checks to make sure the user is not a bot (to prevent it from listening to itself, might want to change this in the 
                //future to allow for music played by bots/itself to be recordable) and makes sure user is not using web version. 
                if (!startingUsers[i].IsBot)
                {
                    //Confusing ass naming i know
                    StreamUser user = new StreamUser(startingUsers[i], startTime);
                    users.Add(user);
                }

            }

            if (users.Count == 0)
            {
                Console.WriteLine("No Viable users found in channel");
            }
            cancellationTokenSourceRecordingQueue = new CancellationTokenSource();
            cancellationTokenSourceSTTManager = new CancellationTokenSource();
        }

        public async Task StartQueuing()
        {
            //remember to change to 2000

            //Starts a task that will never end until a cancellation is called
            var recordTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    //Tries repeatedly to dequeue packet, if it is succesful, writes data to recording stream
                    AudioPacket packet;
                    bool notEmpty = packetQueue.TryDequeue(out packet);

                    if (notEmpty)
                    {
                        //If the time between when the last packet was received and the current packet is greater than 
                        //(arbitrary number, this might come back to bite me in the ass), then create an array of zeros 
                        //the length of the silence and add it to the recording stream. 

                        StreamUser? packetUser = GetUser(packet.User.Id);

                        if (packetUser == null)
                        {
                            //Fucking horrrible code i hope u die in ur sleep
                            AddUser((DiscordMember)packet.User, HelperMethods.getTime32());
                            packetUser = GetUser(packet.User.Id);
                            Console.WriteLine("I am a sign of success");
                        }

                        if (packet.TimeStamp - packetUser.lastPacketReceived > 100)
                        {
                            packetUser.recordingStream.AddSilenceToStream(packet.TimeStamp - packetUser.lastPacketReceived);
                        }

                        Buffer.BlockCopy(packet.PCMdata.ToArray(), 0, resampleStream.SourceBuffer, 0, packet.PCMdata.Length);
                        int sourceBytesConverted = 0;
                        var convertedBytes = resampleStream.Convert(packet.PCMdata.Length, out sourceBytesConverted);
                        byte[] converted = new byte[convertedBytes];
                        Buffer.BlockCopy(resampleStream.DestBuffer, 0, converted, 0, convertedBytes);

                        //Saves the packet as the last packet received, then writes the packet data to the recordingstream 
                        packetUser.lastPacketReceived = packet.TimeStamp;
                        packetUser.recordingStream.writeToStream(packet.PCMdata);

                        if (packetUser.state == UserStates.WAITING_FOR_TRIGGER)
                        {
                            packetUser.triggerStream.writeToStream(converted);
                        }
                        else if (packetUser.state == UserStates.TRIGGERED)
                        {
                            packetUser.commandStream.writeToStream(converted);
                        }


                    }
                }
            }, cancellationTokenSourceRecordingQueue.Token).Unwrap();

            var triggerTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    Thread.Sleep(1000);

                    byte[] audio;
                    int fullAudioLength;
                    if (triggered)
                    {
                        audio = commandUser.commandStream.orderStream();
                        fullAudioLength = audio.Length;
                    }
                    else
                    {

                        int singleAudioLength = users[0].triggerStream.maxLength;
                        fullAudioLength = singleAudioLength * users.Count;
                        audio = new byte[fullAudioLength];
                        for (int i = 0; i < users.Count; i++)
                        {
                            Console.WriteLine(users[i].member.Nickname);
                            Buffer.BlockCopy(users[i].triggerStream.orderStream(), 0, audio, singleAudioLength * i, singleAudioLength);
                        }

                    }

                    //Console.WriteLine(audio.Length == audioLength * 2);

                    short[] sdata;
                    sdata = new short[(int)Math.Ceiling(fullAudioLength / 2.0)];
                    Buffer.BlockCopy(audio, 0, sdata, 0, fullAudioLength);

                    //string speech = sttClient.SpeechToText(sdata, Convert.ToUInt32(sdata.Length));
                    Metadata metadata = sttClient.SpeechToTextWithMetadata(sdata, Convert.ToUInt32(sdata.Length), 1);
                    TokenMetadata[] tokens = metadata.Transcripts[0].Tokens;
                    string speech = HelperMethods.convertTokensToString(tokens);

                    Console.WriteLine(speech);

                    //Will probably have to change to check for the running transcript to make sure it doesnt trigger twice
                    if (!triggered)
                    {
                        Console.WriteLine("NOT TRIGGERED");
                        int triggerIndex = speech.IndexOf("discord");
                        if (triggerIndex >= 0)
                        {
                            string joinSoundPath = "AudioFiles\\wereLive.wav";
                            await HelperMethods.sendAudioToChannel(context, joinSoundPath);


                            TokenMetadata triggerToken = tokens[triggerIndex];
                            Console.WriteLine($"{triggerToken.StartTime * 1000.0f} / {(float)users[0].triggerStream.maxLengthMS}");
                            Console.WriteLine((int)(triggerToken.StartTime * 1000.0f / (float)users[0].triggerStream.maxLengthMS));

                            int userIndex = (int)(triggerToken.StartTime * 1000.0f / ((float)users[0].triggerStream.maxLengthMS)) /* *users.Count*/;
                            commandUser = users[userIndex];
                            commandUser.state = UserStates.TRIGGERED;
                            //Done so the trigger phrase wont be repeated, kinda sucks tho lmao. Expect performance drops
                            commandUser.triggerStream.FillStreamWithSilence();

                            await context.Channel.SendMessageAsync(commandUser.member.DisplayName);
                            triggered = true;
                        }

                        //Can probably use some math to get the transcripts just uhhhhh ignore for now
                        //users[0].runningTranscript = speech;
                    }
                    else
                    {

                        await checkCommands(speech);
                        attempts++;

                    }

                    if (attempts >= 7)
                    {
                        string joinSoundPath = "AudioFiles\\noCommand.wav";
                        await HelperMethods.sendAudioToChannel(context, joinSoundPath);

                        commandUser.commandStream.FillStreamWithSilence();
                        commandUser.state = UserStates.WAITING_FOR_TRIGGER;
                        triggered = false;
                        attempts = 1;
                    }

                }

            }, cancellationTokenSourceSTTManager.Token).Unwrap();

        }

        public async Task StopQueuing()
        {
            //Updates the cancellation token so that the continuous task will stop running
            cancellationTokenSourceRecordingQueue.Cancel();
            cancellationTokenSourceSTTManager.Cancel();
            packetQueue.Clear();

        }


        public async Task checkCommands(string speech)
        {
            Console.WriteLine(speech);
            if (speech.Contains("record") && speech.Contains("that"))
            {

                Command? recordCommand = context.CommandsNext.FindCommand("record", out _);
                await recordCommand.ExecuteAsync(context);

                commandUser.commandStream.FillStreamWithSilence();
                commandUser.state = UserStates.WAITING_FOR_TRIGGER;
                triggered = false;
                attempts = 1;
            }


        }
        public void DisposeStreams(bool final)
        {

            if (!final)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    users[i].recordingStream.Dispose();
                    users[i].recordingStream.Initialize();
                }
            }
            else
            {
                for (int i = 0; i < users.Count; i++)
                {
                    users[i].recordingStream.Dispose();
                }
            }
        }

        public void AddPacketToQueue(AudioPacket packet)
        {
            packetQueue.Enqueue(packet);
        }

        public void AddUser(DiscordMember user, int timeStamp)
        {
            Console.WriteLine("Check 1");
            StreamUser streamUser = new StreamUser(user, timeStamp);
            Console.WriteLine("Check 2");
            Stream joinSound = HelperMethods.convertAudioToPCM("C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\AudioFiles\\join.wav", 1);
            Console.WriteLine("Check 3");
            //Console.WriteLine(((timeStamp - globalStart) % HelperMethods.maxLengthMS));
            streamUser.recordingStream.AddSilenceToStream(((timeStamp - globalStart) % HelperMethods.maxLengthMS));
            Console.WriteLine("Check 4");
            int joinSoundLength = streamUser.recordingStream.AddAudioToStream(joinSound);
            Console.WriteLine("Check 5");
            streamUser.lastPacketReceived = HelperMethods.getTime32();
            //TotalTime since start % full length of stream - length of Join sound effect
            Console.WriteLine("Check 6");


            users.Add(streamUser);
            Console.WriteLine("Check 7");
        }

        public void AddUser(DiscordMember user, int timeStamp, out StreamUser streamer)
        {
            StreamUser streamUser = new StreamUser(user, timeStamp);
            Stream joinSound = HelperMethods.convertAudioToPCM("C:\\Users\\cojac\\source\\repos\\BotPlzWork\\BotPlzWork\\bin\\x64\\Debug\\net6.0\\AudioFiles\\join.wav", 1);

            streamUser.recordingStream.AddSilenceToStream(((timeStamp - globalStart) % HelperMethods.maxLengthMS));
            int joinSoundLength = streamUser.recordingStream.AddAudioToStream(joinSound);
            streamUser.lastPacketReceived = HelperMethods.getTime32();

            streamer = streamUser;
            users.Add(streamUser);

        }

        public void RemoveUser(DiscordMember user)
        {
            try
            {
                users.Remove(GetUser(user.Id));
            }
            catch (NullReferenceException)
            {
                Console.WriteLine($"No user with ID {user.Id} exists");
            }
        }

        private StreamUser? GetUser(ulong id)
        {
            StreamUser? user = null;
            for (int i = 0; i < users.Count; i++)
            {
                if (id == users[i].member.Id)
                {
                    user = users[i];
                    break;
                }
            }
            return user;
        }
    }
}
