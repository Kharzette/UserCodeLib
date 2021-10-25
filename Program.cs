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
			}

			Screen	scr	=new Screen(Screen.Modes.Text, 80, 80);

			StreamReader	sr	=new StreamReader(fs);

			Compiler	cmp	=new Compiler(scr);

			Ram	mem	=new Ram();

			mem.Init(8192, 0);	//8K 16 bit

			cmp.Compile(sr, mem);
        }
    }
}
