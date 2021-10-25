using System;

namespace UserCodeLib
{
	internal class Ram
	{
		byte	[]mChonk;
		UInt64	mSize;
		UInt64	mCur;	//pointer to current spot in ram

		byte	mAddressing;	//0 is 16bit, 1 is 32bit, 2 is 64bit


		internal void Init(UInt64 size, byte addressing)
		{
			mSize	=size;

			mChonk	=new byte[size];

			if(addressing < 0 || addressing > 2)
			{
				//bad addressing
			}
			mAddressing	=addressing;
		}


		internal bool Is16Bit()
		{
			return	(mAddressing == 0);
		}

		internal bool Is32Bit()
		{
			return	((mAddressing & 1) != 0);
		}

		internal bool Is64Bit()
		{
			return	((mAddressing & 2) != 0);
		}

		internal UInt64 GetPointer()
		{
			return	mCur;
		}

		internal int GetAddressSize()
		{
			if(Is16Bit())
			{
				return	2;
			}
			else if(Is32Bit())
			{
				return	4;
			}
			else
			{
				return	8;
			}
		}

		internal void SetPointer(UInt64 loc)
		{
			mCur	=loc;
		}


		internal byte ReadByte()
		{
			return	mChonk[mCur++];
		}

		internal UInt16 ReadWord()
		{
			UInt16	ret	=mChonk[mCur++];

			ret	>>=8;
			ret	|=mChonk[mCur++];

			return	ret;
		}

		internal UInt32 ReadDWord()
		{
			UInt32	ret	=(UInt32)(mChonk[mCur++] >> 24);
			
			ret	|=(UInt32)(mChonk[mCur++] >> 16);
			ret	|=(UInt32)(mChonk[mCur++] >> 8);
			ret	|=(UInt32)(mChonk[mCur++]);

			return	ret;
		}

		internal UInt64 ReadQWord()
		{
			UInt64	ret	=(((UInt64)mChonk[mCur++]) >> 56);
			
			ret	|=(((UInt64)mChonk[mCur++]) >> 48);
			ret	|=(((UInt64)mChonk[mCur++]) >> 40);
			ret	|=(((UInt64)mChonk[mCur++]) >> 32);
			ret	|=(((UInt64)mChonk[mCur++]) >> 24);
			ret	|=(((UInt64)mChonk[mCur++]) >> 16);
			ret	|=(((UInt64)mChonk[mCur++]) >> 8);
			ret	|=mChonk[mCur++];

			return	ret;
		}


		internal bool WriteByte(byte val)
		{
			if(mCur < 0 || mCur >= (UInt64)mChonk.LongLength)
			{
				return	false;
			}

			mChonk[mCur++]	=val;

			return	true;
		}

		internal bool WriteWord(UInt16 val)
		{
			if(mCur < 0 || mCur >= (UInt64)(mChonk.LongLength - 1))
			{
				return	false;
			}

			mChonk[mCur++]	=(byte)(val << 8);
			mChonk[mCur++]	=(byte)(val & 0xFF);

			return	true;
		}

		internal bool WriteDWord(UInt32 val)
		{
			if(mCur < 0 || mCur >= (UInt64)(mChonk.LongLength - 4))
			{
				return	false;
			}

			mChonk[mCur++]	=(byte)((val << 24) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 16) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 8) & 0xFF);
			mChonk[mCur++]	=(byte)(val & 0xFF);

			return	true;
		}

		internal bool WriteQWord(UInt64 val)
		{
			if(mCur < 0 || mCur >= (UInt64)(mChonk.LongLength - 8))
			{
				return	false;
			}

			mChonk[mCur++]	=(byte)((val << 56) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 48) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 40) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 32) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 24) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 16) & 0xFF);
			mChonk[mCur++]	=(byte)((val << 8) & 0xFF);
			mChonk[mCur++]	=(byte)(val & 0xFF);

			return	true;
		}
	}
}