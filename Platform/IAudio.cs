using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public interface IAudio
    {
        void Beep(bool on)
        {
            Console.Title = on ? "CHIP-8: BEEPING…" : "CHIP-8";
        }
    }
}
