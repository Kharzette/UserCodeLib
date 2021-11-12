using System;
using System.Collections.Generic;


namespace UserCodeLib
{
	internal class CPU
	{
		Registers	mRegs;
		Flags		mFlags	=new Flags();

		Ram	mCurCodePage;

		OS	mOS;

		delegate void Instruction(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data);

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
		const byte		SrcVariable		=6;				//ex: mov i, reg00
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
			mInstructionTable.Add(3, Mul);
			mInstructionTable.Add(4, IMul);
			mInstructionTable.Add(5, Div);
			mInstructionTable.Add(6, IDiv);
			mInstructionTable.Add(7, Inc);
			mInstructionTable.Add(8, Dec);
			mInstructionTable.Add(9, Neg);
			mInstructionTable.Add(10, Not);
			mInstructionTable.Add(11, Xor);
			mInstructionTable.Add(12, Or);
			mInstructionTable.Add(13, And);
			mInstructionTable.Add(14, Tst);
			mInstructionTable.Add(15, Cmp);
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

				UInt16	src	=0;
				if(srcArg != 0)
				{
					if(srcArg == SrcRegister || srcArg == SrcRegPointer)
					{
						src	=code.ReadByte();
					}
					else
					{
						src	=code.ReadWord();
					}
				}

				UInt16	dst	=0;
				if(dstArg != 0)
				{
					if(dstArg == DstRegister || dstArg == DstRegPointer)
					{
						dst	=code.ReadByte();
					}
					else
					{
						dst	=code.ReadWord();
					}
				}

				UInt16	opt	=0;
				if(instruction >= 5 && instruction <= 6)
				{
					//div or idiv
					opt	=code.ReadByte();
				}

				mInstructionTable[instruction](args, dst, src, opt, data);

				//out of bounds?
				if(code.GetPointer() >= exeSize)
				{
					break;
				}
			}
		}


		UInt16 GetDstAddress(UInt16 dst, byte args, Ram data)
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
			return	0xFFFF;
		}


		UInt16 GetDstValue(UInt16 dst, byte args, Ram data)
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
			return	0xFFFF;
		}


		UInt16 GetSrcValue(UInt16 src, byte args, Ram data)
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
				srcVal	=src;
			}
			else if((args & 0xF) == SrcRegPointer)
			{
				data.SetPointer(mRegs.Get16((int)src));
				srcVal	=data.ReadWord();
			}

			return	srcVal;
		}


		void WriteDst(UInt16 val, byte args, UInt16 dst, Ram data)
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
				mCurCodePage.WriteWord(val);

				mCurCodePage.SetPointer(cur);
			}
			else	//pointer, number
			{
				data.SetPointer(GetDstAddress(dst, args, data));
				data.WriteWord(val);
			}
		}


		void Mov(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);

			WriteDst(srcVal, args, dst, data);
		}


		void AddrOf(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			if((args & 0x0F) != SrcVariable)
			{
				return;	//only makes sense for variable
			}
			WriteDst(src, args, dst, data);
		}


		void Add(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal + dstVal), args, dst, data);
			mFlags.FlagAdd(dstVal, srcVal);
		}


		void Mul(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);
			mFlags.FlagMul(dstVal, srcVal);
		}


		void IMul(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			Int16	srcVal	=(Int16)GetSrcValue(src, args, data);
			Int16	dstVal	=(Int16)GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);
			mFlags.FlagIMul(dstVal, srcVal);
		}


		void Div(byte args, UInt16 dst, UInt16 src, UInt16 remainder, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);

			mRegs.Set16(remainder, (UInt16)(dstVal % srcVal));
		}


		void IDiv(byte args, UInt16 dst, UInt16 src, UInt16 remainder, Ram data)
		{
			Int16	srcVal	=(Int16)GetSrcValue(src, args, data);
			Int16	dstVal	=(Int16)GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);

			mRegs.Set16(remainder, (UInt16)(dstVal % srcVal));
		}


		void Inc(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			dstVal++;

			WriteDst(dstVal, args, dst, data);
			mFlags.FlagInc(dstVal);
		}


		void Dec(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			dstVal--;

			WriteDst(dstVal, args, dst, data);
			mFlags.FlagDec(dstVal);
		}


		void Neg(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			//TODO: test
			dstVal	=(UInt16)~dstVal;
			dstVal++;

			WriteDst(dstVal, args, dst, data);

			//I THINK this will work
			mFlags.FlagSubtract(0, dstVal);
		}


		void Not(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			//TODO: test
			dstVal	=(UInt16)~dstVal;
			
			WriteDst(dstVal, args, dst, data);
		}


		void Xor(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal ^ dstVal), args, dst, data);

			//should be the same as and
			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Or(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal | dstVal), args, dst, data);
			mFlags.FlagOr(dstVal, srcVal);
		}


		void And(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal & dstVal), args, dst, data);
			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Tst(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Cmp(byte args, UInt16 dst, UInt16 src, UInt16 opt, Ram data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			mFlags.FlagSubtract(dstVal, srcVal);
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