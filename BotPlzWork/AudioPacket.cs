using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPlzWork
{
    internal struct AudioPacket
    {
        public ReadOnlyMemory<byte> PCMdata;
        public DiscordUser User;
        public int TimeStamp;
    }
}
