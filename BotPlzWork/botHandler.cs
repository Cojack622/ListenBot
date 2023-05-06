using BotPlzWork.Commands;
using BotPlzWork.ConfigClasses;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.CommandsNext;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace BotPlzWork
{
    internal class BotHandler
    {
        public DiscordClient client; 
        //Change to take in json file
        public BotHandler() {

            string config = File.ReadAllText("ConfigFiles\\BotConfig.txt");
            BotConfig? botConfig = JsonSerializer.Deserialize<BotConfig>(config);

            if(botConfig == null)
            {
                Console.WriteLine("Make a Config file first loser");
                return;
            }

            client = new DiscordClient(new DiscordConfiguration()
            {
                Token = botConfig.token_ID,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All,
                MinimumLogLevel = LogLevel.Debug
            });

            //Use dependecy injection for STT so that you can have a single instance of a STT manager
            client.UseVoiceNext(new VoiceNextConfiguration()
            {
                EnableIncoming = true
            });

            

            var serviceCollection = new ServiceCollection();
            //serviceCollection.AddSingleton(typeof(STT), sttClient);
            serviceCollection.AddSingleton(typeof(DiscordClient), client);
            var provider = serviceCollection.BuildServiceProvider();

            var commands = client.UseCommandsNext(new CommandsNextConfiguration()
            {
                //Change to load from json
                StringPrefixes = new[] { botConfig.prefix },
                Services = provider
            });
            commands.RegisterCommands<TextCommandModule>();
            commands.RegisterCommands<RecordingCommandModule>();

            client.Ready += onReady;

            run().GetAwaiter().GetResult();
        }

       
        private async Task onReady(DiscordClient s, ReadyEventArgs e)
        {
            Console.WriteLine("Ready!");
        }

        public async Task run()
        {
            await client.ConnectAsync();
            await Task.Delay(-1);
        }
        

    }
}
