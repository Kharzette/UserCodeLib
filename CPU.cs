using System;
using System.Collections.Generic;


namespace UserCodeLib
{
	internal class CPU
	{
		Registers	mRegs;
		Flags		mFlags	=new Flags();

		RamChunk	mCurCodePage;

		OS	mOS;

		delegate void Instruction(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data);

		Dictionary<byte, Instruction>	mInstructionTable	=new Dictionary<byte, Instruction>();

		const int		Num16Regs		=12;
		const int		Num32Regs		=24;
		const int		Num64Regs		=32;
		const UInt32	ExeMagic		=0xF00CF00D;	//exe marker
		const byte		DstRegister		=1;				//ex: mov reg00, reg01
		const byte		DstPointer		=2;				//ex: mov [69], reg00
		const byte		DstLabel		=3;				//ex: jmp label
		const byte		DstNumber		=4;				//ex: mov 7, reg00
		const byte		DstRegPointer	=5;				//ex: mov [reg00], reg01
		const byte		DstVariable		=6;				//ex: mov i, reg00
		const byte		SrcRegister		=DstRegister << 4;
		const byte		SrcPointer		=DstPointer << 4;
		const byte		SrcLabel		=DstLabel << 4;
		const byte		SrcNumber		=DstNumber << 4;
		const byte		SrcRegPointer	=DstRegPointer << 4;
		const byte		SrcVariable		=DstVariable << 4;


		internal CPU(OS os)
		{
			mOS	=os;

			mRegs	=new Registers();

			mRegs.Init(Num16Regs, Num32Regs, Num64Regs);

			byte	idx	=0;
			mInstructionTable.Add(idx++, Mov);
			mInstructionTable.Add(idx++, AddrOf);
			mInstructionTable.Add(idx++, Add);
			mInstructionTable.Add(idx++, Mul);
			mInstructionTable.Add(idx++, IMul);
			mInstructionTable.Add(idx++, Div);
			mInstructionTable.Add(idx++, IDiv);
			mInstructionTable.Add(idx++, Inc);
			mInstructionTable.Add(idx++, Dec);
			mInstructionTable.Add(idx++, Neg);
			mInstructionTable.Add(idx++, Not);
			mInstructionTable.Add(idx++, Xor);
			mInstructionTable.Add(idx++, Or);
			mInstructionTable.Add(idx++, And);
			mInstructionTable.Add(idx++, Tst);
			mInstructionTable.Add(idx++, Cmp);
			mInstructionTable.Add(idx++, Jmp);
			mInstructionTable.Add(idx++, Je);
			mInstructionTable.Add(idx++, Jne);
			mInstructionTable.Add(idx++, Jg);
			mInstructionTable.Add(idx++, Jge);
			mInstructionTable.Add(idx++, Jl);
			mInstructionTable.Add(idx++, Jle);
			mInstructionTable.Add(idx++, Jz);
			mInstructionTable.Add(idx++, Jnz);
			mInstructionTable.Add(idx++, CMovE);
			mInstructionTable.Add(idx++, CMovNE);
			mInstructionTable.Add(idx++, CMovG);
			mInstructionTable.Add(idx++, CMovGE);
			mInstructionTable.Add(idx++, CMovL);
			mInstructionTable.Add(idx++, CMovLE);
		}


		internal void RunCode(RamChunk code)
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
			RamChunk	data;
			if(!mOS.Alloc((UInt16)dataSize, out data))
			{
				mOS.Print("Couldn't alloc data chunk size " + dataSize);
				return;
			}

			for(;;)
			{
				byte	instruction	=code.ReadByte();

				byte	args	=code.ReadByte();

				byte	dstArg	=(byte)(args & 0x0F);
				byte	srcArg	=(byte)(args & 0xF0);

				UInt16	dst	=0;
				if(dstArg != 0)
				{
					if(dstArg == DstNumber)
					{
						mOS.Print("Dest number argument at " + code.GetPointer());
						return;
					}
					else if(dstArg == DstRegister || dstArg == DstRegPointer)
					{
						dst	=code.ReadByte();
					}
					else
					{
						dst	=code.ReadWord();
					}
				}

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


		UInt16 GetDstAddress(UInt16 dst, byte args, RamChunk data)
		{
			if((args & 0x0F) == DstPointer)
			{
				return	dst;
			}
			else if((args & 0x0F) == DstRegPointer)
			{
				return	mRegs.Get16((int)dst);
			}
			else if((args & 0x0F) == DstVariable)
			{
				return	dst;
			}
			return	0xFFFF;
		}


		UInt16 GetDstValue(UInt16 dst, byte args, RamChunk data)
		{
			if((args & 0x0F) == DstRegister)
			{
				return	mRegs.Get16((int)dst);
			}
			else if((args & 0x0F) == DstPointer)
			{
				data.SetPointer(dst);
				return	data.ReadWord();
			}
			else if((args & 0x0F) == DstLabel || (args & 0x0F) == DstNumber
					|| (args & 0x0F) == DstVariable)
			{
				return	dst;
			}
			else if((args & 0x0F) == DstRegPointer)
			{
				data.SetPointer(mRegs.Get16((int)dst));
				return	data.ReadWord();
			}
			return	0xFFFF;
		}


		UInt16 GetSrcValue(UInt16 src, byte args, RamChunk data)
		{
			//get src val
			UInt16	srcVal	=0;
			if((args & 0xF0) == SrcRegister)
			{
				srcVal	=mRegs.Get16((int)src);
			}
			else if((args & 0xF0) == SrcPointer || (args & 0xF0) == SrcVariable)
			{
				data.SetPointer(src);
				srcVal	=data.ReadWord();
			}
			else if((args & 0xF0) == SrcLabel || (args & 0xF0) == SrcNumber)
			{
				srcVal	=src;
			}
			else if((args & 0xF0) == SrcRegPointer)
			{
				data.SetPointer(mRegs.Get16((int)src));
				srcVal	=data.ReadWord();
			}

			return	srcVal;
		}


		void WriteDst(UInt16 val, byte args, UInt16 dst, RamChunk data)
		{
			if((args & 0x0F) == DstRegister)
			{
				mRegs.Set16((int)dst, (UInt16)val);
			}
			else if((args & 0x0F) == DstLabel)
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


		void Mov(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);

			WriteDst(srcVal, args, dst, data);
		}


		void AddrOf(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if((args & 0xF0) != SrcVariable)
			{
				return;	//only makes sense for variable
			}
			WriteDst(src, args, dst, data);
		}


		void Add(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal + dstVal), args, dst, data);
			mFlags.FlagAdd(dstVal, srcVal);
		}


