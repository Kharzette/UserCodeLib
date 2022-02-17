using System;
using System.Collections.Generic;


namespace UserCodeLib
{
	//one physical block of ram, tied to an ingame module
	//be it mosfet, optical, quantum etc
	internal class RamModule
	{
		UInt64	mUID;
		UInt64	mTotalSize;		//in bytes
		bool	mbVolatile;		//lose it when unpowered?

		//could also have speed / latency / failure stuff in here

		//indexed by page number
		Dictionary<UInt16, RamChunk>	mChunks	=new Dictionary<UInt16, RamChunk>();


		internal RamModule(UInt64 id, UInt64 size, bool bVolatile)
		{
			mTotalSize	=size;
			mbVolatile	=bVolatile;
			mUID		=id;
		}


		internal UInt64	GetID()
		{
			return	mUID;
		}

		internal RamChunk GetChunk(UInt16 id)
		{
			if(mChunks.ContainsKey(id))
			{
				return	mChunks[id];
			}
			return	null;
		}

		internal UInt64	GetFreeBytes()
		{
			UInt64	ret	=mTotalSize;

			foreach(KeyValuePair<UInt16, RamChunk> chnk in mChunks)
			{
				ret	-=chnk.Value.GetSize();
			}
			return	ret;
		}


		internal bool CreateChunk(UInt64 size, out UInt16 page)
		{
			UInt64	free	=GetFreeBytes();

			page	=0xFFFF;

			if(size > free)
			{
				return	false;	//not gnouf space
			}

			RamChunk	chonker	=new RamChunk();

			while(true)
			{
				UInt16	pn	=(UInt16)Random.Shared.Next(UInt16.MaxValue);

				if(!mChunks.ContainsKey(pn))
				{
					page	=pn;
					break;
				}
			}

			chonker.Init(size, page);

			mChunks.Add(page, chonker);

			return	true;
		}
	}

	//individual allocated chunks of ram
	//should fit within its module
	internal class RamChunk
	{
		byte	[]mChonk;
		UInt16	mPage;
		UInt64	mSize;
		UInt64	mCur;	//pointer to current spot in ram


		internal void Init(UInt64 size, UInt16 page)
		{
			mSize	=size;
			mPage	=page;

			mChonk	=new byte[size];
		}


		internal UInt64 GetSize()
		{
			return	mSize;
		}

		internal UInt64 GetPointer()
		{
			return	mCur;
		}

		internal void SetPointer(UInt64 loc)
		{
			if(loc >= mSize)
			{
				//problems!
				return;
			}
			mCur	=loc;
		}


		internal byte ReadByte()
		{
			return	mChonk[mCur++];
		}

		internal UInt16 ReadWord()
		{
			UInt16	ret	=mChonk[mCur++];

			byte	high	=mChonk[mCur++];

			ret	|=(UInt16)(high << 8);

			return	ret;
		}

		internal UInt32 ReadDWord()
		{
			UInt32	ret	=(UInt32)(mChonk[mCur++]);
			
			ret	|=(UInt32)(mChonk[mCur++] << 8);
			ret	|=(UInt32)(mChonk[mCur++] << 16);
			ret	|=(UInt32)(mChonk[mCur++] << 24);

			return	ret;
		}

		internal UInt64 ReadQWord()
		{
			UInt64	ret	=mChonk[mCur++];

			ret	|=(((UInt64)mChonk[mCur++]) << 8);
			ret	|=(((UInt64)mChonk[mCur++]) << 16);			
			ret	|=(((UInt64)mChonk[mCur++]) << 24);
			ret	|=(((UInt64)mChonk[mCur++]) << 32);
			ret	|=(((UInt64)mChonk[mCur++]) << 40);
			ret	|=(((UInt64)mChonk[mCur++]) << 48);
			ret	|=(((UInt64)mChonk[mCur++]) << 56);

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

			mChonk[mCur++]	=(byte)(val & 0xFF);
			mChonk[mCur++]	=(byte)(val >> 8);

			return	true;
		}

		internal bool WriteDWord(UInt32 val)
		{
			if(mCur < 0 || mCur >= (UInt64)(mChonk.LongLength - 4))
			{
				return	false;
			}

			mChonk[mCur++]	=(byte)(val & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 8) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 16) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 24) & 0xFF);

			return	true;
		}

		internal bool WriteQWord(UInt64 val)
		{
			if(mCur < 0 || mCur >= (UInt64)(mChonk.LongLength - 8))
			{
				return	false;
			}

			mChonk[mCur++]	=(byte)(val & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 8) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 16) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 24) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 32) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 40) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 48) & 0xFF);
			mChonk[mCur++]	=(byte)((val >> 56) & 0xFF);

			return	true;
		}
	}


	//an overall ram module manager, tied to the overall computer
	//should be one per computer
	internal class Ram
	{
		List<RamModule>	mModules	=new List<RamModule>();

		internal event EventHandler	eAttach;
		internal event EventHandler	eDetach;


		internal RamModule GetModule(UInt64 id)
		{
			foreach(RamModule rm in mModules)
			{
				if(rm.GetID() == id)
				{
					return	rm;
				}
			}
			return	null;
		}


		internal void AttachModule(UInt64 mid, UInt64 size, bool bVolatile)
		{
			RamModule	rm	=new RamModule(mid, size, bVolatile);

			mModules.Add(rm);

			Nullable<UInt64>	id	=mid;

			eAttach?.Invoke(id, null);
		}


		internal void DetachModule(UInt64 mid)
		{
			int	idx	=-1;
			for(int i=0;i < mModules.Count;i++)
			{
				if(mModules[i].GetID() == mid)
				{
					idx	=i;
					break;
				}
			}

			if(idx < 0)
			{
				return;
			}

			mModules.RemoveAt(idx);

			Nullable<UInt64>	id	=mid;

			eDetach?.Invoke(id, null);
		}
	}
}