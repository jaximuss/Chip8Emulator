using Chip8Emulator.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Core
{
    public class Chip8
    {
        // === Public VM constants (useful for renderers/tests)
        public const int ScreenWidth = 64;
        public const int ScreenHeight = 32;
        public const int RAM = 4096;


        // CPU state
        public readonly byte[] V = new byte[16];      // V0..VF (VF is also carry/flag)
        public ushort I;                               // index register, points to the address for sprite or data
        public ushort PC;                     // program counter, starts at 0x200 and points to the current instruction
        public byte DelayTimer;                        // 60 Hz, ticks down to 0
        public byte SoundTimer;                        // 60 Hz, ticks down to 0, makes a sound when > 0



        // === Memory & stack
        public readonly byte[] Memory = new byte[RAM];
        private readonly ushort[] _stack = new ushort[16]; //  a call stack has 16 levels
        private byte _sp;                              // stack pointer, points to the topmost level of the stack

        // === Display buffer (monochrome) => this meas black is false, white is true
        // true = pixel on
        public readonly bool[,] Display = new bool[ScreenWidth, ScreenHeight];

        // === Random for RND opcode (CxNN)
        private readonly Random _rng = new();

        // === Host integrations (injected)
        private readonly IDisplay _display;
        private readonly IKeyboard _keyboard;
        private readonly IAudio _audio;


        public Chip8(IDisplay display, IKeyboard keyboard, IAudio audio)
        {
            _display = display;
            _keyboard = keyboard;
            _audio = audio;
        }
    }
}
