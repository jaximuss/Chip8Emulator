using Chip8Emulator.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public class ConsoleDisplay : IDisplay
    {
        public void Clear() => Console.Clear();

        public void Render(bool[,] buffer)
        {
            Console.SetCursorPosition(0, 0);
            for (int y = 0; y < Chip8.ScreenHeight; y++)
            {
                for (int x = 0; x < Chip8.ScreenWidth; x++)
                    Console.Write(buffer[x, y] ? '█' : ' ');
                Console.WriteLine();
            }
        }
    }
}
