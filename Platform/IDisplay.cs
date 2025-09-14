using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator.Platform
{
    public interface IDisplay
    {
        void Clear();
        void Render(bool[,] buffer); // 64x32
    }
}
