// Program.cs
using Chip8Emulator.Core;
using Chip8Emulator.Platform;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        IDisplay display = new ConsoleDisplay();
        IKeyboard keyboard = new ConsoleKeyboard();
        IAudio audio = new NoAudio();

        var chip8 = new Chip8(display, keyboard, audio);
        chip8.Reset();

        // pick a ROM
        var romPath = "C:\\Users\\chidi\\Desktop\\Emulators\\Chip8Emulator\\Roms\\glitchGhost.ch8";
        if (!File.Exists(romPath))
        {
            Console.WriteLine($"ROM not found: {romPath}");
            return;
        }

        // load bytes into memory
        chip8.LoadRom(File.ReadAllBytes(romPath));


        // If you have a rom: var rom = File.ReadAllBytes("Roms/ibm_logo.ch8");
        // chip8.LoadRom(rom);

        // For now, no ROM: the VM will just clear the screen when told.
        // Timing: ~700 instructions per second, timers at 60 Hz
        const int instructionsPerFrame = 12;          // 12 * 60 ≈ 720 instr/sec
        var sw = Stopwatch.StartNew();
        long nextTimerTick = 0;

        Console.CursorVisible = false;
        while (true)
        {
            // Run some instructions
            chip8.step(instructionsPerFrame);

            // 60 Hz timer tick
            long ticksPerFrame = TimeSpan.FromSeconds(1.0 / 60).Ticks;
            while (sw.ElapsedTicks >= nextTimerTick + ticksPerFrame)
            {
                nextTimerTick += ticksPerFrame;
                chip8.TickTimers();
            }

            // Small sleep to avoid maxing CPU (coarse pacing)

            Thread.Sleep(1);
        }
    }
}
