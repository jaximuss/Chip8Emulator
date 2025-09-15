using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public class ConsoleKeyboard : IKeyboard
    {
        private readonly bool[] _state = new bool[16];

        private static readonly Dictionary<ConsoleKey, byte> Map = new()
    {
        { ConsoleKey.D1, 0x1 }, { ConsoleKey.D2, 0x2 }, { ConsoleKey.D3, 0x3 }, { ConsoleKey.D4, 0xC },
        { ConsoleKey.Q, 0x4 },  { ConsoleKey.W, 0x5 },  { ConsoleKey.E, 0x6 },  { ConsoleKey.R, 0xD },
        { ConsoleKey.A, 0x7 },  { ConsoleKey.S, 0x8 },  { ConsoleKey.D, 0x9 },  { ConsoleKey.F, 0xE },
        { ConsoleKey.Z, 0xA },  { ConsoleKey.X, 0x0 },  { ConsoleKey.C, 0xB },  { ConsoleKey.V, 0xF },
    };

        public bool IsPressed(byte key) => _state[key];

        // Call this frequently from your main loop to update key state without blocking.
        public void Pump()
        {
            while (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);
                if (Map.TryGetValue(keyInfo.Key, out var chipKey))
                {
                    // We don't get true "up" events in pure console apps.
                    // As a simple heuristic: toggle on keypress; optional timeout to clear later.
                    _state[chipKey] = true;
                }
            }
        }

        // Optional: clear a key (e.g., after a short delay)
        public void ClearAll() => Array.Fill(_state, false);

        // FX0A: blocking wait for one CHIP-8 key, using the same mapping
        public byte WaitKey()
        {
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (Map.TryGetValue(keyInfo.Key, out var chipKey))
                    return chipKey;
            }
        }

        public void DebugKeyTest()
        {
            Console.WriteLine("Press mapped keys (1-4, QWER, ASDF, ZXCV). Esc to quit.\n");

            while (true)
            {
                // Blocking read
                var ki = Console.ReadKey(intercept: true);

                if (ki.Key == ConsoleKey.Escape)
                    break;

                if (Map.TryGetValue(ki.Key, out var chipKey))
                {
                    Console.WriteLine($"{ki.Key,-8} -> CHIP-8 {chipKey:X} (hex {chipKey})");
                }
                else
                {
                    Console.WriteLine($"{ki.Key,-8} -> (not mapped)");
                }
            }
        }
    }
}
