using System;
using System.Collections.Generic;


namespace UserCodeLib
{
	internal class CPU
	{
		Registers	mRegs;

		Ram	mCurCodePage;

		OS	mOS;

		delegate void Instruction(byte instruction, byte args, UInt64 src, UInt64 dst, Ram data);

		Dictionary<byte, Instruction>	mInstructionTable	=new Dictionary<byte, Instruction>();

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

			mInstructionTable.Add(0, Mov);
			mInstructionTable.Add(1, AddrOf);
			mInstructionTable.Add(2, Add);
		}


		internal void RunCode(Ram code)
		{
			code.SetPointer(0);

			UInt32	exeMagic	=code.ReadDWord();
			if(exeMagic != ExeMagic)
			{
				mOS.Print("Bad executable.");
				return;
			}

			mCurCodePage	=code;

			//sizes stored at the top
			UInt64	exeSize		=code.ReadQWord();
			UInt64	dataSize	=code.ReadQWord();

			//alloc a data page for the exe
			Ram	data;
			mOS.Alloc((UInt16)dataSize, AddressSpace.SpaceTypes.Data,
						"Program data", out data);

			for(;;)
			{
				byte	instruction	=code.ReadByte();

				byte	args	=code.ReadByte();

				byte	srcArg	=(byte)(args & 0xF);
				byte	dstArg	=(byte)(args & 0xF0);

				UInt64	src	=0;
				if(srcArg != 0)
				{
					if(srcArg == SrcRegister || srcArg == SrcRegPointer)
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
					if(dstArg == DstRegister || dstArg == DstRegPointer)
					{
						dst	=code.ReadByte();
					}
					else
					{
						dst	=ReadExeValue(code);
					}
				}

				mInstructionTable[instruction](instruction, args, src, dst, data);

				//out of bounds?
				if(code.GetPointer() >= exeSize)
				{
					break;
				}
			}
		}


		UInt64 GetDstAddress(UInt64 dst, byte args, Ram data)
		{
			if((args & 0xF0) == DstPointer)
			{
				return	dst;
			}
			else if((args & 0xF0) == DstRegPointer)
			{
				return	mRegs.Get16((int)dst);
			}
			else if((args & 0xF0) == DstVariable)
			{
				return	dst;
			}
			return	0xFFFFFFFFFFFFFFFF;
		}


		UInt64 GetDstValue(UInt64 dst, byte args, Ram data)
		{
			if((args & 0xF0) == DstRegister)
			{
				return	mRegs.Get16((int)dst);
			}
			else if((args & 0xF0) == DstPointer)
			{
				data.SetPointer(dst);
				return	data.ReadWord();
			}
			else if((args & 0xF0) == DstLabel || (args & 0xF0) == DstNumber
					|| (args & 0xF0) == DstVariable)
			{
				return	dst;
			}
			else if((args & 0xF0) == DstRegPointer)
			{
				data.SetPointer(mRegs.Get16((int)dst));
				return	data.ReadWord();
			}
			return	0xFFFFFFFFFFFFFFFF;
		}


		UInt64 GetSrcValue(UInt64 src, byte args, Ram data)
		{
			//get src val
			UInt16	srcVal	=0;
			if((args & 0xF) == SrcRegister)
			{
				srcVal	=mRegs.Get16((int)src);
			}
			else if((args & 0xF) == SrcPointer || (args & 0xF) == SrcVariable)
			{
				data.SetPointer(src);
				srcVal	=data.ReadWord();
			}
			else if((args & 0xF) == SrcLabel || (args & 0xF) == SrcNumber)
			{
				srcVal	=(UInt16)src;
			}
			else if((args & 0xF) == SrcRegPointer)
			{
				data.SetPointer(mRegs.Get16((int)src));
				srcVal	=data.ReadWord();
			}

			return	srcVal;
		}


		void WriteDst(UInt64 val, byte args, UInt64 dst, Ram data)
		{
			if((args & 0xF0) == DstRegister)
			{
				mRegs.Set16((int)dst, (UInt16)val);
			}
			else if((args & 0xF0) == DstLabel)
			{
				//self mod code!  Tricksy!
				//save / restore current pointer
				UInt64	cur	=mCurCodePage.GetPointer();

				mCurCodePage.SetPointer(dst);
				mCurCodePage.WriteWord((UInt16)val);

				mCurCodePage.SetPointer(cur);
			}
			else	//pointer, number
			{
				data.SetPointer(GetDstAddress(dst, args, data));
				data.WriteWord((UInt16)val);
			}
		}


		void Mov(byte instruction, byte args, UInt64 src, UInt64 dst, Ram data)
		{
			UInt64	srcVal	=GetSrcValue(src, args, data);

			WriteDst(srcVal, args, dst, data);
		}


		void AddrOf(byte instruction, byte args, UInt64 src, UInt64 dst, Ram data)
		{
			if((args & 0x0F) != SrcVariable)
			{
				return;	//only makes sense for variable
			}
			WriteDst(src, args, dst, data);
		}


		void Add(byte instruction, byte args, UInt64 src, UInt64 dst, Ram data)
		{
			UInt64	srcVal	=GetSrcValue(src, args, data);
			UInt64	dstVal	=GetDstValue(dst, args, data);

			WriteDst(srcVal + dstVal, args, dst, data);
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