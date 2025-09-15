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
            // ... clear state ...
            PC = (ushort)_programStart;
            _programEnd = _programStart; // empty until we load    
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

            // If no program, just return (don’t throw)
            if (_programEnd <= _programStart) return;

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
                //Console.WriteLine($"PC=0x{PC:X4}");
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
                    OpSkipIfXEqualsNN(x, nn);  // 3xNN: skip if Vx==NN
                    break;       
                case 0x4:
                    OpSkipIfXIsNotNN(x, nn);  // 4xNN: skip if Vx!=NN
                    break;    
                case 0x5:
                        OpSkipIfXEqualY(x, y); // 5xy0: skip if Vx==Vy
                    break;
                case 0x6:
                    OpSetXtoN(x, nn); // 6xNN: Vx = NN
                    break;        
                case 0x7:
                    OpAddNNtoX(x, nn);  // 7xNN: Vx += NN
                    break;
                //arithematic and logic operations
                case 0x8:
                    switch (op & 0x000F)
                    {
                        case 0x0:
                            OpAssignXtoY(x, y); // 8xy0: Vx = Vy
                            break;          
                        case 0x1:
                            OpOrXandY(x, y); // 8xy1: Vx = Vx OR Vy
                            break;          
                        case 0x2:
                            OpAndXandY(x, y); // 8xy2: Vx = Vx AND Vy
                            break;          
                        case 0x3:
                            OpXorXandY(x, y); // 8xy3: Vx = Vx XOR Vy
                            break;          
                        case 0x4:
                            OpAddYtoX(x, y); // 8xy4: Vx += Vy, set VF=carry
                            break;          
                        case 0x5:
                            OpSubYfromX(x, y); // 8xy5: Vx -= Vy, set VF=NOT borrow
                            break;          
                        case 0x6:
                            OpShiftXRight(x); // 8xy6: Vx >>= 1, set VF=LSB before shift
                            break;          
                        case 0x7:
                            OpSubXfromY(x, y); // 8xy7: Vx = Vy - Vx, set VF=NOT borrow
                            break; 
                        case 0xE:
                            OpShiftXLeft(x); // 8xyE: Vx <<= 1, set VF=MSB before shift
                            break;
                        default:
                            throw new NotSupportedException($"Opcode 0x{op:X4} not implemented.");
                    }
                    break;
                case 0x9:
                    OPIfXIsNotY(x, y); // 9xy0: skip if Vx != Vy
                    break;
                case 0xA:
                    OpSetItoNNN(nnn);   // ANNN: I = NNN
                    break;          
                case 0xB:
                    JumpToAddressInNnN(nnn); // BNNN: jump to V0 + NNN
                    break;
                case 0xC:
                    RandomByteInX(x, nn); // CXNN: Vx = random byte AND NN
                    break;
                case 0xD: 
                    OpDRW(x, y, n);  // Dxyn: draw sprite
                    break;             
                case 0xE:
                    if ((op & 0x00FF) == 0x9E)
                    {
                        SkipKeyIfinX(x); // Ex9E: skip if key Vx pressed
                    }
                    else if ((op & 0x00FF) == 0xA1)
                    {
                        SkipKeyIfNotinX(x); // ExA1: skip if key Vx not pressed
                    }
                    else
                    {
                        throw new NotSupportedException($"Opcode 0x{op:X4} not implemented.");
                    }
                    break;
                case 0xF:
                    switch (op & 0x00FF)
                    {
                        case 0x0A:
                            KeyPressAwaited(x); // Fx0A: wait for key press, store in Vx
                            break;
                        case 0x1E:
                            AddXtoI(x); // Fx1E: I += Vx
                            break;
                        case 0x07: 
                            SetXToDelayTimer(x); // Fx07: Vx = delay timer
                            break;
                        case 0x15:
                            SetDelayTimerToX(x); // Fx15: delay timer = Vx
                            break;
                        case 0x18:
                            SetSoundTimerToX(x); // Fx18: sound timer = Vx
                            break;
                        case 0x29:
                            OpSetIToSpriteInX(x); // Fx29: I = location of sprite for digit Vx
                            break;
                        case 0x33:
                            OpSetIToBcdOfX(x); // Fx33: store BCD of Vx in I, I+1, I+2
                            break;
                        case 0x65:
                            FillsV0toXFromI(x); // Fx65: fill V0..Vx from memory starting at I
                            break;
                        case 0x55:
                            FillIFromV0toX(x); // Fx55: fill memory starting at I from V0..Vx
                            break;
                        // More Fx** opcodes to be implemented
                        default:
                            throw new NotSupportedException($"Opcode 0x{op:X4} not implemented.");
                    }
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
        private void OpSubYfromX(byte x, byte y)//8XY5 SET TO Vx - Vy, SET VF = NOT BORROW
        {
            V[0xF] = (byte)(V[x] >= V[y] ? 1 : 0); // set didnt borrow flag, if theres borrow it sets to 0
            V[x] = (byte)((V[x] - V[y]) & 0xFF);
        }
        private void OpShiftXRight(byte x)//8XY6 SET TO Vx SHR 1, SET VF TO LSB BEFORE SHIFT
        {
            V[0xF] = (byte)(V[x] & 0x1); // set LSB flag
            V[x] = (byte)(V[x] >> 1);
        }
        private void OpSubXfromY(byte x, byte y)//8XY7 SET TO Vy - Vx, SET VF = NOT BORROW
        {
            V[0xF] = (byte)(V[y] >= V[x] ? 1 : 0); // set didnt borrow flag, if theres borrow it sets to 0
            V[x] = (byte)((V[y] - V[x]) & 0xFF);
        }
        private void OpShiftXLeft(byte x)//8XYE SET TO Vx SHL 1, SET VF TO MSB BEFORE SHIFT
        {
            V[0xF] = (byte)((V[x] & 0x80) >> 7); // set MSB flag
            V[x] = (byte)((V[x] << 1) & 0xFF);
        }
        private void OPIfXIsNotY(byte x, byte y)//9XY0
        {
            if (V[x] != V[y])
            {
                PC += 2;
            }
        }


        //SETS I TO NNN ADDRESS
        private void OpSetItoNNN(ushort NNN)//ANNN
        {
            I = NNN;
        }
        private void JumpToAddressInNnN(ushort NNN)//BNNN
        {
            PC = (ushort)((V[0] + NNN) & 0xFFF); // wrap around 0xFFF throw away the first 4 bits
        }
        private void RandomByteInX(byte x, byte NN)//CXNN
        {
            V[x] = (byte)(_rng.Next(0, 256) & NN);
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

        private void SkipKeyIfinX(byte x)//EX9E skip the next instruction if the key stored in Vx is pressed
        {
            if (_keyboard.IsPressed(V[x]))
            {
                PC += 2;
            }
        }

        private void SkipKeyIfNotinX(byte x)//EXA1 skip the next instruction if the key stored in Vx is not pressed
        {
            if (!_keyboard.IsPressed(V[x]))
            {
                PC += 2;
            }
        }
        private void KeyPressAwaited(byte x)//FX0A
        {
            V[x] = _keyboard.WaitKey();
        }
        private void SetXToDelayTimer(byte x)//FX07
        {
            V[x] = DelayTimer;
        }
        private void SetDelayTimerToX(byte x)//FX15
        {
            DelayTimer = V[x];
        }
        private void SetSoundTimerToX(byte x)//FX18
        {
            SoundTimer = V[x];
        }
        private void AddXtoI(byte x)//FX1E
        {
            I = (ushort)((I + V[x]) & 0xFFF); // wrap around 0xFFF throw away the first 4 bits
        }
        private void OpSetIToSpriteInX(byte x)//FX29
        {
            I = (ushort)(V[x] * 5); // each sprite is 5 bytes long, sprites start at 0x000
        }
        private void OpSetIToBcdOfX(byte x)//FX33
        {
            byte value = V[x];
            Memory[I + 2] = (byte)(value % 10); // ones
            value /= 10;
            Memory[I + 1] = (byte)(value % 10); // tens
            value /= 10;
            Memory[I] = (byte)(value % 10);     // hundreds
        }
        private void FillIFromV0toX(byte x)//FX55
        {
            for (int i = 0; i <= x; i++)
            {
                Memory[I + i] = V[i]; //fills memory starting at address I with values from V0 to Vx
            }
        }
        private void FillsV0toXFromI(byte x)//FX65
        {
            for (int i = 0; i <= x; i++)
            {
                V[i] = Memory[I + i];  //fill V0 to Vx with values from memory starting at address I
            }
        }
       

    }
}
