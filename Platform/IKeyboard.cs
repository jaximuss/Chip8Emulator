using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public interface IKeyboard
    {
        bool IsPressed(byte key);  // 0x0..0xF
        byte WaitKey();            // blocking until a key in 0..F is pressed
    }
}