		void Mul(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);
			mFlags.FlagMul(dstVal, srcVal);
		}


		void IMul(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			Int16	srcVal	=(Int16)GetSrcValue(src, args, data);
			Int16	dstVal	=(Int16)GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);
			mFlags.FlagIMul(dstVal, srcVal);
		}


		void Div(byte args, UInt16 dst, UInt16 src, UInt16 remainder, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);

			mRegs.Set16(remainder, (UInt16)(dstVal % srcVal));
		}


		void IDiv(byte args, UInt16 dst, UInt16 src, UInt16 remainder, RamChunk data)
		{
			Int16	srcVal	=(Int16)GetSrcValue(src, args, data);
			Int16	dstVal	=(Int16)GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal * dstVal), args, dst, data);

			mRegs.Set16(remainder, (UInt16)(dstVal % srcVal));
		}


		void Inc(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			dstVal++;

			WriteDst(dstVal, args, dst, data);
			mFlags.FlagInc(dstVal);
		}


		void Dec(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			dstVal--;

			WriteDst(dstVal, args, dst, data);
			mFlags.FlagDec(dstVal);
		}


		void Neg(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			//TODO: test
			dstVal	=(UInt16)~dstVal;
			dstVal++;

			WriteDst(dstVal, args, dst, data);

			//I THINK this will work
			mFlags.FlagSubtract(0, dstVal);
		}


		void Not(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	dstVal	=GetDstValue(dst, args, data);

			//TODO: test
			dstVal	=(UInt16)~dstVal;
			
			WriteDst(dstVal, args, dst, data);
		}


		void Xor(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal ^ dstVal), args, dst, data);

			//should be the same as and
			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Or(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal | dstVal), args, dst, data);
			mFlags.FlagOr(dstVal, srcVal);
		}


		void And(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			WriteDst((UInt16)(srcVal & dstVal), args, dst, data);
			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Tst(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			mFlags.FlagAnd(dstVal, srcVal);
		}


		void Cmp(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			UInt16	srcVal	=GetSrcValue(src, args, data);
			UInt16	dstVal	=GetDstValue(dst, args, data);

			mFlags.FlagSubtract(dstVal, srcVal);
		}


		void Jmp(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			//can't remember if dst arrives as the memory location
			//or if it is still an index
			mCurCodePage.SetPointer(dst);
		}


		void Je(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetZero())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jne(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(!mFlags.GetZero())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jg(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(!mFlags.GetZero() && mFlags.GetSign() == mFlags.GetOverFlow())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jge(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetSign() == mFlags.GetOverFlow())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jl(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetSign() != mFlags.GetOverFlow())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jle(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetZero() || mFlags.GetSign() != mFlags.GetOverFlow())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jz(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetZero())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void Jnz(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(!mFlags.GetZero())
			{
				mCurCodePage.SetPointer(dst);
			}
		}


		void CMovE(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetZero())
			{
				Mov(args, dst, src, opt, data);
			}
		}


		void CMovNE(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(!mFlags.GetZero())
			{
				Mov(args, dst, src, opt, data);
			}
		}


		void CMovG(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(!mFlags.GetZero() && mFlags.GetSign() == mFlags.GetOverFlow())
			{
				Mov(args, dst, src, opt, data);
			}
		}


		void CMovGE(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetSign() == mFlags.GetOverFlow())
			{
				Mov(args, dst, src, opt, data);
			}
		}


		void CMovL(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetSign() != mFlags.GetOverFlow())
			{
				Mov(args, dst, src, opt, data);
			}
		}


		void CMovLE(byte args, UInt16 dst, UInt16 src, UInt16 opt, RamChunk data)
		{
			if(mFlags.GetZero() || mFlags.GetSign() != mFlags.GetOverFlow())
			{
				Mov(args, dst, src, opt, data);
			}
		}
	}
}