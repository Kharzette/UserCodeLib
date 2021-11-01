using System;

namespace UserCodeLib
{
	internal class CPU
	{
		Registers	mRegs;

		OS	mOS;

		const int		Num16Regs	=12;
		const int		Num32Regs	=24;
		const int		Num64Regs	=32;
		const UInt32	ExeMagic	=0xF00CF00D;	//exe marker


		internal CPU(OS os)
		{
			mOS	=os;

			mRegs	=new Registers();

			mRegs.Init(Num16Regs, Num32Regs, Num64Regs);
		}


		internal void RunCode(Ram code)
		{
			UInt32	exeMagic	=code.ReadDWord();
			if(exeMagic != ExeMagic)
			{
				mOS.Print("Bad executable.");
				return;
			}
			
		}
	}
}