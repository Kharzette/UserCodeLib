using System;

namespace UserCodeLib
{
	//automagical c style variable handling
	//register saving for calls etc
	internal class Stack
	{
		//store nonzero registers for restoration on a return
		internal class Frame16
		{
			UInt32	mZeroRegs;	//bits indicate nonzero regs

		}

		byte	[]mStack;	//the program stack

		UInt64	mCurStackPos;



		internal void Init(UInt64 size)
		{
			mStack	=new byte[size];

			mCurStackPos	=size;
		}


		internal void Push(byte val)
		{
			mCurStackPos--;

			mStack[mCurStackPos]	=val;
		}

		internal void Push(UInt16 val)
		{
			mCurStackPos	-=2;

			mStack[mCurStackPos]		=(byte)(val & 0xFF);
			mStack[mCurStackPos + 1]	=(byte)(val >> 8);
		}
	}
}