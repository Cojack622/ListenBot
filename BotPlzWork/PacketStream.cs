using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPlzWork
{
    internal class PacketStream
    {
        public MemoryStream audioStream { get; private set; }
        public int sampleRate;
        public int maxLengthMS;
        public int maxLength;
        public int oneMillisecondLength;

        public PacketStream(int lengthSeconds, int sampleRate)
        {
            audioStream = new MemoryStream();
            int multiplier = 48000 / sampleRate;

            maxLengthMS = lengthSeconds * 1000;
            oneMillisecondLength = HelperMethods.oneMillisecondLength / multiplier;
            maxLength = maxLengthMS * oneMillisecondLength;
            
        }

        public void writeToStream(ReadOnlyMemory<byte> PCMData)
        {
            if (canOverwrite(PCMData.Length))
            {
                byte[] firstChunk = PCMData.Slice(0, (int)(maxLength - audioStream.Length)).ToArray();
                byte[] secondChunk = PCMData.Slice((int)(maxLength - audioStream.Length)).ToArray();

                audioStream.Write(firstChunk, 0, firstChunk.Length);
                audioStream.Seek(0, SeekOrigin.Begin);
                audioStream.Write(secondChunk, 0, secondChunk.Length);

            }
            else
            {
                audioStream.Write(PCMData.ToArray(), 0, PCMData.Length);
            }
        }

        public void writeToStream(byte[] PCMArray)
        {
            if (canOverwrite(PCMArray.Length))
            {
                ReadOnlyMemory<byte> PCMData = PCMArray;
                byte[] firstChunk = PCMData.Slice(0, (int)(maxLength - audioStream.Length)).ToArray();
                byte[] secondChunk = PCMData.Slice((int)(maxLength - audioStream.Length)).ToArray();

                audioStream.Write(firstChunk, 0, firstChunk.Length);
                audioStream.Seek(0, SeekOrigin.Begin);
                audioStream.Write(secondChunk, 0, secondChunk.Length);

            }
            else
            {
                audioStream.Write(PCMArray, 0, PCMArray.Length);
            }
        }
        public byte[] readStream(int startingPosition, int seconds)
        {

            if ((startingPosition + seconds) * 1000 > maxLengthMS)
            {
                throw new ArgumentException("Took too much :( L Bozo");
            }
            //Done so data doesnt change while writing
            Console.WriteLine("Check 1");
            int position = (int)audioStream.Position;
            int length = seconds * 1000 * oneMillisecondLength;
            int startReadPosition = position + (startingPosition * 1000 * oneMillisecondLength);

            Console.WriteLine("Check 2");
            byte[] streamArray = audioStream.ToArray();
            byte[] orderedBuffer = new byte[length];
            

            if (canOverwrite(length, startReadPosition))
            {
                //byte[] firstChunk = new byte[maxLength - startingPosition];
                //audioStream.Read(firstChunk, (int)audioStream.Position, (int)(audioStream.Length - audioStream.Position));

                Console.WriteLine("Check 3A");
                Buffer.BlockCopy(streamArray, startReadPosition, orderedBuffer, 0, streamArray.Length - startReadPosition);
                Console.WriteLine("Check 3A-1");
                Buffer.BlockCopy(streamArray, 0, orderedBuffer, length - startReadPosition, length - (streamArray.Length - startReadPosition));
                Console.WriteLine("Check 3A-2");

            }
            else
            {
                Console.WriteLine("Check 3B");
                Buffer.BlockCopy(streamArray, startReadPosition, orderedBuffer, 0, orderedBuffer.Length);
            }

            Console.WriteLine("Check 4");
            return orderedBuffer;
        }


        public byte[] orderStream()
        {
            //Console.WriteLine("Gate 1");
            //Stabilizes values to single value juuuuuuuuuuuust incase they get changed halfway through method even tho this would like never happen i dont think idk
            int position = (int)audioStream.Position;
            int length = (int)audioStream.Length;
            if (position == 0 || position == maxLength || length < maxLength)
            {
                //Console.WriteLine("Special Gate A");
                return audioStream.ToArray();
            }
            else
            {
                
                byte[] firstChunk = new byte[maxLength - position];
                byte[] secondChunk = new byte[position];

                //audioStream.Read(firstChunk, position, firstChunk.Length);
                //audioStream.Read(secondChunk, 0, secondChunk.Length);
                //Console.WriteLine("Gate 2");
                //Testing bc the read method wont work (wtf why)
                byte[] unordered = audioStream.ToArray();
                for (int i = 0; i < position; i++)
                {
                    secondChunk[i] = unordered[i];
                }
                for (int i = position; i < unordered.Length; i++)
                {
                    firstChunk[i - position] = unordered[i];
                }



                byte[] fullAudio = new byte[(int)audioStream.Length];
                firstChunk.CopyTo(fullAudio, 0);
                secondChunk.CopyTo(fullAudio, firstChunk.Length);
                //Console.WriteLine("Gate 3");
                return fullAudio;
            }
        }

        public void AddSilenceToStream(int lengthTime)
        {
            int zeroArraySize = oneMillisecondLength * lengthTime;
            if (lengthTime > maxLengthMS)
            {
                //zeroArraySize = (maxLength - (int)audioStream.Position) + maxLength;
                zeroArraySize = maxLength;
            }
            //Since c# initializes array to 0s, just declare the array 
            byte[] zeroBuffer = new byte[zeroArraySize];
            writeToStream(new ReadOnlyMemory<byte>(zeroBuffer));
        }

        public void FillStreamWithSilence()
        {
            audioStream.Position = 0;
            AddSilenceToStream(maxLengthMS);
        }
        public int AddAudioToStream(Stream audio)
        {
            MemoryStream audio2Memory = new MemoryStream();
            audio.CopyTo(audio2Memory);
            writeToStream(new ReadOnlyMemory<byte>(audio2Memory.ToArray()));
            return (int)audio2Memory.Length;
        }

        public void Initialize()
        {
            audioStream = new MemoryStream();
        }

        public void Dispose()
        {
            audioStream.Dispose();
        }

        public long Length()
        {
            return audioStream.Length;
        }

        private bool canOverwrite(int length)
        {
            return (length + audioStream.Position) > maxLength;
        }

        private bool canOverwrite(int length, int position)
        {
            return length + position > maxLength;
        }

        

    }
}
