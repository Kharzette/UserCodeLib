using System;
using System.IO;

namespace UserCodeLib
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Kompiler!");

			if(args.Length != 1)
			{
				Console.WriteLine("Usage Kompile sourceFile.whatever");
				return;
			}

			if(!File.Exists(args[0]))
			{
				Console.WriteLine("No such file: " + args[0]);
				return;
			}

			FileStream	fs	=new FileStream(args[0], FileMode.Open, FileAccess.Read);
			if(fs == null)
			{
				Console.WriteLine("Couldn't open file " + args[0]);
				return;
			}

			Screen	scr	=new Screen(Screen.Modes.Text, 80, 80);

			StreamReader	sr	=new StreamReader(fs);

			Compiler	cmp	=new Compiler(scr);

			//make memory handler
			Ram	mem	=new Ram();

			//this would normally be a unique id from unity or something
			UInt64	fakeModuleID	=0xDEADD00DBAD;

			//attach a physical memory module
			mem.AttachModule(fakeModuleID, 65535, true);			

			OS	os	=new OS(0, mem, fakeModuleID);

			RamChunk	proggy;
			if(!os.Alloc(8192, out proggy))
			{
				Console.WriteLine("Couldn't alloc ram for compiled code!");
				return;
			}

			cmp.Compile(sr, proggy);

			sr.Close();
			fs.Close();

			CPU	cpu	=new CPU(os);

			cpu.RunCode(proggy);
        }
    }
}
