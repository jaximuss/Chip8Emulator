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

        private int _programStart = 0x200;
        private int _programEnd = 0x200; // set after LoadRom

        public Chip8(IDisplay display, IKeyboard keyboard, IAudio audio)
        {
            _display = display;
            _keyboard = keyboard;
            _audio = audio;
        }
        // Reset VM to power-on state
        public void Reset()
        {
            Array.Clear(V, 0, V.Length);
            Array.Clear(Memory, 0, Memory.Length);
            Array.Clear(_stack, 0, _stack.Length);
            _sp = 0;
            I = 0;
            PC = 0x0200; // programs start at 0x200
            DelayTimer = 0;
            SoundTimer = 0;

            // Clear display buffer
            for (int x = 0; x < ScreenWidth; x++)
                for (int y = 0; y < ScreenHeight; y++)
                    Display[x, y] = false;

            // Load fontset into memory (0x000..)
            var font = Fonts.Bytes;
            Array.Copy(font, 0, Memory, 0x000, font.Length);
        }

        // Load a ROM into memory at 0x200
        public void LoadRom(byte[] rom)
        {
            if (rom == null || rom.Length == 0)
            {
                throw new ArgumentException("ROM is empty.");
            }

            if (rom.Length > Memory.Length - _programStart)
            {
                throw new ArgumentException("ROM too large.");
            }

            // we take the rom read it from the start postion and copy it to the memory
            Array.Copy(rom, 0, Memory, _programStart, rom.Length);
            PC = (ushort)_programStart;
            _programEnd = _programStart + rom.Length;
        }

        // Tick timers at 60Hz (call from host)
        public void TickTimers()
        {
            if (DelayTimer > 0) DelayTimer--;
            if (SoundTimer > 0)
            {
                SoundTimer--;
                _audio.Beep(SoundTimer > 0);
            }
        }

            
        public void step(int instructionCount = 1)
        {
            for (int i = 0; i < instructionCount; i++)
            {
                if (PC < _programStart || PC + 1 >= _programEnd)
                {
                    throw new InvalidOperationException(
                        $"PC out of bounds: 0x{PC:X4}, program 0x{_programStart:X4}..0x{_programEnd - 1:X4}");
                }
                // fetch opcode (big-endian) and advance PC by 2 this happens after we load the rom
                ushort opcode = (ushort)((Memory[PC] << 8) | Memory[PC + 1]);
                PC += 2;
                Console.WriteLine($"PC=0x{PC:X4}");
                Execute(opcode);
            }
        }
        // Decode & execute a single opcode
        private void Execute(ushort op)
        {
            // break into common fields
            ushort nnn = (ushort)(op & 0x0FFF);
            byte nn = (byte)(op & 0x00FF);
            byte n = (byte)(op & 0x000F);
            byte x = (byte)((op & 0x0F00) >> 8);
            byte y = (byte)((op & 0x00F0) >> 4);

            switch ((op & 0xF000) >> 12)
            {
                case 0x0:
                    if (op == 0x00E0) 
                    { 
                        OpCLS();// 00E0: clear screen
                    }           
                    else if (op == 0x00EE) 
                    {
                        OpRET();  // 00EE: return
                    }      
                    else 
                    { 
                        /* 0NNN ignored (RCA call) */ 
                    }
                    break;

                case 0x1:
                    OpJPtoNNN(nnn); // 1NNN: jump
                    break;                   
                case 0x2: 
                    OpCALLNNN(nnn);  // 2NNN: call
                    break;                
                case 0x3: 
                    OpSE_Vx_byte(x, nn);  // 3xNN: skip if Vx==NN
                    break;       
                case 0x4: 
                    OpSNE_Vx_byte(x, nn);  // 4xNN: skip if Vx!=NN
                    break;       
                case 0x6: 
                    OpLD_Vx_byte(x, nn); // 6xNN: Vx = NN
                    break;        
                case 0x7: 
                    OpADD_Vx_byte(x, nn);  // 7xNN: Vx += NN
                    break;       
                case 0xA: 
                    OpLD_I_addr(nnn);   // ANNN: I = NNN
                    break;          
                case 0xD: 
                    OpDRW(x, y, n);  // Dxyn: draw sprite
                    break;             
                // … we’ll add more gradually
                default:
                    throw new NotSupportedException($"Opcode 0x{op:X4} not implemented.");
            }
        }

        // === Opcode implementations (first batch) ===

        private void OpCLS()
        {
            for (int x = 0; x < ScreenWidth; x++)
                for (int y = 0; y < ScreenHeight; y++)
                    Display[x, y] = false;
            _display.Clear();
            _display.Render(Display);
        }//0E00

        private void OpRET()//00EE
        {
            _sp--;
            PC = _stack[_sp];
        }

        /// <summary>
        /// JUMP TO ADDRESS NNN
        /// </summary>
        /// <param name="addr"></param>
        private void OpJPtoNNN(ushort NNN)//1NNN
        {
            PC = NNN;
        }
        private void OpCALLNNN(ushort NNN)//2NNN
        {
            _stack[_sp] = PC;
            _sp++;
            PC = NNN;
        }

        private void OpSkipIfXEqualsNN(byte x, byte NN)//3XNN
        {
            if (V[x] == NN)
            {
                PC += 2;
            }
        }

        private void OpSkipIfXIsNotNN(byte x, byte NN)//4XNN
        {
            if (V[x] != NN)
            {
                PC += 2;
            }
        }
        private void OpSkipIfXEqualY(byte x, byte y)//5XY0
        {
            if (V[x] == V[y])
            {
                PC += 2;
            }
        }

        private void OpSetXtoN(byte x, byte NN)//6XNN
        {
            V[x] = NN;
        }

        private void OpAddNNtoX(byte x, byte NN)//7XNN
        {
            V[x] = (byte)((V[x] + NN) & 0xFF);
        }
        private void OpAssignXtoY(byte x, byte y)//8XY0
        {
            V[x] = V[y];
        }
        private void OpOrXandY(byte x, byte y)//8XY1 SET TO Vx OR Vy
        {
            V[x] = (byte)(V[x] | V[y]);
        }
        private void OpAndXandY(byte x, byte y)//8XY2 SET TO Vx AND Vy
        {
            V[x] = (byte)(V[x] & V[y]);
        }
        private void OpXorXandY(byte x, byte y)//8XY3 SET TO Vx XOR Vy
        {
            V[x] = (byte)(V[x] ^ V[y]);
        }
        private void OpAddYtoX(byte x, byte y)//8XY4 SET TO Vx + Vy, SET VF = CARRY
        {
            int sum = V[x] + V[y];
            V[0xF] = (byte)(sum > 0xFF ? 1 : 0); // set carry flag
            V[x] = (byte)(sum & 0xFF);
        }


        //SETS I TO NNN ADDRESS
        private void OpSetItoNNN(ushort NNN)//ANNN
        {
            I = NNN;
        }

        // Draw n-byte sprite at (Vx, Vy), set VF=collision
        private void OpDRW(byte xReg, byte yReg, byte height)
        {
            byte x = (byte)(V[xReg] % ScreenWidth);
            byte y = (byte)(V[yReg] % ScreenHeight);
            V[0xF] = 0;

            for (int row = 0; row < height; row++)
            {
                byte spriteByte = Memory[I + row];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((spriteByte & (0x80 >> bit)) != 0)
                    {
                        int px = (x + bit) % ScreenWidth;
                        int py = (y + row) % ScreenHeight;
                        bool old = Display[px, py];
                        bool @new = old ^ true; // XOR pixel
                        Display[px, py] = @new;
                        if (old && !@new) V[0xF] = 1; // collision
                    }
                }
            }
            _display.Render(Display);
        }//DXYN

    }
}
