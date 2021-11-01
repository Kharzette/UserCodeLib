using System;

namespace UserCodeLib
{
	internal class CPU
	{
		Registers	mRegs;

		OS	mOS;

		const int		Num16Regs		=12;
		const int		Num32Regs		=24;
		const int		Num64Regs		=32;
		const UInt32	ExeMagic		=0xF00CF00D;	//exe marker
		const byte		SrcRegister		=1;				//ex: mov reg00, reg01
		const byte		SrcPointer		=2;				//ex: mov [69], reg00
		const byte		SrcLabel		=3;				//ex: jmp label
		const byte		SrcNumber		=4;				//ex: mov 7, reg00
		const byte		SrcRegPointer	=5;				//ex: mov [reg00], reg01
		const byte		SrcVariable		=6;				//ex: mov i, reg00 Converts to SrcPointer after compile
		const byte		DstRegister		=SrcRegister << 4;
		const byte		DstPointer		=SrcPointer << 4;
		const byte		DstLabel		=SrcLabel << 4;
		const byte		DstNumber		=SrcNumber << 4;
		const byte		DstRegPointer	=SrcRegPointer << 4;
		const byte		DstVariable		=SrcVariable << 4;


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

			//alloc a data page for the exe
			Ram	data;
			mOS.Alloc(4096, AddressSpace.SpaceTypes.Ram, "Program data", out data);

			for(int lineNum=0;;lineNum++)
			{
				byte	instruction	=code.ReadByte();

				byte	args	=code.ReadByte();

				byte	srcArg	=(byte)(args & 0xF);
				byte	dstArg	=(byte)(args & 0xF0);

				UInt64	src	=0;
				if(srcArg != 0)
				{
					if(srcArg == SrcRegister)
					{
						src	=code.ReadByte();
					}
					else
					{
						src	=ReadExeValue(code);
					}
				}

				UInt64	dst	=0;
				if(dstArg != 0)
				{
					if(dstArg == DstRegister)
					{
						dst	=code.ReadByte();
					}
					else
					{
						dst	=ReadExeValue(code);
					}
				}

				ExecuteInstruction(instruction, args, src, dst);
			}
		}


		void ExecuteInstruction(byte instruction, byte args, UInt64 src, UInt64 dst)
		{

		}


		UInt64	ReadExeValue(Ram exe)
		{
			if(exe.Is16Bit())
			{
				return	exe.ReadWord();
			}
			else if(exe.Is32Bit())
			{
				return	exe.ReadDWord();
			}
			else if(exe.Is64Bit())
			{
				return	exe.ReadQWord();
			}
			return	0;
		}
	}
}