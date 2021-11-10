using System;

namespace UserCodeLib
{
	internal class Flags
	{
		bool	bCarry;
		bool	bOverFlow;
		bool	bZero;
		bool	bSign;


		internal bool GetCarry()
		{
			return	bCarry;
		}

		internal bool GetOverFlow()
		{
			return	bOverFlow;
		}

		internal bool GetZero()
		{
			return	bZero;
		}

		internal bool GetSign()
		{
			return	bSign;
		}


		internal void SetCarry(bool bVal)
		{
			bCarry	=bVal;
		}

		internal void SetOverFlow(bool bVal)
		{
			bOverFlow	=bVal;
		}

		internal void SetZero(bool bVal)
		{
			bZero	=bVal;
		}

		internal void SetSign(bool bVal)
		{
			bSign	=bVal;
		}


		//borrowed alot of this flag stuff from x86 emulators
		//on github, namely the one from shift-crops, thanks!
		internal void FlagAdd(UInt16 src, UInt16 dst)
		{
			UInt32	result	=(UInt32)dst + src;

			bool	srcSign	=((src >> 15) != 0);
			bool	dstSign	=((dst >> 15) != 0);
			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=((result >> 16) != 0);
			bZero		=(result == 0);
			bSign		=resSign;
			bOverFlow	=(!(srcSign ^ dstSign) && srcSign ^ resSign);
		}

		internal void FlagSubtract(UInt16 dst, UInt16 src)
		{
			UInt32	result	=(UInt32)dst - src;

			bool	srcSign	=((src >> 15) != 0);
			bool	dstSign	=((dst >> 15) != 0);
			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=((result >> 16) != 0);
			bZero		=(result == 0);
			bSign		=resSign;
			bOverFlow	=(srcSign ^ dstSign && srcSign ^ resSign);
		}

		internal void FlagMul(UInt16 dst, UInt16 src)
		{
			UInt32	result	=(UInt32)dst * src;

			bCarry		=((result >> 16) != 0);
			bOverFlow	=((result >> 16) != 0);
			bZero		=(result == 0);	//undefined on x86
		}

		internal void FlagIMul(UInt16 dst, UInt16 src)
		{
			UInt32	result	=(UInt32)dst * src;

			bCarry		=((result >> 16) != 0xFFFFFFFF);
			bOverFlow	=((result >> 16) != 0xFFFFFFFF);
			bZero		=(result == 0);	//undefined on x86
		}

		internal void FlagAnd(UInt16 dst, UInt16 src)
		{
			UInt16	result	=(UInt16)(dst & src);

			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=false;
			bZero		=(result == 0);
			bSign		=resSign;
			bOverFlow	=false;
		}

		internal void FlagOr(UInt16 dst, UInt16 src)
		{
			UInt16	result	=(UInt16)(dst | src);

			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=false;
			bZero		=(result == 0);
			bSign		=resSign;
			bOverFlow	=false;
		}

		internal void FlagShl(UInt16 dst, int amount)
		{
			UInt16	result	=(UInt16)(dst << amount);

			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=(((dst >> (16 - amount)) & 1) != 0);
			bZero		=(result == 0);
			bSign		=resSign;
			if(amount == 1)
			{
				bOverFlow	=((((dst >> 15) & 1) ^ ((dst >> 14) & 1)) != 0);
			}
		}

		internal void FlagShr(UInt16 dst, int amount)
		{
			UInt16	result	=(UInt16)(dst >> amount);

			bool	resSign	=(((result >> 15) & 1) != 0);

			bCarry		=(((dst >> (amount - 1)) & 1) != 0);
			bZero		=(result == 0);
			bSign		=resSign;
			if(amount == 1)
			{
				bOverFlow	=(((dst >> 15) & 1) != 0);
			}
		}
	}
}