using STTClient;
using STTClient.Interfaces;
using STTClient.Models;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using System.Threading.Tasks;
using DSharpPlus;
using Newtonsoft.Json.Linq;

namespace BotPlzWork
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BotHandler bot = new BotHandler();
        }
    }
}