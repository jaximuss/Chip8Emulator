using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public class ConsoleKeyboard : IKeyboard
    {
        public bool IsPressed(byte key) => false; // we'll improve later

        public byte WaitKey()
        {
            // Map hex digits to keys 0-9, A-F via keyboard
            while (true)
            {
                ///EXPLAIN THIS LOGIC
                var k = Console.ReadKey(true).KeyChar;
                if (k >= '0' && k <= '9')
                {
                    return (byte)(k - '0');
                }

                if (k >= 'a' && k <= 'f')
                {
                    return (byte)(10 + (k - 'a'));
                }

                if (k >= 'A' && k <= 'F')
                {
                    return (byte)(10 + (k - 'A'));
                }
            }
        }
    }
}
